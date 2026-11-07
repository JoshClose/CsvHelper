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
/// Excel refuses two names that differ in case, ASCII or not (<c>Äpfel</c> and <c>äpfel</c>, <c>straße</c> and
/// <c>STRASSE</c>, <c>σ</c> and <c>ς</c>, <c>K</c> and the Kelvin sign), or by Unicode equivalence (<c>é</c> composed and
/// decomposed, <c>ﬁ</c> and <c>fi</c>). It opens names that differ in width (<c>A</c>, <c>Ａ</c>), in kana, in accents, in
/// dots (<c>I</c>, <c>ı</c>, <c>i</c>, <c>İ</c>), or by a zero-width space. <see cref="Key"/> agrees with every pair
/// checked but the compatibility forms, which it takes to be the same name, so it is stricter there.
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
	}

	/// <summary>
	/// The key of a name, which names Excel takes to be the same share: its NFKC form (Unicode's compatibility
	/// composition), with <c>ß</c> and <c>ẞ</c> as <c>ss</c>, upper-cased in the invariant culture as .NET does it (on .NET
	/// Framework, with the mappings it lacks: <c>InvariantUpper</c>).
	/// </summary>
	/// <param name="name">The name, which <see cref="Check"/> accepted.</param>
	/// <returns>The key, compared ordinally.</returns>
	public static string Key(string name)
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

		normal = normal.Replace("\u00DF", "ss").Replace("\u1E9E", "ss");
#if NET

		return normal.ToUpperInvariant();
#else

		return InvariantUpper.ToUpper(normal);
#endif
	}

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
