using System;

namespace CsvHelper.Excel;

/// <summary>
/// Cell references in Excel's A1 style, such as <c>C14</c>: column letters (<c>A</c> to <c>XFD</c>), then the row number
/// (1 to 1,048,576). Columns here are 0-based (<c>A</c> is 0), rows 1-based, as Excel numbers them.
/// </summary>
internal static class CellReference
{
	/// <summary>The number of rows of a sheet, and so its last row's number.</summary>
	public const int MaxRow = 1048576;

	/// <summary>The number of columns of a sheet: <c>A</c> to <c>XFD</c>.</summary>
	public const int MaxColumns = 16384;

	/// <summary>The most letters a column has: <c>XFD</c>.</summary>
	private const int MaxColumnLetters = 3;

	/// <summary>The most digits a row has: <c>1048576</c>.</summary>
	private const int MaxRowDigits = 7;

	/// <summary>The letters of a column, such as <c>A</c>, <c>Z</c>, <c>AA</c> or <c>XFD</c>.</summary>
	/// <param name="column">The 0-based column: 0 to <see cref="MaxColumns"/> - 1.</param>
	/// <returns>The letters.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="column"/> is not a column of a sheet.</exception>
	public static string ColumnName(int column)
	{
		if ((uint)column >= MaxColumns)
		{
			throw new ArgumentOutOfRangeException(nameof(column), column, "The column is not one of a sheet's.");
		}

		// Bijective base 26: A is 1 and Z is 26, so AA follows Z.
		Span<char> letters = stackalloc char[MaxColumnLetters];
		var i = MaxColumnLetters;
		var n = column + 1;
		while (n > 0)
		{
			n--;
			letters[--i] = (char)('A' + n % 26);
			n /= 26;
		}

		return letters.Slice(i).ToString();
	}

	/// <summary>
	/// Parses a reference such as <c>C14</c>: letters of either case, then digits, with no white space or <c>$</c>, within
	/// the sheet's columns and rows.
	/// </summary>
	/// <param name="text">The reference, as UTF-8.</param>
	/// <param name="column">The 0-based column.</param>
	/// <param name="row">The 1-based row.</param>
	/// <returns>Whether <paramref name="text"/> is a reference to a cell of a sheet.</returns>
	public static bool TryParse(ReadOnlySpan<byte> text, out int column, out int row)
	{
		column = 0;
		row = 0;
		var i = 0;
		var letters = 0;
		while (i < text.Length && letters <= MaxColumnLetters)
		{
			var letter = text[i] | 0x20;
			if (letter < 'a' || letter > 'z')
			{
				break;
			}

			column = column * 26 + (letter - 'a' + 1);
			letters++;
			i++;
		}

		var digits = text.Length - i;
		if (letters == 0 || letters > MaxColumnLetters || column > MaxColumns || digits == 0 || digits > MaxRowDigits)
		{
			return false;
		}

		for (; i < text.Length; i++)
		{
			var digit = text[i] - '0';
			if ((uint)digit > 9)
			{
				return false;
			}

			row = row * 10 + digit;
		}

		column--;

		return row >= 1 && row <= MaxRow;
	}
}
