using System;

namespace CsvHelper.Excel;

/// <summary>
/// How <see cref="ExcelConvert.ToCsv(string, string, ExcelToCsvOptions?)"/> and its overloads convert a sheet to CSV: how
/// the sheet is read, and how the CSV is written. Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record ExcelToCsvOptions
{
	/// <summary>The default options: <see cref="ExcelReaderOptions.Default"/> and <see cref="CsvWriterOptions.Default"/>.</summary>
	public static ExcelToCsvOptions Default { get; } = new();

	/// <summary>
	/// How the sheet is read, as <see cref="ExcelReader"/> reads it: the sheet or area, the header and where it is, hidden
	/// rows and columns, merged cells, error cells, formulas, and <see cref="ExcelReaderOptions.SignificantDigits"/>, the
	/// digits a number is written with. <see cref="ExcelReaderOptions.Csv"/>'s <see cref="CsvReaderOptions.HasHeader"/>
	/// says whether the first record is written as the header, and its <see cref="CsvReaderOptions.SkipEmptyLines"/>,
	/// <see cref="CsvReaderOptions.TrimWhitespace"/> and <see cref="CsvReaderOptions.ShouldSkipRecord"/> apply.
	/// </summary>
	public ExcelReaderOptions Excel { get; init; } = ExcelReaderOptions.Default;

	/// <summary>
	/// How the CSV is written: the separator, quoting, line endings, the byte-order mark (none by default, so Excel reads
	/// the file in the system's code page unless <see cref="CsvWriterOptions.WriteByteOrderMark"/> is set), and injection
	/// protection. <see cref="CsvWriterOptions.ValidateFieldCount"/> is ignored, since a row can be longer than the first,
	/// and so is <see cref="CsvWriterOptions.HasHeader"/>: the reader's options say whether there is a header.
	/// </summary>
	public CsvWriterOptions Csv { get; init; } = CsvWriterOptions.Default;

	/// <summary>Checks the options before a conversion uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException"><see cref="Excel"/> or <see cref="Csv"/> is <see langword="null"/>, or <see cref="Excel"/> is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option of <see cref="Excel"/> is out of range.</exception>
	internal void Validate(string paramName)
	{
		if (Excel is null)
		{
			throw new ArgumentException("ExcelToCsvOptions.Excel can't be null.", paramName);
		}

		if (Csv is null)
		{
			throw new ArgumentException("ExcelToCsvOptions.Csv can't be null.", paramName);
		}

		Excel.Validate(paramName);
	}

	/// <summary>The options of the CSV writer a conversion creates: <see cref="Csv"/>, without the field-count check.</summary>
	/// <returns>The writer's options.</returns>
	internal CsvWriterOptions WriterOptions() => Csv.ValidateFieldCount ? Csv with { ValidateFieldCount = false } : Csv;
}
