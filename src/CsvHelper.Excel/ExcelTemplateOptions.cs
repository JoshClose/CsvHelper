using System;

namespace CsvHelper.Excel;

/// <summary>
/// How <see cref="ExcelTemplate.Create{T}(string, ExcelTemplateOptions?)"/> and its overloads write a template: how the
/// workbook is written, the sheet's name, and how it looks. Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record ExcelTemplateOptions
{
	/// <summary>What is wrong with a template's look that has a table.</summary>
	internal const string TableProblem =
		"A template's sheet is protected, so it can't have a table, since Excel doesn't add rows to a table on a protected sheet: leave Table out.";

	/// <summary>
	/// The default options: <see cref="ExcelWriterOptions.Default"/>, the sheet named after the record type, and its look
	/// from the type's <see cref="ExcelSheetAttribute"/>, else <see cref="ExcelWriterOptions.Sheets"/>.
	/// </summary>
	public static ExcelTemplateOptions Default { get; } = new();

	/// <summary>
	/// The sheet's name, or <see langword="null"/>, the default, for the record type's name, as a sheet's name has it: without
	/// a generic type's arity (<c>`1</c>), and cut to 31 characters.
	/// </summary>
	public string? SheetName { get; init; }

	/// <summary>
	/// How the workbook is written: where text cells keep their strings, compression, how a
	/// <see cref="DateTimeOffset"/> column validates (<see cref="ExcelWriterOptions.DateTimeOffsets"/>), and
	/// <see cref="ExcelWriterOptions.Sheets"/>, the sheet's look without <see cref="Sheet"/> or the type's
	/// <see cref="ExcelSheetAttribute"/>. Its <see cref="ExcelWriterOptions.Csv"/> writes the header, so its
	/// <see cref="CsvWriterOptions.HasHeader"/> must be on.
	/// </summary>
	public ExcelWriterOptions Excel { get; init; } = ExcelWriterOptions.Default;

	/// <summary>
	/// How the sheet looks, or <see langword="null"/>, the default, for the record type's <see cref="ExcelSheetAttribute"/>,
	/// else <see cref="ExcelWriterOptions.Sheets"/>: a title, a bold and frozen header, auto-fit, and each column's width,
	/// format, list and prompt. Whichever it is, the template turns on <see cref="ExcelSheetOptions.Validate"/> and
	/// <see cref="ExcelSheetOptions.Protect"/>, and so its <see cref="ExcelSheetOptions.Password"/> needs no
	/// <see cref="ExcelSheetOptions.Protect"/> here; a <see cref="ExcelSheetOptions.Table"/> throws, since Excel doesn't add
	/// rows to a table on a protected sheet.
	/// </summary>
	public ExcelSheetOptions? Sheet { get; init; }

	/// <summary>Checks the options before a template uses them.</summary>
	/// <param name="paramName">The name of the parameter the options came from.</param>
	/// <exception cref="ArgumentException">
	/// <see cref="Excel"/> is <see langword="null"/> or not valid, its <see cref="CsvWriterOptions.HasHeader"/> is off,
	/// <see cref="SheetName"/> is not one Excel opens, or <see cref="Sheet"/>, validated and protected, is not valid.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	internal void Validate(string paramName)
	{
		if (Excel is null)
		{
			throw new ArgumentException("ExcelTemplateOptions.Excel can't be null.", paramName);
		}

		Excel.Validate(paramName);
		if (!Excel.Csv.HasHeader)
		{
			throw new ArgumentException("ExcelTemplateOptions.Excel: a template is its record type's header, and CsvWriterOptions.HasHeader is off.", paramName);
		}

		if (SheetName is not null)
		{
			SheetNames.Check(SheetName, paramName);
		}

		if (Sheet is null)
		{
			return;
		}

		if (Sheet.Table is not null)
		{
			throw new ArgumentException("ExcelTemplateOptions.Sheet: " + TableProblem, paramName);
		}

		if (Template(Sheet).Problem(Sheet.Columns) is { } problem)
		{
			throw new ArgumentException("ExcelTemplateOptions.Sheet: " + problem, paramName);
		}
	}

	/// <summary>A sheet's look as a template has it: validated and protected (Phase 7's decision 16).</summary>
	/// <param name="look">The look.</param>
	/// <returns>The look with <see cref="ExcelSheetOptions.Validate"/> and <see cref="ExcelSheetOptions.Protect"/> on.</returns>
	internal static ExcelSheetOptions Template(ExcelSheetOptions look) => look with { Validate = true, Protect = true };
}
