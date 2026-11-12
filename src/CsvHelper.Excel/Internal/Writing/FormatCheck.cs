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
/// The rules are a grammar of the codes Excel 365 opened and refused through COM (checked on 2026-11-06, 2026-11-07,
/// 2026-11-08 and, after Phase 5's format fuzzer, 2026-11-11: <c>Csv\_bench\results\phase5\step7\probe</c>), and are
/// stricter than Excel where a form wasn't checked:
/// </para>
/// <list type="bullet">
/// <item><description>
/// At most 255 characters, which Excel keeps, and at most 4 sections, split by <c>;</c>. And no longer than Excel opens,
/// which isn't a count of characters (it opened 83 <c>=</c> and 255 <c>0</c>, but not 84 <c>=</c>): the check counts 3
/// for each character outside quotes that shows as itself (and each escape, <c>_</c> and <c>*</c> with its character), 2
/// for each character in quotes and each digit placeholder after a <c>.</c>, and 1 for anything else, and takes 249 at
/// most, which every refusal checked passes.
/// </description></item>
/// <item><description>
/// Quotes and brackets that are closed, and a character after each <c>\</c>, <c>_</c> and <c>*</c>.
/// </description></item>
/// <item><description>
/// In brackets: the eight colors, <c>Color1</c> to <c>Color56</c>, a condition without spaces (<c>[&gt;=100]</c>), a
/// currency or locale (<c>[$€-407]</c>, with at most 8 hex digits), elapsed time (<c>[h]</c>, <c>[mm]</c>), and
/// <c>DBNum1</c> to <c>DBNum3</c>; at most one of each kind in a section. Elapsed time doesn't follow an hour, a minute or
/// a second (Excel refuses <c>h:[mm]</c> and <c>ss[ss]</c>), nor goes with <c>AM/PM</c>. Conditions are in the first two
/// sections, without <c>@</c>.
/// </description></item>
/// <item><description>
/// Letters only as tokens: <c>General</c>, <c>E+</c> and <c>E-</c> after a digit placeholder and before another (Excel
/// refuses <c>e+</c> there, and <c>E</c> without a sign anywhere), and the date and time tokens <c>y</c>, <c>m</c>,
/// <c>d</c>, <c>h</c>, <c>s</c>, <c>e</c>, <c>g</c>, <c>b</c> (in lower case: Excel refuses <c>B</c> alone), <c>aaa</c>
/// and longer, <c>AM/PM</c> and <c>A/P</c>, in any case, and one <c>B1</c> or <c>B2</c> before them. Other letters go in
/// quotes.
/// </description></item>
/// <item><description>
/// A section with a date or time has no <c>#</c>, <c>?</c>, <c>%</c>, <c>@</c>, <c>General</c> or exponent, and its
/// <c>0</c>s are fractions of a second, at most 3 after a <c>.</c> (<c>ss.000</c>), and it has no other digit outside
/// quotes. A <c>.</c> or <c>/</c> comes after its first date or time token. Excel refuses <c>0 h</c>, <c>dd @</c>,
/// <c>ss.0000</c>, <c>d 5</c>, <c>.y</c> and <c>/d</c>.
/// </description></item>
/// <item><description>
/// A section without one has no <c>:</c> (Excel refuses <c>0:00</c>), neither <c>@</c> nor <c>General</c> with digit
/// placeholders, no <c>General</c> twice or with <c>@</c> or a <c>.</c>, one exponent at most, and no <c>,</c> after it.
/// Its <c>/</c> is a fraction's: one, after a digit placeholder and before its denominator (a placeholder or a number,
/// after spaces), in a section without a <c>.</c>, a <c>,</c> or an exponent.
/// </description></item>
/// <item><description>
/// <c>@</c> is in the last section, without <c>.</c> or <c>%</c>; a fourth section, which is text's, has no digit
/// placeholder, date, time, <c>.</c> or <c>%</c>; and the code has at most 30 <c>%</c>.
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

	/// <summary>The most a code counts toward Excel's length (<see cref="Weight"/>).</summary>
	public const int MaxWeight = 249;

	/// <summary>The most sections a code has: positive, negative, zero and text.</summary>
	private const int MaxSections = 4;

	/// <summary>The most digits of a fraction of a second Excel shows.</summary>
	private const int MaxFractionDigits = 3;

	/// <summary>The most sections that have conditions: the first two.</summary>
	private const int MaxConditionSections = 2;

	/// <summary>The most <c>%</c> of a code that the check takes: Excel opened 30 in a section, and refused 40.</summary>
	private const int MaxPercents = 30;

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

		var weight = Weight(code);
		if (weight > MaxWeight)
		{
			return $"The format code \"{code}\" is longer than Excel opens: it counts {weight.ToString(CultureInfo.InvariantCulture)}, 3 for each character outside quotes that shows as itself and 2 for each in quotes, and the check takes {MaxWeight.ToString(CultureInfo.InvariantCulture)}.";
		}

		var sections = 0;
		var percents = 0;
		var i = 0;
		while (true)
		{
			if (++sections > MaxSections)
			{
				return $"The format code \"{code}\" has more than {MaxSections.ToString(CultureInfo.InvariantCulture)} sections, the most Excel opens.";
			}

			var problem = Section(code, ref i, sections, out var section);
			if (problem is not null)
			{
				return $"The format code \"{code}\" {problem}";
			}

			if (sections == MaxSections)
			{
				if (i < code.Length)
				{
					return $"The format code \"{code}\" has more than {MaxSections.ToString(CultureInfo.InvariantCulture)} sections, the most Excel opens.";
				}

				if (section.Placeholders || section.Date || section.Percents > 0 || section.Point)
				{
					var what = section.Placeholders ? "a digit placeholder" : section.Date ? "a date or time" : section.Point ? "." : "%";

					return $"The format code \"{code}\" has {what} in its fourth section, which is text's, and where Excel doesn't open it.";
				}
			}

			percents += section.Percents;
			if (percents > MaxPercents)
			{
				return $"The format code \"{code}\" has more than {MaxPercents.ToString(CultureInfo.InvariantCulture)} %, which Excel doesn't open.";
			}

			if (i >= code.Length)
			{
				return null;
			}

			if (section.At)
			{
				return $"The format code \"{code}\" has @ in a section before its last, which Excel doesn't open: the last section is text's.";
			}

			// The ; that ends the section.
			i++;
		}
	}

	/// <summary>
	/// What a code counts toward the length Excel opens: 3 for each character outside quotes that shows as itself, and each
	/// escape, <c>_</c> or <c>*</c> with its character; 2 for each character in quotes, and each digit placeholder after a
	/// <c>.</c> in its section; 1 for anything else (a placeholder, a <c>.</c>, a quote, a letter, a bracket's characters).
	/// </summary>
	/// <param name="code">The code.</param>
	/// <returns>The count.</returns>
	private static int Weight(string code)
	{
		var weight = 0;
		var afterPoint = false;
		for (var i = 0; i < code.Length; i++)
		{
			var c = code[i];
			switch (c)
			{
				case '"':
					var close = code.IndexOf('"', i + 1);
					var end = close < 0 ? code.Length : close;
					weight += 2 + (2 * (end - i - 1));
					i = end;

					continue;
				case '\\' or '_' or '*':
					weight += 3;
					i++;

					continue;
				case '[':
					var bracket = code.IndexOf(']', i + 1);
					var last = bracket < 0 ? code.Length - 1 : bracket;
					weight += last - i + 1;
					i = last;

					continue;
				case ';':
					afterPoint = false;
					weight++;

					continue;
				case '.':
					afterPoint = true;
					weight++;

					continue;
				case '0' or '#' or '?':
					weight += afterPoint ? 2 : 1;

					continue;
				case ':' or '/':
					weight += 3;

					continue;
			}

			weight += IsLiteral(c) ? 3 : 1;
		}

		return weight;
	}

	/// <summary>Checks a section: its tokens one by one, and then what they mean together.</summary>
	/// <param name="code">The code.</param>
	/// <param name="i">Where the section starts; on return, where it ends: at its <c>;</c> or the code's end.</param>
	/// <param name="number">The section's number, from 1.</param>
	/// <param name="section">What the section has.</param>
	/// <returns>What is wrong, to follow the code in a message, or <see langword="null"/>.</returns>
	private static string? Section(string code, ref int i, int number, out SectionFacts section)
	{
		section = new SectionFacts { Number = number };
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
					section.Point = true;
					section.PointBeforeDate |= !section.Date;
					section.FractionZeros = 0;
					section.Last = Token.Point;

					continue;
				case '%':
					section.Percents++;
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
				case '/':
					problem = Slash(code, i, ref section);
					if (problem is not null)
					{
						return problem;
					}

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

			section.Comma |= c == ',';
			section.CommaAfterExponent |= c == ',' && section.Exponent;
			section.Digit |= c is >= '1' and <= '9';
			section.Last = Token.Other;
		}

		return section.Check();
	}

	/// <summary>
	/// Checks a <c>/</c> outside quotes: in a section with a date or time, after its first token; otherwise a fraction's,
	/// after a digit placeholder and before its denominator, a placeholder or a number after spaces (Excel opened <c>d/</c>,
	/// <c>0/ 8</c> and <c>#/1</c>, and refused <c>/d</c>, <c>0/</c>, <c>#/</c>, <c>0/$</c> and <c>@/</c>).
	/// </summary>
	/// <param name="code">The code.</param>
	/// <param name="i">The <c>/</c>'s position.</param>
	/// <param name="section">What the section has so far.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Slash(string code, int i, ref SectionFacts section)
	{
		section.Slashes++;
		if (section.Date)
		{
			return null;
		}

		if (!section.Placeholders)
		{
			return "has / without a digit placeholder or a date or time token before it, which Excel doesn't open: put it in quotes.";
		}

		var next = i + 1;
		while (next < code.Length && code[next] == ' ')
		{
			next++;
		}

		return next < code.Length && code[next] is '0' or '#' or '?' or (>= '1' and <= '9')
			? null
			: "has / without a denominator after it (a digit placeholder or a number), which Excel doesn't open.";
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
			if (section.General)
			{
				return "has General twice in a section, which Excel doesn't open.";
			}

			section.General = true;
			section.Last = Token.Other;
			i += 6;

			return null;
		}

		// E is an exponent wherever it is: E+ or E- after a digit placeholder (Excel refused E, Ed and E+d).
		if (c == 'E')
		{
			if (i + 1 >= code.Length || code[i + 1] is not ('+' or '-'))
			{
				return section.Placeholders && !section.Date && i + 1 < code.Length && code[i + 1] is '0' or '#' or '?'
					? "has an exponent without its sign, which Excel doesn't open: use E+ or E-."
					: "has E without + or - after it, which Excel opens only as an exponent (E+ or E-): put text in quotes.";
			}

			if (!section.Placeholders)
			{
				return "has an exponent without a digit placeholder before it.";
			}

			if (section.Exponent)
			{
				return "has two exponents in a section, which Excel doesn't open.";
			}

			section.Exponent = true;
			section.ExponentDigits = false;
			section.Last = Token.Other;
			i++;

			return null;
		}

		// e+ or e- in a number, or before a digit placeholder, is an exponent in lower case; otherwise e is the era's year (e-m-d).
		if (c == 'e' && i + 1 < code.Length && code[i + 1] is '+' or '-' &&
			((section.Placeholders && !section.Date) || (i + 2 < code.Length && code[i + 2] is '0' or '#' or '?')))
		{
			return "has an exponent in lower case (e+ or e-), which Excel doesn't open: use E+ or E-.";
		}

		if (c == 'e' && section.Placeholders && !section.Date && i + 1 < code.Length && code[i + 1] is '0' or '#' or '?')
		{
			return "has an exponent without its sign, which Excel doesn't open: use E+ or E-.";
		}

		// B1 and B2 are a calendar, once, before the date's tokens; b alone is the Buddhist year, in lower case.
		if (c == 'B')
		{
			if (i + 1 >= code.Length || code[i + 1] is not ('1' or '2'))
			{
				return "has B outside quotes, which Excel opens only as B1 or B2 (a calendar) and as b in lower case (the Buddhist year): put text in quotes.";
			}

			if (section.Calendar)
			{
				return "has B1 or B2 twice, which Excel doesn't open.";
			}

			if (section.Date)
			{
				return "has B1 or B2 after a date or time token, which Excel doesn't open: put it first.";
			}

			section.Calendar = true;
			section.Date = true;
			section.Last = Token.Other;
			i++;

			return null;
		}

		if (lower == 'a' && Matches(code, i, "am/pm"))
		{
			section.AmPm = true;
			section.Date = true;
			section.Last = Token.Other;
			i += 4;

			return null;
		}

		if (lower == 'a' && Matches(code, i, "a/p"))
		{
			section.AmPm = true;
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
			section.Time |= lower is 'h' or 'm' or 's';
			section.Last = Token.Other;

			return null;
		}

		return $"has the letter {c} outside quotes, which Excel opens only as a date or time token: put text in quotes.";
	}

	/// <summary>Checks a bracket's content: what it is, and that the section has no other of its kind.</summary>
	/// <param name="content">The text between the brackets.</param>
	/// <param name="section">What the section has so far: elapsed time makes it a time.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Bracket(string content, ref SectionFacts section)
	{
		if (IsElapsed(content))
		{
			if (section.Elapsed)
			{
				return $"has [{content}], a second elapsed time in a section, which Excel doesn't open.";
			}

			if (section.Time)
			{
				return $"has [{content}] after an hour, a minute or a second, which Excel doesn't open: put elapsed time first.";
			}

			section.Elapsed = true;
			section.Date = true;

			return null;
		}

		if (IsColor(content))
		{
			return Once(ref section.Color, content, "color");
		}

		if (IsCondition(content))
		{
			if (section.Number > MaxConditionSections)
			{
				return $"has [{content}], a condition in its third or fourth section, which Excel doesn't open.";
			}

			return Once(ref section.Condition, content, "condition");
		}

		if (IsLocale(content))
		{
			return Once(ref section.Locale, content, "currency or locale");
		}

		if (content is "DBNum1" or "DBNum2" or "DBNum3")
		{
			return Once(ref section.DBNum, content, "DBNum");
		}

		return $"has [{content}], which isn't a color, a condition, a currency or locale, or elapsed time.";
	}

	/// <summary>Takes a bracket of a kind a section has at most one of.</summary>
	/// <param name="seen">Whether the section has one already; set on return.</param>
	/// <param name="content">The bracket's content.</param>
	/// <param name="kind">The kind, for the message.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Once(ref bool seen, string content, string kind)
	{
		if (seen)
		{
			return $"has [{content}], a second {kind} in a section, which Excel doesn't open.";
		}

		seen = true;

		return null;
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
			return c is ' ' or '$' or '-' or '+' or '(' or ')' or '!' or '^' or '&' or '\'' or '`' or '~' or '{' or '}' or '<' or '>' or '=' or '|' or ',' ||
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
		/// <summary>The section's number, from 1.</summary>
		public int Number;

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

		/// <summary>Whether it has a date or time token, or elapsed time, or a calendar.</summary>
		public bool Date;

		/// <summary>Whether it has an hour, a minute or a second (an <c>h</c>, <c>m</c> or <c>s</c> token), which elapsed time can't follow.</summary>
		public bool Time;

		/// <summary>Whether it has <c>B1</c> or <c>B2</c>.</summary>
		public bool Calendar;

		/// <summary>Whether it has elapsed time.</summary>
		public bool Elapsed;

		/// <summary>Whether it has a color.</summary>
		public bool Color;

		/// <summary>Whether it has a condition.</summary>
		public bool Condition;

		/// <summary>Whether it has a currency or locale.</summary>
		public bool Locale;

		/// <summary>Whether it has <c>DBNum</c>.</summary>
		public bool DBNum;

		/// <summary>The <c>%</c> it has.</summary>
		public int Percents;

		/// <summary>Whether it has <c>@</c>.</summary>
		public bool At;

		/// <summary>Whether it has <c>General</c>.</summary>
		public bool General;

		/// <summary>Whether it has <c>:</c>.</summary>
		public bool Colon;

		/// <summary>Whether it has a <c>.</c>.</summary>
		public bool Point;

		/// <summary>Whether a <c>.</c> comes before its first date or time token.</summary>
		public bool PointBeforeDate;

		/// <summary>The <c>/</c> it has.</summary>
		public int Slashes;

		/// <summary>Whether it has a <c>,</c> outside quotes.</summary>
		public bool Comma;

		/// <summary>Whether it has a <c>,</c> after its exponent.</summary>
		public bool CommaAfterExponent;

		/// <summary>Whether it has a digit from 1 to 9 outside quotes, which shows as itself.</summary>
		public bool Digit;

		/// <summary>Whether it has <c>AM/PM</c> or <c>A/P</c>.</summary>
		public bool AmPm;

		/// <summary>Whether it has an exponent.</summary>
		public bool Exponent;

		/// <summary>Whether a digit placeholder follows the exponent.</summary>
		public bool ExponentDigits;

		/// <summary>Checks what the section's tokens mean together.</summary>
		/// <returns>What is wrong, or <see langword="null"/>.</returns>
		public readonly string? Check()
		{
			if (Condition && At)
			{
				return "has a condition in a section with @, which Excel doesn't open.";
			}

			if (General && (At || Point))
			{
				return $"has General with {(At ? "@" : ".")} in its section, which Excel doesn't open.";
			}

			if (At && (Point || Percents > 0))
			{
				return $"has {(Point ? "." : "%")} in a section with @, which Excel doesn't open.";
			}

			if (Elapsed && AmPm)
			{
				return "has elapsed time with AM/PM or A/P, which Excel doesn't open.";
			}

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

				if (Digit)
				{
					return "has a digit outside quotes in a section with a date or time, which Excel doesn't open: put it in quotes.";
				}

				if (PointBeforeDate)
				{
					return "has . before its first date or time token, which Excel doesn't open: put it in quotes.";
				}

				var other = Percents > 0 ? "%" : At ? "@" : General ? "General" : Exponent ? "an exponent" : null;

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

			if (CommaAfterExponent)
			{
				return "has a , after its exponent, which Excel doesn't open.";
			}

			if (Placeholders && (At || General))
			{
				return $"has {(At ? "@" : "General")} in a section with digit placeholders, which Excel doesn't open.";
			}

			if (Slashes > 1)
			{
				return "has more than one / in a fraction, which Excel doesn't open.";
			}

			if (Slashes == 1 && (Point || Exponent || Comma))
			{
				return $"has a fraction with {(Point ? "a decimal point" : Exponent ? "an exponent" : "a ,")}, which Excel doesn't open.";
			}

			return null;
		}
	}
}
