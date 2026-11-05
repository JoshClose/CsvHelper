using System;

namespace CsvHelper.Excel;

/// <summary>
/// How a workbook is read: which sheet a reader reads, the options of its <see cref="CsvReader"/>, and the size limits
/// that keep a hostile file (a zip bomb) from inflating without bound. Immutable: use <c>with</c> to change a copy.
/// </summary>
/// <remarks>
/// <para>
/// The options given to <see cref="ExcelWorkbook.Open(string, ExcelReaderOptions?)"/> belong to the workbook, and are
/// what <see cref="ExcelWorkbook.OpenReader(ExcelReaderOptions?)"/> uses when it is given none. Options given to
/// <c>OpenReader</c> replace the workbook's for that reader; they are not merged.
/// </para>
/// <para>
/// Each call checks the parts it reads against its own options' limits: <c>Open</c> reads the content types, the
/// relationships and the workbook part; the first <c>OpenReader</c> reads the styles and the shared strings, which the
/// workbook then keeps; and each reader streams its sheet.
/// </para>
/// </remarks>
public sealed record ExcelReaderOptions
{
	/// <summary>The default options: the first worksheet, <see cref="CsvReaderOptions.Default"/>, and the default limits.</summary>
	public static ExcelReaderOptions Default { get; } = new();

	/// <summary>
	/// The name of the sheet to read, matched ignoring case (as Excel keeps names unique); <see langword="null"/> to read
	/// <see cref="SheetIndex"/>, or else the first worksheet. A sheet that <c>OpenReader</c> is given wins over it.
	/// </summary>
	public string? Sheet { get; init; }

	/// <summary>
	/// The 0-based place in tab order (<see cref="ExcelWorkbook.Sheets"/>, chartsheets included) of the sheet to read;
	/// <see langword="null"/> to read <see cref="Sheet"/>, or else the first worksheet. Setting both is an error.
	/// </summary>
	public int? SheetIndex { get; init; }

	/// <summary>
	/// A block of cells to read, in Excel's A1 form: <c>B5:F200</c>, <c>B5</c>, whole columns <c>B:F</c> or whole rows
	/// <c>5:200</c>, with or without <c>$</c>, in either case. With a sheet's name in front, quoted as Excel quotes it
	/// (<c>Orders!B5:F200</c>, <c>'Q1 Orders'!B5:F200</c>), it chooses the sheet, and no sheet can be given with it;
	/// without one, it is a block of the sheet chosen as usual. <see langword="null"/> to read the whole sheet.
	/// </summary>
	/// <remarks>
	/// Field 0 is the block's first column, and cells outside its columns are no fields. Rows above it are skipped, and
	/// reading stops after its last row, so nothing of the sheet after it is inflated. The block's first record is the
	/// header when <see cref="CsvReaderOptions.HasHeader"/> is set. At most one of <see cref="Range"/>, <see cref="Table"/>
	/// and <see cref="DefinedName"/> can be set.
	/// </remarks>
	public string? Range { get; init; }

	/// <summary>
	/// The name of an Excel table to read (<see cref="ExcelTable.Name"/>), matched ignoring case; it chooses the sheet, and no
	/// sheet can be given with it. <see langword="null"/> for none.
	/// </summary>
	/// <remarks>
	/// The reader reads the table's columns and its data rows: those below its header row and above its totals row. With
	/// <see cref="CsvReaderOptions.HasHeader"/>, its header record is the table's column names (<see cref="ExcelTable.Columns"/>),
	/// whether or not its header row shows; Excel keeps the header cells and the names alike. Without it, the reader reads
	/// the data rows alone.
	/// </remarks>
	public string? Table { get; init; }

	/// <summary>
	/// A defined name to read, whose block of cells (<see cref="ExcelDefinedName.Range"/>) it reads as <see cref="Range"/>
	/// does; it chooses the sheet, and no sheet can be given with it. <see langword="null"/> for none.
	/// </summary>
	/// <remarks>
	/// <c>Data</c> is the workbook's name <c>Data</c>, or else the one sheet's name <c>Data</c>; <c>Orders!Data</c> (quoted
	/// as Excel quotes the sheet's name) is that sheet's own. Excel's built-in names go without their <c>_xlnm.</c> prefix,
	/// such as <c>Orders!Print_Area</c>. Names are matched ignoring case, as Excel matches them.
	/// </remarks>
	public string? DefinedName { get; init; }

