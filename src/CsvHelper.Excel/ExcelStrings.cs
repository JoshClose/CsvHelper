namespace CsvHelper.Excel;

/// <summary>Where a written workbook keeps the text of its text cells (<see cref="ExcelWriterOptions.Strings"/>).</summary>
public enum ExcelStrings
{
	/// <summary>
	/// In each cell (<c>t="inlineStr"</c>), as streaming writers write them: nothing is kept between rows, so a sheet of
	/// any size is written in the same memory. The default.
	/// </summary>
	Inline,

	/// <summary>
	/// In the workbook's shared-strings part, each distinct string once, as Excel saves them: a smaller file when strings
	/// repeat, at the cost of memory, since the table holds every distinct string until the workbook is disposed.
	/// </summary>
	Shared,
}
