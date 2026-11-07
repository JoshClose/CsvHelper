using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// Translates a .NET date and time format, in a culture, into an Excel number format code that shows the same text, so
/// that a date written with its member's <see cref="CsvFormatAttribute"/> is a date cell rather than text.
/// </summary>
/// <remarks>
/// <para>
/// The standard formats are the culture's patterns (<c>d</c> its short date, <c>g</c> its short date and short time),
/// and <c>R</c>, <c>s</c> and <c>u</c> the invariant culture's; <c>O</c> is .NET's default text, which the sink reads
/// without a format, and <c>U</c> depends on the machine's time zone. Then the custom tokens become Excel's:
/// </para>
/// <list type="bullet">
/// <item><description><c>d</c> to <c>dddd</c>, and <c>M</c> to <c>MMMM</c> as <c>m</c> to <c>mmmm</c>.</description></item>
/// <item><description><c>yy</c> and <c>yyyy</c>.</description></item>
/// <item><description>
/// <c>H</c> and <c>HH</c> as <c>h</c> and <c>hh</c>; <c>h</c> and <c>hh</c> with <c>tt</c> (<c>AM/PM</c>) or <c>t</c>
/// (<c>A/P</c>), when the culture's designators are <c>AM</c> and <c>PM</c>. Under a locale, Excel shows <c>A/P</c> as
/// the whole designator, so <c>t</c> has none, and isn't translated beside names or <c>tt</c>.
/// </description></item>
/// <item><description>
/// <c>m</c>, <c>mm</c>, <c>s</c> and <c>ss</c>; <c>f</c> to <c>fff</c> after <c>s.</c> or <c>ss.</c>, as <c>.0</c> to
/// <c>.000</c>.
/// </description></item>
/// <item><description>
/// Everything else as quoted text, the culture's date and time separators (<c>/</c> and <c>:</c>) included, so that Excel
/// shows them whatever its own locale.
/// </description></item>
/// <item><description>
/// Names of days and months, and <c>AM/PM</c>, under the culture's locale (<c>[$-407]</c>), which Excel shows them in:
/// <c>[$-409]</c> for the invariant culture, and for <c>AM/PM</c> without names.
/// </description></item>
/// </list>
/// <para>
/// Not translated, and so written as text: eras (<c>g</c>), offsets (<c>z</c>, <c>K</c>), <c>F</c>, four or more
/// fraction digits (Excel refuses them), <c>y</c>, <c>yyy</c> and five or more, a 12-hour hour without <c>tt</c> or a
/// 24-hour one with it, other designators, a month where Excel would read minutes (next to an hour or a second) and minutes
/// where it would read a month, a date without a year (<c>dd MMMM</c> reads back in the current year), month names that
/// .NET would write in the genitive (Excel shows the nominative), names in a culture without a locale id, and calendars
/// other than the Gregorian.
/// </para>
/// </remarks>
internal static class FormatTranslator
{
	/// <summary>The locale of <c>AM/PM</c> without names, and of the invariant culture's English names.</summary>
	private const int English = 0x409;

	/// <summary>
	/// Translates a format, when Excel can show its text.
	/// </summary>
	/// <param name="format">The .NET format, standard or custom, which isn't .NET's default (<c>O</c>).</param>
	/// <param name="kind">The kind of the values: <see cref="CellKind.DateTime"/>, <see cref="CellKind.DateTimeOffset"/>, <see cref="CellKind.DateOnly"/> or <see cref="CellKind.TimeOnly"/>.</param>
	/// <param name="provider">The provider the values are formatted with.</param>
	/// <returns>The translation, or <see langword="null"/> when Excel can't show the format's text.</returns>
	public static DateFormat? Translate(string format, CellKind kind, IFormatProvider provider)
	{
		var culture = provider as CultureInfo;
		var info = DateTimeFormatInfo.GetInstance(provider);
		var pattern = Expand(format, kind, ref info, ref culture);
		if (pattern is null || info.Calendar is not GregorianCalendar)
		{
			return null;
		}

		var tokens = Tokenize(pattern, info);
		if (tokens is null || Code(tokens, info, culture, out var hasDate) is not { } code || FormatCheck.Problem(code) is not null)
		{
			return null;
		}

		// A pattern of one character would be a standard format.
		return new DateFormat(code, pattern.Length == 1 ? "%" + pattern : pattern, info, hasDate);
	}

