namespace CsvHelper.Excel;

/// <summary>What a workbook writer does with a record past a sheet's last row, 1,048,576 (<see cref="ExcelWriterOptions.RowLimit"/>).</summary>
public enum ExcelRowLimit
{
	/// <summary>
	/// Throws <see cref="System.InvalidOperationException"/>, and the sheet keeps the rows before it: Excel opens no
	/// workbook with a row past its last. The record methods discard the record, as for any record that fails.
	/// </summary>
	Throw,

	/// <summary>
	/// Completes the sheet and goes on in a new one after it, named as Excel names a copy of it: <c>Orders (2)</c>, then
	/// <c>Orders (3)</c> and so on, the name shortened to fit 31 characters, and made unique. The header, when
	/// <see cref="CsvWriterOptions.HasHeader"/> is set, is repeated as each new sheet's first row, and so are the columns'
	/// widths. <see cref="ExcelWorkbookWriter.SheetNames"/> has the new sheets' names.
	/// </summary>
	NewSheet,
}
