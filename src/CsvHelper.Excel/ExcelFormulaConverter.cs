namespace CsvHelper.Excel;

/// <summary>
/// Reads and writes an <see cref="ExcelFormula"/> member: its text, <c>=</c> and the formula, which
/// <see cref="ExcelFormulas.FormulaText"/> reads; and a formula cell in a workbook. On .NET the generated record writer and
/// mapper need no converter; on the other targets, a member reads through this one, with
/// <c>[CsvConverter(typeof(ExcelFormulaConverter))]</c>.
/// </summary>
public sealed class ExcelFormulaConverter : ICsvConverter<ExcelFormula>
{
	/// <summary>
	/// Reads a field as a formula: <c>=</c> and the formula, or empty for <c>default(ExcelFormula)</c>
	/// (see <see cref="ExcelFormula.Parse(string, System.IFormatProvider?)"/>).
	/// </summary>
	/// <param name="reader">The reader, positioned on the record.</param>
	/// <param name="index">The 0-based index of the member's column.</param>
	/// <returns>The formula.</returns>
	/// <exception cref="System.FormatException">The field is not a formula's text, or Excel opens no workbook with the formula.</exception>
	public ExcelFormula Read(CsvReader reader, int index) => ExcelFormula.Parse(reader.GetString(index));

	/// <summary>Writes a formula: a formula cell in a workbook, its text (<c>=</c> and the formula) in CSV.</summary>
	/// <param name="writer">The writer, in the middle of the record.</param>
	/// <param name="value">The formula; <c>default(ExcelFormula)</c> is an empty field.</param>
	public void Write(CsvWriter writer, ExcelFormula value) => writer.WriteField(value);
}
