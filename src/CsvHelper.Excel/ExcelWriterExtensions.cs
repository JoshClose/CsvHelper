using System;

namespace CsvHelper.Excel;

/// <summary>
/// Writes what only a workbook has into the <see cref="CsvWriter"/> of a sheet (<see cref="ExcelWorkbookWriter.AddSheet(string, ExcelSheetOptions)"/>):
/// formulas and merged cells.
/// </summary>
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

	/// <summary>
	/// Merges a range of cells of the sheet being written, such as a label over the columns of a group: Excel shows the
	/// range as one cell, with the text of its top-left cell. The range is written with the sheet's other merged ranges
	/// after its rows, so it can be merged before, while or after its rows are written, until the sheet is complete.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The range is in the sheet's own cells, as Excel names them: with a <see cref="ExcelSheetOptions.Title"/>, row 1 is the
	/// title's and the writer's first record is row 2. With <see cref="ExcelRowLimit.NewSheet"/>, it is a range of the
	/// sheet being written when the call is made, and each new sheet starts without merged ranges but the title's.
	/// </para>
	/// <para>
	/// Excel refuses a workbook whose merged ranges overlap, or that has a merged range in a table, so a range throws when
	/// it overlaps one merged before, or is in row 1 with a title. With a <see cref="ExcelSheetOptions.Table"/>, which
	/// reaches every row below its header, a range in the header's columns at or below it throws: when merged after the
	/// header, or, merged before the header, when the header record ends, which discards it as any record that fails.
	/// </para>
	/// <para>
	/// Excel's AutoFit, and the sheet's (<see cref="ExcelSheetOptions.AutoFit"/>), don't count the cells of a range across
	/// two columns or more; a range down one column counts as its cells would unmerged.
	/// </para>
	/// </remarks>
	/// <param name="writer">A sheet's writer, from <see cref="ExcelWorkbookWriter.AddSheet(string, ExcelSheetOptions)"/> or <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/>.</param>
	/// <param name="range">
	/// The range: two corners, such as <c>A1:C3</c>, in either order, with or without <c>$</c>; or whole columns
	/// (<c>B:C</c>) or rows (<c>5:6</c>). It has two cells or more.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="range"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The range is not one, is one cell, overlaps a range merged before, is in row 1 under a title, or is in the table's
	/// columns at or below its header.
	/// </exception>
	/// <exception cref="InvalidOperationException">The writer writes CSV, not a sheet.</exception>
	/// <exception cref="ObjectDisposedException">The sheet is complete: its writer has been disposed.</exception>
	public static void MergeCells(this CsvWriter writer, string range)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(range);
		if (ExcelWorkbookWriter.SinkOf(writer) is not { } sink)
		{
			throw new InvalidOperationException("MergeCells merges a sheet's cells, and this CsvWriter writes CSV: use a writer from ExcelWorkbookWriter.AddSheet.");
		}

		if (!ExcelRange.TryParse(range, out var cells))
		{
			throw new ArgumentException($"\"{range}\" is not a range of cells, such as A1:C3.", nameof(range));
		}

		if (cells.RowCount == 1 && cells.ColumnCount == 1)
		{
			throw new ArgumentException($"\"{range}\" is one cell, and a merged range has two or more.", nameof(range));
		}

		sink.MergeCells(cells);
	}
}
