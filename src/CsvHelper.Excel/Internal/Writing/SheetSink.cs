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
/// The first records are held, since the columns' widths come before the rows (<see cref="AutoFit"/>). With auto-fit
/// (<see cref="ExcelSheetOptions.AutoFit"/>), they are the header and the records auto-fit measures, and each column is as
/// wide as its widest cell among them: the header's in bold when it is, with its filter button when it has one; text on
/// one line, or its widest line when it wraps; numbers in General (<see cref="NumberFormatWidth.General"/>) or in their
/// column's format; booleans as <c>TRUE</c> and <c>FALSE</c>; dates, times and durations in their formats; and formulas,
/// whose values aren't known, as the default column. The title counts nothing. Without auto-fit, they are the records up
/// to the first data record (the one after the header, with <see cref="CsvWriterOptions.HasHeader"/>), and a typed cell
/// of it in a format that its column's default width can't show (a date, a time, a duration, or a cell in its column's
/// format) gets a width that fits it (<see cref="ColumnWidths"/>). A column's widths are the first sheet's in the new
/// sheets of <see cref="ExcelRowLimit.NewSheet"/> too.
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
/// A filter (<see cref="SheetSettings.AutoFilter"/>) spans the header's fields, from the header to the last row with a
/// cell, and the workbook gets its hidden <c>_xlnm._FilterDatabase</c> name. A table (<see cref="SheetSettings.Table"/>)
/// spans them from the header to the last row, one row below the header at least: its header's cells wait for the
/// header's end, when every name is known, and are written as text named as Excel names a table's columns
/// (<see cref="TableWriter.ColumnNames"/>). Its part and the sheet's relationships part are written after the sheet's
/// entry is closed. The ranges merged (<see cref="MergeCells"/>) are checked as they are added, and written after the
/// rows, after the title's.
/// </para>
/// <para>
/// A <see cref="Uri"/> field below the header is a text cell of its text, as for CSV, with a link to it in the
/// <c>Hyperlink</c> cell style when <see cref="ExcelWriterOptions.Links"/> takes it, up to Excel's 65,530 links a sheet
/// (<see cref="SheetLinks"/>). The links are written after the merged ranges, and their relationships after the table's,
/// in the sheet's relationships part.
/// </para>
/// <para>
/// A record past the sheet's last row follows <see cref="ExcelWriterOptions.RowLimit"/>. Its first field finds it, through
/// the field limit that <see cref="EndRecord"/> lowers to 0 once the sheet is full, so that the test of the column limit
/// is the only one per field. With <see cref="ExcelRowLimit.NewSheet"/>, the full sheet's end goes into the buffer, and the
/// next sheet's rows after it, the title's and the header's first; the next flush, which <see cref="EndRecord"/> asks
/// for, writes the full sheet, closes its entry, writes its relationships and table parts and opens the next one's. Each
/// new sheet has its own filter, its own table, named with a number after the first's, its own links, and no merged
/// ranges but the title's. So no I/O happens in <see cref="WriteField"/>, and the switch is sync or async as the caller
/// is.
/// </para>
/// </remarks>
internal sealed class SheetSink : CsvCellSink
{
	/// <summary>The bytes of XML a sheet buffers before it asks to be flushed.</summary>
	public const int FlushSize = 64 * 1024;

	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxSignificantDigits = 15;

	/// <summary>Why Excel can't show a date before 1900-01-01, for <see cref="WriteOutOfRange"/>'s message.</summary>
	private const string BeforeFirstDate = "before 1900-01-01, Excel's first date";

	/// <summary>Why Excel can't show a negative duration, for <see cref="WriteOutOfRange"/>'s message.</summary>
	private const string NegativeDuration = "a negative duration, which Excel shows as ########";

