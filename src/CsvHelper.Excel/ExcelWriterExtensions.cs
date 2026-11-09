using System;

namespace CsvHelper.Excel;

/// <summary>Writes what only a workbook has into the <see cref="CsvWriter"/> of a sheet (<see cref="ExcelWorkbookWriter.AddSheet(string, ExcelSheetOptions)"/>).</summary>
public static class ExcelWriterExtensions
{
	/// <summary>
	/// Writes a formula as the next field: a formula cell in a workbook, which Excel computes when it opens it; its text,
	/// <c>=</c> and the formula, in CSV (as text, so that <see cref="CsvWriterOptions.Injection"/> applies there). See
	/// <see cref="ExcelFormula"/>.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="formula">The formula, with or without its <c>=</c>, such as <c>SUM(A1:A3)</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="formula"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The formula is empty, or Excel opens no workbook with it (see <see cref="ExcelFormula(string)"/>).</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public static void WriteFormula(this CsvWriter writer, string formula)
	{
		ArgumentNullException.ThrowIfNull(writer);
		writer.WriteField(new ExcelFormula(formula));
	}
}