	/// <summary>
	/// Whether the rows Excel hides are skipped, so that the reader reads the rows Excel shows. <see langword="false"/> by
	/// default: hidden rows are read as any other, since hidden data is still data, and a filter hides rows too.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A row is hidden as Excel hides it: hidden by hand, by a filter or by a collapsed outline, or given a height of 0;
	/// and, in a sheet that hides the rows it doesn't list (as Excel writes a sheet whose rows are hidden from one row to
	/// the last), each row the file lacks. Excel also hides a row whose height is too small to show a pixel, which depends
	/// on the screen; only a height of 0 is hidden here.
	/// </para>
	/// <para>
	/// A hidden row is no record, and neither is a missing row that is hidden when
	/// <see cref="CsvReaderOptions.SkipEmptyLines"/> is off. A record's line is still its row, so record <c>n</c> is no
	/// longer row <c>n</c>.
	/// </para>
	/// </remarks>
	public bool SkipHiddenRows { get; init; }

	/// <summary>
	/// Whether the columns Excel hides are skipped, so that a record's fields are the columns Excel shows.
	/// <see langword="false"/> by default: hidden columns are read as any other.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A column is hidden as Excel hides it: hidden by hand or by a collapsed outline, or given a width of 0 (or none, which
	/// Excel reads as 0); and, in a sheet whose default width is 0 (as Excel writes a sheet whose columns are hidden from
	/// one column to the last), each column the file says nothing of. Excel also hides a column too narrow to show a pixel,
	/// which depends on the workbook's font; only a width of 0 is hidden here.
	/// </para>
	/// <para>
	/// A hidden column is no field: the fields are the shown columns of the sheet, or of the range, table or defined name
	/// read, in order, so field 2 is column <c>E</c> when <c>D</c> is hidden. Errors still name the cell. A table's header
	/// leaves out the names of its hidden columns.
	/// </para>
	/// </remarks>
	public bool SkipHiddenColumns { get; init; }

	/// <summary>
	/// The options of the readers: <see cref="CsvReaderOptions.HasHeader"/>, <see cref="CsvReaderOptions.SkipEmptyLines"/>,
	/// trimming, null values, header matching, <see cref="CsvReaderOptions.OnRecordError"/> and the rest apply as for CSV.
	/// <see cref="CsvReaderOptions.FormatProvider"/>, <see cref="CsvReaderOptions.TrueValues"/> and
	/// <see cref="CsvReaderOptions.FalseValues"/> apply to text cells only: numbers and booleans are stored in invariant
	/// form. CSV's dialect (the separator, quotes, comments, fixed widths) is ignored.
	/// </summary>
	public CsvReaderOptions Csv { get; init; } = CsvReaderOptions.Default;

	/// <summary>
	/// The most bytes a part held in memory may inflate to (the content types, relationships, workbook, styles and shared
	/// strings), the largest single token or cell of a sheet, which is streamed, and the most a sheet's shared formulas may
	/// take when formulas read as text (<see cref="ExcelFormulas.FormulaText"/>). 256 MiB by default.
	/// </summary>
	public long MaxPartSize { get; init; } = 256L * 1024 * 1024;

	/// <summary>
	/// The most bytes the workbook may inflate to in all, across every call that reads it, including the copy of a stream
	/// that can't seek. It also counts what a sheet's cells give beyond their XML: each empty field that a gap adds as one
	/// byte, and the bytes a shared string or a moved shared formula has beyond the cell that reads it, so that a short
	/// cell repeated can't make gigabytes of fields. 4 GiB by default.
	/// </summary>
	public long MaxUncompressedSize { get; init; } = 4L * 1024 * 1024 * 1024;

	/// <summary>
	/// How a cell holding an error value (<c>#N/A</c>, <c>#DIV/0!</c> and the rest) reads: as its text (the default),
	/// as an empty cell, or failing every read. See <see cref="ExcelErrorCells"/>.
	/// </summary>
	public ExcelErrorCells ErrorCells { get; init; } = ExcelErrorCells.Text;

	/// <summary>
	/// The significant digits that <see cref="decimal"/> and integer targets round a number cell to, half away from zero,
	/// as Excel displays numbers: 15 by default, so that <c>0.30000000000000004</c> reads as 0.3 and
	/// <c>3.0000000000000004</c> as 3. From 1 to 28, or <see langword="null"/> to read the stored number exactly.
	/// </summary>
	/// <remarks>
	/// <see cref="double"/> and <see cref="float"/> targets, and text cells, are never rounded. An integer target takes a
	/// number that is whole after rounding; a 17-digit ID stored as a number reads rounded, as Excel shows it.
	/// </remarks>
	public int? SignificantDigits { get; init; } = 15;

	/// <summary>What a cell with a formula reads as: its saved value (the default), or the formula's text. See <see cref="ExcelFormulas"/>.</summary>
	public ExcelFormulas Formulas { get; init; } = ExcelFormulas.CachedValue;

	/// <summary>The limits as the package layer takes them; the options must be valid (<see cref="Validate"/>).</summary>
	internal PartLimits Limits => new(MaxPartSize, MaxUncompressedSize);