	/// <summary>Why Excel can't show a duration of <see cref="Serial.PastLast"/> or more, for <see cref="WriteOutOfRange"/>'s message.</summary>
	private const string LongDuration = "a duration of 2,958,466 days or more, which Excel shows as ########";

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
	// The row of the writer's first record: 2 below a title, else 1; the header's row, or 0 without a header; the row of
	// the first data record; and the last row held while its cells are measured: the first data record's, or, with
	// auto-fit, the last record's that it measures.
	private readonly long firstRow;
	private readonly long headerRowNumber;
	private readonly long firstDataRow;
	private readonly long lastHeldRow;
	// Whether auto-fit measures every held cell, rather than the first data record's typed cells in a format; and whether
	// the header's cells measure in bold, and with a filter button.
	private readonly bool fits;
	private readonly bool boldHeaderWidths;
	private readonly bool filterButtons;
	// The rows a sheet holds: Excel's last row's number, or fewer in tests.
	private readonly int maxRows;
	// Whether each new sheet of RowLimit.NewSheet repeats the header, which the first record is.
	private readonly bool repeatsHeader;
	private readonly XmlOutput output = new(FlushSize + 4096);
	// The XML of a shared string, before the table gives its index.
	private readonly XmlOutput? item;
	// The held cells' widths; and the columns' widths once known, by column (0 for none), or null for none.
	private readonly AutoFit measured = new();
	private double[]? columnWidths;
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
	// In the header's row of a sheet whose columns may have styles: the header's cells say their style, General's (s="0")
	// too, since LibreOffice gives a written cell without one its column's style, where Excel gives it General.
	private bool headerSaysStyle;
	// The title's merge: the last column of the first record, which it spans; -1 until known, or without a title.
	private int titleLastColumn = -1;
	// The first sheet's table's name, and the name of the table of the sheet being written (a number after the first's in
	// the new sheets of NewSheet); null without a table. And its columns' names, once the header has ended.
	private readonly string? firstTableName;
	private string? tableName;
	private string[]? tableColumns;
	// The header's fields, which the filter and the table span; 0 until the header has ended.
	private int headerColumns;
	// The ranges merged in the sheet being written (MergeCells), or null for none.
	private MergedRanges? merges;
	// The links of the sheet being written, or null for none; and the cell format of a link's cell, 0 until first used.
	private SheetLinks? links;
	private int linkStyle;
	// The parts to write once a sheet's entry is closed: its relationships and its table's; null for none. A full sheet's
	// (NewSheet) and the next one's can both wait, when the next one completes before a flush.
	private List<PartsAfter>? partsAfter;
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
	// Whether the columns' widths are known, so that the held records can be written; whether the current record's cells
	// are measured; and whether the XML before the rows is written.
	private bool widthsKnown;
	private bool measuring;
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
	/// <param name="tableName">The name of the sheet's table, which the workbook doesn't have; <see langword="null"/> without a table.</param>
	public SheetSink(ExcelWorkbookWriter workbook, PackageWriter package, string name, string partName, SharedStringTable? strings, StyleRegistry styles, ExcelWriterOptions options, int maxRows,
		SheetSettings settings, MemberFormats? members, IReadOnlyList<string>? headerNames, ExcelColumnOptions?[]? columns, string? tableName)
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
		fits = settings.AutoFit > 0;
		lastHeldRow = fits ? firstDataRow + settings.AutoFit - 1 : firstDataRow;
		boldHeaderWidths = settings.BoldHeader || (settings.HasHeader && settings.Table?.Style is not null);
		filterButtons = settings.HasHeader && (settings.AutoFilter || settings.Table is { FilterButtons: true });
		repeatsHeader = options.Csv.HasHeader && options.RowLimit == ExcelRowLimit.NewSheet;
		headerSaysStyle = options.Csv.HasHeader && settings.HasColumns;
		Location = new SheetLocation(name);
		item = strings is null ? null : new XmlOutput(256);
		firstTableName = this.tableName = options.Csv.HasHeader ? tableName : null;
		if (options.Csv.HasHeader)
		{
			if (members is not null || settings.HasColumns || this.tableName is not null)
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
		UpdateMeasuring();
	}

	/// <summary>The name of the sheet being written (a new one's, after <see cref="ExcelRowLimit.NewSheet"/>), and how messages name its cells.</summary>
	public SheetLocation Location { get; private set; }

	/// <summary>How the sheet looks: its options, resolved and checked when it was added.</summary>
	public SheetSettings Settings { get; }

	/// <summary>The number of distinct link targets the sheet holds until it ends, the current record's included.</summary>
	public int LinkTargets => links?.Targets ?? 0;

	/// <summary>Whether the cell being measured is the header's, in bold.</summary>
	private bool MeasuresBold => boldHeaderWidths && row == headerRowNumber;

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

