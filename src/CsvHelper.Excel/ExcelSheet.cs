namespace CsvHelper.Excel;

/// <summary>
/// A sheet of an <see cref="ExcelWorkbook"/>, as the workbook lists it: its name, place in tab order, visibility and kind.
/// Pass it to <see cref="ExcelWorkbook.OpenReader(ExcelSheet, ExcelReaderOptions?)"/> to read it.
/// </summary>
public sealed class ExcelSheet
{
	/// <summary>Creates the sheet.</summary>
	/// <param name="workbook">The workbook it is a sheet of.</param>
	/// <param name="sheet">The sheet, as the workbook part lists it.</param>
	internal ExcelSheet(ExcelWorkbook workbook, WorkbookSheet sheet)
	{
		Workbook = workbook;
		Part = sheet;
	}

	/// <summary>The sheet's name, as its tab shows it.</summary>
	public string Name => Part.Name;

	/// <summary>The sheet's 0-based place in tab order, in <see cref="ExcelWorkbook.Sheets"/>.</summary>
	public int Index => Part.Index;

	/// <summary>Whether the sheet shows as a tab.</summary>
	public ExcelSheetVisibility Visibility => Part.Visibility;

	/// <summary>What the sheet is: only a <see cref="ExcelSheetKind.Worksheet"/> can be read.</summary>
	public ExcelSheetKind Kind => Part.Kind;

	/// <summary>The workbook the sheet is a sheet of.</summary>
	internal ExcelWorkbook Workbook { get; }

	/// <summary>The sheet as the workbook part lists it, with its part.</summary>
	internal WorkbookSheet Part { get; }

	/// <summary>The sheet's name.</summary>
	/// <returns><see cref="Name"/>.</returns>
	public override string ToString() => Name;
}

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
