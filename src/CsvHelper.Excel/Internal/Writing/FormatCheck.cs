using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// Whether Excel opens a number format code: those that <see cref="FormatTranslator"/> gives, and those a caller gives
/// (<see cref="ExcelColumnOptions.Format"/>). Excel opens no workbook with a code it can't read, so a code is checked
/// before it is written.
/// </summary>
/// <remarks>
/// <para>
/// The rules are a grammar of the codes Excel 365 opened and refused through COM (checked on 2026-11-06, 2026-11-07 and
/// 2026-11-08), and are stricter than Excel where a form wasn't checked:
/// </para>
/// <list type="bullet">
/// <item><description>At most 255 characters, which Excel keeps, and at most 4 sections, split by <c>;</c>.</description></item>
/// <item><description>
/// Quotes and brackets that are closed, and a character after each <c>\</c>, <c>_</c> and <c>*</c>.
/// </description></item>
/// <item><description>
/// In brackets: the eight colors, <c>Color1</c> to <c>Color56</c>, a condition without spaces (<c>[&gt;=100]</c>), a
/// currency or locale (<c>[$€-407]</c>, with at most 8 hex digits), elapsed time (<c>[h]</c>, <c>[mm]</c>), and
/// <c>DBNum1</c> to <c>DBNum3</c>.
/// </description></item>
/// <item><description>
/// Letters only as tokens: <c>General</c>, <c>E+</c> and <c>E-</c> after a digit placeholder and before another (Excel
/// refuses <c>e+</c>, and <c>E</c> without a sign), and the date and time tokens <c>y</c>, <c>m</c>, <c>d</c>, <c>h</c>,
/// <c>s</c>, <c>e</c>, <c>g</c>, <c>b</c>, <c>aaa</c> and longer, <c>AM/PM</c> and <c>A/P</c>, in any case. Other
/// letters go in quotes.
/// </description></item>
/// <item><description>
/// A section with a date or time has no <c>#</c>, <c>?</c>, <c>%</c>, <c>@</c>, <c>General</c> or exponent, and its
/// <c>0</c>s are fractions of a second, at most 3 after a <c>.</c> (<c>ss.000</c>). Excel refuses <c>0 h</c>,
/// <c>dd @</c> and <c>ss.0000</c>.
/// </description></item>
/// <item><description>
/// A section without one has no <c>:</c> (Excel refuses <c>0:00</c>), and neither <c>@</c> nor <c>General</c> with digit
/// placeholders.
/// </description></item>
/// <item><description>
/// Other characters outside quotes: a space, <c>$ - + / ( ) ! ^ &amp; ' ` ~ { } &lt; &gt; = |</c>, the digits 1 to 9,
/// <c>. , %</c>, and characters past ASCII that aren't letters or control characters (<c>€</c>, <c>£</c>, <c>‰</c>,
/// <c>°</c>, a no-break space).
/// </description></item>
/// </list>
/// </remarks>
internal static class FormatCheck
{
	/// <summary>The longest code Excel keeps: it cuts a longer one to this many characters.</summary>
	public const int MaxLength = 255;

	/// <summary>The most sections a code has: positive, negative, zero and text.</summary>
	private const int MaxSections = 4;

	/// <summary>The most digits of a fraction of a second Excel shows.</summary>
	private const int MaxFractionDigits = 3;

	/// <summary>The colors Excel knows by name.</summary>
	private static readonly string[] Colors = ["black", "blue", "cyan", "green", "magenta", "red", "white", "yellow"];

	/// <summary>What is wrong with a format code, if Excel wouldn't open it.</summary>
	/// <param name="code">The code.</param>
	/// <returns>Why Excel doesn't open it, or <see langword="null"/> when it does.</returns>
	public static string? Problem(string code)
	{
		if (code.Length > MaxLength)
		{
			return $"The format code is {code.Length.ToString(CultureInfo.InvariantCulture)} characters long, and Excel keeps {MaxLength.ToString(CultureInfo.InvariantCulture)}.";
		}

		var sections = 0;
		var i = 0;
		while (true)
		{
			if (++sections > MaxSections)
			{
				return $"The format code \"{code}\" has more than {MaxSections.ToString(CultureInfo.InvariantCulture)} sections, the most Excel opens.";
			}

			var problem = Section(code, ref i);
			if (problem is not null)
			{
				return $"The format code \"{code}\" {problem}";
			}

			if (i >= code.Length)
			{
				return null;
			}

			// The ; that ends the section.
			i++;
		}
	}

