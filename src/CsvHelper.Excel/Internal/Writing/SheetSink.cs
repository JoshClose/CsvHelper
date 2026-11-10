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
/// <para>
/// A sheet of <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/> knows its columns' members' formats and cultures
/// (<see cref="MemberFormats"/>), by the header record's names, or, without a header, by the names that the record
/// writer writes in its header. A number of a column whose member has a <see cref="CsvCultureAttribute"/> is read with
/// that culture; a date or time with a <see cref="CsvFormatAttribute"/> is read back with its format
/// (<see cref="DateFormat"/>), and is a date cell in that format translated to Excel's when it formats to the same text
/// again, and a text cell otherwise. A duration with a format other than .NET's default is a text cell.
/// </para>
/// <para>
/// How the sheet looks comes from its <see cref="SheetSettings"/>. A title is row 1, in bold Calibri 14, merged over the
/// first record's columns, and the writer's records follow from row 2. The header's cells are bold, in their own number
/// formats. The rows through the header, and the first columns asked for, are frozen (<c>sheetViews</c>). A column's
/// options (<see cref="ExcelSheetOptions.Columns"/>, else its member's <see cref="ExcelColumnAttribute"/>) are resolved by
/// the header's names when the header record ends, or, without a header, when the sheet is added: its width beats the one
/// measured, it can be hidden, and its format and wrap style its cells below the header (<see cref="ColumnStyle"/>) and
/// are its default style. A cell's style is its own number format but in the title and header rows and in a column with a
/// style, so that a sheet without either tests one flag per cell.
/// </para>
/// <para>
/// A record past the sheet's last row follows <see cref="ExcelWriterOptions.RowLimit"/>. Its first field finds it, through
/// the field limit that <see cref="EndRecord"/> lowers to 0 once the sheet is full, so that the test of the column limit
/// is the only one per field. With <see cref="ExcelRowLimit.NewSheet"/>, the full sheet's end goes into the buffer, and the
/// next sheet's rows after it, the header's first; the next flush, which <see cref="EndRecord"/> asks for, writes the full
/// sheet, closes its entry and opens the next one's. So no I/O happens in <see cref="WriteField"/>, and the switch is sync
/// or async as the caller is.
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

	/// <summary>Excel's default page margins, which it writes on every sheet, after the rows and the merged cells.</summary>
	private static readonly byte[] PageMargins = "<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>"u8.ToArray();

	/// <summary>The letters of each column, made as they are first needed.</summary>
	private static readonly byte[]?[] ColumnLetters = new byte[CellReference.MaxColumns][];

	private readonly ExcelWorkbookWriter workbook;
	private readonly PackageWriter package;
	private readonly SharedStringTable? strings;
	private readonly StyleRegistry styles;
	private readonly ExcelWriterOptions options;
	// The writer's culture's signs and separator, or null for the invariant ones.
	private readonly NumberCulture? culture;
	// The formats of the record type's members, until the header's names say which columns they are for.
	private readonly MemberFormats? members;
	// The title's text, or null without one.
	private readonly byte[]? title;
	// The row of the writer's first record: 2 below a title, else 1; the header's row, or 0 without a header; and the row
	// of the first data record, whose cells set the columns' widths.
	private readonly long firstRow;
	private readonly long headerRowNumber;
	private readonly long firstDataRow;
	// The rows a sheet holds: Excel's last row's number, or fewer in tests.
	private readonly int maxRows;
	// Whether each new sheet of RowLimit.NewSheet repeats the header, which the first record is.
	private readonly bool repeatsHeader;
	private readonly XmlOutput output = new(FlushSize + 4096);
	// The XML of a shared string, before the table gives its index.
	private readonly XmlOutput? item;
	// The columns that get a width, in order: the first data record's while it is written, then the sheet's.
	private readonly List<(int Column, double Width)> widths = [];
	// Each column's last type, and the kind of cell it makes.
	private Type?[] columnTypes = new Type?[16];
	private CellKind[] columnKinds = new CellKind[16];
	// Each column's member's format and culture, once known; and the header record's names while it is written, to know them.
	private ColumnFormat?[]? columnFormats;
	private List<string>? header;
	// Each column's options and the style they give its cells, once known; null when no column has any.
	private ExcelColumnOptions?[]? columnOptions;
	private ColumnStyle?[]? columnStyles;
	// The font of the current row's cells: the title's in its row, bold in the header's (BoldHeader), else regular. And
	// whether a cell's style is more than its own number format: in those rows, and in a sheet whose columns have styles.
	private CellFont rowFont;
	private bool styling;
	// The title's merge: the last column of the first record, which it spans; -1 until known, or without a title.
	private int titleLastColumn = -1;
	// The part of the sheet being written, and its entry once a flush opened it.
	private string partName;
	private Stream? entry;
	// The fields a record has before WriteField looks at the limits: Excel's columns, or 0 once the sheet is full.
	private int fieldLimit = CellReference.MaxColumns;
	// The header's row as each new sheet repeats it (repeatsHeader): its XML, its references to shared strings, its last
	// cell's column (-1 without one), and whether one of its text cells starts like a number.
	private byte[]? headerRow;
	private int headerReferences;
	private int headerLastColumn = -1;
	private bool headerNumberLikeText;
	// Where a full sheet ends in the buffer, until the flush that closes its entry (NewSheet); -1 otherwise. And the part of
	// the sheet after it.
	private int sheetEnd = -1;
	private string? nextPartName;
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
	/// <param name="maxRows">The rows a sheet holds before <see cref="ExcelWriterOptions.RowLimit"/> applies: at least 2.</param>
	/// <param name="settings">How the sheet looks.</param>
	/// <param name="members">The formats of the record type's members, or <see langword="null"/> for none.</param>
	/// <param name="headerNames">
	/// Without <see cref="CsvWriterOptions.HasHeader"/>, the names the record writer writes in its header, which say which
	/// member each column is; or <see langword="null"/>. With a header, the header record's names say.
	/// </param>
	/// <param name="columns">
	/// Without <see cref="CsvWriterOptions.HasHeader"/>, each column's options (<see cref="SheetSettings.ColumnsFor"/>), or
	/// <see langword="null"/> for none. With a header, they are resolved when the header record ends.
	/// </param>
	public SheetSink(ExcelWorkbookWriter workbook, PackageWriter package, string name, string partName, SharedStringTable? strings, StyleRegistry styles, ExcelWriterOptions options, int maxRows,
		SheetSettings settings, MemberFormats? members, IReadOnlyList<string>? headerNames, ExcelColumnOptions?[]? columns)
	{
		this.workbook = workbook;
		this.package = package;
		this.partName = partName;
		this.strings = strings;
		this.styles = styles;
		this.options = options;
		this.maxRows = maxRows;
		Settings = settings;
		culture = NumberCulture.For(options.Csv.FormatProvider);
		title = settings.Title is { } text ? Encoding.UTF8.GetBytes(text) : null;
		firstRow = title is null ? 1 : 2;
		headerRowNumber = options.Csv.HasHeader ? firstRow : 0;
		firstDataRow = options.Csv.HasHeader ? firstRow + 1 : firstRow;
		repeatsHeader = options.Csv.HasHeader && options.RowLimit == ExcelRowLimit.NewSheet;
		Location = new SheetLocation(name);
		item = strings is null ? null : new XmlOutput(256);
		if (options.Csv.HasHeader)
		{
			if (members is not null || settings.HasColumns)
			{
				this.members = members;
				header = [];
			}
		}
		else
		{
			if (members is not null && headerNames is not null)
			{
				columnFormats = members.Columns(headerNames, options.Csv.FormatProvider);
			}

			SetColumns(columns);
		}

		WriteTitle();
		SetRowFont(settings.BoldHeader ? CellFont.Bold : CellFont.Regular);
	}

	/// <summary>The name of the sheet being written (a new one's, after <see cref="ExcelRowLimit.NewSheet"/>), and how messages name its cells.</summary>
	public SheetLocation Location { get; private set; }

	/// <summary>How the sheet looks: its options, resolved and checked when it was added.</summary>
	public SheetSettings Settings { get; }

	/// <summary>Whether the cells being written are the first data record's, whose typed cells set their columns' widths.</summary>
	private bool MeasuringWidths => !widthsKnown && row == firstDataRow;

	/// <summary>Writes a field's cell into the current record.</summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="type">The type it was formatted from.</param>
	/// <exception cref="InvalidOperationException">
	/// The field is past Excel's last column, or its record past the sheet's last row with
	/// <see cref="ExcelRowLimit.Throw"/>; its text is too long for a cell; or its value is one that the options say to
	/// throw for.
	/// </exception>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type)
	{
		// The column counts whether or not the field throws, as the writer's field count does.
		var at = column++;
		if (at >= fieldLimit)
		{
			OnLimit(at);
		}

		// The header record's names, which say which member each column is.
		header?.Add(Encoding.UTF8.GetString(utf8.ToArray()));
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
			case CellKind.Formula:
				WriteFormula(utf8, at);

				break;
			default:
				WriteText(utf8, at);

				break;
		}
	}

	/// <summary>
	/// Ends the current record: closes its row, if it has one, and keeps the header's row for the new sheets of
	/// <see cref="ExcelRowLimit.NewSheet"/>. When the sheet is full, the next record's first field finds it.
	/// </summary>
	/// <returns>
	/// Whether the buffer has passed <see cref="FlushSize"/>, or holds a full sheet's end, and can be written: not while
	/// records are held.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The record is the header, and <see cref="ExcelSheetOptions.Columns"/> names a column it doesn't have; the record is
	/// discarded.
	/// </exception>
	public override bool EndRecord()
	{
		if (column == 0)
		{
			// The writer ends records of one field or more, so this one was discarded here when it failed to end (the
			// header's columns, below), and the writer, which doesn't discard it then, ends it again as it is disposed.
			return false;
		}

		if (header is not null)
		{
			ResolveColumns(header);
		}

		var hasRow = rowOpen;
		if (rowOpen)
		{
			output.Append("</row>"u8);
			rowOpen = false;
			lastRow = row;
			lastColumn = Math.Max(lastColumn, recordLastColumn);
		}

		if (row == headerRowNumber && repeatsHeader)
		{
			headerRow = output.Written.Slice(recordStart).ToArray();
			headerReferences = recordReferences;
			headerLastColumn = hasRow ? recordLastColumn : -1;
			headerNumberLikeText = recordNumberLikeText;
		}

		if (row == firstRow && title is not null)
		{
			titleLastColumn = column - 1;
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
		if (row > maxRows)
		{
			fieldLimit = 0;
		}

		if (rowFont != CellFont.Regular)
		{
			// The header's row is over.
			SetRowFont(CellFont.Regular);
		}

		column = 0;
		recordStart = output.Length;

		return widthsKnown && (recordStart >= FlushSize || sheetEnd >= 0);
	}

	/// <summary>Drops the current record's cells, and the widths they set.</summary>
	public override void DiscardRecord()
	{
		output.Truncate(recordStart);
		rowOpen = false;
		column = 0;
		recordReferences = 0;
		recordNumberLikeText = false;
		header?.Clear();
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
			AppendEnd();
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

	/// <summary>
	/// A field at the field limit: past Excel's last column, which throws, or the first of a record past the sheet's last
	/// row, which throws or starts the next sheet, as <see cref="ExcelWriterOptions.RowLimit"/> says.
	/// </summary>
	/// <param name="at">The field's column.</param>
	/// <exception cref="InvalidOperationException">The field is past the last column, or its record past the last row with <see cref="ExcelRowLimit.Throw"/>.</exception>
	private void OnLimit(int at)
	{
		if (row > maxRows)
		{
			if (options.RowLimit == ExcelRowLimit.Throw)
			{
				throw new InvalidOperationException(
					$"The sheet {Location.QuotedName} is full at row {maxRows.ToString("N0", CultureInfo.InvariantCulture)}, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet.");
			}

			// The record's first field: the next sheet takes it.
			StartNextSheet();

			return;
		}

		throw new InvalidOperationException(
			$"Record {row.ToString("N0", CultureInfo.InvariantCulture)} has a field in column {Letters(at)}, past XFD, Excel's last column.");
	}

	/// <summary>
	/// Ends the full sheet in the buffer and starts the next one there (<see cref="ExcelRowLimit.NewSheet"/>), named as
	/// Excel names a copy of it, with the title's row and the header's first. The next flush, which <see cref="EndRecord"/>
	/// asks for, writes the full sheet, closes its entry and opens the next one's.
	/// </summary>
	private void StartNextSheet()
	{
		AppendEnd();
		sheetEnd = output.Length;
		var (name, part) = workbook.ContinueSheet(Location.Name);
		Location = new SheetLocation(name);
		nextPartName = part;
		row = 1;
		lastRow = 0;
		lastColumn = -1;
		numberLikeText = false;
		recordStart = output.Length;
		WriteTitle();
		if (headerRow is not null)
		{
			// Its cells are in the same row as in every sheet, and its shared strings are referred to once more.
			output.Append(headerRow);
			if (headerLastColumn >= 0)
			{
				lastRow = headerRowNumber;
				lastColumn = Math.Max(lastColumn, headerLastColumn);
			}

			numberLikeText |= headerNumberLikeText;
			if (strings is not null)
			{
				strings.References += headerReferences;
			}

			row = headerRowNumber + 1;
		}

		recordStart = output.Length;
		fieldLimit = CellReference.MaxColumns;
	}

	/// <summary>The format and culture of a column's member, once the columns' members are known.</summary>
	/// <param name="at">The column.</param>
	/// <returns>Them, or <see langword="null"/> when the column's member has neither, or isn't known.</returns>
	private ColumnFormat? FormatOf(int at) => columnFormats is { } formats && at < formats.Length ? formats[at] : null;

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

		StartCell(at, 0);
		output.Append(" t=\"b\"><v>"u8);
		output.Append(value);
		output.Append("</v></c>"u8);
	}

	/// <summary>
	/// Writes a number cell, when the text is a number in .NET's default form, in the invariant culture or the writer's (or
	/// the column's member's <see cref="CsvCultureAttribute"/>): its invariant text goes into <c>v</c>. Other text, such as
	/// a format's, is a text cell.
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
		var numbers = FormatOf(at) is { HasCulture: true } column ? column.Numbers : culture;
		var length = DotNetText.ToInvariantNumber(text, numbers, invariant, out var significant);
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

		StartCell(at, 0);
		output.Append("><v>"u8);
		output.Append(invariant.Slice(0, length));
		output.Append("</v></c>"u8);
	}

	/// <summary>
	/// Writes a date cell from a <see cref="DateTime"/>'s <c>"O"</c> text: its clock time, whatever its kind; or from its
	/// member's format's text.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTime(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.DateTime);

			return;
		}

		if (!DotNetText.TryDateTime(text, out var ticks, out _))
		{
			WriteText(text, at);

			return;
		}

		WriteDateTime(text, at, ticks);
	}

	/// <summary>
	/// Writes a cell from a <see cref="DateTimeOffset"/>'s <c>"O"</c> text, as <see cref="ExcelWriterOptions.DateTimeOffsets"/>
	/// says: its text, or a date cell of its time in UTC or of its clock time. The text of its member's format shows a clock
	/// time, without its offset, so it is a date cell only with <see cref="ExcelDateTimeOffsets.ClockTime"/>.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTimeOffset(ReadOnlySpan<byte> text, int at)
	{
		if (options.DateTimeOffsets != ExcelDateTimeOffsets.Text && FormatOf(at) is { HasDateFormat: true } column)
		{
			if (options.DateTimeOffsets == ExcelDateTimeOffsets.ClockTime)
			{
				WriteFormatted(text, at, column, CellKind.DateTimeOffset);
			}
			else
			{
				WriteText(text, at);
			}

			return;
		}

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

	/// <summary>Writes a date cell from a <c>DateOnly</c>'s <c>"O"</c> text, in <c>yyyy-mm-dd</c>, or from its member's format's text.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDate(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.DateOnly);

			return;
		}

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

	/// <summary>
	/// Writes a time cell from a <c>TimeOnly</c>'s <c>"O"</c> text, in <c>hh:mm:ss</c>, with <c>.000</c> when it has
	/// milliseconds; or from its member's format's text.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteTime(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.TimeOnly);

			return;
		}

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
	/// <c>.000</c> when it has milliseconds. Its column's width is its own text's. A duration in its member's format, other
	/// than <c>c</c>, is a text cell, even when its text looks like <c>c</c>'s (<c>hh\:mm\:ss</c> drops the days).
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDuration(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDurationFormat: true } || !DotNetText.TryDuration(text, out var ticks))
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

	/// <summary>
	/// Writes a date or time cell from the text of its member's format, when Excel can show the format and the text reads
	/// back as a value that formats to the same text; otherwise a text cell. A date before 1900-01-01 follows
	/// <see cref="ExcelWriterOptions.OutOfRangeDates"/>.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="column">The column's format.</param>
	/// <param name="kind">The kind of value.</param>
	/// <exception cref="InvalidOperationException">The date is before 1900-01-01, and the options say to throw.</exception>
	private void WriteFormatted(ReadOnlySpan<byte> text, int at, ColumnFormat column, CellKind kind)
	{
		if (column.Translation(kind) is not { } format || !format.TryRead(text, out var milliseconds))
		{
			WriteText(text, at);

			return;
		}

		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at, duration: false);

			return;
		}

		WriteSerial(at, milliseconds, FormatsColumn(at) ? 0 : column.Style(styles), MeasuringWidths ? format.Width : 0);
	}

	/// <summary>Writes a number cell of a serial, in one of the writer's formats, and notes its column's width.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="format">The cell's number format.</param>
	/// <param name="width">The width its column needs, or 0 for none.</param>
	private void WriteSerial(int at, long milliseconds, FixedFormat format, double width) =>
		WriteSerial(at, milliseconds, FormatsColumn(at) ? 0 : styles.IndexOf(format), width);

	/// <summary>
	/// Whether a cell's column has a format of its own below the header, which takes the place of the cell's: then its own
	/// isn't added to the workbook's cell formats, which no cell would use.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>Whether it has.</returns>
	private bool FormatsColumn(int at) =>
		styling && rowFont == CellFont.Regular && columnStyles is { } columns && at < columns.Length && columns[at] is { HasFormat: true };

	/// <summary>Writes a number cell of a serial, in a cell format, and notes its column's width.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="style">The cell format's index.</param>
	/// <param name="width">The width its column needs, or 0 for none.</param>
	private void WriteSerial(int at, long milliseconds, int style, double width)
	{
		StartCell(at, style);
		output.Append("><v>"u8);
		output.AppendDouble(Serial.Days(milliseconds));
		output.Append("</v></c>"u8);
		if (width > 0 && MeasuringWidths)
		{
			widths.Add((at, width));
		}
	}

	/// <summary>
	/// Writes a formula cell from an <see cref="ExcelFormula"/>'s text, <c>=</c> and the formula: <c>f</c> alone, without a
	/// cached value, which Excel and LibreOffice compute on open. The formula is escaped as text is: Excel decodes
	/// <c>_xHHHH_</c> in it (checked on 2026-11-06), and its CRs, which <see cref="ExcelFormula"/> keeps in its strings only,
	/// as <c>_x000D_</c>. A formula that starts or ends with white space has <c>xml:space="preserve"</c>, as text does, since
	/// a reader trims it otherwise: Excel writes it on a formula that starts with a space, and keeps that space.
	/// </summary>
	/// <param name="text">The field's text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteFormula(ReadOnlySpan<byte> text, int at)
	{
		var formula = text[0] == (byte)'=' ? text.Slice(1) : text;
		StartCell(at, 0);
		output.Append(XmlOutput.NeedsPreserve(formula) ? "><f xml:space=\"preserve\">"u8 : "><f>"u8);
		output.AppendText(formula);
		output.Append("</f></c>"u8);
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
		StartCell(at, 0);
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

	/// <summary>
	/// Writes the start of a cell, up to its attributes' end, with its style: its row first, when it is the record's first
	/// cell.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own: 0 (General) but for dates, times and durations.</param>
	private void StartCell(int at, int style)
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
		if (styling)
		{
			style = StyleOf(at, style);
		}

		if (style != 0)
		{
			output.Append("\" s=\""u8);
			output.AppendNumber(style);
		}

		output.Append((byte)'"');
	}

	/// <summary>A cell's style in the title's or the header's row, or in a column with a style.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="own">The cell format the cell has of its own.</param>
	/// <returns>The cell format: its own in the row's font, or in its column's style.</returns>
	private int StyleOf(int at, int own)
	{
		if (rowFont != CellFont.Regular)
		{
			return styles.WithFont(own, rowFont);
		}

		return columnStyles is { } columns && at < columns.Length && columns[at] is { } column ? column.Of(own) : own;
	}

	/// <summary>Sets the font of the current row's cells, and whether cells need more than their own style.</summary>
	/// <param name="font">The font.</param>
	private void SetRowFont(CellFont font)
	{
		rowFont = font;
		styling = font != CellFont.Regular || columnStyles is not null;
	}

	/// <summary>Resolves the columns' options and formats by the header's names, when the header record ends.</summary>
	/// <param name="names">The header's names.</param>
	/// <exception cref="InvalidOperationException">
	/// <see cref="ExcelSheetOptions.Columns"/> names a column the header doesn't have; the header record is discarded.
	/// </exception>
	private void ResolveColumns(List<string> names)
	{
		ExcelColumnOptions?[]? columns;
		try
		{
			columns = Settings.ColumnsFor(names);
		}
		catch
		{
			DiscardRecord();

			throw;
		}

		if (members is not null)
		{
			columnFormats = members.Columns(names, options.Csv.FormatProvider);
		}

		SetColumns(columns);
		header = null;
	}

	/// <summary>Takes the columns' options, and makes the styles of those with a format or wrap.</summary>
	/// <param name="columns">Each column's options, or <see langword="null"/> for none.</param>
	private void SetColumns(ExcelColumnOptions?[]? columns)
	{
		columnOptions = columns;
		if (columns is null)
		{
			return;
		}

		for (var i = 0; i < columns.Length; i++)
		{
			if (ColumnStyle.Styles(columns[i]))
			{
				columnStyles ??= new ColumnStyle?[columns.Length];
				columnStyles[i] = new ColumnStyle(columns[i]!, styles);
			}
		}

		SetRowFont(rowFont);
	}

	/// <summary>
	/// Writes the title's row, when the sheet has a title: its cell in bold Calibri 14, as a text cell, before the writer's
	/// first record. The record's row then starts below it.
	/// </summary>
	private void WriteTitle()
	{
		if (title is null)
		{
			return;
		}

		SetRowFont(CellFont.Title);
		WriteText(title, 0);
		output.Append("</row>"u8);
		rowOpen = false;
		lastRow = row;
		lastColumn = Math.Max(lastColumn, 0);
		numberLikeText |= recordNumberLikeText;
		recordNumberLikeText = false;
		if (strings is not null)
		{
			strings.References += recordReferences;
			recordReferences = 0;
		}

		row++;
		recordStart = output.Length;
		SetRowFont(CellFont.Regular);
	}

	/// <summary>
	/// Writes the XML before the rows into a buffer: the worksheet's start, the frozen panes, the columns, and
	/// <c>sheetData</c>'s start.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	private void AppendStart(XmlOutput xml)
	{
		xml.Append(Start);
		AppendViews(xml);
		AppendColumns(xml);
		xml.Append("<sheetData>"u8);
	}

	/// <summary>
	/// Writes the sheet's view, when it has frozen panes, as Excel writes them: the rows through the header
	/// (<see cref="SheetSettings.FreezeHeader"/>), the first columns (<see cref="SheetSettings.FreezeColumns"/>), or both,
	/// with the active pane's selection.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	private void AppendViews(XmlOutput xml)
	{
		var rows = Settings.FreezeHeader ? headerRowNumber : 0;
		var columns = Settings.FreezeColumns;
		if (rows == 0 && columns == 0)
		{
			return;
		}

		var pane = rows == 0 ? "topRight"u8 : columns == 0 ? "bottomLeft"u8 : "bottomRight"u8;
		xml.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane"u8);
		if (columns > 0)
		{
			xml.Append(" xSplit=\""u8);
			xml.AppendNumber(columns);
			xml.Append((byte)'"');
		}

		if (rows > 0)
		{
			xml.Append(" ySplit=\""u8);
			xml.AppendNumber(rows);
			xml.Append((byte)'"');
		}

		xml.Append(" topLeftCell=\""u8);
		xml.AppendReference(ColumnName(columns), rows + 1);
		xml.Append("\" activePane=\""u8);
		xml.Append(pane);
		xml.Append("\" state=\"frozen\"/><selection pane=\""u8);
		xml.Append(pane);
		xml.Append("\"/></sheetView></sheetViews>"u8);
	}

	/// <summary>
	/// Writes the columns that have a width, a style or are hidden, in order, each as a <c>col</c> of its own: the width
	/// its options give (<see cref="ExcelColumnOptions.Width"/>), else the one measured, else the default width, which a
	/// <c>col</c> needs, since Excel hides one without.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	private void AppendColumns(XmlOutput xml)
	{
		var count = Math.Max(columnOptions?.Length ?? 0, widths.Count > 0 ? widths[widths.Count - 1].Column + 1 : 0);
		var measured = 0;
		var any = false;
		for (var at = 0; at < count; at++)
		{
			var width = 0.0;
			var custom = false;
			if (measured < widths.Count && widths[measured].Column == at)
			{
				width = widths[measured++].Width;
				custom = true;
			}

			var column = columnOptions is { } columns && at < columns.Length ? columns[at] : null;
			if (column?.Width is { } characters)
			{
				width = ColumnWidths.FromCharacters(characters);
				custom = true;
			}

			var style = columnStyles is { } styled && at < styled.Length ? styled[at]?.Default ?? 0 : 0;
			var hidden = column?.Hidden == true;
			if (!custom && style == 0 && !hidden)
			{
				continue;
			}

			if (!any)
			{
				xml.Append("<cols>"u8);
				any = true;
			}

			xml.Append("<col min=\""u8);
			xml.AppendNumber(at + 1);
			xml.Append("\" max=\""u8);
			xml.AppendNumber(at + 1);
			xml.Append("\" width=\""u8);
			xml.AppendDouble(custom ? width : ColumnWidths.Default);
			xml.Append((byte)'"');
			if (style != 0)
			{
				xml.Append(" style=\""u8);
				xml.AppendNumber(style);
				xml.Append((byte)'"');
			}

			if (hidden)
			{
				xml.Append(" hidden=\"1\""u8);
			}

			xml.Append(custom ? " customWidth=\"1\"/>"u8 : "/>"u8);
		}

		if (any)
		{
			xml.Append("</cols>"u8);
		}
	}

	/// <summary>
	/// Writes the XML after the sheet's rows into the buffer: the end of <c>sheetData</c>, the title's merge, the page
	/// margins, <c>ignoredErrors</c> over the used range when a text cell starts like a number, and the worksheet's end, in
	/// the schema's order, which Excel keeps to.
	/// </summary>
	private void AppendEnd()
	{
		output.Append("</sheetData>"u8);
		if (titleLastColumn > 0)
		{
			output.Append("<mergeCells count=\"1\"><mergeCell ref=\"A1:"u8);
			output.AppendReference(ColumnName(titleLastColumn), 1);
			output.Append("\"/></mergeCells>"u8);
		}

		output.Append(PageMargins);
		if (numberLikeText)
		{
			// Excel flags text that reads as a number with a warning triangle; over the used range, it doesn't.
			output.Append("<ignoredErrors><ignoredError sqref=\"A1:"u8);
			output.AppendReference(ColumnName(lastColumn), lastRow);
			output.Append("\" numberStoredAsText=\"1\"/></ignoredErrors>"u8);
		}

		output.Append("</worksheet>"u8);
	}

	/// <summary>
	/// Writes the whole records buffered to the entry, opening it and writing the XML before the rows first, once the
	/// columns' widths are known; until then the records are held. A full sheet in the buffer (<see cref="ExcelRowLimit.NewSheet"/>)
	/// is written first, and its entry closed, so that the next sheet's entry can be opened.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask FlushAsync(bool async, CancellationToken cancellationToken)
	{
		if (!widthsKnown)
		{
			return;
		}

		if (sheetEnd >= 0)
		{
			await WriteBufferAsync(sheetEnd, async, cancellationToken).ConfigureAwait(false);
			await PackageWriter.CloseAsync(entry!, async).ConfigureAwait(false);
			entry = null;
			startWritten = false;
			partName = nextPartName!;
			sheetEnd = -1;
		}

		if (recordStart > 0 || !startWritten)
		{
			await WriteBufferAsync(recordStart, async, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Writes the buffer's first bytes to the entry, opening it and writing the XML before the rows first.</summary>
	/// <param name="count">The bytes: whole records, or a whole sheet's rows and end, at most <see cref="recordStart"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask WriteBufferAsync(int count, bool async, CancellationToken cancellationToken)
	{
		entry ??= await package.OpenPartAsync(partName, async, cancellationToken).ConfigureAwait(false);
		if (!startWritten)
		{
			using var start = new XmlOutput(Start.Length + 256 + ((widths.Count + (columnOptions?.Length ?? 0)) * 96));
			AppendStart(start);
			await start.WriteToAsync(entry, start.Length, async, cancellationToken).ConfigureAwait(false);
			startWritten = true;
		}

		await output.WriteToAsync(entry, count, async, cancellationToken).ConfigureAwait(false);
		output.RemoveFirst(count);
		recordStart -= count;
	}
}
