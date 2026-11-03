namespace CsvHelper.Excel;

/// <summary>Whether a sheet shows as a tab in Excel.</summary>
public enum ExcelSheetVisibility
{
	/// <summary>The sheet shows as a tab.</summary>
	Visible = 0,

	/// <summary>The sheet is hidden; users can show it again from Excel's Unhide command.</summary>
	Hidden = 1,

	/// <summary>The sheet is hidden, and only code (VBA, or a file edited by a program) can show it again.</summary>
	VeryHidden = 2,
}

/// <summary>What a sheet of a workbook is. Only worksheets have cells to read.</summary>
public enum ExcelSheetKind
{
	/// <summary>A worksheet: a grid of cells.</summary>
	Worksheet = 0,

	/// <summary>A chartsheet: a sheet that is one chart, with no cells.</summary>
	Chartsheet = 1,

	/// <summary>An Excel 5 dialog sheet, a legacy kind of sheet.</summary>
	DialogSheet = 2,

	/// <summary>An Excel 4 macro sheet (XLM), a legacy kind of sheet.</summary>
	MacroSheet = 3,
}