	/// <summary>Checks a section: its tokens one by one, and then what they mean together.</summary>
	/// <param name="code">The code.</param>
	/// <param name="i">Where the section starts; on return, where it ends: at its <c>;</c> or the code's end.</param>
	/// <returns>What is wrong, to follow the code in a message, or <see langword="null"/>.</returns>
	private static string? Section(string code, ref int i)
	{
		var section = new SectionFacts();
		for (; i < code.Length && code[i] != ';'; i++)
		{
			var c = code[i];
			string? problem;
			switch (c)
			{
				case '"':
					var close = code.IndexOf('"', i + 1);
					if (close < 0)
					{
						return "has a quote that isn't closed.";
					}

					i = close;
					section.Last = Token.Other;

					continue;
				case '\\' or '_' or '*':
					if (i + 1 >= code.Length)
					{
						return $"ends with {c}, which needs a character after it.";
					}

					i++;
					section.Last = Token.Other;

					continue;
				case '[':
					var end = code.IndexOf(']', i + 1);
					if (end < 0)
					{
						return "has a bracket that isn't closed.";
					}

					problem = Bracket(code.Substring(i + 1, end - i - 1), ref section);
					if (problem is not null)
					{
						return problem;
					}

					i = end;
					section.Last = Token.Other;

					continue;
				case '0':
					if (section.Last == Token.Point || (section.Last == Token.Zero && section.FractionZeros > 0))
					{
						section.FractionZeros++;
						section.MaxFractionZeros = Math.Max(section.MaxFractionZeros, section.FractionZeros);
					}
					else
					{
						section.Zero = true;
					}

					section.Placeholders = true;
					section.ExponentDigits |= section.Exponent;
					section.Last = Token.Zero;

					continue;
				case '#' or '?':
					section.Placeholders = true;
					section.ExponentDigits |= section.Exponent;
					section.Hash ??= c;
					section.Last = Token.Placeholder;

					continue;
				case '.':
					section.FractionZeros = 0;
					section.Last = Token.Point;

					continue;
				case '%':
					section.Percent = true;
					section.Last = Token.Other;

					continue;
				case '@':
					section.At = true;
					section.Last = Token.Other;

					continue;
				case ':':
					section.Colon = true;
					section.Last = Token.Other;

					continue;
			}

			section.FractionZeros = 0;
			if (IsAsciiLetter(c))
			{
				problem = Letters(code, ref i, ref section);
				if (problem is not null)
				{
					return problem;
				}

				continue;
			}

			if (!IsLiteral(c))
			{
				return $"has {c} outside quotes: put it in quotes.";
			}

			section.Last = Token.Other;
		}

		return section.Check();
	}

	/// <summary>Reads the letters at a position: a token, or a letter that Excel doesn't open outside quotes.</summary>
	/// <param name="code">The code.</param>
	/// <param name="i">The letter's position; on return, the token's last character's.</param>
	/// <param name="section">What the section has so far.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Letters(string code, ref int i, ref SectionFacts section)
	{
		var c = code[i];
		var lower = (char)(c | 0x20);
		if (lower == 'g' && Matches(code, i, "general"))
		{
			section.General = true;
			section.Last = Token.Other;
			i += 6;

			return null;
		}

		// E+ or E- after a digit placeholder, or before one, is an exponent; otherwise e is a year (e-m-d).
		if (lower == 'e' && i + 1 < code.Length && code[i + 1] is '+' or '-' &&
			((section.Placeholders && !section.Date) || (i + 2 < code.Length && code[i + 2] is '0' or '#' or '?')))
		{
			if (c == 'e')
			{
				return "has an exponent in lower case (e+ or e-), which Excel doesn't open: use E+ or E-.";
			}

			if (!section.Placeholders)
			{
				return "has an exponent without a digit placeholder before it.";
			}

			section.Exponent = true;
			section.ExponentDigits = false;
			section.Last = Token.Other;
			i++;

			return null;
		}

		if (lower == 'e' && section.Placeholders && !section.Date && i + 1 < code.Length && code[i + 1] is '0' or '#' or '?')
		{
			return "has an exponent without its sign, which Excel doesn't open: use E+ or E-.";
		}

		if (lower == 'a' && Matches(code, i, "am/pm"))
		{
			section.Date = true;
			section.Last = Token.Other;
			i += 4;

			return null;
		}

		if (lower == 'a' && Matches(code, i, "a/p"))
		{
			section.Date = true;
			section.Last = Token.Other;
			i += 2;

			return null;
		}

		if (lower == 'a')
		{
			var run = 1;
			while (i + run < code.Length && (code[i + run] | 0x20) == 'a')
			{
				run++;
			}

			if (run < 3)
			{
				return $"has the letter {c} outside quotes, which Excel opens only as a date or time token: put text in quotes.";
			}

			section.Date = true;
			section.Last = Token.Other;
			i += run - 1;

			return null;
		}

		if (lower is 'y' or 'm' or 'd' or 'h' or 's' or 'e' or 'g' or 'b')
		{
			section.Date = true;
			section.Last = Token.Other;

			return null;
		}

		return $"has the letter {c} outside quotes, which Excel opens only as a date or time token: put text in quotes.";
	}

