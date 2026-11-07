namespace CsvHelper.Excel;

/// <summary>
/// What a written workbook does with an integer or a <see cref="decimal"/> of more than 15 significant digits
/// (<see cref="ExcelWriterOptions.LongNumbers"/>), which Excel would round to 15: an ID or a card number would silently
/// change. Leading zeros and the zeros that end the digits don't count, so <c>10000000000000000000</c> has one significant
/// digit. Doubles are numbers whatever their digits, since the cell stores the same double.
/// </summary>
public enum ExcelLongNumbers
{
	/// <summary>A text cell of the number's text, which keeps every digit. The default.</summary>
	Text,

	/// <summary>A number cell of the number's text, which Excel reads as the nearest double and shows to 15 digits.</summary>
	Number,

	/// <summary>Throw <see cref="System.InvalidOperationException"/>, naming the cell.</summary>
	Throw,
}
