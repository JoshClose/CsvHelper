using System;

namespace CsvHelper.Excel;

/// <summary>
/// How <see cref="ExcelConvert.ToExcel(string, string, CsvToExcelOptions?)"/> and its overloads convert CSV to a sheet: how
/// the CSV is read, how the workbook is written, the sheet's name and look, and how many records decide the columns'
/// types. Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record CsvToExcelOptions
{
	/// <summary>
	/// The default options: <see cref="CsvReaderOptions.Default"/>, <see cref="ExcelWriterOptions.Default"/>, the sheet named
	/// after the CSV file, and columns' types inferred from 1,000 records.
	/// </summary>
	public static CsvToExcelOptions Default { get; } = new();

	/// <summary>
	/// How the CSV is read: the separator, the header, the encoding, the culture of its numbers
	/// (<see cref="CsvReaderOptions.FormatProvider"/>, the invariant culture by default), the null values, trimming and the
	/// rest. <see cref="CsvReaderOptions.HasHeader"/> says whether the sheet's first record is its header, for
	/// <see cref="ExcelConvert.ToExcel(string, string, CsvToExcelOptions?)"/>; for <c>ToExcel&lt;T&gt;</c>, whether the CSV
	/// has one to read <c>T</c>'s records by.
	/// </summary>
	public CsvReaderOptions Csv { get; init; } = CsvReaderOptions.Default;

	/// <summary>
	/// How the workbook is written: where text cells keep their strings, compression, the row limit, long numbers, offsets,
	/// dates before 1900, links, and <see cref="ExcelWriterOptions.Sheets"/>, the sheet's look without <see cref="Sheet"/>.
	/// Its <see cref="ExcelWriterOptions.Csv"/> is for record writers: it applies to <c>ToExcel&lt;T&gt;</c>, whose sheet has
	/// <c>T</c>'s header when its <see cref="CsvWriterOptions.HasHeader"/> is set, and not to the inferred
	/// sheet, whose header and culture are the CSV's.
	/// </summary>
	public ExcelWriterOptions Excel { get; init; } = ExcelWriterOptions.Default;

	/// <summary>
	/// The sheet's name, or <see langword="null"/>, the default, for the CSV file's name as Excel names the sheet of a CSV it
	/// opens: without its extension, cut to 31 characters, with <c>[</c> and <c>]</c> as <c>(</c> and <c>)</c>, the other
	/// characters a sheet's name can't have as <c>_</c>, and no apostrophe at either end; <c>Sheet1</c> from a stream, or
	/// when nothing is left.
	/// </summary>
	public string? SheetName { get; init; }

	/// <summary>
	/// How the sheet looks, or <see langword="null"/>, the default, for <see cref="ExcelWriterOptions.Sheets"/> (for
	/// <c>ToExcel&lt;T&gt;</c>, <c>T</c>'s <see cref="ExcelSheetAttribute"/> before them).
	/// </summary>
	public ExcelSheetOptions? Sheet { get; init; }

	/// <summary>
	/// How many records after the header decide each column's type, held in memory until they do: 1,000 by default; 0 for
	/// text cells only. <c>ToExcel&lt;T&gt;</c> types its cells by <c>T</c>, and doesn't infer.
	/// </summary>
	public int InferTypes { get; init; } = 1000;

	/// <summary>Checks the options before a conversion uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <param name="records">
	/// Whether the conversion writes a type's records (<c>ToExcel&lt;T&gt;</c>), whose sheet's header is
	/// <see cref="Excel"/>'s; otherwise its header is <see cref="Csv"/>'s.
	/// </param>
	/// <exception cref="ArgumentException">
	/// <see cref="Csv"/> or <see cref="Excel"/> is <see langword="null"/>, an option is not valid, <see cref="SheetName"/> is
	/// not one Excel opens, or the sheet's look needs a header that the sheet doesn't have.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range, or <see cref="InferTypes"/> is negative.</exception>
	internal void Validate(string paramName, bool records)
	{
		if (Csv is null)
		{
			throw new ArgumentException("CsvToExcelOptions.Csv can't be null.", paramName);
		}

		if (Excel is null)
		{
			throw new ArgumentException("CsvToExcelOptions.Excel can't be null.", paramName);
		}

		Csv.Validate();
		Excel.Validate(paramName);
		ExcelWorkbookWriter.CheckInferTypes(InferTypes, paramName);
		if (SheetName is not null)
		{
			SheetNames.Check(SheetName, paramName);
		}

		var hasHeader = records ? Excel.Csv.HasHeader : Csv.HasHeader;
		var setting = records ? "CsvWriterOptions.HasHeader" : "CsvReaderOptions.HasHeader";
		if (Sheet is not null)
		{
			if ((Sheet.Problem(Sheet.Columns) ?? Sheet.HeaderProblem(hasHeader, setting)) is { } problem)
			{
				throw new ArgumentException("CsvToExcelOptions.Sheet: " + problem, paramName);
			}
		}
		else if (!records && Excel.Sheets.HeaderProblem(hasHeader, setting) is { } problem)
		{
			throw new ArgumentException("ExcelWriterOptions.Sheets: " + problem, paramName);
		}
	}
}
