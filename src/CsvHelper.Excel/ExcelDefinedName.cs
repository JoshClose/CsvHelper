using System;
using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// A defined name of a workbook (Excel's Name Manager lists them): the workbook's own, or a sheet's. When it refers to one
/// block of cells on a worksheet, <see cref="Sheet"/> and <see cref="Range"/> say which, and a reader can read it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Range"/> is set for a sheet's name, <c>!</c>, and a cell or a block, with or without <c>$</c>, in either case,
/// with white space around it, as Excel reads them: <c>Orders!$B$4:$F$14</c>, <c>'Q1 Orders'!B:C</c>. A relative reference
/// reads as stored, which is relative to <c>A1</c>: Excel stores relative names that way, and shows them so while <c>A1</c>
/// is the active cell.
/// </para>
/// <para>
/// Not blocks of cells: constants (<c>0.05</c>), formulas (<c>OFFSET(...)</c>), unions (<c>Orders!$A$1,Orders!$C$3</c>),
/// intersections, 3-D references (<c>Jan:Dec!$A$1</c>), other workbooks' (<c>[1]Orders!$A$1</c>), <c>#REF!</c> (a name of
/// a sheet that was deleted), table references (<c>Sales[Qty]</c>), and a chartsheet's.
/// </para>
/// </remarks>
public sealed class ExcelDefinedName
{
	/// <summary>The prefix of Excel's built-in names in a file, such as <c>_xlnm.Print_Area</c>.</summary>
	private const string BuiltInPrefix = "_xlnm.";

	/// <summary>Creates the name.</summary>
	/// <param name="sheets">The workbook's sheets, in tab order.</param>
	/// <param name="name">The name, as the workbook part lists it.</param>
	internal ExcelDefinedName(IReadOnlyList<ExcelSheet> sheets, WorkbookName name)
	{
		Name = name.Name.StartsWith(BuiltInPrefix, StringComparison.OrdinalIgnoreCase) ? name.Name.Substring(BuiltInPrefix.Length) : name.Name;
		Scope = name.LocalSheetId is int index ? sheets[index] : null;
		Hidden = name.Hidden;
		RefersTo = name.Text;
		if (AreaReference.TryParse(RefersTo.AsSpan(), out var sheetName, out var range) && sheetName is not null)
		{
			foreach (var sheet in sheets)
			{
				if (sheet.Kind == ExcelSheetKind.Worksheet && string.Equals(sheet.Name, sheetName, StringComparison.OrdinalIgnoreCase))
				{
					Sheet = sheet;
					Range = range;

					break;
				}
			}
		}
	}

	/// <summary>
	/// The name, ignoring case as Excel does. Excel's built-in names go without the <c>_xlnm.</c> prefix their files store,
	/// as Excel shows them: <c>Print_Area</c>, <c>Print_Titles</c>, <c>_FilterDatabase</c>.
	/// </summary>
	public string Name { get; }

	/// <summary>The sheet whose own name it is, or <see langword="null"/> for the workbook's.</summary>
	public ExcelSheet? Scope { get; }

	/// <summary>Whether the name is hidden from Excel's Name Manager, as <c>_FilterDatabase</c> is.</summary>
	public bool Hidden { get; }

	/// <summary>What the name refers to: its formula as the file stores it, without <c>=</c>, such as <c>Orders!$B$4:$F$14</c>.</summary>
	public string RefersTo { get; }

	/// <summary>The worksheet <see cref="Range"/> is on; <see langword="null"/> when the name is not one block of cells on a worksheet.</summary>
	public ExcelSheet? Sheet { get; }

	/// <summary>The block of cells the name refers to; <see langword="null"/> when it is not one block of cells on a worksheet.</summary>
	public ExcelRange? Range { get; }

	/// <summary>The name as Excel's Name Box writes it: <c>Data</c>, or <c>Orders!Local</c> for a sheet's own, its sheet quoted when it needs to be.</summary>
	/// <returns>The name.</returns>
	public override string ToString() => Scope is null ? Name : SheetLocation.Quote(Scope.Name) + "!" + Name;
}
