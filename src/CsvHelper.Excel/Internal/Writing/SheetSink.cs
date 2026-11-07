using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The cell sink of a sheet a workbook writer writes: the fields of its <see cref="CsvWriter"/> become the cells of the
/// sheet's XML, which streams into the sheet's zip entry.
/// </summary>
/// <remarks>
/// <para>
/// Record <c>n</c> (from 0) is row <c>n + 1</c>, and field <c>i</c> is column <c>i</c> from <c>A</c>. Every row and cell
/// has its <c>r</c>, as Excel writes them. A record without a value has no <c>row</c> element, and an empty field no
/// <c>c</c>. There is no <c>dimension</c> and no <c>spans</c>: both need rows the sink hasn't seen yet.
/// </para>
/// <para>
/// A field's cell comes from its type and text (<see cref="CellKinds"/>): numbers, booleans, dates, times and durations
/// are typed cells when their text is .NET's default (<see cref="DotNetText"/>); anything else is a text cell. Each
/// column keeps the last type it saw and the kind of cell that type makes, so a column of one type costs a reference
/// comparison per field.
/// </para>
/// <para>
/// A field's cell is written into the record's part of the buffer when it arrives, after its checks pass, so a field that
/// throws leaves no cell. <see cref="DiscardRecord"/> moves the buffer back to the record's start; the shared strings and
/// cell formats it added stay, which Excel ignores. The buffer is written to the entry when a flush asks, in whole
/// records, and the entry is opened then, sync or async as the caller is.
/// </para>
/// <para>
/// The records up to the first data record (the one after the header, with <see cref="CsvWriterOptions.HasHeader"/>) are
/// held, since the columns' widths, which come before the rows, are that record's: a date or a time that its column's
/// default width can't show gets a width that fits it (<see cref="ColumnWidths"/>). Text, and numbers in General, never
/// show <c>########</c> and get none.
/// </para>
/// </remarks>
internal sealed class SheetSink : CsvCellSink
{
	/// <summary>The bytes of XML a sheet buffers before it asks to be flushed.</summary>
	public const int FlushSize = 64 * 1024;

	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxSignificantDigits = 15;

	/// <summary>The XML before the columns and the rows.</summary>
	private static readonly byte[] Start = Concat(
		PackageParts.Declaration,
		"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"u8);

	/// <summary>The XML after the rows: Excel's default page margins, which it writes on every sheet.</summary>
	private static readonly byte[] End = "</sheetData><pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>"u8.ToArray();

	/// <summary>The letters of each column, made as they are first needed.</summary>
	private static readonly byte[]?[] ColumnLetters = new byte[CellReference.MaxColumns][];

	private readonly ExcelWorkbookWriter workbook;
	private readonly PackageWriter package;
	private readonly string partName;
	private readonly SharedStringTable? strings;
	private readonly StyleRegistry styles;
	private readonly ExcelWriterOptions options;
	// The writer's culture's signs and separator, or null for the invariant ones.
	private readonly NumberCulture? culture;
	// The row of the first data record, whose cells set the columns' widths.
	private readonly long firstDataRow;
	private readonly XmlOutput output = new(FlushSize + 4096);
	// The XML of a shared string, before the table gives its index.
	private readonly XmlOutput? item;
	// The columns that get a width, in order: the first data record's while it is written, then the sheet's.
	private readonly List<(int Column, double Width)> widths = [];
	// Each column's last type, and the kind of cell it makes.
	private Type?[] columnTypes = new Type?[16];
	private CellKind[] columnKinds = new CellKind[16];
	private Stream? entry;
	// The current record's row, and the column of its next field.
	private long row = 1;
	private int column;
	// Whether the current record's row element has been written, which its first cell with a value does.
	private bool rowOpen;
	// Where the current record starts in the buffer: what is before it is whole records.
	private int recordStart;
	// The cells of the current record that refer to a shared string.
	private int recordReferences;
	// The current record's last cell's column, and whether one of its text cells starts like a number.
	private int recordLastColumn;
	private bool recordNumberLikeText;
	// The used range of the records kept: the last row with a cell and the last column, and whether a text cell starts
	// like a number, which Excel would flag.
	private long lastRow;
	private int lastColumn = -1;
	private bool numberLikeText;
	// Whether the columns' widths are known, so that the held records can be written; and whether the XML before the rows is.
	private bool widthsKnown;
	private bool startWritten;
	private bool completed;

