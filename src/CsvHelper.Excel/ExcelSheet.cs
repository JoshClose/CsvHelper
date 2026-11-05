using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CsvHelper.Excel;

/// <summary>
/// A sheet of an <see cref="ExcelWorkbook"/>, as the workbook lists it: its name, place in tab order, visibility and kind,
/// its tables, and the used range it states. Pass it to <see cref="ExcelWorkbook.OpenReader(ExcelSheet, ExcelReaderOptions?)"/>
/// to read it.
/// </summary>
public sealed class ExcelSheet
{
	private bool dimensionRead;
	private ExcelRange? dimension;

	/// <summary>Creates the sheet.</summary>
	/// <param name="workbook">The workbook it is a sheet of.</param>
	/// <param name="sheet">The sheet, as the workbook part lists it.</param>
	internal ExcelSheet(ExcelWorkbook workbook, WorkbookSheet sheet)
	{
		Workbook = workbook;
		Part = sheet;
		Tables = new ReadOnlyCollection<ExcelTable>([.. sheet.Tables.Select(t => new ExcelTable(this, t))]);
	}

	/// <summary>The sheet's name, as its tab shows it.</summary>
	public string Name => Part.Name;

	/// <summary>The sheet's 0-based place in tab order, in <see cref="ExcelWorkbook.Sheets"/>.</summary>
	public int Index => Part.Index;

	/// <summary>Whether the sheet shows as a tab.</summary>
	public ExcelSheetVisibility Visibility => Part.Visibility;

	/// <summary>What the sheet is: only a <see cref="ExcelSheetKind.Worksheet"/> can be read.</summary>
	public ExcelSheetKind Kind => Part.Kind;

	/// <summary>The sheet's tables, in the order of its relationships; none for a sheet other than a worksheet.</summary>
	public IReadOnlyList<ExcelTable> Tables { get; }

	/// <summary>
	/// The used range the sheet states (its <c>&lt;dimension ref&gt;</c>), as its producer wrote it. Excel keeps it as the
	/// block from the first used cell to the last, cells with only formatting included, and states <c>A1</c> for an empty
	/// sheet. It is <see langword="null"/> when the sheet states none (the Open XML SDK, SpreadCheetah, LargeXlsx and Google
	/// Sheets write none), when the sheet is not a worksheet, or when the start of its part can't be read (reading the sheet
	/// then says why).
	/// </summary>
	/// <remarks>
	/// It is read the first time it is asked for, from the start of the sheet's part (a few KB inflated, under the
	/// workbook's options), and then kept.
	/// </remarks>
	/// <exception cref="ObjectDisposedException">The workbook is disposed, and the dimension wasn't read before.</exception>
	public ExcelRange? Dimension
	{
		get
		{
			if (!dimensionRead)
			{
				dimension = Workbook.ReadDimension(Part);
				dimensionRead = true;
			}

			return dimension;
		}
	}

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
