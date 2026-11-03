namespace CsvHelper;

/// <summary>
/// What a field from a <see cref="CsvRecordSource"/> holds, such as a spreadsheet cell: it decides how the field is
/// parsed and what its text is (see <see cref="CsvCells"/>). Fields of CSV input have no kind.
/// </summary>
internal enum CsvCellKind : byte
{
	/// <summary>Text, parsed with the reader's format provider, as a CSV field is. A cleared kind array is all text.</summary>
	Text = 0,

	/// <summary>A number in invariant form, such as <c>1.5</c> or <c>-2E-05</c>, parsed with the invariant culture.</summary>
	Number = 1,

	/// <summary>
	/// A number in invariant form whose number format makes it a date or a time: a serial date such as <c>45292.5</c>.
	/// Date and time targets get the date; its text is ISO 8601.
	/// </summary>
	DateNumber = 2,

	/// <summary>A boolean, <c>1</c> or <c>0</c>, parsed with the invariant culture and without the reader's true and false values.</summary>
	Bool = 3,

	/// <summary>An error value, such as <c>#N/A</c> or <c>#DIV/0!</c>: its text is the error, and other targets fail.</summary>
	Error = 4,

	/// <summary>No value (a cell that is not there, or has nothing in it): an empty field.</summary>
	Empty = 5,

	/// <summary>
	/// A number in invariant form whose number format shows elapsed time, such as <c>[h]:mm</c>: a number of days, such as
	/// <c>1.5</c>. Its text is <c>36:00:00</c>; it converts as a <see cref="DateNumber"/> does.
	/// </summary>
	Duration = 6,
}

/// <summary>What the kinds of <see cref="CsvCellKind"/> mean for parsing.</summary>
internal static class CsvCellKinds
{
	/// <summary>
	/// Whether a field of this kind is converted by <see cref="CsvCells"/>: everything but text and empty fields, which
	/// read as CSV text does. What it doesn't convert, it leaves to the caller to parse in invariant form, with the
	/// invariant culture and the built-in boolean values instead of the reader's format provider and its true and false
	/// values.
	/// </summary>
	/// <param name="kind">The kind of the field.</param>
	/// <returns><see langword="true"/> for numbers, dates, durations, booleans and errors.</returns>
	public static bool IsTyped(this CsvCellKind kind) => kind is not (CsvCellKind.Text or CsvCellKind.Empty);

	/// <summary>
	/// Whether a field of this kind has text of its own, which is not its bytes, so that the source flags it for
	/// <see cref="CsvReader.GetString(int)"/>'s slow path: dates, durations, booleans and errors.
	/// </summary>
	/// <param name="kind">The kind of the field.</param>
	/// <returns><see langword="true"/> for the kinds whose text <see cref="CsvCells.Text"/> may give.</returns>
	public static bool HasText(this CsvCellKind kind) =>
		kind is CsvCellKind.DateNumber or CsvCellKind.Bool or CsvCellKind.Error or CsvCellKind.Duration;
}