	/// <summary>Starts a sheet.</summary>
	/// <param name="workbook">The workbook writer, which is told when the sheet completes.</param>
	/// <param name="package">The package.</param>
	/// <param name="name">The sheet's name.</param>
	/// <param name="partName">The sheet's part.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> to write each cell's string in the cell.</param>
	/// <param name="styles">The workbook's cell formats.</param>
	/// <param name="options">The workbook's options.</param>
	public SheetSink(ExcelWorkbookWriter workbook, PackageWriter package, string name, string partName, SharedStringTable? strings, StyleRegistry styles, ExcelWriterOptions options)
	{
		this.workbook = workbook;
		this.package = package;
		this.partName = partName;
		this.strings = strings;
		this.styles = styles;
		this.options = options;
		culture = NumberCulture.For(options.Csv.FormatProvider);
		firstDataRow = options.Csv.HasHeader ? 2 : 1;
		Location = new SheetLocation(name);
		item = strings is null ? null : new XmlOutput(256);
	}

	/// <summary>The sheet's name and how messages name its cells.</summary>
	public SheetLocation Location { get; }

	/// <summary>Whether the cells being written are the first data record's, whose typed cells set their columns' widths.</summary>
	private bool MeasuringWidths => !widthsKnown && row == firstDataRow;

	/// <summary>Writes a field's cell into the current record.</summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="type">The type it was formatted from.</param>
	/// <exception cref="InvalidOperationException">
	/// The field is past Excel's last column, its text is too long for a cell, or its value is one that the options say to
	/// throw for.
	/// </exception>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type)
	{
		// The column counts whether or not the field throws, as the writer's field count does.
		var at = column++;
		if (at >= CellReference.MaxColumns)
		{
			throw new InvalidOperationException(
				$"Record {row.ToString("N0", CultureInfo.InvariantCulture)} has a field in column {Letters(at)}, past XFD, Excel's last column.");
		}

		if (utf8.IsEmpty)
		{
			return;
		}

		switch (KindOf(at, type))
		{
			case CellKind.Bool:
				WriteBool(utf8, at);

				break;
			case CellKind.Integer or CellKind.Decimal:
				WriteNumber(utf8, at, exact: true);

				break;
			case CellKind.Float:
				WriteNumber(utf8, at, exact: false);

				break;
			case CellKind.DateTime:
				WriteDateTime(utf8, at);

				break;
			case CellKind.DateTimeOffset:
				WriteDateTimeOffset(utf8, at);

				break;
			case CellKind.DateOnly:
				WriteDate(utf8, at);

				break;
			case CellKind.TimeOnly:
				WriteTime(utf8, at);

				break;
			case CellKind.TimeSpan:
				WriteDuration(utf8, at);

				break;
			default:
				WriteText(utf8, at);

				break;
		}
	}

	/// <summary>Ends the current record: closes its row, if it has one.</summary>
	/// <returns>Whether the buffer has passed <see cref="FlushSize"/> and can be written: not while records are held.</returns>
	public override bool EndRecord()
	{
		if (rowOpen)
		{
			output.Append("</row>"u8);
			rowOpen = false;
			lastRow = row;
			lastColumn = Math.Max(lastColumn, recordLastColumn);
		}

		numberLikeText |= recordNumberLikeText;
		recordNumberLikeText = false;
		if (strings is not null)
		{
			strings.References += recordReferences;
			recordReferences = 0;
		}

		if (row == firstDataRow)
		{
			widthsKnown = true;
		}

		row++;
		column = 0;
		recordStart = output.Length;

		return widthsKnown && recordStart >= FlushSize;
	}

	/// <summary>Drops the current record's cells, and the widths they set.</summary>
	public override void DiscardRecord()
	{
		output.Truncate(recordStart);
		rowOpen = false;
		column = 0;
		recordReferences = 0;
		recordNumberLikeText = false;
		if (MeasuringWidths)
		{
			widths.Clear();
		}
	}