		// The header record's names, which say which member each column is, and name a table's columns, whose cells are
		// written when the header ends.
		if (header is not null && AddHeaderName(utf8, at))
		{
			return;
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
			case CellKind.Formula:
				WriteFormula(utf8, at);

				break;
			case CellKind.Uri:
				WriteUri(utf8, at);

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
			EndHeader(header);
		}

		if (row == headerRowNumber)
		{
			headerColumns = column;
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

		if (measuring)
		{
			if (filterButtons && row == headerRowNumber)
			{
				measured.AddFilterButtons(column, row);
			}

			measured.EndRecord();
		}

		links?.EndRecord();
		row++;
		if (row > maxRows)
		{
			fieldLimit = 0;
		}

		if (!widthsKnown && (row > lastHeldRow || row > maxRows))
		{
			// The last record held, or the sheet is full: the next sheets of NewSheet take its widths.
			KnowWidths();
		}
		else
		{
			UpdateMeasuring();
		}

		if (rowFont != CellFont.Regular || headerSaysStyle)
		{
			// The header's row is over.
			headerSaysStyle = false;
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
		links?.DiscardRecord();
		if (measuring)
		{
			measured.DiscardRecord();
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
			if (!widthsKnown)
			{
				KnowWidths();
			}

			AppendEnd();
			recordStart = output.Length;
			await FlushAsync(async, CancellationToken.None).ConfigureAwait(false);
			await PackageWriter.CloseAsync(entry!, async).ConfigureAwait(false);
			await WritePartsAfterAsync(async, CancellationToken.None).ConfigureAwait(false);
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
	internal static byte[] ColumnName(int column) =>
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
		merges = null;
		if (tableColumns is not null)
		{
			// Each sheet has its own table, named with a number after the first's.
			tableName = workbook.NextTableName(firstTableName!);
		}

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
		if (measuring)
		{
			MeasureBool(at, value == (byte)'1');
		}
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
		if (measuring)
		{
			MeasureNumber(at, invariant.Slice(0, length));
		}
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
			WriteOutOfRange(text, at, BeforeFirstDate);

			return;
		}

		var format = milliseconds % Serial.MillisecondsPerDay == 0 ? FixedFormat.Date : milliseconds % 1000 == 0 ? FixedFormat.DateTime : FixedFormat.DateTimeMilliseconds;
		WriteSerial(at, milliseconds, format, format == FixedFormat.DateTimeMilliseconds ? ColumnWidths.DateTimeMillisecondsPixels : ColumnWidths.DateTimePixels);
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
			WriteOutOfRange(text, at, BeforeFirstDate);

			return;
		}

		WriteSerial(at, milliseconds, FixedFormat.Date, ColumnWidths.DatePixels);
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
		WriteSerial(at, milliseconds, whole ? FixedFormat.Time : FixedFormat.TimeMilliseconds, whole ? ColumnWidths.TimePixels : ColumnWidths.TimeMillisecondsPixels);
	}

	/// <summary>
	/// Writes a duration cell from a <see cref="TimeSpan"/>'s <c>"c"</c> text: its days, in <c>[h]:mm:ss</c>, with
	/// <c>.000</c> when it has milliseconds. Its column's width is its own text's. A duration in its member's format, other
	/// than <c>c</c>, is a text cell, even when its text looks like <c>c</c>'s (<c>hh\:mm\:ss</c> drops the days). A negative
	/// duration, and one of <see cref="Serial.PastLast"/> or more, follow <see cref="ExcelWriterOptions.OutOfRangeDates"/>.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <exception cref="InvalidOperationException">Excel can't show the duration, and the options say to throw.</exception>
	private void WriteDuration(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDurationFormat: true } || !DotNetText.TryDuration(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.DurationMilliseconds(ticks);
		if (milliseconds < 0 || milliseconds >= Serial.PastLast)
		{
			WriteOutOfRange(text, at, milliseconds < 0 ? NegativeDuration : LongDuration);

			return;
		}

		var format = milliseconds % 1000 == 0 ? FixedFormat.Duration : FixedFormat.DurationMilliseconds;
		WriteSerial(at, milliseconds, format, measuring ? ColumnWidths.TextPixels(DurationText(milliseconds).AsSpan()) : 0);
	}

