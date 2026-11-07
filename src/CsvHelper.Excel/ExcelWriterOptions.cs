using System;
using System.IO.Compression;

namespace CsvHelper.Excel;

/// <summary>
/// How a workbook is written: the options of its sheets' <see cref="CsvWriter"/>s, where text cells keep their strings,
/// how the package is compressed, what a record past a sheet's last row does, and what becomes of the values Excel can't
/// hold as they are (long numbers, offsets, dates before 1900). Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record ExcelWriterOptions
{
	/// <summary>
	/// The default options: <see cref="CsvWriterOptions.Default"/>, inline strings, the fastest compression, an exception
	/// past a sheet's last row, and text cells for long numbers, offsets and dates before 1900.
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
	/// What a date before 1900-01-01, Excel's first, and a negative <see cref="TimeSpan"/> become: a text cell
	/// (<see cref="ExcelOutOfRangeDates.Text"/>, the default) or an exception.
	/// </summary>
	public ExcelOutOfRangeDates OutOfRangeDates { get; init; } = ExcelOutOfRangeDates.Text;

	/// <summary>Checks the options before a writer uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException"><see cref="Csv"/> is <see langword="null"/>, or its options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <see cref="Strings"/>, <see cref="CompressionLevel"/>, <see cref="RowLimit"/>, <see cref="LongNumbers"/>,
	/// <see cref="DateTimeOffsets"/> or <see cref="OutOfRangeDates"/> is not a value of its enum, or <see cref="Csv"/> has a
	/// value out of range.
	/// </exception>
	internal void Validate(string paramName)
	{
		if (Csv is null)
		{
			throw new ArgumentException("ExcelWriterOptions.Csv can't be null.", paramName);
		}

		Csv.Validate();
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