	/// <summary>The custom pattern of a format: a standard format's, for the kind of values, or the format itself.</summary>
	/// <param name="format">The format.</param>
	/// <param name="kind">The kind of the values.</param>
	/// <param name="info">The culture's formats; the invariant culture's for <c>R</c>, <c>s</c> and <c>u</c>.</param>
	/// <param name="culture">The culture, for the locale of names; the invariant culture for <c>R</c>, <c>s</c> and <c>u</c>.</param>
	/// <returns>The pattern, or <see langword="null"/> for a standard format that isn't translated.</returns>
	private static string? Expand(string format, CellKind kind, ref DateTimeFormatInfo info, ref CultureInfo? culture)
	{
		if (format.Length != 1)
		{
			return format;
		}

		var c = format[0];
		if (c is 'r' or 'R' or 's' or 'u')
		{
			info = DateTimeFormatInfo.InvariantInfo;
			culture = CultureInfo.InvariantCulture;
		}

		return kind switch
		{
			CellKind.DateOnly => c switch
			{
				'd' => info.ShortDatePattern,
				'D' => info.LongDatePattern,
				'm' or 'M' => info.MonthDayPattern,
				'y' or 'Y' => info.YearMonthPattern,
				'r' or 'R' => "ddd, dd MMM yyyy",
				_ => null,
			},
			CellKind.TimeOnly => c switch
			{
				't' => info.ShortTimePattern,
				'T' => info.LongTimePattern,
				'r' or 'R' => "HH:mm:ss",
				_ => null,
			},
			_ => c switch
			{
				'd' => info.ShortDatePattern,
				'D' => info.LongDatePattern,
				'f' => info.LongDatePattern + " " + info.ShortTimePattern,
				'F' => info.FullDateTimePattern,
				'g' => info.ShortDatePattern + " " + info.ShortTimePattern,
				'G' => info.ShortDatePattern + " " + info.LongTimePattern,
				'm' or 'M' => info.MonthDayPattern,
				't' => info.ShortTimePattern,
				'T' => info.LongTimePattern,
				'y' or 'Y' => info.YearMonthPattern,
				'r' or 'R' => info.RFC1123Pattern,
				's' => info.SortableDateTimePattern,
				'u' => info.UniversalSortableDateTimePattern,
				_ => null,
			},
		};
	}