	/// <summary>
	/// Writes a date, time or duration that Excel can't show, as <see cref="ExcelWriterOptions.OutOfRangeDates"/> says: a
	/// text cell of its text, or an exception.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="why">Why Excel can't show it, for the exception's message: <see cref="BeforeFirstDate"/>, <see cref="NegativeDuration"/> or <see cref="LongDuration"/>.</param>
	/// <exception cref="InvalidOperationException">The options say to throw.</exception>
	private void WriteOutOfRange(ReadOnlySpan<byte> text, int at, string why)
	{
		if (options.OutOfRangeDates == ExcelOutOfRangeDates.Throw)
		{
			var value = Encoding.UTF8.GetString(text.ToArray());

			throw new InvalidOperationException($"Cell {Location.Cell(at, row)} holds {value}, {why} (ExcelWriterOptions.OutOfRangeDates is Throw).");
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
			WriteOutOfRange(text, at, BeforeFirstDate);

			return;
		}

		WriteSerial(at, milliseconds, FormatsColumn(at) ? 0 : column.Style(styles), measuring ? format.Pixels : 0);
	}

	/// <summary>Writes a number cell of a serial, in one of the writer's formats, and measures it.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="format">The cell's number format.</param>
	/// <param name="pixels">The pixels of its widest text in its format, or 0 when it isn't measured.</param>
	private void WriteSerial(int at, long milliseconds, FixedFormat format, double pixels) =>
		WriteSerial(at, milliseconds, FormatsColumn(at) ? 0 : styles.IndexOf(format), pixels);

	/// <summary>
	/// Whether a cell's column has a format of its own below the header, which takes the place of the cell's: then its own
	/// isn't added to the workbook's cell formats, which no cell would use.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>Whether it has.</returns>
	private bool FormatsColumn(int at) =>
		styling && rowFont == CellFont.Regular && columnStyles is { } columns && at < columns.Length && columns[at] is { HasFormat: true };

	/// <summary>The style a cell takes from its column: below the header, in a column with a format or wrap.</summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>The style, or <see langword="null"/>.</returns>
	private ColumnStyle? ColumnStyleOf(int at) =>
		styling && rowFont == CellFont.Regular && columnStyles is { } columns && at < columns.Length ? columns[at] : null;

	/// <summary>The widths of the format a cell takes from its column, if it takes one.</summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>The format's widths, or <see langword="null"/>.</returns>
	private NumberFormatWidth? FormatWidthOf(int at) => ColumnStyleOf(at)?.FormatWidth;

	/// <summary>
	/// Measures a number cell: in its column's format, or, with auto-fit, in General. Without auto-fit, a number in General
	/// needs no width, since Excel shows it in fewer digits rather than as <c>########</c>.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="invariant">The number's invariant text.</param>
	private void MeasureNumber(int at, ReadOnlySpan<byte> invariant)
	{
		var format = FormatWidthOf(at);
		if (format is null && !fits)
		{
			return;
		}

		var value = double.Parse(Encoding.ASCII.GetString(invariant.ToArray()), NumberStyles.Float, CultureInfo.InvariantCulture);
		var pixels = format?.NumberPixels(value) ?? NumberFormatWidth.GeneralPixels(value);
		measured.Add(at, row, pixels, typed: true);
	}

	/// <summary>Measures a boolean cell: <c>TRUE</c> or <c>FALSE</c>, in its column format's text section if it has one.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="value">The boolean.</param>
	private void MeasureBool(int at, bool value)
	{
		var format = FormatWidthOf(at);
		if (format is null && !fits)
		{
			return;
		}

		var pixels = ColumnWidths.TextPixels((value ? "TRUE" : "FALSE").AsSpan(), MeasuresBold);
		measured.Add(at, row, format?.TextPixels(pixels) ?? pixels, typed: true);
	}

	/// <summary>Measures a date, time or duration cell: in its column's format, else in its own.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="pixels">The pixels of its widest text in its own format.</param>
	private void MeasureSerial(int at, long milliseconds, double pixels)
	{
		if (FormatWidthOf(at) is { } format)
		{
			pixels = format.NumberPixels(Serial.Days(milliseconds));
		}

		if (pixels > 0)
		{
			measured.Add(at, row, pixels, typed: true);
		}
	}

