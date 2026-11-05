using System;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// References to blocks of cells in Excel's A1 form, as ranges, defined names, tables and merged cells write them: an area
/// (<c>B5</c>, <c>B5:F200</c>, <c>B:F</c>, <c>5:200</c>, with or without <c>$</c>), with a sheet's name in front where a
/// name or a range can have one (<c>Orders!B5:F200</c>, <c>'Q1 Orders'!$B$5</c>).
/// </summary>
/// <remarks>
/// <para>
/// Letters may be of either case, the corners in either order, and white space may surround the whole: Excel reads defined
/// names written so (checked through COM on 2026-11-04). Anything else is not a block: unions (<c>A1,C3</c>), intersections
/// (<c>A1:B2 B1:B3</c>), 3-D references (<c>Sheet1:Sheet3!A1</c>), other workbooks' (<c>[1]Sheet1!A1</c>), <c>#REF!</c>,
/// parentheses, functions and constants.
/// </para>
/// <para>
/// A quoted sheet name doubles each <c>'</c> in it. An unquoted one is everything before the first <c>!</c>, which must
/// have none of the characters that make a reference something else: <c>' [ ] : , ( ) " # =</c> and white space.
/// </para>
/// </remarks>
internal static class AreaReference
{
	/// <summary>The longest area in UTF-8 that <see cref="TryParseArea(ReadOnlySpan{byte}, out ExcelRange)"/> reads.</summary>
	private const int MaxUtf8Length = 64;

	/// <summary>The characters an unquoted sheet name can't have.</summary>
	private const string NotInPlainSheetNames = "'[]:,()\"#= \t\r\n";

	/// <summary>Parses a block with an optional sheet's name in front.</summary>
	/// <param name="text">The text.</param>
	/// <param name="sheet">The sheet's name, unquoted; <see langword="null"/> when the text has none.</param>
	/// <param name="range">The block.</param>
	/// <returns>Whether the text is a block, with or without a sheet's name.</returns>
	public static bool TryParse(ReadOnlySpan<char> text, out string? sheet, out ExcelRange range)
	{
		range = default;

		return TrySplitSheet(Trim(text), out sheet, out var area) && TryParseArea(area, out range);
	}

	/// <summary>
	/// Splits a sheet's name off the front of a reference, such as <c>'Q1 Orders'!B5</c> or <c>Orders!Data</c> (a sheet's
	/// defined name): a quoted name, with each <c>'</c> in it doubled, or an unquoted one, before a <c>!</c>.
	/// </summary>
	/// <param name="text">The reference.</param>
	/// <param name="sheet">The sheet's name, unquoted; <see langword="null"/> when the reference has none.</param>
	/// <param name="rest">What follows the <c>!</c>, or the whole reference when it has no sheet's name.</param>
	/// <returns>Whether the reference has a well-formed sheet's name in front, or none.</returns>
	public static bool TrySplitSheet(ReadOnlySpan<char> text, out string? sheet, out ReadOnlySpan<char> rest)
	{
		sheet = null;
		rest = text;
		if (text.Length > 0 && text[0] == '\'')
		{
			var name = new StringBuilder();
			var i = 1;
			while (true)
			{
				if (i >= text.Length)
				{
					return false;
				}

				if (text[i] == '\'')
				{
					if (i + 1 < text.Length && text[i + 1] == '\'')
					{
						name.Append('\'');
						i += 2;

						continue;
					}

					break;
				}

				name.Append(text[i]);
				i++;
			}

			// i is at the closing quote, which a '!' must follow.
			if (name.Length == 0 || i + 1 >= text.Length || text[i + 1] != '!')
			{
				return false;
			}

			sheet = name.ToString();
			rest = text.Slice(i + 2);

			return true;
		}

		var bang = text.IndexOf('!');
		if (bang < 0)
		{
			return true;
		}

		var plain = text.Slice(0, bang);
		if (plain.IsEmpty || plain.IndexOfAny(NotInPlainSheetNames.AsSpan()) >= 0)
		{
			return false;
		}

		sheet = plain.ToString();
		rest = text.Slice(bang + 1);

		return true;
	}

	/// <summary>Parses a block without a sheet's name: <c>B5</c>, <c>B5:F200</c>, <c>B:F</c> or <c>5:200</c>.</summary>
	/// <param name="text">The text.</param>
	/// <param name="range">The block.</param>
	/// <returns>Whether the text is a block of cells of a sheet.</returns>
	public static bool TryParseArea(ReadOnlySpan<char> text, out ExcelRange range)
	{
		range = default;
		text = Trim(text);
		var colon = text.IndexOf(':');
		if (colon < 0)
		{
			if (!TryParsePart(text, out var column, out var row) || column < 0 || row < 0)
			{
				return false;
			}

			range = new ExcelRange(row, column + 1, row, column + 1);

			return true;
		}

		var second = text.Slice(colon + 1);
		if (second.IndexOf(':') >= 0 || !TryParsePart(text.Slice(0, colon), out var column1, out var row1) || !TryParsePart(second, out var column2, out var row2))
		{
			return false;
		}

		if (column1 >= 0 && row1 >= 0 && column2 >= 0 && row2 >= 0)
		{
			range = new ExcelRange(Math.Min(row1, row2), Math.Min(column1, column2) + 1, Math.Max(row1, row2), Math.Max(column1, column2) + 1);

			return true;
		}

		if (row1 < 0 && row2 < 0)
		{
			// Whole columns: neither part has a row.
			range = new ExcelRange(1, Math.Min(column1, column2) + 1, CellReference.MaxRow, Math.Max(column1, column2) + 1);

			return true;
		}

		if (column1 < 0 && column2 < 0)
		{
			// Whole rows: neither part has a column.
			range = new ExcelRange(Math.Min(row1, row2), 1, Math.Max(row1, row2), CellReference.MaxColumns);

			return true;
		}

		return false;
	}

