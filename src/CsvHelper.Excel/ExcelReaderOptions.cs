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
	/// The options of the readers: <see cref="CsvReaderOptions.HasHeader"/>, <see cref="CsvReaderOptions.SkipEmptyLines"/>,
	/// trimming, null values, header matching, <see cref="CsvReaderOptions.OnRecordError"/> and the rest apply as for CSV.
	/// <see cref="CsvReaderOptions.FormatProvider"/>, <see cref="CsvReaderOptions.TrueValues"/> and
	/// <see cref="CsvReaderOptions.FalseValues"/> apply to text cells only: numbers and booleans are stored in invariant
	/// form. CSV's dialect (the separator, quotes, comments, fixed widths) is ignored.
	/// </summary>
	public CsvReaderOptions Csv { get; init; } = CsvReaderOptions.Default;

	/// <summary>
	/// The most bytes a part held in memory may inflate to (the content types, relationships, workbook, styles and shared
	/// strings), and the largest single token or cell of a sheet, which is streamed. 256 MiB by default.
	/// </summary>
	public long MaxPartSize { get; init; } = 256L * 1024 * 1024;

	/// <summary>
	/// The most bytes the workbook may inflate to in all, across every call that reads it, including the copy of a stream
	/// that can't seek, and counting each empty field that a gap in a sheet adds as one byte. 4 GiB by default.
	/// </summary>
	public long MaxUncompressedSize { get; init; } = 4L * 1024 * 1024 * 1024;

	/// <summary>The limits as the package layer takes them; the options must be valid (<see cref="Validate"/>).</summary>
	internal PartLimits Limits => new(MaxPartSize, MaxUncompressedSize);

	/// <summary>Checks the options before a call uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException"><see cref="Csv"/> is <see langword="null"/>, or both <see cref="Sheet"/> and <see cref="SheetIndex"/> are set.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit is 0 or less.</exception>
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

		if (MaxPartSize <= 0)
		{
			throw new ArgumentOutOfRangeException(paramName, MaxPartSize, "ExcelReaderOptions.MaxPartSize must be more than 0.");
		}

		if (MaxUncompressedSize <= 0)
		{
			throw new ArgumentOutOfRangeException(paramName, MaxUncompressedSize, "ExcelReaderOptions.MaxUncompressedSize must be more than 0.");
		}
	}
}
