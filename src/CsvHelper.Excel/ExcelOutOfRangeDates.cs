namespace CsvHelper.Excel;

/// <summary>
/// What a written workbook does with a date before 1900-01-01, the first date of Excel's 1900 date system, and with a
/// <see cref="System.TimeSpan"/> that is negative or of 2,958,466 days or more (past 9999-12-31 as a serial), which Excel
/// shows as <c>########</c> (<see cref="ExcelWriterOptions.OutOfRangeDates"/>).
/// </summary>
public enum ExcelOutOfRangeDates
{
	/// <summary>A text cell of the value's text, as the writer formatted it. The default.</summary>
	Text,

	/// <summary>Throw <see cref="System.InvalidOperationException"/>, naming the cell.</summary>
	Throw,
}