	/// <summary>Parses a block without a sheet's name, as UTF-8, such as an attribute's value. See <see cref="TryParseArea(ReadOnlySpan{char}, out ExcelRange)"/>.</summary>
	/// <param name="utf8">The text, as UTF-8.</param>
	/// <param name="range">The block.</param>
	/// <returns>Whether the text is a block of cells of a sheet.</returns>
	public static bool TryParseArea(ReadOnlySpan<byte> utf8, out ExcelRange range)
	{
		range = default;
		if (utf8.Length > MaxUtf8Length)
		{
			utf8 = TrimBytes(utf8);
			if (utf8.Length > MaxUtf8Length)
			{
				return false;
			}
		}

		Span<char> chars = stackalloc char[MaxUtf8Length];
		for (var i = 0; i < utf8.Length; i++)
		{
			if (utf8[i] >= 0x80)
			{
				return false;
			}

			chars[i] = (char)utf8[i];
		}

		return TryParseArea(chars.Slice(0, utf8.Length), out range);
	}

	/// <summary>
	/// Parses one end of an area: a cell (<c>$B$5</c>), a column (<c>$B</c>) or a row (<c>$5</c>), each part with an
	/// optional <c>$</c> in front.
	/// </summary>
	/// <param name="part">The text.</param>
	/// <param name="column">The column, from 0; -1 when the part has none.</param>
	/// <param name="row">The row, from 1; -1 when the part has none.</param>
	/// <returns>Whether the part is one of those, within the sheet.</returns>
	private static bool TryParsePart(ReadOnlySpan<char> part, out int column, out int row)
	{
		column = -1;
		row = -1;
		var i = 0;
		if (i < part.Length && part[i] == '$')
		{
			i++;
		}

		var letters = 0;
		var number = 0;
		while (i < part.Length && IsAsciiLetter(part[i]))
		{
			if (++letters > 3)
			{
				return false;
			}

			number = number * 26 + ((part[i] | 0x20) - 'a' + 1);
			i++;
		}

		if (letters > 0)
		{
			if (number > CellReference.MaxColumns)
			{
				return false;
			}

			column = number - 1;
			if (i < part.Length && part[i] == '$')
			{
				i++;
			}
		}
		else if (i > 1 || (i == 1 && part.Length == 1))
		{
			// A '$' with nothing after it, or two.
			return false;
		}

		var digits = 0;
		number = 0;
		while (i < part.Length && part[i] is >= '0' and <= '9')
		{
			// Leading zeros are allowed (A01), as Excel's formulas allow them; the value stops growing past the last row.
			if (number <= CellReference.MaxRow)
			{
				number = number * 10 + (part[i] - '0');
			}

			digits++;
			i++;
		}

		if (i != part.Length || (letters == 0 && digits == 0) || (letters > 0 && digits == 0 && part[part.Length - 1] == '$'))
		{
			return false;
		}

		if (digits > 0)
		{
			if (number < 1 || number > CellReference.MaxRow)
			{
				return false;
			}

			row = number;
		}

		return true;
	}

	/// <summary>Removes the XML white space (space, tab, CR, LF) around a text.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text without it.</returns>
	private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
	{
		var start = 0;
		var end = text.Length;
		while (start < end && text[start] is ' ' or '\t' or '\r' or '\n')
		{
			start++;
		}

		while (end > start && text[end - 1] is ' ' or '\t' or '\r' or '\n')
		{
			end--;
		}

		return text.Slice(start, end - start);
	}

	/// <summary>Removes the XML white space around a UTF-8 text.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text without it.</returns>
	private static ReadOnlySpan<byte> TrimBytes(ReadOnlySpan<byte> text)
	{
		var start = 0;
		var end = text.Length;
		while (start < end && XmlScanner.IsWhitespace(text[start]))
		{
			start++;
		}

		while (end > start && XmlScanner.IsWhitespace(text[end - 1]))
		{
			end--;
		}

		return text.Slice(start, end - start);
	}

	/// <summary>Whether a character is an ASCII letter.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is <c>A</c> to <c>Z</c> or <c>a</c> to <c>z</c>.</returns>
	private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
