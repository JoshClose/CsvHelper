namespace CsvHelper.Excel;

/// <summary>
/// What a cell holds, as the reader's fields carry it: a mirror of CsvHelper's internal <see cref="CsvCellKind"/>, with the
/// same names and values. The tests check kinds through it without seeing CsvHelper's internals, and
/// <see cref="SheetCellKinds.ToCsv"/> converts with a cast.
/// </summary>
internal enum SheetCellKind : byte
{
	/// <summary>Text, parsed with the reader's format provider.</summary>
	Text = 0,

	/// <summary>A number in invariant form.</summary>
	Number = 1,

	/// <summary>A number in invariant form whose number format makes it a date or a time.</summary>
	DateNumber = 2,

	/// <summary>A boolean, <c>1</c> or <c>0</c>.</summary>
	Bool = 3,

	/// <summary>An error value, such as <c>#N/A</c>.</summary>
	Error = 4,

	/// <summary>No value: an empty field.</summary>
	Empty = 5,
}

/// <summary>Converts <see cref="SheetCellKind"/> to <see cref="CsvCellKind"/>.</summary>
internal static class SheetCellKinds
{
	/// <summary>
	/// 1 when every kind has the value of CsvHelper's kind of the same name. Otherwise the division by zero is a compile
	/// error, so the build breaks as soon as the two enums differ.
	/// </summary>
	private const int Mirrored = 1 / (
		(byte)SheetCellKind.Text == (byte)CsvCellKind.Text &&
		(byte)SheetCellKind.Number == (byte)CsvCellKind.Number &&
		(byte)SheetCellKind.DateNumber == (byte)CsvCellKind.DateNumber &&
		(byte)SheetCellKind.Bool == (byte)CsvCellKind.Bool &&
		(byte)SheetCellKind.Error == (byte)CsvCellKind.Error &&
		(byte)SheetCellKind.Empty == (byte)CsvCellKind.Empty ? 1 : 0);

	/// <summary>The same kind as CsvHelper's.</summary>
	/// <param name="kind">The kind.</param>
	/// <returns>The <see cref="CsvCellKind"/> of the same name.</returns>
	public static CsvCellKind ToCsv(this SheetCellKind kind) => (CsvCellKind)kind;
}