	/// <summary>
	/// Splits a custom pattern into its tokens as .NET formats it: runs of a token letter, <c>%</c> before a letter on its
	/// own, quoted text (with <c>\</c> escapes inside), <c>\</c> before a character, the separators, and other characters.
	/// </summary>
	/// <param name="pattern">The pattern.</param>
	/// <param name="info">The culture's formats, for the separators.</param>
	/// <returns>The tokens, or <see langword="null"/> for a token that isn't translated or a pattern .NET can't format.</returns>
	private static List<Token>? Tokenize(string pattern, DateTimeFormatInfo info)
	{
		var tokens = new List<Token>();
		var i = 0;
		while (i < pattern.Length)
		{
			var c = pattern[i];
			var single = false;
			if (c == '%')
			{
				// The next character on its own: %d is a day.
				if (i + 1 >= pattern.Length || pattern[i + 1] == '%')
				{
					return null;
				}

				c = pattern[++i];
				single = true;
			}

			var count = single ? 1 : Repeat(pattern, i);
			switch (c)
			{
				case 'd':
					tokens.Add(count <= 2 ? new Token(Part.Day, count) : new Token(Part.DayName, Math.Min(count, 4)));

					break;
				case 'M':
					tokens.Add(count <= 2 ? new Token(Part.Month, count) : new Token(Part.MonthName, Math.Min(count, 4)));

					break;
				case 'y':
					tokens.Add(new Token(Part.Year, count));

					break;
				case 'h':
					tokens.Add(new Token(Part.Hour12, count));

					break;
				case 'H':
					tokens.Add(new Token(Part.Hour24, count));

					break;
				case 'm':
					tokens.Add(new Token(Part.Minute, count));

					break;
				case 's':
					tokens.Add(new Token(Part.Second, count));

					break;
				case 'f':
					tokens.Add(new Token(Part.Fraction, count));

					break;
				case 't':
					tokens.Add(new Token(Part.AmPm, count));

					break;
				case 'g' or 'F' or 'z' or 'K':
					return null;
				case ':':
					tokens.Add(Token.Literal(info.TimeSeparator));
					count = 1;

					break;
				case '/':
					tokens.Add(Token.Literal(info.DateSeparator));
					count = 1;

					break;
				case '\'' or '"':
					var text = new StringBuilder();
					var j = i + 1;
					while (j < pattern.Length && pattern[j] != c)
					{
						if (pattern[j] == '\\' && ++j >= pattern.Length)
						{
							return null;
						}

						text.Append(pattern[j++]);
					}

					if (j >= pattern.Length)
					{
						return null;
					}

					tokens.Add(Token.Literal(text.ToString()));
					count = j - i + 1;

					break;
				case '\\':
					if (i + 1 >= pattern.Length)
					{
						return null;
					}

					tokens.Add(Token.Literal(pattern[i + 1].ToString()));
					count = 2;

					break;
				default:
					tokens.Add(Token.Literal(c.ToString()));
					count = 1;

					break;
			}

			i += count;
		}

		return tokens;
	}

	/// <summary>The Excel code of a pattern's tokens, when Excel shows them as .NET does.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="info">The culture's formats: designators and names.</param>
	/// <param name="culture">The culture, for the locale of names, or <see langword="null"/> when the provider isn't one.</param>
	/// <param name="hasDate">Whether the tokens have a date.</param>
	/// <returns>The code, or <see langword="null"/>.</returns>
	private static string? Code(List<Token> tokens, DateTimeFormatInfo info, CultureInfo? culture, out bool hasDate)
	{
		hasDate = false;
		bool hasYear = false, hasDay = false, names = false, hour12 = false, hour24 = false, amPm = false, initial = false, values = false;
		foreach (var token in tokens)
		{
			hasDate |= token.Part is Part.Day or Part.DayName or Part.Month or Part.MonthName or Part.Year;
			hasYear |= token.Part == Part.Year;
			hasDay |= token.Part == Part.Day;
			names |= token.Part is Part.DayName or Part.MonthName;
			hour12 |= token.Part == Part.Hour12;
			hour24 |= token.Part == Part.Hour24;
			amPm |= token.Part == Part.AmPm && token.Count != 1;
			initial |= token.Part == Part.AmPm && token.Count == 1;
			values |= token.Part != Part.Literal;
		}

		// A date without a year reads back in the current year; Excel's h is 12-hour with AM/PM and 24-hour without; and
		// under a locale, Excel shows A/P as the whole designator (checked through COM), so t has none.
		if (!values || (hasDate && !hasYear) || (hour12 && (hour24 || !(amPm || initial))) || ((amPm || initial) && hour24) ||
			((amPm || initial) && (info.AMDesignator != "AM" || info.PMDesignator != "PM")) || (initial && (names || amPm)))
		{
			return null;
		}

		var code = new StringBuilder();
		if (names || amPm)
		{
			if (Locale(names, hasDay, tokens, info, culture) is not { } locale)
			{
				return null;
			}

			code.Append("[$-").Append(locale.ToString("X", CultureInfo.InvariantCulture)).Append(']');
		}

		var literal = new StringBuilder();
		for (var k = 0; k < tokens.Count; k++)
		{
			var token = tokens[k];
			if (token.Part == Part.Literal)
			{
				// The . before a fraction of a second is the fraction's own.
				if (token.Text != "." || k + 1 >= tokens.Count || tokens[k + 1].Part != Part.Fraction)
				{
					literal.Append(token.Text);
				}

				continue;
			}

			if (TokenCode(tokens, k) is not { } part)
			{
				return null;
			}

			AppendLiteral(code, literal);
			// Excel reads two tokens of a letter side by side as one (%H%H as hh).
			if (code.Length > 0 && char.ToLowerInvariant(code[code.Length - 1]) == char.ToLowerInvariant(part[0]))
			{
				return null;
			}

			code.Append(part);
		}

		AppendLiteral(code, literal);

		return code.ToString();
	}

