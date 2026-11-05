using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// An Excel table (a ListObject) of a worksheet: its name, its cells, its header and totals rows, and its columns' names.
/// Read one with <see cref="ExcelReaderOptions.Table"/>.
/// </summary>
public sealed class ExcelTable
{
	/// <summary>Creates the table.</summary>
	/// <param name="sheet">The worksheet the table is on.</param>
	/// <param name="part">The table's part.</param>
	internal ExcelTable(ExcelSheet sheet, TablePart part)
	{
		Sheet = sheet;
		Part = part;
	}

	/// <summary>
	/// The table's name, which formulas use (<c>=SUM(Sales[Qty])</c>): its part's <c>displayName</c>. (Excel's COM
	/// <c>ListObject.Name</c> gives the part's <c>name</c> instead; Excel writes the two alike.)
	/// </summary>
	public string Name => Part.Name;

	/// <summary>The worksheet the table is on.</summary>
	public ExcelSheet Sheet { get; }

	/// <summary>The table's cells: its header row, its data rows and its totals row.</summary>
	public ExcelRange Range => Part.Range;

	/// <summary>The number of header rows at the top of <see cref="Range"/>: 1, or 0 when the table's header row is hidden.</summary>
	public int HeaderRowCount => Part.HeaderRowCount;

	/// <summary>The number of totals rows at the bottom of <see cref="Range"/>: 0, or 1 when the table shows totals.</summary>
	public int TotalsRowCount => Part.TotalsRowCount;

	/// <summary>The columns' names, in order, which the header row shows, and which a table whose header row is hidden still has.</summary>
	public IReadOnlyList<string> Columns => Part.Columns;

	/// <summary>The table's part.</summary>
	internal TablePart Part { get; }

	/// <summary>The table's name.</summary>
	/// <returns><see cref="Name"/>.</returns>
	public override string ToString() => Name;
}