	/// <summary>The options of a reader's typed cells; the options must be valid (<see cref="Validate"/>).</summary>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The cell options.</returns>
	internal CsvCellOptions CellOptions(bool date1904) => new()
	{
		Date1904 = date1904,
		SignificantDigits = SignificantDigits,
		ThrowOnErrorCells = ErrorCells == ExcelErrorCells.Error,
	};

	/// <summary>
	/// Which option chooses the sheet, for messages: <see cref="Table"/>, <see cref="DefinedName"/>, or a <see cref="Range"/>
	/// with a sheet's name. The options must be valid (<see cref="Validate"/>).
	/// </summary>
	/// <returns>Such as "ExcelReaderOptions.Table chooses its own sheet"; <see langword="null"/> when the sheet is chosen as usual.</returns>
	internal string? ChoosesSheet()
	{
		string? rangeSheet = null;
		if (Range is not null)
		{
			AreaReference.TryParse(Range.AsSpan(), out rangeSheet, out _);
		}

		return ChoosesSheet(rangeSheet);
	}

	/// <summary>Checks the options before a call uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException">
	/// <see cref="Csv"/> is <see langword="null"/>; both <see cref="Sheet"/> and <see cref="SheetIndex"/> are set; more than
	/// one of <see cref="Range"/>, <see cref="Table"/> and <see cref="DefinedName"/> is set; <see cref="Range"/> is not a
	/// block of cells; or one that chooses its sheet is set with <see cref="Sheet"/> or <see cref="SheetIndex"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// A limit is 0 or less, <see cref="ErrorCells"/> or <see cref="Formulas"/> is not a value of its enum, or
	/// <see cref="SignificantDigits"/> is not from 1 to 28.
	/// </exception>
	internal void Validate(string paramName)
	{
		if (Csv is null)
		{
			throw new ArgumentException("ExcelReaderOptions.Csv can't be null.", paramName);
		}

		if (Sheet is not null && SheetIndex is not null)
		{
			throw new ArgumentException("ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't both be set.", paramName);
		}

		if ((Range is null ? 0 : 1) + (Table is null ? 0 : 1) + (DefinedName is null ? 0 : 1) > 1)
		{
			throw new ArgumentException("ExcelReaderOptions.Range, ExcelReaderOptions.Table and ExcelReaderOptions.DefinedName can't be set together.", paramName);
		}

		string? rangeSheet = null;
		if (Range is not null && !AreaReference.TryParse(Range.AsSpan(), out rangeSheet, out _))
		{
			throw new ArgumentException($"ExcelReaderOptions.Range, \"{Range}\", is not a range of cells, such as B5:F200, B:F or 5:200.", paramName);
		}

		if ((Sheet is not null || SheetIndex is not null) && ChoosesSheet(rangeSheet) is { } chooser)
		{
			throw new ArgumentException($"{chooser}, so ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't be set with it.", paramName);
		}

		if (MaxPartSize <= 0)
		{
			throw new ArgumentOutOfRangeException(paramName, MaxPartSize, "ExcelReaderOptions.MaxPartSize must be more than 0.");
		}

		if (MaxUncompressedSize <= 0)
		{
			throw new ArgumentOutOfRangeException(paramName, MaxUncompressedSize, "ExcelReaderOptions.MaxUncompressedSize must be more than 0.");
		}

		if (ErrorCells is not (ExcelErrorCells.Text or ExcelErrorCells.Null or ExcelErrorCells.Error))
		{
			throw new ArgumentOutOfRangeException(paramName, ErrorCells, "ExcelReaderOptions.ErrorCells is not an ExcelErrorCells value.");
		}

		if (Formulas is not (ExcelFormulas.CachedValue or ExcelFormulas.FormulaText))
		{
			throw new ArgumentOutOfRangeException(paramName, Formulas, "ExcelReaderOptions.Formulas is not an ExcelFormulas value.");
		}

		if (SignificantDigits is < 1 or > 28)
		{
			throw new ArgumentOutOfRangeException(paramName, SignificantDigits, "ExcelReaderOptions.SignificantDigits must be from 1 to 28, or null.");
		}
	}

	/// <summary>Which option chooses the sheet, for messages.</summary>
	/// <param name="rangeSheet">The sheet <see cref="Range"/> names, or <see langword="null"/>.</param>
	/// <returns>Such as "ExcelReaderOptions.Table chooses its own sheet"; <see langword="null"/> when the sheet is chosen as usual.</returns>
	private string? ChoosesSheet(string? rangeSheet) =>
		Table is not null ? "ExcelReaderOptions.Table chooses its own sheet"
		: DefinedName is not null ? "ExcelReaderOptions.DefinedName chooses its own sheet"
		: rangeSheet is not null ? "ExcelReaderOptions.Range names its sheet"
		: null;
}