	/// <summary>Checks a bracket's content.</summary>
	/// <param name="content">The text between the brackets.</param>
	/// <param name="section">What the section has so far: elapsed time makes it a time.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Bracket(string content, ref SectionFacts section)
	{
		if (IsElapsed(content))
		{
			section.Date = true;

			return null;
		}

		if (IsColor(content) || IsCondition(content) || IsLocale(content) || content is "DBNum1" or "DBNum2" or "DBNum3")
		{
			return null;
		}

		return $"has [{content}], which isn't a color, a condition, a currency or locale, or elapsed time.";
	}

	/// <summary>Whether a bracket's content is one of Excel's colors: a name or <c>Color1</c> to <c>Color56</c>, in any case.</summary>
	/// <param name="content">The content.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsColor(string content)
	{
		foreach (var color in Colors)
		{
			if (string.Equals(content, color, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return content.Length is 6 or 7 && content.StartsWith("color", StringComparison.OrdinalIgnoreCase) &&
			int.TryParse(content.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 56 &&
			content[5] != '0';
	}

	/// <summary>
	/// Whether a bracket's content is a condition: <c>&lt;</c>, <c>&gt;</c>, <c>=</c>, <c>&lt;=</c>, <c>&gt;=</c> or
	/// <c>&lt;&gt;</c>, then a number (<c>-?digits(.digits)?</c>), without spaces, which Excel refuses.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsCondition(string content)
	{
		var i = 0;
		if (content.StartsWith("<=", StringComparison.Ordinal) || content.StartsWith(">=", StringComparison.Ordinal) || content.StartsWith("<>", StringComparison.Ordinal))
		{
			i = 2;
		}
		else if (content.Length > 0 && content[0] is '<' or '>' or '=')
		{
			i = 1;
		}
		else
		{
			return false;
		}

		if (i < content.Length && content[i] == '-')
		{
			i++;
		}

		var digits = i;
		while (i < content.Length && content[i] is >= '0' and <= '9')
		{
			i++;
		}

		if (i == digits)
		{
			return false;
		}

		if (i < content.Length && content[i] == '.')
		{
			var fraction = ++i;
			while (i < content.Length && content[i] is >= '0' and <= '9')
			{
				i++;
			}

			if (i == fraction)
			{
				return false;
			}
		}

		return i == content.Length;
	}

	/// <summary>
	/// Whether a bracket's content is a currency or a locale: <c>$</c>, a symbol without <c>-</c>, and then, optionally,
	/// <c>-</c> and 1 to 8 hex digits (<c>[$-409]</c>, <c>[$€-407]</c>, <c>[$€]</c>).
	/// </summary>
	/// <param name="content">The content.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsLocale(string content)
	{
		if (content.Length == 0 || content[0] != '$')
		{
			return false;
		}

		var dash = content.IndexOf('-');
		if (dash < 0)
		{
			return true;
		}

		var hex = content.Length - dash - 1;
		if (hex is < 1 or > 8)
		{
			return false;
		}

		for (var i = dash + 1; i < content.Length; i++)
		{
			if (!Uri.IsHexDigit(content[i]))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Whether a bracket's content is elapsed time: one or more of the same letter, <c>h</c>, <c>m</c> or <c>s</c>, in any case.</summary>
	/// <param name="content">The content.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsElapsed(string content)
	{
		if (content.Length == 0 || (content[0] | 0x20) is not ('h' or 'm' or 's'))
		{
			return false;
		}

		foreach (var c in content)
		{
			if ((c | 0x20) != (content[0] | 0x20))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Whether a character other than a token's opens outside quotes.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it does: the characters Excel shows as they are, digits, and characters past ASCII that aren't letters or control characters.</returns>
	private static bool IsLiteral(char c)
	{
		if (c < 0x80)
		{
			return c is ' ' or '$' or '-' or '+' or '/' or '(' or ')' or '!' or '^' or '&' or '\'' or '`' or '~' or '{' or '}' or '<' or '>' or '=' or '|' or ',' ||
				c is >= '1' and <= '9';
		}

		return !char.IsLetter(c) && !char.IsControl(c);
	}

	/// <summary>Whether a character is an ASCII letter.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is <c>a</c> to <c>z</c> in either case.</returns>
	private static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';

	/// <summary>Whether <paramref name="code"/> has <paramref name="word"/> at <paramref name="index"/>, in any case.</summary>
	/// <param name="code">The code.</param>
	/// <param name="index">Where to look.</param>
	/// <param name="word">The word, in lower case.</param>
	/// <returns>Whether the word is there.</returns>
	private static bool Matches(string code, int index, string word) =>
		index + word.Length <= code.Length && string.Compare(code, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;

	/// <summary>The last token of a section that a <c>0</c> after it depends on.</summary>
	private enum Token
	{
		/// <summary>Anything else.</summary>
		Other,

		/// <summary>A <c>.</c>.</summary>
		Point,

		/// <summary>A <c>0</c>.</summary>
		Zero,

		/// <summary>A <c>#</c> or <c>?</c>.</summary>
		Placeholder,
	}

	/// <summary>What a section has, to check what its tokens mean together once it is read.</summary>
	private struct SectionFacts
	{
		/// <summary>The last token, for the <c>0</c>s after a <c>.</c>.</summary>
		public Token Last;

		/// <summary>The <c>0</c>s since the last <c>.</c>, while they follow it.</summary>
		public int FractionZeros;

		/// <summary>The longest run of <c>0</c>s after a <c>.</c>.</summary>
		public int MaxFractionZeros;

		/// <summary>Whether a <c>0</c> is anywhere but right after a <c>.</c>.</summary>
		public bool Zero;

		/// <summary>Whether the section has a digit placeholder, <c>0</c>s after a <c>.</c> included.</summary>
		public bool Placeholders;

		/// <summary>The first <c>#</c> or <c>?</c>, if any.</summary>
		public char? Hash;

		/// <summary>Whether it has a date or time token, or elapsed time.</summary>
		public bool Date;

		/// <summary>Whether it has <c>%</c>.</summary>
		public bool Percent;

		/// <summary>Whether it has <c>@</c>.</summary>
		public bool At;

		/// <summary>Whether it has <c>General</c>.</summary>
		public bool General;

		/// <summary>Whether it has <c>:</c>.</summary>
		public bool Colon;

		/// <summary>Whether it has an exponent.</summary>
		public bool Exponent;

		/// <summary>Whether a digit placeholder follows the exponent.</summary>
		public bool ExponentDigits;

		/// <summary>Checks what the section's tokens mean together.</summary>
		/// <returns>What is wrong, or <see langword="null"/>.</returns>
		public readonly string? Check()
		{
			if (Date)
			{
				if (Hash is { } hash)
				{
					return $"has {hash} in a section with a date or time, which Excel doesn't open.";
				}

				if (Zero)
				{
					return "has 0 in a section with a date or time, where Excel opens it only for fractions of a second (ss.000).";
				}

				if (MaxFractionZeros > MaxFractionDigits)
				{
					return $"shows fractions of a second to {MaxFractionZeros.ToString(CultureInfo.InvariantCulture)} digits, and Excel shows at most {MaxFractionDigits.ToString(CultureInfo.InvariantCulture)}.";
				}

				var other = Percent ? "%" : At ? "@" : General ? "General" : Exponent ? "an exponent" : null;

				return other is null ? null : $"has {other} in a section with a date or time, which Excel doesn't open.";
			}

			if (Colon)
			{
				return "has : in a section without a date or time, which Excel doesn't open.";
			}

			if (Exponent && !ExponentDigits)
			{
				return "has an exponent without a digit placeholder after it.";
			}

			if (Placeholders && (At || General))
			{
				return $"has {(At ? "@" : "General")} in a section with digit placeholders, which Excel doesn't open.";
			}

			return null;
		}
	}
}
