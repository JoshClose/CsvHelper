namespace CsvHelper.Excel;

/// <summary>What a cell with a formula reads as (<see cref="ExcelReaderOptions.Formulas"/>).</summary>
public enum ExcelFormulas
{
	/// <summary>
	/// The value the file saved with the formula, as the cell's type says (a number, text, a boolean or an error). A
	/// formula saved without a value, as some libraries write them, is an empty cell.
	/// </summary>
	CachedValue,

	/// <summary>
	/// The formula as text: <c>=</c> and the formula as the file stores it, such as <c>=SUM(A1:A3)</c>, whatever the
	/// cell's value, so <see cref="ExcelReaderOptions.ErrorCells"/> doesn't apply to it. Cells without a formula read their
	/// values.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A shared formula, which the file stores once for a range of cells that a fill made, reads in each of its cells with
	/// the relative references moved to the cell, as Excel moves them: <c>=A1*2</c> in <c>B1</c> is <c>=A2*2</c> in
	/// <c>B2</c>. A reference moved past an edge of the sheet wraps around it, as Excel reads it, and a moved formula's
	/// references are in capitals.
	/// </para>
	/// <para>
	/// The other cells of an array formula, and the cells of a data table, read their values: the file gives them no
	/// formula. Newer functions keep the prefix the file stores them with (<c>=_xlfn.XLOOKUP(...)</c>), which Excel
	/// doesn't show.
	/// </para>
	/// </remarks>
	FormulaText,
}