	/// <summary>The Excel code of a token that isn't text.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="k">The token's index.</param>
	/// <returns>Its code, or <see langword="null"/> when Excel can't show it as .NET does.</returns>
	private static string? TokenCode(List<Token> tokens, int k)
	{
		var token = tokens[k];
		var count = token.Count;
		// Excel reads m and mm as minutes right after an hour or right before a second, and as a month otherwise.
		var minutes = Neighbor(tokens, k, -1) is Part.Hour12 or Part.Hour24 || Neighbor(tokens, k, 1) == Part.Second;

		return token.Part switch
		{
			Part.Day => count == 1 ? "d" : "dd",
			Part.DayName => count == 3 ? "ddd" : "dddd",
			Part.Month when !minutes => count == 1 ? "m" : "mm",
			Part.MonthName => count == 3 ? "mmm" : "mmmm",
			Part.Year when count is 2 or 4 => count == 2 ? "yy" : "yyyy",
			Part.Hour12 or Part.Hour24 when count <= 2 => count == 1 ? "h" : "hh",
			Part.Minute when minutes && count <= 2 => count == 1 ? "m" : "mm",
			Part.Second when count <= 2 => count == 1 ? "s" : "ss",
			// After s. or ss., with the . the fraction's own.
			Part.Fraction when count <= 3 && k >= 2 && tokens[k - 1] is { Part: Part.Literal, Text: "." } && tokens[k - 2].Part == Part.Second =>
				"." + new string('0', count),
			Part.AmPm when count <= 2 => count == 1 ? "A/P" : "AM/PM",
			_ => null,
		};
	}

	/// <summary>
	/// The locale id that makes Excel show the names and designators as .NET does: the culture's, or English for the
	/// invariant culture and for designators without names.
	/// </summary>
	/// <param name="names">Whether the tokens have names of days or months.</param>
	/// <param name="hasDay">Whether they have a day of the month, with which .NET writes month names in the genitive.</param>
	/// <param name="tokens">The tokens.</param>
	/// <param name="info">The culture's formats.</param>
	/// <param name="culture">The culture, or <see langword="null"/>.</param>
	/// <returns>The id, or <see langword="null"/> when Excel can't show the names.</returns>
	private static int? Locale(bool names, bool hasDay, List<Token> tokens, DateTimeFormatInfo info, CultureInfo? culture)
	{
		if (!names)
		{
			return English;
		}

		// .NET writes month names in the genitive with a day of the month, where they differ; Excel never does.
		foreach (var token in tokens)
		{
			if (hasDay && token.Part == Part.MonthName &&
				(token.Count == 3 ? !Same(info.AbbreviatedMonthGenitiveNames, info.AbbreviatedMonthNames) : !Same(info.MonthGenitiveNames, info.MonthNames)))
			{
				return null;
			}
		}

		var id = culture?.LCID ?? 0;
		if (id == CultureInfo.InvariantCulture.LCID)
		{
			return English;
		}

		// 0x1000, 0xC00 and 0x1400 are the ids of cultures Windows doesn't know, which Excel shows in English.
		if (id is 0 or 0x1000 or 0xC00 or 0x1400)
		{
			var invariant = DateTimeFormatInfo.InvariantInfo;

			return Same(info.DayNames, invariant.DayNames) && Same(info.AbbreviatedDayNames, invariant.AbbreviatedDayNames) &&
				Same(info.MonthNames, invariant.MonthNames) && Same(info.AbbreviatedMonthNames, invariant.AbbreviatedMonthNames)
				? English
				: null;
		}

		return id;
	}

