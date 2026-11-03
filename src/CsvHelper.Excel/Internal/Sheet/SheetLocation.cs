using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// Where something is in a sheet, as Excel writes it in a formula: <c>Orders!C14</c> for a cell, <c>Orders!14:14</c> for a
/// row, with the sheet's name quoted when Excel would quote it (<c>'Q1 Orders'!C14</c>, <c>'Bob''s'!A1</c>). It is
/// immutable, so the workers of a parallel read can share it.
/// </summary>
internal sealed class SheetLocation
{
	private readonly string prefix;

	/// <summary>Creates the locations of a sheet.</summary>
	/// <param name="sheetName">The sheet's name.</param>
	/// <exception cref="ArgumentNullException"><paramref name="sheetName"/> is <see langword="null"/>.</exception>
	public SheetLocation(string sheetName)
	{
		ArgumentNullException.ThrowIfNull(sheetName);
		Name = sheetName;
		QuotedName = Quote(sheetName);
		prefix = QuotedName + "!";
	}

	/// <summary>The sheet's name.</summary>
	public string Name { get; }

	/// <summary>The sheet's name as a formula writes it: quoted when it needs to be (see <see cref="Quote"/>).</summary>
	public string QuotedName { get; }

	/// <summary>The location of a cell, such as <c>Orders!C14</c>.</summary>
	/// <param name="column">The 0-based column.</param>
	/// <param name="row">The 1-based row.</param>
	/// <returns>The location.</returns>
	public string Cell(int column, long row) => prefix + CellReference.ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

	/// <summary>The location of a row, such as <c>Orders!14:14</c>.</summary>
	/// <param name="row">The 1-based row.</param>
	/// <returns>The location.</returns>
	public string Row(long row)
	{
		var number = row.ToString(CultureInfo.InvariantCulture);

		return prefix + number + ":" + number;
	}

	/// <summary>
	/// Quotes a sheet's name as Excel does in a formula: in <c>'</c>, with each <c>'</c> in it doubled, when it has a
	/// character other than a letter, a digit, <c>_</c> and <c>.</c>, starts with a digit or a <c>.</c>, is <c>TRUE</c> or
	/// <c>FALSE</c>, or reads as a cell reference (see <see cref="ReadsAsReference"/>).
	/// </summary>
	/// <remarks>Checked against Excel 365 (build 20527) on 2026-11-02: it re-quotes the formulas it is given this way.</remarks>
	/// <param name="name">The sheet's name.</param>
	/// <returns>The name as a formula writes it.</returns>
	public static string Quote(string name)
	{
		if (name.Length > 0 && !IsDigit(name[0]) && name[0] != '.' && IsPlain(name) && !ReadsAsReference(name.ToUpperInvariant()))
		{
			return name;
		}

		return "'" + name.Replace("'", "''") + "'";
	}

	/// <summary>Whether every character of a name is a letter, a digit, <c>_</c> or <c>.</c>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>Whether the name needs no quotes for its characters.</returns>
	private static bool IsPlain(string name)
	{
		foreach (var c in name)
		{
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Whether a formula would take a name for a reference, or for the start of one:
	/// <list type="bullet">
	/// <item><c>TRUE</c> and <c>FALSE</c>;</item>
	/// <item>an A1 reference within the sheet (<c>A1</c>, <c>XFD1048576</c>, <c>A01</c>; not <c>XFE1</c> or <c>A0</c>);</item>
	/// <item>an R1C1 reference: <c>R</c>, <c>C</c>, <c>RC</c>, <c>R5</c>, <c>C5</c>, <c>R1C</c>, <c>RC1</c>, <c>R1C1</c>, with numbers 1 to 1,048,576;</item>
	/// <item>
	/// one followed by a letter or <c>_</c>: <c>R2D2</c>, <c>R1_</c>, <c>C3PO</c>, <c>RC1x</c>, where a column is 1 to
	/// 16,384 (<c>C16385x</c> is not quoted). A <c>.</c> after it doesn't count (<c>R1.</c>).
	/// </item>
	/// </list>
	/// </summary>
	/// <param name="upper">The name in upper case: letters, digits, <c>_</c> and <c>.</c> only, not empty.</param>
	/// <returns>Whether the name reads as a reference.</returns>
	private static bool ReadsAsReference(string upper)
	{
		if (upper is "TRUE" or "FALSE")
		{
			return true;
		}

		var letters = 0;
		while (letters < upper.Length && IsAsciiLetter(upper[letters]))
		{
			letters++;
		}

		if (letters is >= 1 and <= 3 && ReadNumber(upper, letters, out var row) == upper.Length && row is >= 1 and <= CellReference.MaxRow)
		{
			var column = 0;
			for (var i = 0; i < letters; i++)
			{
				column = column * 26 + (upper[i] - 'A' + 1);
			}

			if (column <= CellReference.MaxColumns)
			{
				return true;
			}
		}

		if (upper[0] == 'R')
		{
			var end = ReadNumber(upper, 1, out var number);
			if (end > 1)
			{
				// R5, or R5 and more: R2D2, R1C1, R1_.
				return number is >= 1 and <= CellReference.MaxRow && (end == upper.Length || IsWordStart(upper[end]));
			}

			// RC, RC1, RC1x; not RX or RCx.
			return upper.Length == 1 || (upper[1] == 'C' && IsColumnPart(upper, 2));
		}

		return upper[0] == 'C' && IsColumnPart(upper, 1);
	}

	/// <summary>
	/// Whether the rest of a name after R1C1's <c>C</c> reads as its column: nothing, a number up to 1,048,576, or a
	/// column number (1 to 16,384) followed by a letter or <c>_</c>.
	/// </summary>
	/// <param name="upper">The name in upper case.</param>
	/// <param name="start">The index after the <c>C</c>.</param>
	/// <returns>Whether it reads as a column.</returns>
	private static bool IsColumnPart(string upper, int start)
	{
		var end = ReadNumber(upper, start, out var number);
		if (end == upper.Length)
		{
			return end == start || number is >= 1 and <= CellReference.MaxRow;
		}

		return end > start && number is >= 1 and <= CellReference.MaxColumns && IsWordStart(upper[end]);
	}

	/// <summary>Reads the ASCII digits from <paramref name="start"/> as a number, which stops growing past the sheet's rows.</summary>
	/// <param name="text">The text.</param>
	/// <param name="start">The index of the first digit.</param>
	/// <param name="number">The number; 0 when there are no digits.</param>
	/// <returns>The index after the last digit: <paramref name="start"/> when there are none.</returns>
	private static int ReadNumber(string text, int start, out int number)
	{
		number = 0;
		var i = start;
		while (i < text.Length && IsDigit(text[i]))
		{
			if (number <= CellReference.MaxRow)
			{
				number = number * 10 + (text[i] - '0');
			}

			i++;
		}

		return i;
	}

	/// <summary>Whether a character after a reference makes the name read as one that goes on: a letter or <c>_</c>.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is a letter or <c>_</c>.</returns>
	private static bool IsWordStart(char c) => char.IsLetter(c) || c == '_';

	/// <summary>Whether a character is an ASCII digit.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is <c>0</c> to <c>9</c>.</returns>
	private static bool IsDigit(char c) => c is >= '0' and <= '9';

	/// <summary>Whether a character is an ASCII letter.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is <c>A</c> to <c>Z</c> or <c>a</c> to <c>z</c>.</returns>
	private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