	/// <summary>Writes the whole records buffered to the sheet's entry, unless they are held for the columns' widths.</summary>
	public override void Flush() => SyncOrAsync.Wait(FlushAsync(false, CancellationToken.None));

	/// <summary>Asynchronously writes the whole records buffered to the sheet's entry, unless they are held.</summary>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>A task that completes when they are written.</returns>
	public override ValueTask FlushAsync(CancellationToken cancellationToken) => FlushAsync(true, cancellationToken);

	/// <summary>Finishes the sheet and closes its entry, so that the workbook can write the next part.</summary>
	public override void Complete() => SyncOrAsync.Wait(CompleteAsync(false));

	/// <summary>Asynchronously finishes the sheet and closes its entry.</summary>
	/// <returns>A task that completes when the entry is closed.</returns>
	public override ValueTask CompleteAsync() => CompleteAsync(true);

	/// <summary>
	/// Finishes the sheet: the XML before the rows if no record was written out, the XML after them (with
	/// <c>ignoredErrors</c> when a text cell starts like a number), the last of the buffer, and the entry closed. The
	/// workbook writer is told that the sheet is complete, even when writing fails. Called once; the writer has ended its
	/// last record.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	public async ValueTask CompleteAsync(bool async)
	{
		if (completed)
		{
			return;
		}

		completed = true;
		try
		{
			widthsKnown = true;
			output.Append(End);
			if (numberLikeText)
			{
				// Excel flags text that reads as a number with a warning triangle; over the used range, it doesn't.
				output.Append("<ignoredErrors><ignoredError sqref=\"A1:"u8);
				output.AppendReference(ColumnName(lastColumn), lastRow);
				output.Append("\" numberStoredAsText=\"1\"/></ignoredErrors>"u8);
			}

			output.Append("</worksheet>"u8);
			recordStart = output.Length;
			await FlushAsync(async, CancellationToken.None).ConfigureAwait(false);
			await PackageWriter.CloseAsync(entry!, async).ConfigureAwait(false);
		}
		finally
		{
			output.Dispose();
			item?.Dispose();
			workbook.SheetCompleted(this);
		}
	}

	/// <summary>The letters of a column, past Excel's last one too, for messages.</summary>
	/// <param name="column">The 0-based column.</param>
	/// <returns>The letters, such as <c>XFE</c>.</returns>
	private static string Letters(int column)
	{
		var letters = "";
		for (var n = column + 1; n > 0; n = (n - 1) / 26)
		{
			letters = (char)('A' + (n - 1) % 26) + letters;
		}

		return letters;
	}

	/// <summary>The bytes of two spans, one after the other.</summary>
	/// <param name="first">The first.</param>
	/// <param name="second">The second.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
	{
		var bytes = new byte[first.Length + second.Length];
		first.CopyTo(bytes);
		second.CopyTo(bytes.AsSpan(first.Length));

		return bytes;
	}

	/// <summary>The letters of a column of the sheet, as UTF-8.</summary>
	/// <param name="column">The 0-based column, below <see cref="CellReference.MaxColumns"/>.</param>
	/// <returns>The letters.</returns>
	private static byte[] ColumnName(int column) =>
		ColumnLetters[column] ??= Encoding.ASCII.GetBytes(CellReference.ColumnName(column));