	/// <summary>
	/// Measures a text cell, with auto-fit: in bold in a bold header; its widest line when its column wraps, else all of it
	/// on one line; in its column format's text section if it has one.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="text">The text, valid UTF-8.</param>
	private void MeasureText(int at, ReadOnlySpan<byte> text)
	{
		var style = ColumnStyleOf(at);
		var pixels = AutoFit.TextPixels(text, MeasuresBold, style?.Wrap == true);
		if (style?.FormatWidth is { } format)
		{
			pixels = format.TextPixels(pixels);
		}

		measured.Add(at, row, pixels, typed: false);
	}

	/// <summary>
	/// Sets whether the current record's cells are measured: with auto-fit, the header's and those of the records it
	/// measures; without, the first data record's.
	/// </summary>
	private void UpdateMeasuring() =>
		measuring = !widthsKnown && (fits ? row >= firstRow && row <= lastHeldRow : row == firstDataRow);

	/// <summary>Works out the columns' widths from the cells measured, once the held records are: they can be written now.</summary>
	private void KnowWidths()
	{
		widthsKnown = true;
		measuring = false;
		columnWidths = measured.Widths(fits, ColumnWidths.FromCharacters(Settings.MaxAutoFitWidth), merges);
	}

	/// <summary>Writes a number cell of a serial, in a cell format, and measures it.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="style">The cell format's index.</param>
	/// <param name="pixels">The pixels of its widest text in its own format, or 0 when it isn't measured.</param>
	private void WriteSerial(int at, long milliseconds, int style, double pixels)
	{
		StartCell(at, style);
		output.Append("><v>"u8);
		output.AppendDouble(Serial.Days(milliseconds));
		output.Append("</v></c>"u8);
		if (measuring)
		{
			MeasureSerial(at, milliseconds, pixels);
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
		if (measuring && fits)
		{
			measured.AddFormula(at, row);
		}
	}

	/// <summary>
	/// Writes a URI's cell: a text cell of its text, as written, with a link to it in the <c>Hyperlink</c> cell style when
	/// <see cref="ExcelWriterOptions.Links"/> takes it (<see cref="SheetLinks.Target"/>), below the header and while the
	/// sheet has fewer than <see cref="SheetLinks.MaxLinks"/> links. The header's cells name its columns, and are text.
	/// </summary>
	/// <param name="text">The URI's text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	/// <exception cref="InvalidOperationException">The text has more than <see cref="XmlOutput.MaxCellLength"/> UTF-16 code units.</exception>
	private void WriteUri(ReadOnlySpan<byte> text, int at)
	{
		if (options.Links == ExcelLinks.None || row == headerRowNumber || links is { IsFull: true } ||
			SheetLinks.Target(Encoding.UTF8.GetString(text.ToArray()), options.Links) is not { } target)
		{
			WriteText(text, at);

			return;
		}

		if (linkStyle == 0)
		{
			linkStyle = styles.IndexOf(new CellXf(0, CellFont.Link, false));
		}

		WriteText(text, at, linkStyle);
		(links ??= new SheetLinks()).Add(row, at, target);
	}

	/// <summary>Writes a text cell: the string inline, or its index in the shared strings.</summary>
	/// <param name="text">The text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own: 0 (General), or a link's.</param>
	/// <exception cref="InvalidOperationException">The text has more than <see cref="XmlOutput.MaxCellLength"/> UTF-16 code units.</exception>
	private void WriteText(ReadOnlySpan<byte> text, int at, int style = 0)
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
		if (measuring && fits)
		{
			MeasureText(at, text);
		}

		StartCell(at, style);
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
	/// <param name="style">The cell format the cell has of its own: 0 (General) but for dates, times, durations and links.</param>
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
			if (style == 0 && headerSaysStyle)
			{
				output.Append("\" s=\"0"u8);
			}
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
		styling = font != CellFont.Regular || columnStyles is not null || headerSaysStyle;
	}

