namespace CsvHelper;

/// <summary>
/// What a field from a <see cref="CsvRecordSource"/> holds, such as a spreadsheet cell: it decides the format provider the
/// field is parsed with, and (from CsvHelper.Excel's typed reading on) its text. Fields of CSV input have no kind.
/// </summary>
internal enum CsvCellKind : byte
{
	/// <summary>Text, parsed with the reader's format provider, as a CSV field is. A cleared kind array is all text.</summary>
	Text = 0,

	/// <summary>A number in invariant form, such as <c>1.5</c> or <c>-2E-05</c>, parsed with the invariant culture.</summary>
	Number = 1,

	/// <summary>
	/// A number in invariant form whose number format makes it a date or a time: a serial date such as <c>45292.5</c>.
	/// Parsed as a number for now; converting it for date and time targets is CsvHelper.Excel's typed reading.
	/// </summary>
	DateNumber = 2,

	/// <summary>A boolean, <c>1</c> or <c>0</c>, parsed with the invariant culture and without the reader's true and false values.</summary>
	Bool = 3,

	/// <summary>An error value, such as <c>#N/A</c> or <c>#DIV/0!</c>, read as text.</summary>
	Error = 4,

	/// <summary>No value (a cell that is not there, or has nothing in it): an empty field.</summary>
	Empty = 5,
}

/// <summary>What the kinds of <see cref="CsvCellKind"/> mean for parsing.</summary>
internal static class CsvCellKinds
{
	/// <summary>
	/// Whether a field of this kind is stored in invariant form, and is parsed with the invariant culture and the built-in
	/// boolean values instead of the reader's format provider and its true and false values.
	/// </summary>
	/// <param name="kind">The kind of the field.</param>
	/// <returns><see langword="true"/> for numbers, dates (serial numbers) and booleans.</returns>
	public static bool IsInvariant(this CsvCellKind kind) => kind is CsvCellKind.Number or CsvCellKind.DateNumber or CsvCellKind.Bool;
}
