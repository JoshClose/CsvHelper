using System;
using System.IO.Compression;

namespace CsvHelper.Excel;

/// <summary>
/// How a workbook is written: the options of its sheets' <see cref="CsvWriter"/>s, where text cells keep their strings,
/// how the package is compressed, what a record past a sheet's last row does, what becomes of the values Excel can't
/// hold as they are (long numbers, offsets, dates before 1900), how sheets look by default, and which URIs are links.
/// Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record ExcelWriterOptions
{
	/// <summary>
	/// The default options: <see cref="CsvWriterOptions.Default"/>, inline strings, the fastest compression, an exception
	/// past a sheet's last row, text cells for long numbers, offsets and dates before 1900, sheets of
	/// <see cref="ExcelSheetOptions.Default"/>, and links for <c>http</c>, <c>https</c> and <c>mailto</c> URIs.
	/// </summary>
	public static ExcelWriterOptions Default { get; } = new();

	/// <summary>
	/// The options of the sheets' writers. <see cref="CsvWriterOptions.HasHeader"/> (the first record of a sheet is its
	/// header), <see cref="CsvWriterOptions.ValidateFieldCount"/>, <see cref="CsvWriterOptions.FormatProvider"/> and
	/// <see cref="CsvWriterOptions.BufferSize"/> apply as for CSV.
	/// </summary>
	/// <remarks>
	/// <see cref="CsvWriterOptions.ValidateFieldCount"/> is on by default, as for CSV: a sheet whose records have different
	/// numbers of fields, written by hand, needs it off. CSV's dialect is ignored, not rejected: the separator, quoting, escapes, fixed widths, the null, true and false
	/// texts, line endings, the byte order mark, and injection protection, since Excel never evaluates a text cell. A
	/// sheet's writer writes no comments: <see cref="CsvWriter.WriteComment(string?)"/> throws
	/// <see cref="NotSupportedException"/>. <c>[CsvOptions]</c> on a record type applies through
	/// <c>Csv = CsvWriterOptions.For&lt;T&gt;()</c>, as for CSV.
	/// </remarks>
	public CsvWriterOptions Csv { get; init; } = CsvWriterOptions.Default;

	/// <summary>
	/// Where text cells keep their strings: in each cell (<see cref="ExcelStrings.Inline"/>, the default), or once each in
	/// the shared-strings part (<see cref="ExcelStrings.Shared"/>).
	/// </summary>
	public ExcelStrings Strings { get; init; } = ExcelStrings.Inline;

	/// <summary>
	/// The compression of the package's entries: <see cref="CompressionLevel.Fastest"/> by default, the deflate that
	/// Excel's own entries are flagged with.
	/// </summary>
	public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Fastest;

	/// <summary>
	/// What a record past a sheet's last row, 1,048,576, does: throw (<see cref="ExcelRowLimit.Throw"/>, the default),
	/// since Excel opens no workbook with a row past its last, or go on in a new sheet with the header repeated.
	/// </summary>
	public ExcelRowLimit RowLimit { get; init; } = ExcelRowLimit.Throw;

	/// <summary>
	/// What an integer or a <see cref="decimal"/> of more than 15 significant digits becomes, which Excel would round to 15:
	/// a text cell (<see cref="ExcelLongNumbers.Text"/>, the default), a number cell, or an exception.
	/// </summary>
	public ExcelLongNumbers LongNumbers { get; init; } = ExcelLongNumbers.Text;

	/// <summary>
	/// What a <see cref="DateTimeOffset"/> becomes: a text cell of its ISO 8601 text (<see cref="ExcelDateTimeOffsets.Text"/>,
	/// the default), or a date cell of its time in UTC or of its clock time.
	/// </summary>
	public ExcelDateTimeOffsets DateTimeOffsets { get; init; } = ExcelDateTimeOffsets.Text;

	/// <summary>
	/// What a date before 1900-01-01, Excel's first, and a <see cref="TimeSpan"/> that is negative or of 2,958,466 days or
	/// more (which Excel shows as <c>########</c>) become: a text cell
	/// (<see cref="ExcelOutOfRangeDates.Text"/>, the default) or an exception.
	/// </summary>
	public ExcelOutOfRangeDates OutOfRangeDates { get; init; } = ExcelOutOfRangeDates.Text;

	/// <summary>
	/// The options of a sheet added without options of its own, whose record type has no <see cref="ExcelSheetAttribute"/>:
	/// <see cref="ExcelSheetOptions.Default"/> by default, a bold, frozen header and columns auto-fitted over the first 100
	/// records. They are checked when the writer is created.
	/// </summary>
	public ExcelSheetOptions Sheets { get; init; } = ExcelSheetOptions.Default;

	/// <summary>
	/// Which <see cref="Uri"/> fields are links: those with <c>http</c>, <c>https</c> and <c>mailto</c>
	/// (<see cref="ExcelLinks.Web"/>, the default), every one, or none. Every <see cref="Uri"/> field is a text cell of its
	/// <see cref="Uri.OriginalString"/>, as CSV writes it.
	/// </summary>
	/// <remarks>
	/// A link's cell is in Excel's <c>Hyperlink</c> cell style (underlined, blue), and opens an absolute URI's
	/// <see cref="Uri.AbsoluteUri"/>, with the letters past ASCII as themselves, or a relative URI's text. These are text
	/// without a link: the header's cells, which name its columns; a URI whose link Excel wouldn't open, longer than 8,192
	/// characters; and a URI past a sheet's 65,530 links, which is as many as Excel opens. A sheet holds each distinct
	/// link's target until it ends, since the targets are written after it: about two bytes a character, so at most about
	/// 1 GiB, for 65,530 targets of 8,192 characters. <see cref="ExcelLinks.None"/> holds none.
	/// </remarks>
	public ExcelLinks Links { get; init; } = ExcelLinks.Web;

	/// <summary>Checks the options before a writer uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException">
	/// <see cref="Csv"/> or <see cref="Sheets"/> is <see langword="null"/>, or its options are not valid.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <see cref="Strings"/>, <see cref="CompressionLevel"/>, <see cref="RowLimit"/>, <see cref="LongNumbers"/>,
	/// <see cref="DateTimeOffsets"/>, <see cref="OutOfRangeDates"/> or <see cref="Links"/> is not a value of its enum, or
	/// <see cref="Csv"/> has a value out of range.
	/// </exception>
	internal void Validate(string paramName)
	{
		if (Csv is null)
		{
			throw new ArgumentException("ExcelWriterOptions.Csv can't be null.", paramName);
		}

		Csv.Validate();
		if (Sheets is null)
		{
			throw new ArgumentException("ExcelWriterOptions.Sheets can't be null: use ExcelSheetOptions.Default.", paramName);
		}

		if ((Sheets.Problem(Sheets.Columns) ?? Sheets.HeaderProblem(Csv.HasHeader)) is { } problem)
		{
			throw new ArgumentException("ExcelWriterOptions.Sheets: " + problem, paramName);
		}

		if (Links is not (ExcelLinks.Web or ExcelLinks.All or ExcelLinks.None))
		{
			throw new ArgumentOutOfRangeException(paramName, Links, "ExcelWriterOptions.Links is not an ExcelLinks value.");
		}

		if (Strings is not (ExcelStrings.Inline or ExcelStrings.Shared))
		{
			throw new ArgumentOutOfRangeException(paramName, Strings, "ExcelWriterOptions.Strings is not an ExcelStrings value.");
		}

#if NET
		const CompressionLevel Last = CompressionLevel.SmallestSize;
#else
		const CompressionLevel Last = CompressionLevel.NoCompression;
#endif
		if (CompressionLevel is < CompressionLevel.Optimal or > Last)
		{
			throw new ArgumentOutOfRangeException(paramName, CompressionLevel, "ExcelWriterOptions.CompressionLevel is not a CompressionLevel value.");
		}

		if (RowLimit is not (ExcelRowLimit.Throw or ExcelRowLimit.NewSheet))
		{
			throw new ArgumentOutOfRangeException(paramName, RowLimit, "ExcelWriterOptions.RowLimit is not an ExcelRowLimit value.");
		}

		if (LongNumbers is not (ExcelLongNumbers.Text or ExcelLongNumbers.Number or ExcelLongNumbers.Throw))
		{
			throw new ArgumentOutOfRangeException(paramName, LongNumbers, "ExcelWriterOptions.LongNumbers is not an ExcelLongNumbers value.");
		}

		if (DateTimeOffsets is not (ExcelDateTimeOffsets.Text or ExcelDateTimeOffsets.Utc or ExcelDateTimeOffsets.ClockTime))
		{
			throw new ArgumentOutOfRangeException(paramName, DateTimeOffsets, "ExcelWriterOptions.DateTimeOffsets is not an ExcelDateTimeOffsets value.");
		}

		if (OutOfRangeDates is not (ExcelOutOfRangeDates.Text or ExcelOutOfRangeDates.Throw))
		{
			throw new ArgumentOutOfRangeException(paramName, OutOfRangeDates, "ExcelWriterOptions.OutOfRangeDates is not an ExcelOutOfRangeDates value.");
		}
	}
}