	/// <summary>
	/// Adds a header field's text to the header's names. With a table, the text is its column's name, checked, and its cell
	/// is written when the header ends, once every column's name is known (<see cref="EndHeader"/>).
	/// </summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="at">The field's column.</param>
	/// <returns>Whether the field's cell waits for the header's end: with a table.</returns>
	/// <exception cref="InvalidOperationException">With a table, the text has more than 255 characters; the field has no cell, as any field that fails.</exception>
	private bool AddHeaderName(ReadOnlySpan<byte> utf8, int at)
	{
		var name = Encoding.UTF8.GetString(utf8.ToArray());
		header!.Add(name);
		if (tableName is null)
		{
			return false;
		}

		if (name.Length > TableNames.MaxLength)
		{
			// The field has no cell, so its column is named as an empty one's if the header goes on.
			header[header.Count - 1] = "";

			throw new InvalidOperationException(
				$"Cell {Location.Cell(at, row)} has {name.Length.ToString(CultureInfo.InvariantCulture)} characters, and the header's cells name the table's columns, which Excel opens with {TableNames.MaxLength} at most.");
		}

		return true;
	}

	/// <summary>
	/// Ends the header record: checks the columns' options by the header's names and, with a table, the ranges merged before
	/// it; writes a table's header cells, named as Excel names a table's columns (<see cref="TableWriter.ColumnNames"/>);
	/// and resolves the columns' options and formats.
	/// </summary>
	/// <param name="names">The header's names.</param>
	/// <exception cref="InvalidOperationException">
	/// <see cref="ExcelSheetOptions.Columns"/> names a column the header doesn't have, or a range merged before the header
	/// is in the table's columns at or below it; the header record is discarded.
	/// </exception>
	private void EndHeader(List<string> names)
	{
		ExcelColumnOptions?[]? columns;
		try
		{
			columns = Settings.ColumnsFor(names);
			if (tableName is not null && merges is not null)
			{
				foreach (var range in merges.Ranges)
				{
					if (InTable(range, names.Count))
					{
						throw new InvalidOperationException(TableMergeProblem(range, names.Count));
					}
				}
			}
		}
		catch
		{
			DiscardRecord();

			throw;
		}

		if (tableName is not null)
		{
			// Before the columns' styles, which the header's cells don't take.
			tableColumns = TableWriter.ColumnNames(names);
			for (var at = 0; at < tableColumns.Length; at++)
			{
				WriteText(Encoding.UTF8.GetBytes(tableColumns[at]), at);
			}
		}

		if (members is not null)
		{
			columnFormats = members.Columns(names, options.Csv.FormatProvider);
		}

		SetColumns(columns);
		header = null;
	}

	/// <summary>Whether a range has a cell in the table's columns, at or below its header.</summary>
	/// <param name="range">The range.</param>
	/// <param name="columns">The table's columns.</param>
	/// <returns>Whether it has.</returns>
	private bool InTable(ExcelRange range, int columns) => range.LastRow >= headerRowNumber && range.FirstColumnIndex < columns;

	/// <summary>What is wrong with a range merged in the table.</summary>
	/// <param name="range">The range.</param>
	/// <param name="columns">The table's columns.</param>
	/// <returns>The message.</returns>
	private string TableMergeProblem(ExcelRange range, int columns) =>
		$"The range {range.Corners()} is merged in the columns of the table of the sheet {Location.QuotedName} (A to {CellReference.ColumnName(columns - 1)}), at or below its header in row {headerRowNumber.ToString(CultureInfo.InvariantCulture)}: Excel opens no table with merged cells.";