	/// <summary>A duration as <c>[h]:mm:ss</c> shows it, with <c>.000</c> when it has milliseconds, for its width.</summary>
	/// <param name="milliseconds">The duration's milliseconds, not negative.</param>
	/// <returns>The text.</returns>
	private static string DurationText(long milliseconds)
	{
		var text = (milliseconds / 3_600_000).ToString(CultureInfo.InvariantCulture) + ":" +
			(milliseconds / 60_000 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
			(milliseconds / 1000 % 60).ToString("00", CultureInfo.InvariantCulture);

		return milliseconds % 1000 == 0 ? text : text + "." + (milliseconds % 1000).ToString("000", CultureInfo.InvariantCulture);
	}

	/// <summary>The kind of cell a field of a type makes in a column, from the column's last type when it is the same.</summary>
	/// <param name="at">The column.</param>
	/// <param name="type">The field's type.</param>
	/// <returns>The kind.</returns>
	private CellKind KindOf(int at, Type type)
	{
		if (at >= columnTypes.Length)
		{
			var size = Math.Min(Math.Max(at + 1, columnTypes.Length * 2), CellReference.MaxColumns);
			Array.Resize(ref columnTypes, size);
			Array.Resize(ref columnKinds, size);
		}

		if (!ReferenceEquals(columnTypes[at], type))
		{
			columnKinds[at] = CellKinds.Of(type);
			columnTypes[at] = type;
		}

		return columnKinds[at];
	}

	/// <summary>Writes a boolean cell from <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteBool(ReadOnlySpan<byte> text, int at)
	{
		var value = text.SequenceEqual("True"u8) ? (byte)'1' : text.SequenceEqual("False"u8) ? (byte)'0' : (byte)0;
		if (value == 0)
		{
			WriteText(text, at);

			return;
		}

		StartCell(at);
		output.Append(" t=\"b\"><v>"u8);
		output.Append(value);
		output.Append("</v></c>"u8);
	}

	/// <summary>
	/// Writes a number cell, when the text is a number in .NET's default form, in the invariant culture or the writer's:
	/// its invariant text goes into <c>v</c>. Other text, such as a format's, is a text cell.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="exact">
	/// Whether the number is an integer or a <see cref="decimal"/>, whose digits past Excel's 15 follow
	/// <see cref="ExcelWriterOptions.LongNumbers"/>. A floating-point number's cell stores the same double, whatever its digits.
	/// </param>
	/// <exception cref="InvalidOperationException">The number has more than 15 significant digits, and the options say to throw.</exception>
	private void WriteNumber(ReadOnlySpan<byte> text, int at, bool exact)
	{
		Span<byte> invariant = stackalloc byte[DotNetText.MaxNumberLength];
		var length = DotNetText.ToInvariantNumber(text, culture, invariant, out var significant);
		if (length < 0)
		{
			WriteText(text, at);

			return;
		}

		if (exact && significant > MaxSignificantDigits && options.LongNumbers != ExcelLongNumbers.Number)
		{
			if (options.LongNumbers == ExcelLongNumbers.Throw)
			{
				throw new InvalidOperationException(
					$"Cell {Location.Cell(at, row)} holds {Encoding.UTF8.GetString(text.ToArray())}, which has {significant} significant digits, and Excel keeps {MaxSignificantDigits} (ExcelWriterOptions.LongNumbers is Throw).");
			}

			WriteText(text, at);

			return;
		}

		StartCell(at);
		output.Append("><v>"u8);
		output.Append(invariant.Slice(0, length));
		output.Append("</v></c>"u8);
	}

	/// <summary>Writes a date cell from a <see cref="DateTime"/>'s <c>"O"</c> text: its clock time, whatever its kind.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTime(ReadOnlySpan<byte> text, int at)
	{
		if (!DotNetText.TryDateTime(text, out var ticks, out _))
		{
			WriteText(text, at);

			return;
		}

		WriteDateTime(text, at, ticks);
	}

	/// <summary>
	/// Writes a cell from a <see cref="DateTimeOffset"/>'s <c>"O"</c> text, as <see cref="ExcelWriterOptions.DateTimeOffsets"/>
	/// says: its text, or a date cell of its time in UTC or of its clock time.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTimeOffset(ReadOnlySpan<byte> text, int at)
	{
		if (options.DateTimeOffsets == ExcelDateTimeOffsets.Text || !DotNetText.TryDateTime(text, out var ticks, out var offset))
		{
			WriteText(text, at);

			return;
		}

		WriteDateTime(text, at, options.DateTimeOffsets == ExcelDateTimeOffsets.Utc ? ticks - offset : ticks);
	}

	/// <summary>
	/// Writes a date cell of a date and time, in the format of what it holds: <c>yyyy-mm-dd</c> at midnight,
	/// <c>yyyy-mm-dd hh:mm:ss</c> in whole seconds, <c>yyyy-mm-dd hh:mm:ss.000</c> otherwise. Its column gets the width of a
	/// date and time in seconds at least, so that the later values of a column whose first is a date alone show too.
	/// </summary>
	/// <param name="text">The field's text, for a text cell or a message.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="ticks">The date and time's ticks.</param>
	private void WriteDateTime(ReadOnlySpan<byte> text, int at, long ticks)
	{
		var milliseconds = Serial.DateMilliseconds(ticks);
		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at, duration: false);

			return;
		}

