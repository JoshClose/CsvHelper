using System;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The rules for the names of the sheets a workbook writer writes, as Excel opens them (checked through COM on
/// 2026-11-06), and the key that two names Excel takes to be the same share.
/// </summary>
/// <remarks>
/// <para>
/// A name has 1 to 31 UTF-16 code units, none of <c>\ / ? * [ ] :</c>, no apostrophe first or last, and no control
/// characters (U+0000 to U+001F and U+007F to U+009F) or lone surrogates. Excel opens names with control characters, but
/// its tabs can't show them, so the writer is stricter. Anything else is allowed: <c>History</c>, <c>TRUE</c>, <c>A1</c>,
/// spaces at either end, and <c>= + ! # $ % &amp; ; , " &lt;</c>, all of which Excel opens.
/// </para>
/// <para>
/// The 31 are counted as the workbook part writes the name, too, where an <c>_</c> that would start an escape is
/// <c>_x005F_</c>, and U+FFFE and U+FFFF are <c>_xFFFE_</c> and <c>_xFFFF_</c>, 7 code units each: the schema limits the
/// attribute so, and the Open XML SDK's validator with it. Excel counts the characters, and opens a name of 31 with such
/// escapes (checked on 2026-11-07), so the writer is stricter there too.
/// </para>
/// <para>
/// Excel refuses two names that differ in case, ASCII or not (<c>Äpfel</c> and <c>äpfel</c>, <c>straße</c> and
/// <c>STRASSE</c>, <c>σ</c> and <c>ς</c>, <c>K</c> and the Kelvin sign), or by Unicode equivalence (<c>é</c> composed and
/// decomposed, <c>ﬁ</c> and <c>fi</c>). It opens names that differ in width (<c>A</c>, <c>Ａ</c>), in kana, in accents, in
/// dots (<c>I</c>, <c>ı</c>, <c>i</c>, <c>İ</c>), or by a zero-width space. <see cref="Key"/> agrees with every pair
/// checked but the compatibility forms, which it takes to be the same name, so it is stricter there.
/// </para>
/// <para>
/// Excel ignores some characters when it compares names, a sheet's or a table's column's (checked through COM on
/// 2026-11-11, after Phase 5's presentation fuzzer): <c>\uFEFFa</c>, <c>\u00ADa</c> and <c>\uFFFDa</c> are <c>a</c>, and
/// <c>a\u00ADb</c> is <c>ab</c>. They are the soft hyphen, the combining grapheme joiner, the format characters (the joiners,
/// the directions' marks, the word joiner, the byte order mark), the variation selectors, U+FFFC, U+FFFD and the
/// non-characters; not U+200B, U+180E or the language tags. <see cref="Key"/> drops them wherever they are, where Excel
/// kept one at a name's end, so it is stricter there.
/// </para>
/// </remarks>
internal static class SheetNames
{
	/// <summary>The most UTF-16 code units a sheet's name has.</summary>
	public const int MaxLength = 31;

	/// <summary>The characters a sheet's name can't have.</summary>
	private static readonly char[] Forbidden = ['\\', '/', '?', '*', '[', ']', ':'];

	/// <summary>Checks that Excel opens a sheet's name.</summary>
	/// <param name="name">The name.</param>
	/// <param name="paramName">The name of the parameter it came from.</param>
	/// <exception cref="ArgumentException">The name is not one Excel opens, or has control characters or lone surrogates.</exception>
	public static void Check(string name, string paramName)
	{
		if (name.Length is 0 or > MaxLength)
		{
			throw new ArgumentException($"Sheet names are 1 to {MaxLength} characters long, and \"{name}\" has {name.Length}.", paramName);
		}

		var forbidden = name.IndexOfAny(Forbidden);
		if (forbidden >= 0)
		{
			throw new ArgumentException($"A sheet name can't have \\, /, ?, *, [, ] or :, and \"{name}\" has {name[forbidden]}.", paramName);
		}

		if (name[0] == '\'' || name[name.Length - 1] == '\'')
		{
			throw new ArgumentException($"A sheet name can't start or end with an apostrophe: \"{name}\".", paramName);
		}

		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsControl(c))
			{
				throw new ArgumentException($"A sheet name can't have control characters, and \"{Shown(name)}\" has U+{(int)c:X4}.", paramName);
			}

			if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
			{
				i++;
			}
			else if (char.IsSurrogate(c))
			{
				throw new ArgumentException($"A sheet name can't have a lone surrogate, and \"{Shown(name)}\" has U+{(int)c:X4}.", paramName);
			}
		}

		var written = WrittenLength(name);
		if (written > MaxLength)
		{
			throw new ArgumentException(
				$"A sheet name is at most {MaxLength} characters long as the workbook writes it, and \"{Shown(name)}\" is {written}: an _ before x and four hexadecimal digits is written _x005F_, and U+FFFE and U+FFFF _xFFFE_ and _xFFFF_.",
				paramName);
		}
	}

	/// <summary>
	/// The key of a name, which names Excel takes to be the same share: without the characters Excel ignores
	/// (<see cref="IsIgnored"/>), its NFKC form (Unicode's compatibility composition), with <c>ß</c> and <c>ẞ</c> as <c>ss</c>,
	/// upper-cased in the invariant culture as .NET does it (on .NET Framework, with the mappings it lacks:
	/// <c>InvariantUpper</c>); and that again, as Unicode's caseless match does, since each step can undo the other's:
	/// <c>I</c> and U+0307 compose to <c>İ</c>, while <c>i</c> and U+0307, which Excel takes to be the same name (checked on
	/// 2026-11-07), don't compose until they are upper-cased.
	/// </summary>
	/// <param name="name">The name, which <see cref="Check"/> accepted.</param>
	/// <returns>The key, compared ordinally.</returns>
	public static string Key(string name) => Upper(Normalized(Upper(Normalized(WithoutIgnored(name)))));

	/// <summary>A name without the characters Excel ignores when it compares names (<see cref="IsIgnored"/>).</summary>
	/// <param name="name">The name, without lone surrogates.</param>
	/// <returns>The name, or the same string when it has none of them.</returns>
	private static string WithoutIgnored(string name)
	{
		StringBuilder? kept = null;
		for (var i = 0; i < name.Length; i++)
		{
			var pair = char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]);
			var code = pair ? char.ConvertToUtf32(name[i], name[i + 1]) : name[i];
			if (IsIgnored(code))
			{
				kept ??= new StringBuilder(name, 0, i, name.Length);
			}
			else
			{
				kept?.Append(name, i, pair ? 2 : 1);
			}

			if (pair)
			{
				i++;
			}
		}

		return kept?.ToString() ?? name;
	}

	/// <summary>
	/// Whether Excel ignores a character when it compares names: the soft hyphen, the combining grapheme joiner, Unicode's
	/// format characters but U+200B, U+180E and the language tags (which Excel compares), the variation selectors of the
	/// Basic Multilingual Plane, U+FFFC, U+FFFD and the non-characters. A fixed list, so that every target agrees.
	/// </summary>
	/// <param name="code">The character's code point.</param>
	/// <returns>Whether it is ignored.</returns>
	private static bool IsIgnored(int code) =>
		code is 0x00AD or 0x034F or 0x061C or 0x06DD or 0x070F or 0x08E2 or 0x110BD or 0x110CD or 0xFEFF ||
		code is (>= 0x0600 and <= 0x0605) or (>= 0x0890 and <= 0x0891) or (>= 0x200C and <= 0x200F) or (>= 0x202A and <= 0x202E) ||
		code is (>= 0x2060 and <= 0x2064) or (>= 0x2066 and <= 0x206F) or (>= 0xFE00 and <= 0xFE0F) or (>= 0xFFF9 and <= 0xFFFD) ||
		code is (>= 0x13430 and <= 0x1343F) or (>= 0x1BCA0 and <= 0x1BCA3) or (>= 0x1D173 and <= 0x1D17A) ||
		code is (>= 0xFDD0 and <= 0xFDEF) || (code & 0xFFFE) == 0xFFFE;

	/// <summary>A name's NFKC form, with <c>ß</c> and <c>ẞ</c> as <c>ss</c>.</summary>
	/// <param name="name">The name, which has no lone surrogates.</param>
	/// <returns>The normalized name.</returns>
	private static string Normalized(string name)
	{
		string normal;
		try
		{
			normal = name.Normalize(NormalizationForm.FormKC);
		}
		catch (PlatformNotSupportedException)
		{
			// Globalization's invariant mode without normalization: names are then compared by case alone.
			normal = name;
		}
		catch (ArgumentException)
		{
			// The normalization refuses non-characters (U+FFFE on .NET, every one on .NET Framework), which the key drops first.
			normal = name;
		}

		normal = normal.Replace("\u00DF", "ss").Replace("\u1E9E", "ss");

		return normal;
	}

	/// <summary>A name upper-cased in the invariant culture, as .NET does it.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The name in upper case.</returns>
	private static string Upper(string name)
	{
#if NET
		return name.ToUpperInvariant();
#else
		return InvariantUpper.ToUpper(name);
#endif
	}

	/// <summary>
	/// The stem of the names of a sheet's copies, and the number of the first, as Excel names copies (checked through COM
	/// on 2026-11-06): <c>Orders</c> gives <c>Orders (2)</c>, and <c>Orders (2)</c> gives <c>Orders (3)</c>, whose stem is
	/// <c>Orders</c>.
	/// </summary>
	/// <param name="name">The sheet's name.</param>
	/// <param name="number">The number of its first copy.</param>
	/// <returns>The stem: the name before a space and a number in parentheses at its end, or the whole name.</returns>
	public static string CopyStem(string name, out long number)
	{
		number = 2;
		var open = name.LastIndexOf(" (", StringComparison.Ordinal);
		var digits = name.Length - open - 3;
		if (open < 0 || digits is < 1 or > 9 || name[name.Length - 1] != ')')
		{
			return name;
		}

		long value = 0;
		for (var i = open + 2; i < name.Length - 1; i++)
		{
			if (name[i] is < '0' or > '9')
			{
				return name;
			}

			value = value * 10 + (name[i] - '0');
		}

		number = value + 1;

		return name.Substring(0, open);
	}

	/// <summary>
	/// The name of a sheet's copy, as Excel names it: the stem, then a space and the number in parentheses, with the stem
	/// cut to fit 31 code units (<c>abcdefghijklmnopqrstuvwxyz1 (2)</c> for a name of 31 letters and digits), as written
	/// (see <see cref="Check"/>), but not between a surrogate pair's halves.
	/// </summary>
	/// <param name="stem">The stem (<see cref="CopyStem"/>).</param>
	/// <param name="number">The copy's number.</param>
	/// <returns>The name, which <see cref="Check"/> accepts when the sheet's name was accepted.</returns>
	public static string CopyName(string stem, long number)
	{
		var suffix = " (" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
		var room = MaxLength - suffix.Length;
		if (stem.Length > room)
		{
			stem = stem.Substring(0, char.IsHighSurrogate(stem[room - 1]) ? room - 1 : room);
		}

		// Its escapes take more as written (see Check), so it is cut a character at a time until it fits.
		while (WrittenLength(stem + suffix) > MaxLength)
		{
			var cut = stem.Length - 1;
			stem = stem.Substring(0, cut > 0 && char.IsLowSurrogate(stem[cut]) ? cut - 1 : cut);
		}

		return stem + suffix;
	}

	/// <summary>The code units a name takes as the workbook part writes it (<see cref="XmlOutput.WrittenLength"/>).</summary>
	/// <param name="name">The name, which has no lone surrogates.</param>
	/// <returns>The code units.</returns>
	private static int WrittenLength(string name) => name.IndexOf('_') < 0 && name.IndexOf((char)0xFFFE) < 0 && name.IndexOf((char)0xFFFF) < 0
		? name.Length
		: XmlOutput.WrittenLength(Encoding.UTF8.GetBytes(name), attribute: true);

	/// <summary>A name for a message, with its control characters as escapes, such as <c>a\tb</c>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The name shown.</returns>
	private static string Shown(string name)
	{
		var shown = new StringBuilder(name.Length);
		foreach (var c in name)
		{
			shown.Append(c switch
			{
				'\t' => "\\t",
				'\n' => "\\n",
				'\r' => "\\r",
				_ when char.IsControl(c) || char.IsSurrogate(c) => $"\\u{(int)c:X4}",
				_ => c.ToString(),
			});
		}

		return shown.ToString();
	}
}
