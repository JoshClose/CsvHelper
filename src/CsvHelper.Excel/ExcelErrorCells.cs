namespace CsvHelper.Excel;

/// <summary>How a cell holding an error value, such as <c>#N/A</c> or <c>#DIV/0!</c>, reads (<see cref="ExcelReaderOptions.ErrorCells"/>).</summary>
public enum ExcelErrorCells
{
	/// <summary>
	/// As its text: strings, <c>GetString</c>, dynamic records and <c>CsvDataReader</c> values get <c>#N/A</c>, and any
	/// other target fails with a <see cref="System.FormatException"/> that names the error. Inside <c>GetRecords</c>, that
	/// is a <see cref="CsvFormatException"/> with the cell's location, which goes to
	/// <see cref="CsvReaderOptions.OnRecordError"/>. <see cref="CsvReaderOptions.NullValues"/> can name error texts to
	/// read them as null.
	/// </summary>
	Text,

	/// <summary>As an empty cell: nullable targets get null, and a row whose only values are errors is an empty row.</summary>
	Null,

	/// <summary>
	/// Reading the cell fails for every target, strings included: <c>GetString</c>, <c>Parse</c>, dynamic record values
	/// and <c>CsvDataReader</c> values throw <see cref="System.FormatException"/>, and inside <c>GetRecords</c> the cell
	/// goes to <see cref="CsvReaderOptions.OnRecordError"/>. A cell that isn't read raises nothing, and its span
	/// (<c>GetSpan</c>) is still the error's text. A header cell is a name, not a value: it names its column with the
	/// error's text, as under <see cref="Text"/>.
	/// </summary>
	Error,
}