		var format = milliseconds % Serial.MillisecondsPerDay == 0 ? FixedFormat.Date : milliseconds % 1000 == 0 ? FixedFormat.DateTime : FixedFormat.DateTimeMilliseconds;
		WriteSerial(at, milliseconds, format, format == FixedFormat.DateTimeMilliseconds ? ColumnWidths.DateTimeMilliseconds : ColumnWidths.DateTime);
	}

	/// <summary>Writes a date cell from a <c>DateOnly</c>'s <c>"O"</c> text, in <c>yyyy-mm-dd</c>.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDate(ReadOnlySpan<byte> text, int at)
	{
		if (!DotNetText.TryDate(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.DateMilliseconds(ticks);
		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at, duration: false);

			return;
		}

		WriteSerial(at, milliseconds, FixedFormat.Date, ColumnWidths.Date);
	}

	/// <summary>Writes a time cell from a <c>TimeOnly</c>'s <c>"O"</c> text, in <c>hh:mm:ss</c>, with <c>.000</c> when it has milliseconds.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteTime(ReadOnlySpan<byte> text, int at)
	{
		if (!DotNetText.TryTime(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.TimeMilliseconds(ticks);
		var whole = milliseconds % 1000 == 0;
		WriteSerial(at, milliseconds, whole ? FixedFormat.Time : FixedFormat.TimeMilliseconds, whole ? ColumnWidths.Time : ColumnWidths.TimeMilliseconds);
	}

	/// <summary>
	/// Writes a duration cell from a <see cref="TimeSpan"/>'s <c>"c"</c> text: its days, in <c>[h]:mm:ss</c>, with
	/// <c>.000</c> when it has milliseconds. Its column's width is its own text's.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDuration(ReadOnlySpan<byte> text, int at)
	{
		if (!DotNetText.TryDuration(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.DurationMilliseconds(ticks);
		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at, duration: true);

			return;
		}

		var format = milliseconds % 1000 == 0 ? FixedFormat.Duration : FixedFormat.DurationMilliseconds;
		WriteSerial(at, milliseconds, format, MeasuringWidths ? ColumnWidths.Of(DurationText(milliseconds).AsSpan()) : 0);
	}

	/// <summary>
	/// Writes a date, time or duration that Excel can't show, as <see cref="ExcelWriterOptions.OutOfRangeDates"/> says: a
	/// text cell of its text, or an exception.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="duration">Whether it is a negative duration, rather than a date before 1900-01-01.</param>
	/// <exception cref="InvalidOperationException">The options say to throw.</exception>
	private void WriteOutOfRange(ReadOnlySpan<byte> text, int at, bool duration)
	{
		if (options.OutOfRangeDates == ExcelOutOfRangeDates.Throw)
		{
			var value = Encoding.UTF8.GetString(text.ToArray());

			throw new InvalidOperationException(duration
				? $"Cell {Location.Cell(at, row)} holds {value}, a negative duration, which Excel shows as ######## (ExcelWriterOptions.OutOfRangeDates is Throw)."
				: $"Cell {Location.Cell(at, row)} holds {value}, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw).");
		}

		WriteText(text, at);
	}

	/// <summary>Writes a number cell of a serial, in one of the writer's formats, and notes its column's width.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="format">The cell's number format.</param>
	/// <param name="width">The width its column needs, or 0 for none.</param>
	private void WriteSerial(int at, long milliseconds, FixedFormat format, double width)
	{
		StartCell(at);
		output.Append(" s=\""u8);
		output.AppendNumber(styles.IndexOf(format));
		output.Append("\"><v>"u8);
		output.AppendDouble(Serial.Days(milliseconds));
		output.Append("</v></c>"u8);
		if (width > 0 && MeasuringWidths)
		{
			widths.Add((at, width));
		}
	}

	/// <summary>Writes a text cell: the string inline, or its index in the shared strings.</summary>
	/// <param name="text">The text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	/// <exception cref="InvalidOperationException">The text has more than <see cref="XmlOutput.MaxCellLength"/> UTF-16 code units.</exception>
	private void WriteText(ReadOnlySpan<byte> text, int at)
	{
		if (!XmlOutput.IsValidUtf8(text))
		{
			text = XmlOutput.ReplaceInvalidUtf8(text);
		}

		var units = XmlOutput.Utf16Length(text);
		if (units > XmlOutput.MaxCellLength)
		{
			throw new InvalidOperationException(
				$"Cell {Location.Cell(at, row)} has {units.ToString("N0", CultureInfo.InvariantCulture)} characters, and Excel opens no cell of more than {XmlOutput.MaxCellLength.ToString("N0", CultureInfo.InvariantCulture)}.");
		}

		// Text that starts like a number (00123, 1.5, -1, .5) gets Excel's warning triangle unless the sheet ignores it.
		recordNumberLikeText |= text[0] is >= (byte)'0' and <= (byte)'9' or (byte)'+' or (byte)'-' or (byte)'.';
		StartCell(at);
		if (strings is null)
		{
			output.Append(" t=\"inlineStr\"><is>"u8);
			AppendElement(output, text);
			output.Append("</is></c>"u8);

			return;
		}

		item!.Truncate(0);
		AppendElement(item, text);
		var index = strings.IndexOf(item.Written);
		recordReferences++;
		output.Append(" t=\"s\"><v>"u8);
		output.AppendNumber(index);
		output.Append("</v></c>"u8);
	}

	/// <summary>Writes a text's <c>t</c> element, with <c>xml:space="preserve"</c> when Excel would trim it otherwise.</summary>
	/// <param name="target">The buffer.</param>
	/// <param name="text">The text, valid UTF-8.</param>
	private static void AppendElement(XmlOutput target, ReadOnlySpan<byte> text)
	{
		target.Append(XmlOutput.NeedsPreserve(text) ? "<t xml:space=\"preserve\">"u8 : "<t>"u8);
		target.AppendText(text);
		target.Append("</t>"u8);
	}

	/// <summary>Writes the start of a cell, up to its attributes' end: its row first, when it is the record's first cell.</summary>
	/// <param name="at">The cell's column.</param>
	private void StartCell(int at)
	{
		if (!rowOpen)
		{
			output.Append("<row r=\""u8);
			output.AppendNumber(row);
			output.Append("\">"u8);
			rowOpen = true;
		}

		recordLastColumn = at;
		output.Append("<c r=\""u8);
		output.AppendReference(ColumnName(at), row);
		output.Append((byte)'"');
	}

	/// <summary>
	/// Writes the XML before the rows into a buffer: the worksheet's start, the columns' widths, and <c>sheetData</c>'s start.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	private void AppendStart(XmlOutput xml)
	{
		xml.Append(Start);
		if (widths.Count > 0)
		{
			xml.Append("<cols>"u8);
			foreach (var (at, width) in widths)
			{
				xml.Append("<col min=\""u8);
				xml.AppendNumber(at + 1);
				xml.Append("\" max=\""u8);
				xml.AppendNumber(at + 1);
				xml.Append("\" width=\""u8);
				xml.AppendDouble(width);
				xml.Append("\" customWidth=\"1\"/>"u8);
			}

			xml.Append("</cols>"u8);
		}

		xml.Append("<sheetData>"u8);
	}

	/// <summary>
	/// Writes the whole records buffered to the entry, opening it and writing the XML before the rows first, once the
	/// columns' widths are known; until then the records are held.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask FlushAsync(bool async, CancellationToken cancellationToken)
	{
		var count = recordStart;
		if (!widthsKnown || (count == 0 && startWritten))
		{
			return;
		}

		entry ??= await package.OpenPartAsync(partName, async, cancellationToken).ConfigureAwait(false);
		if (!startWritten)
		{
			using var start = new XmlOutput(Start.Length + 64 + (widths.Count * 64));
			AppendStart(start);
			await start.WriteToAsync(entry, start.Length, async, cancellationToken).ConfigureAwait(false);
			startWritten = true;
		}

		await output.WriteToAsync(entry, count, async, cancellationToken).ConfigureAwait(false);
		output.RemoveFirst(count);
		recordStart = 0;
	}
}