	/// <summary>Writes the text pending before a token as Excel's quoted text, with each <c>"</c> as <c>\"</c>, and clears it.</summary>
	/// <param name="code">The code.</param>
	/// <param name="literal">The text.</param>
	private static void AppendLiteral(StringBuilder code, StringBuilder literal)
	{
		var open = false;
		for (var i = 0; i < literal.Length; i++)
		{
			var c = literal[i];
			if (c == '"')
			{
				if (open)
				{
					code.Append('"');
					open = false;
				}

				code.Append("\\\"");

				continue;
			}

			if (!open)
			{
				code.Append('"');
				open = true;
			}

			code.Append(c);
		}

		if (open)
		{
			code.Append('"');
		}

		literal.Clear();
	}

	/// <summary>The part of the nearest token that isn't text, before or after a token.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="k">The token's index.</param>
	/// <param name="step">-1 to look before it, 1 after it.</param>
	/// <returns>The part, or <see cref="Part.Literal"/> when there is none.</returns>
	private static Part Neighbor(List<Token> tokens, int k, int step)
	{
		for (var n = k + step; n >= 0 && n < tokens.Count; n += step)
		{
			if (tokens[n].Part != Part.Literal)
			{
				return tokens[n].Part;
			}
		}

		return Part.Literal;
	}

	/// <summary>The number of times the character at a position repeats from there.</summary>
	/// <param name="pattern">The pattern.</param>
	/// <param name="i">The position.</param>
	/// <returns>The run's length, at least 1.</returns>
	private static int Repeat(string pattern, int i)
	{
		var n = 1;
		while (i + n < pattern.Length && pattern[i + n] == pattern[i])
		{
			n++;
		}

		return n;
	}

	/// <summary>Whether two lists of names are the same, ordinally.</summary>
	/// <param name="first">The first.</param>
	/// <param name="second">The second.</param>
	/// <returns>Whether they are.</returns>
	private static bool Same(string[] first, string[] second)
	{
		if (first.Length != second.Length)
		{
			return false;
		}

		for (var i = 0; i < first.Length; i++)
		{
			if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>What a token of a pattern is.</summary>
	private enum Part : byte
	{
		/// <summary>Text, shown as it is.</summary>
		Literal,

		/// <summary><c>d</c> or <c>dd</c>.</summary>
		Day,

		/// <summary><c>ddd</c> (abbreviated) or <c>dddd</c> (full).</summary>
		DayName,

		/// <summary><c>M</c> or <c>MM</c>.</summary>
		Month,

		/// <summary><c>MMM</c> (abbreviated) or <c>MMMM</c> (full).</summary>
		MonthName,

		/// <summary><c>y</c> and longer.</summary>
		Year,

		/// <summary><c>h</c> and longer: the 12-hour clock.</summary>
		Hour12,

		/// <summary><c>H</c> and longer: the 24-hour clock.</summary>
		Hour24,

		/// <summary><c>m</c> and longer.</summary>
		Minute,

		/// <summary><c>s</c> and longer.</summary>
		Second,

		/// <summary><c>f</c> and longer: digits of a fraction of a second.</summary>
		Fraction,

		/// <summary><c>t</c> (the designator's first character) or <c>tt</c>.</summary>
		AmPm,
	}

	/// <summary>A token of a pattern: its part and its letters' count, or its text.</summary>
	/// <param name="Part">What it is.</param>
	/// <param name="Count">How many times its letter repeats: 3 and 4 for abbreviated and full names.</param>
	/// <param name="Text">The text of a <see cref="Part.Literal"/>.</param>
	private readonly record struct Token(Part Part, int Count, string? Text = null)
	{
		/// <summary>A token of text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The token.</returns>
		public static Token Literal(string text) => new(Part.Literal, 0, text);
	}
}