	/// <summary>
	/// Merges a range of the sheet being written: written with the sheet's other merged ranges after its rows. Its cells
	/// count nothing in auto-fit when it is across two columns or more.
	/// </summary>
	/// <param name="range">The range, of two cells or more.</param>
	/// <exception cref="ObjectDisposedException">The sheet is complete.</exception>
	/// <exception cref="ArgumentException">
	/// The range overlaps one merged before, is in row 1 under a title, or is in the table's columns at or below its header,
	/// which has ended; Excel would refuse the workbook.
	/// </exception>
	public void MergeCells(ExcelRange range)
	{
		if (completed)
		{
			throw new ObjectDisposedException(nameof(CsvWriter), $"The sheet {Location.QuotedName} is complete: merge its cells before disposing its CsvWriter.");
		}

		if (title is not null && range.FirstRow == 1)
		{
			throw new ArgumentException($"The range {range.Corners()} is in row 1, which holds the title of the sheet {Location.QuotedName}, merged across the header's columns.", nameof(range));
		}

		if (tableColumns is not null && InTable(range, tableColumns.Length))
		{
			throw new ArgumentException(TableMergeProblem(range, tableColumns.Length), nameof(range));
		}

		merges ??= new MergedRanges();
		if (merges.Overlapping(range) is { } other)
		{
			throw new ArgumentException($"The range {range.Corners()} overlaps {other.Corners()}, merged before in the sheet {Location.QuotedName}: Excel opens no sheet whose merged cells overlap.", nameof(range));
		}

		merges.Add(range);
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
	/// <c>col</c> needs, since Excel hides one without. A width auto-fit gives is marked <c>bestFit</c>, as Excel marks its
	/// AutoFit's, which widens such a column as numbers are typed into it.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	private void AppendColumns(XmlOutput xml)
	{
		var count = Math.Max(columnOptions?.Length ?? 0, columnWidths?.Length ?? 0);
		var any = false;
		for (var at = 0; at < count; at++)
		{
			var width = 0.0;
			var custom = false;
			var bestFit = false;
			if (columnWidths is { } fitted && at < fitted.Length && fitted[at] > 0)
			{
				width = fitted[at];
				custom = true;
				bestFit = fits;
			}

			var column = columnOptions is { } columns && at < columns.Length ? columns[at] : null;
			if (column?.Width is { } characters)
			{
				width = ColumnWidths.FromCharacters(characters);
				custom = true;
				bestFit = false;
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
			// A column's own width of 255 characters, or a formatted number's without auto-fit, is wider than the schema takes.
			xml.AppendDouble(custom ? Math.Min(width, ColumnWidths.Widest) : ColumnWidths.Default);
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

			xml.Append(bestFit ? " bestFit=\"1\" customWidth=\"1\"/>"u8 : custom ? " customWidth=\"1\"/>"u8 : "/>"u8);
		}

		if (any)
		{
			xml.Append("</cols>"u8);
		}
	}

	/// <summary>
	/// Writes the XML after the sheet's rows into the buffer, in the schema's order, which Excel keeps to: the end of
	/// <c>sheetData</c>; the filter over the header's columns, from the header down to the last row (and the workbook's
	/// hidden <c>_xlnm._FilterDatabase</c> name for it); the title's merge and the ranges merged; the links; the page
	/// margins; <c>ignoredErrors</c> over the used range when a text cell starts like a number; the table's part; and the
	/// worksheet's end. The sheet's relationships (its table's, <c>rId1</c>, then its links') and its table's part are
	/// kept to write once its entry is closed.
	/// </summary>
	private void AppendEnd()
	{
		output.Append("</sheetData>"u8);
		if (Settings.AutoFilter && headerColumns > 0)
		{
			var filter = new ExcelRange((int)headerRowNumber, 1, (int)Math.Max(lastRow, headerRowNumber), headerColumns);
			output.Append("<autoFilter ref=\""u8);
			output.Append(Encoding.ASCII.GetBytes(filter.Corners()));
			output.Append("\"/>"u8);
			workbook.AddFilter(filter);
		}

		var mergeCount = (titleLastColumn > 0 ? 1 : 0) + (merges?.Count ?? 0);
		if (mergeCount > 0)
		{
			output.Append("<mergeCells count=\""u8);
			output.AppendNumber(mergeCount);
			output.Append("\">"u8);
			if (titleLastColumn > 0)
			{
				output.Append("<mergeCell ref=\"A1:"u8);
				output.AppendReference(ColumnName(titleLastColumn), 1);
				output.Append("\"/>"u8);
			}

			if (merges is not null)
			{
				foreach (var range in merges.Ranges)
				{
					output.Append("<mergeCell ref=\""u8);
					output.Append(Encoding.ASCII.GetBytes(range.Corners()));
					output.Append("\"/>"u8);
				}
			}

			output.Append("</mergeCells>"u8);
		}

		var sheetLinks = links is { Count: > 0 } ? links : null;
		links = null;
		sheetLinks?.WriteHyperlinks(output, tableColumns is null ? 1 : 2);
		output.Append(PageMargins);
		if (numberLikeText)
		{
			// Excel flags text that reads as a number with a warning triangle; over the used range, it doesn't.
			output.Append("<ignoredErrors><ignoredError sqref=\"A1:"u8);
			output.AppendReference(ColumnName(lastColumn), lastRow);
			output.Append("\" numberStoredAsText=\"1\"/></ignoredErrors>"u8);
		}

		var tableId = 0;
		byte[]? table = null;
		if (tableColumns is not null)
		{
			// The header and the rows below it, one row at least, as Excel opens no table of a header alone.
			output.Append("<tableParts count=\"1\"><tablePart r:id=\"rId1\"/></tableParts>"u8);
			tableId = workbook.AddTable();
			using var xml = new XmlOutput(1024);
			TableWriter.Write(xml, tableId, tableName!, new ExcelRange((int)headerRowNumber, 1, (int)Math.Max(lastRow, headerRowNumber + 1), tableColumns.Length), tableColumns, Settings.Table!);
			table = xml.Written.ToArray();
		}

		output.Append("</worksheet>"u8);
		if (tableId > 0 || sheetLinks is not null)
		{
			var relationships = "xl/worksheets/_rels/" + partName.Substring("xl/worksheets/".Length) + ".rels";
			(partsAfter ??= []).Add(new PartsAfter(relationships, tableId, table, sheetLinks));
		}
	}

	/// <summary>
	/// Writes the parts kept to write once a sheet's entry is closed, each in an entry of its own: its relationships, its
	/// table's first and its links' after, streamed, since a sheet's links can have many targets; and its table's part.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask WritePartsAfterAsync(bool async, CancellationToken cancellationToken)
	{
		if (partsAfter is null)
		{
			return;
		}

		using var xml = new XmlOutput(FlushSize + 4096);
		foreach (var parts in partsAfter)
		{
			var entry = await package.OpenPartAsync(parts.Relationships, async, cancellationToken).ConfigureAwait(false);
			try
			{
				xml.Truncate(0);
				xml.Append(PackageParts.Declaration);
				xml.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"u8);
				if (parts.TableId > 0)
				{
					TableWriter.AppendRelationship(xml, parts.TableId);
				}

				if (parts.Links is { } sheetLinks)
				{
					for (var i = 0; i < sheetLinks.Relationships; i++)
					{
						sheetLinks.AppendRelationship(xml, i);
						if (xml.Length >= FlushSize)
						{
							await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
							xml.Truncate(0);
						}
					}
				}

				xml.Append("</Relationships>"u8);
				await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await PackageWriter.CloseAsync(entry, async).ConfigureAwait(false);
			}

			if (parts.Table is { } table)
			{
				entry = await package.OpenPartAsync(TableWriter.PartName(parts.TableId), async, cancellationToken).ConfigureAwait(false);
				try
				{
					if (async)
					{
						await entry.WriteAsync(table, 0, table.Length, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						entry.Write(table, 0, table.Length);
					}
				}
				finally
				{
					await PackageWriter.CloseAsync(entry, async).ConfigureAwait(false);
				}
			}
		}

		partsAfter = null;
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
			await WritePartsAfterAsync(async, cancellationToken).ConfigureAwait(false);
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
			using var start = new XmlOutput(Start.Length + 256 + (((columnWidths?.Length ?? 0) + (columnOptions?.Length ?? 0)) * 96));
			AppendStart(start);
			await start.WriteToAsync(entry, start.Length, async, cancellationToken).ConfigureAwait(false);
			startWritten = true;
		}

		await output.WriteToAsync(entry, count, async, cancellationToken).ConfigureAwait(false);
		output.RemoveFirst(count);
		recordStart -= count;
	}

	/// <summary>A sheet's parts to write once its entry is closed.</summary>
	/// <param name="Relationships">The name of the sheet's relationships part.</param>
	/// <param name="TableId">The sheet's table's id in the workbook, or 0 without a table.</param>
	/// <param name="Table">The table's part, or <see langword="null"/> without a table.</param>
	/// <param name="Links">The sheet's links, written into its XML, or <see langword="null"/> without any.</param>
	private sealed record PartsAfter(string Relationships, int TableId, byte[]? Table, SheetLinks? Links);
}
