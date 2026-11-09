using System;

namespace CsvHelper.Excel;

/// <summary>
/// The rules for the names of the tables a workbook writer writes, as Excel opens them (checked through COM on
/// 2026-11-08; the probes are in <c>Csv\_bench\results\phase5\design</c>).
/// </summary>
/// <remarks>
/// <para>
/// A name has 1 to 255 characters. It starts with a letter, <c>_</c> or <c>\</c>, and then has letters, the digits 0 to
/// 9, <c>_</c>, <c>.</c>, <c>?</c>, <c>\</c> and characters beyond the Basic Multilingual Plane. Excel opened <c>_1</c>,
/// <c>\Orders</c>, <c>Q1.Orders</c>, <c>a?b</c>, CJK, <c>ü</c>, an emoji after a letter, <c>SUM</c>, <c>Print_Area</c>,
/// <c>Table1</c> and 255 characters; and refused a space, a hyphen, a digit or a dot first, and 256 characters.
/// </para>
/// <para>
/// It isn't a cell reference: one to three letters up to <c>XFD</c>, then a row (Excel refused <c>A1</c>, <c>a1</c> and
/// <c>XFD1048576</c>, and opened <c>XFE1</c>, <c>ABCD1</c>, <c>A0</c> and <c>XFD</c>). Nor is it <c>R</c> or <c>C</c>
/// followed by a digit (<c>R1</c>, <c>C1</c>, <c>R1C</c>, <c>R1C1</c>, <c>R2D2</c> were refused), <c>RC</c> followed by
/// one (<c>RC1</c>), <c>R</c>, <c>C</c> or <c>RC</c>, in any case, or <c>TRUE</c> (refused) and <c>FALSE</c>.
/// </para>
/// <para>
/// The rules are stricter than Excel where a form wasn't checked: characters in the Basic Multilingual Plane past ASCII
/// that aren't letters, a cell reference whose row is past Excel's last (<c>A1048577</c>) or has leading zeros
/// (<c>A01</c>), and names that start with <c>_xlnm.</c>, which Excel keeps for its own names.
/// </para>
/// </remarks>
internal static class TableNames
{
	/// <summary>The most characters a table's name has.</summary>
	public const int MaxLength = 255;

	/// <summary>The prefix of Excel's own names, such as <c>_xlnm._FilterDatabase</c>.</summary>
	private const string ReservedPrefix = "_xlnm.";

	/// <summary>The last column's number, <c>XFD</c>.</summary>
	private const int MaxColumn = 16384;

	/// <summary>What is wrong with a table's name, if Excel wouldn't open it.</summary>
	/// <param name="name">The name.</param>
	/// <returns>What is wrong, to follow "the table" in a message, or <see langword="null"/>.</returns>
	public static string? Problem(string name)
	{
		if (name.Length is 0 or > MaxLength)
		{
			return $"has a name of {name.Length} characters, and Excel's table names have 1 to {MaxLength}.";
		}

		var first = name[0];
		if (!(char.IsLetter(first) || first is '_' or '\\'))
		{
			return $"has the name \"{name}\", which starts with {Shown(first)}: Excel's table names start with a letter, _ or \\.";
		}

		for (var i = 1; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
			{
				i++;

				continue;
			}

			if (!(char.IsLetter(c) || c is (>= '0' and <= '9') or '_' or '.' or '?' or '\\'))
			{
				return $"has the name \"{name}\", which has {Shown(c)}: Excel's table names have letters, digits, _, ., ? and \\.";
			}
		}

		if (name.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return $"has the name \"{name}\", which starts with {ReservedPrefix}, as Excel's own names do.";
		}

		if (IsCellReference(name))
		{
			return $"has the name \"{name}\", which is a cell reference: Excel opens no table of that name.";
		}

		if (IsRowOrColumnReference(name))
		{
			return $"has the name \"{name}\", which Excel reads as a row or column reference (R1C1): Excel opens no table of that name.";
		}

		if (string.Equals(name, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "FALSE", StringComparison.OrdinalIgnoreCase))
		{
			return $"has the name \"{name}\", a boolean: Excel opens no table of that name.";
		}

		return null;
	}

	/// <summary>
	/// Whether a name is a cell reference: one to three letters naming a column up to <c>XFD</c>, then digits naming a row
	/// from 1, in any case.
	/// </summary>
	/// <param name="name">The name, which has only letters, digits and <c>_ . ? \</c>.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsCellReference(string name)
	{
		var letters = 0;
		var column = 0;
		while (letters < name.Length && IsAsciiLetter(name[letters]))
		{
			if (++letters > 3)
			{
				return false;
			}

			column = column * 26 + ((name[letters - 1] | 0x20) - 'a' + 1);
		}

		if (letters == 0 || letters == name.Length || column > MaxColumn)
		{
			return false;
		}

		var nonZero = false;
		for (var i = letters; i < name.Length; i++)
		{
			if (name[i] is not (>= '0' and <= '9'))
			{
				return false;
			}

			nonZero |= name[i] != '0';
		}

		// A0 isn't a cell: Excel opened it.
		return nonZero;
	}

	/// <summary>
	/// Whether Excel reads a name as a row or column reference: <c>R</c>, <c>C</c> or <c>RC</c>, or <c>R</c>, <c>C</c> or
	/// <c>RC</c> followed by a digit, in any case.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>Whether it does.</returns>
	private static bool IsRowOrColumnReference(string name)
	{
		var lower0 = name[0] | 0x20;
		if (lower0 is not ('r' or 'c'))
		{
			return false;
		}

		var at = 1;
		if (lower0 == 'r' && at < name.Length && (name[at] | 0x20) == 'c')
		{
			at++;
		}

		return at == name.Length || name[at] is >= '0' and <= '9';
	}

	/// <summary>Whether a character is an ASCII letter.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is <c>a</c> to <c>z</c> in either case.</returns>
	private static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';

	/// <summary>A character as a message shows it: itself when it shows, else its code.</summary>
	/// <param name="c">The character.</param>
	/// <returns>The text.</returns>
	private static string Shown(char c) =>
		char.IsControl(c) || char.IsWhiteSpace(c) || char.IsSurrogate(c) ? $"U+{(int)c:X4}" : c.ToString();
}
