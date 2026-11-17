using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// A number format code compiled once (Phase 8): its sections, which numbers, text and booleans take, each with its
/// condition, color and locale, and its tokens, with the facts the renderer needs: placeholder counts, grouping, scaling,
/// percents, exponent, fraction, and the date and time parts. Literal text is kept as UTF-8, so that rendering copies
/// bytes.
/// </summary>
/// <remarks>
/// <para>
/// The code reads as Excel 365 read it (checked on 2026-11-16 through COM, <c>Csv\_bench\results\phase8\design</c> and
/// <c>step2</c>, and in Phase 5): quoted text, <c>\x</c> and characters outside quotes that aren't tokens show as
/// themselves, letters too (<c>h:mm a</c> shows <c>0:00 a</c>, <c>A/P/X</c> <c>A/X</c>); <c>_x</c> is one space and
/// <c>*x</c> nothing, whatever <c>x</c> is; colors show nothing (<c>[Color 10]</c> is <c>[Color10]</c>); <c>[$-en-US]</c>
/// is <c>[$-409]</c>, and <c>[$-x-sysdate]</c> and <c>[$-x-systime]</c> are <c>[$-F800]</c> and <c>[$-F400]</c>.
/// </para>
/// <para>
/// Sections are positive, negative, zero and text, as MS-OI29500 has them: the fourth is text's, and so is a last section
/// with <c>@</c>; a code of one section with <c>@</c> shows numbers in General. One number section with a condition is
/// followed by General (<c>[&gt;5]0</c> is <c>[&gt;5]0;General</c>).
/// </para>
/// <para>
/// A code the renderer can't read (Phase 8's decision 5) compiles to one that isn't <see cref="IsReadable"/>: one Excel
/// wouldn't open as written (a quote or bracket left open, <c>\</c>, <c>_</c> or <c>*</c> at its end, more than four
/// sections, a bracket that is no color, condition, locale, elapsed time or <c>DBNum</c>), and the forms Excel refused,
/// where the renderer would have to guess what they show: an exponent in lower case (<c>0.00e+00</c>), <c>:</c> in a
/// number (<c>0:00</c>), <c>General</c> with digit placeholders, <c>.</c> or <c>@</c> (<c>0.00 General</c>), colors
/// past <c>Color1</c> to <c>Color56</c>, a condition in the third or the text section (<c>0;0;[=0]"z"</c>), and an
/// exponent without placeholders after it (<c>0E+</c>). A
/// fraction's denominator over 32,768 (<c>?/50000</c>) can't be read either: Excel takes it, and shows fractions that
/// aren't the value (0.5 as <c>1/5</c>; step 3's probes).
/// </para>
/// <para>
/// A comma right before the point scales, as one after the last placeholder does (Excel writes <c>0,.0</c> as
/// <c>0.0,</c>), and one among the decimal placeholders does nothing (<c>0.0,0</c> is <c>0.00</c>).
/// </para>
/// </remarks>
internal sealed class FormatCode
{
	/// <summary>The most sections a code has: positive, negative, zero and text.</summary>
	public const int MaxSections = 4;

	/// <summary>The largest fixed denominator Excel shows fractions over as they are (step 3's probes: 32,769 and above aren't).</summary>
	public const int MaxDenominator = 32768;

	/// <summary>How many sections can have a condition: the first two (Excel refuses one in the third).</summary>
	private const int ConditionalSections = 2;

	/// <summary>The colors Excel knows by name.</summary>
	private static readonly string[] ColorNames = ["black", "blue", "cyan", "green", "magenta", "red", "white", "yellow"];

	private readonly byte[] literals;

	/// <summary>Creates a compiled code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="numbers">The sections numbers take, in order.</param>
	/// <param name="text">The text section, or <see langword="null"/>.</param>
	/// <param name="literals">The literal text of every section, in UTF-8.</param>
	/// <param name="readable">Whether the renderer can read it.</param>
	private FormatCode(string code, FormatSection[] numbers, FormatSection? text, byte[] literals, bool readable)
	{
		Code = code;
		Numbers = numbers;
		Text = text;
		this.literals = literals;
		IsReadable = readable;
		foreach (var section in numbers)
		{
			IsConditional |= section.Condition != ConditionOperator.None;
			HasDate |= section.Kind == FormatSectionKind.Date || section.System != SystemFormat.None;
		}
	}

	/// <summary>General, compiled.</summary>
	public static FormatCode General { get; } = Compile(BuiltInFormats.General);

	/// <summary>The code.</summary>
	public string Code { get; }

	/// <summary>Whether the renderer can read the code; when it can't, a cell shows its raw text (Phase 8's decision 5).</summary>
	public bool IsReadable { get; }

	/// <summary>
	/// The sections numbers take, in order: positive, negative and zero, or the order their conditions are tried in. Empty
	/// when numbers show in General: a code of one text section, and one the renderer can't read.
	/// </summary>
	public FormatSection[] Numbers { get; }

	/// <summary>The text section, which text and booleans show through, or <see langword="null"/>.</summary>
	public FormatSection? Text { get; }

	/// <summary>Whether a number section has a condition, which chooses sections by value rather than by sign.</summary>
	public bool IsConditional { get; }

	/// <summary>Whether a number section shows a date, a time or elapsed time.</summary>
	public bool HasDate { get; }

	/// <summary>Compiles a code.</summary>
	/// <param name="code">The code, as a style holds it.</param>
	/// <returns>The compiled code; one that isn't <see cref="IsReadable"/> when the renderer can't read it.</returns>
	public static FormatCode Compile(string code)
	{
		var raw = Read(code);
		if (raw is null)
		{
			return new FormatCode(code, [], null, [], false);
		}

		// The fourth section is text's, and so is a last section with @. Excel refuses a condition past the second section,
		// and in the text section.
		var count = raw.Count;
		var textIndex = count == MaxSections || raw[count - 1].Ats > 0 ? count - 1 : -1;
		for (var i = 0; i < count; i++)
		{
			if (raw[i].Condition != ConditionOperator.None && (i >= ConditionalSections || i == textIndex))
			{
				return new FormatCode(code, [], null, [], false);
			}
		}

		var pool = new List<byte>();
		var numbers = new List<FormatSection>(count);
		FormatSection? text = null;
		for (var i = 0; i < count; i++)
		{
			var section = i == textIndex ? Finish(raw[i], FormatSectionKind.Text, pool) : Finish(raw[i], raw[i].IsDate ? FormatSectionKind.Date : FormatSectionKind.Number, pool);
			if (section is null)
			{
				return new FormatCode(code, [], null, [], false);
			}

			if (i == textIndex)
			{
				text = section;
			}
			else
			{
				numbers.Add(section);
			}
		}

		// One number section with a condition is followed by General.
		if (numbers.Count == 1 && numbers[0].Condition != ConditionOperator.None)
		{
			numbers.Add(Finish(new RawSection { HasGeneral = true, Tokens = { new Raw(FormatTokenKind.General) } }, FormatSectionKind.Number, pool)!);
		}

		return new FormatCode(code, [.. numbers], text, [.. pool], true);
	}

	/// <summary>A literal token's text, <c>A/P</c>'s two letters, or a fixed denominator's digits.</summary>
	/// <param name="token">The token.</param>
	/// <returns>Its UTF-8.</returns>
	public ReadOnlySpan<byte> Literal(FormatToken token) => literals.AsSpan(token.Start, token.Length);

	/// <summary>Reads a code into its sections' raw tokens and brackets.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The sections, one to four; <see langword="null"/> when the code can't be read.</returns>
	private static List<RawSection>? Read(string code)
	{
		var sections = new List<RawSection>();
		var section = new RawSection();
		for (var i = 0; i < code.Length; i++)
		{
			var c = code[i];
			switch (c)
			{
				case ';':
					sections.Add(section);
					if (sections.Count == MaxSections)
					{
						return null;
					}

					section = new RawSection();

					continue;
				case '"':
					var close = code.IndexOf('"', i + 1);
					if (close < 0)
					{
						return null;
					}

					section.Add(Raw.Shown(code.Substring(i + 1, close - i - 1)));
					i = close;

					continue;
				case '\\' or '_' or '*':
					if (i + 1 >= code.Length)
					{
						return null;
					}

					var width = CharWidth(code, i + 1);
					if (c == '\\')
					{
						section.Add(Raw.Shown(code.Substring(i + 1, width)));
					}
					else if (c == '_')
					{
						section.Add(Raw.Shown(" "));
					}

					i += width;

					continue;
				case '[':
					var end = code.IndexOf(']', i + 1);
					if (end < 0 || !section.Bracket(code.Substring(i + 1, end - i - 1)))
					{
						return null;
					}

					i = end;

					continue;
				case '0' or '#' or '?':
					section.Add(new Raw(FormatTokenKind.Digit) { Char = c });

					continue;
				case '.' or ',' or '%' or '/' or ':':
					section.Add(new Raw(FormatTokenKind.Literal) { Text = c.ToString(), Char = c, Bare = true });

					continue;
				case '@':
					section.Add(new Raw(FormatTokenKind.At));

					continue;
			}

			if ((uint)((c | 0x20) - 'a') <= 'z' - 'a')
			{
				var read = section.Letters(code, i);
				if (read < 0)
				{
					return null;
				}

				i = read;

				continue;
			}

			// Digits 1 to 9 and any other character show as themselves; a surrogate pair stays whole.
			var length = CharWidth(code, i);
			section.Add(new Raw(FormatTokenKind.Literal) { Text = code.Substring(i, length), Char = c, Bare = true });
			i += length - 1;
		}

		sections.Add(section);

		return sections;
	}

	/// <summary>How many chars a character at a position takes: 2 for a surrogate pair.</summary>
	/// <param name="code">The code.</param>
	/// <param name="i">The position.</param>
	/// <returns>1 or 2.</returns>
	private static int CharWidth(string code, int i) => char.IsHighSurrogate(code[i]) && i + 1 < code.Length && char.IsLowSurrogate(code[i + 1]) ? 2 : 1;

	/// <summary>Works out what a section's tokens make of it, and writes its literals to the pool.</summary>
	/// <param name="raw">The section as read.</param>
	/// <param name="kind">What it shows: <see cref="FormatSectionKind.Number"/> becomes a fraction when it has one.</param>
	/// <param name="pool">The code's literal text.</param>
	/// <returns>The section; <see langword="null"/> when it has a form the renderer can't read.</returns>
	private static FormatSection? Finish(RawSection raw, FormatSectionKind kind, List<byte> pool)
	{
		var tokens = raw.Tokens;
		var section = new FormatSection
		{
			Kind = kind,
			Condition = raw.Condition,
			Operand = raw.Operand,
			HasColor = raw.HasColor,
			HasDBNum = raw.HasDBNum,
			HasCalendar = raw.HasCalendar,
			HasLocale = raw.HasLocale,
			Names = raw.Names,
			System = raw.System,
			HasGeneral = raw.HasGeneral,
			Ats = raw.Ats,
		};
		switch (kind)
		{
			case FormatSectionKind.Date:
				FinishDate(tokens, section);

				break;
			case FormatSectionKind.Text:
				FinishText(tokens);

				break;
			default:
				// Excel refuses : in a number, and General beside placeholders, a point or @.
				var placeholders = tokens.Exists(t => t.Kind == FormatTokenKind.Digit);
				var point = tokens.Exists(t => t.Bare && t.Char == '.');
				if (tokens.Exists(t => t.Bare && t.Char == ':') || (raw.HasGeneral && (placeholders || point || raw.Ats > 0)))
				{
					return null;
				}

				if (FinishFraction(tokens, section))
				{
					section.Kind = FormatSectionKind.Fraction;
					if (section.Denominator > MaxDenominator)
					{
						return null;
					}
				}
				else
				{
					FinishNumber(tokens, section);
					if (section.HasExponent && section.ExponentPlaceholders == 0)
					{
						// Excel refuses an exponent without placeholders after it (0E+, 0.0E+; step 3's probes).
						return null;
					}
				}

				break;
		}

		section.Tokens = Pool(tokens, pool);
		if (section.Kind == FormatSectionKind.Fraction)
		{
			Locate(section);
		}

		return section;
	}

	/// <summary>Finds where a fraction's parts are among its compiled tokens: after its whole number, its numerator's start, and after its denominator.</summary>
	/// <param name="section">The fraction.</param>
	private static void Locate(FormatSection section)
	{
		var tokens = section.Tokens;
		section.NumeratorStart = -1;
		for (var i = 0; i < tokens.Length; i++)
		{
			var token = tokens[i];
			if (token.Role == PlaceholderRole.Integer)
			{
				section.WholeEnd = i + 1;
			}
			else if (token.Role == PlaceholderRole.Numerator && section.NumeratorStart < 0)
			{
				section.NumeratorStart = i;
			}
			else if (token.Role == PlaceholderRole.Denominator || token.Kind == FormatTokenKind.FixedDenominator)
			{
				section.FractionEnd = i + 1;
			}
		}
	}

	/// <summary>
	/// A number section: each placeholder's role (integer, decimal or exponent), each comma's (grouping, scaling or itself),
	/// the first point before the exponent, and the percent signs.
	/// </summary>
	/// <param name="tokens">The tokens, changed in place.</param>
	/// <param name="section">The section's facts.</param>
	private static void FinishNumber(List<Raw> tokens, FormatSection section)
	{
		var exponentAt = tokens.FindIndex(t => t.Kind == FormatTokenKind.Exponent);
		var mantissaEnd = exponentAt < 0 ? tokens.Count : exponentAt;
		var pointAt = tokens.FindIndex(0, mantissaEnd, t => t.Bare && t.Char == '.');
		var integerEnd = pointAt < 0 ? mantissaEnd : pointAt;
		var firstInteger = -1;
		var lastInteger = -1;
		var lastMantissa = -1;
		var kinds = new StringBuilder();
		for (var i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			if (token.Bare && token.Char == '%')
			{
				section.Percents++;
			}

			if (token.Kind != FormatTokenKind.Digit)
			{
				continue;
			}

			section.Placeholders++;
			if (i < integerEnd)
			{
				tokens[i] = token with { Role = PlaceholderRole.Integer };
				section.IntegerPlaceholders++;
				kinds.Append(token.Char);
				firstInteger = firstInteger < 0 ? i : firstInteger;
				lastInteger = i;
				lastMantissa = i;
			}
			else if (i < mantissaEnd)
			{
				tokens[i] = token with { Role = PlaceholderRole.Decimal };
				section.DecimalPlaceholders++;
				lastMantissa = i;
			}
			else
			{
				tokens[i] = token with { Role = PlaceholderRole.Exponent };
				section.ExponentPlaceholders++;
				section.ExponentDigits += token.Char == '0' ? 1 : 0;
			}
		}

		section.IntegerKinds = kinds.ToString();
		section.HasExponent = exponentAt >= 0;
		section.ExponentDigits = Math.Max(section.ExponentDigits, 1);
		if (pointAt >= 0)
		{
			tokens[pointAt] = new Raw(FormatTokenKind.Point);
		}

		// Commas between integer placeholders group the digits; those right after the last placeholder of the mantissa, or
		// right before the point after the last integer placeholder, divide by 1,000 each; those among the decimal
		// placeholders do nothing; others show as themselves. Grouping and scaling commas, and those that do nothing, show
		// nothing. Each is judged on the tokens as read, then removed.
		var lastDecimal = mantissaEnd > 0 ? tokens.FindLastIndex(mantissaEnd - 1, mantissaEnd, t => t.Role == PlaceholderRole.Decimal) : -1;
		var removed = new List<int>();
		for (var i = mantissaEnd - 1; i >= 0; i--)
		{
			if (!(tokens[i].Bare && tokens[i].Char == ','))
			{
				continue;
			}

			if (i > firstInteger && i < lastInteger)
			{
				section.Grouping = true;
				removed.Add(i);
			}
			else if ((lastMantissa >= 0 && i > lastMantissa && OnlyCommasBetween(tokens, lastMantissa + 1, i)) ||
				(pointAt >= 0 && lastInteger >= 0 && i > lastInteger && i < pointAt && OnlyCommasBetween(tokens, lastInteger + 1, i) &&
				OnlyCommasBetween(tokens, i + 1, pointAt)))
			{
				section.Scale++;
				removed.Add(i);
			}
			else if (pointAt >= 0 && i > pointAt && i < lastDecimal)
			{
				removed.Add(i);
			}
		}

		// Descending, so that each removal leaves the positions still to remove where they were.
		foreach (var i in removed)
		{
			tokens.RemoveAt(i);
		}
	}

	/// <summary>Whether every token between two positions is a comma outside quotes.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="start">The first position.</param>
	/// <param name="end">The position after the last.</param>
	/// <returns>Whether they are.</returns>
	private static bool OnlyCommasBetween(List<Raw> tokens, int start, int end)
	{
		for (var i = start; i < end; i++)
		{
			if (!(tokens[i].Bare && tokens[i].Char == ','))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// A fraction: a <c>/</c> outside quotes after a placeholder, before its denominator (placeholders, or a number, after
	/// any spaces). The placeholders right before the <c>/</c> are the numerator's, those before them the whole number's,
	/// grouped by a comma between them; the <c>/</c> is a token of its own, and so are a number's digits, which show as
	/// themselves, so that the renderer finds the fraction's end.
	/// </summary>
	/// <param name="tokens">The tokens, changed in place.</param>
	/// <param name="section">The section's facts.</param>
	/// <returns>Whether the section is a fraction.</returns>
	private static bool FinishFraction(List<Raw> tokens, FormatSection section)
	{
		var bar = -1;
		var denominator = -1;
		for (var i = 1; i < tokens.Count && bar < 0; i++)
		{
			if (!(tokens[i].Bare && tokens[i].Char == '/') || tokens[i - 1].Kind != FormatTokenKind.Digit)
			{
				continue;
			}

			var next = i + 1;
			while (next < tokens.Count && tokens[next].Bare && tokens[next].Char == ' ')
			{
				next++;
			}

			if (next < tokens.Count && (tokens[next].Kind == FormatTokenKind.Digit || (tokens[next].Bare && tokens[next].Char is >= '1' and <= '9')))
			{
				bar = i;
				denominator = next;
			}
		}

		if (bar < 0)
		{
			return false;
		}

		var numerator = bar - 1;
		while (numerator > 0 && tokens[numerator - 1].Kind == FormatTokenKind.Digit)
		{
			numerator--;
		}

		// A number's digits: 1 to 9 outside quotes, then any of them or 0 placeholders, which show as themselves, as one
		// token. One past MaxDenominator makes the code unreadable, so reading stops well past it.
		if (tokens[denominator].Kind != FormatTokenKind.Digit)
		{
			var value = 0;
			var digits = new StringBuilder();
			for (var i = denominator; i < tokens.Count; i++)
			{
				var token = tokens[i];
				var digit = token.Kind == FormatTokenKind.Digit && token.Char == '0' ? 0 : token.Bare && token.Char is >= '0' and <= '9' ? token.Char - '0' : -1;
				if (digit < 0 || value > 100_000_000)
				{
					break;
				}

				value = (value * 10) + digit;
				digits.Append((char)('0' + digit));
			}

			tokens.RemoveRange(denominator + 1, digits.Length - 1);
			tokens[denominator] = new Raw(FormatTokenKind.FixedDenominator) { Text = digits.ToString() };
			section.Denominator = value;
		}

		tokens[bar] = new Raw(FormatTokenKind.Bar);
		var firstWhole = -1;
		var lastWhole = -1;
		var kinds = new StringBuilder();
		var numeratorKinds = new StringBuilder();
		var denominatorKinds = new StringBuilder();
		for (var i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			if (token.Bare && token.Char == '%')
			{
				section.Percents++;
			}

			if (token.Kind != FormatTokenKind.Digit)
			{
				continue;
			}

			section.Placeholders++;
			if (i < numerator)
			{
				tokens[i] = token with { Role = PlaceholderRole.Integer };
				section.IntegerPlaceholders++;
				kinds.Append(token.Char);
				firstWhole = firstWhole < 0 ? i : firstWhole;
				lastWhole = i;
			}
			else if (i < bar)
			{
				tokens[i] = token with { Role = PlaceholderRole.Numerator };
				section.NumeratorPlaceholders++;
				numeratorKinds.Append(token.Char);
			}
			else
			{
				tokens[i] = token with { Role = PlaceholderRole.Denominator };
				section.DenominatorPlaceholders++;
				denominatorKinds.Append(token.Char);
			}
		}

		section.IntegerKinds = kinds.ToString();
		section.NumeratorKinds = numeratorKinds.ToString();
		section.DenominatorKinds = denominatorKinds.ToString();
		for (var i = lastWhole - 1; i > firstWhole; i--)
		{
			if (tokens[i].Bare && tokens[i].Char == ',')
			{
				section.Grouping = true;
				tokens.RemoveAt(i);
			}
		}

		return true;
	}

	/// <summary>
	/// A date or time section: <c>m</c> and <c>mm</c> after an hour or before a second are minutes, the <c>0</c>s after a
	/// <c>.</c> are fractions of a second, elapsed time after a date part is the time of day in its unit, and anything
	/// else outside quotes shows as itself (a <c>0</c> as <c>0</c>, <c>#</c> and <c>?</c> as nothing).
	/// </summary>
	/// <param name="tokens">The tokens, changed in place.</param>
	/// <param name="section">The section's facts.</param>
	private static void FinishDate(List<Raw> tokens, FormatSection section)
	{
		for (var i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			if (token.Kind == FormatTokenKind.Month && token.Count <= 2 &&
				(Previous(tokens, i) is FormatTokenKind.Hour or FormatTokenKind.ElapsedHours || Next(tokens, i) is FormatTokenKind.Second or FormatTokenKind.ElapsedSeconds))
			{
				tokens[i] = token with { Kind = FormatTokenKind.Minute };
			}
		}

		for (var i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			if (token.Bare && token.Char == '.' && i + 1 < tokens.Count && tokens[i + 1] is { Kind: FormatTokenKind.Digit, Char: '0' })
			{
				var zeros = 0;
				while (i + 1 + zeros < tokens.Count && tokens[i + 1 + zeros] is { Kind: FormatTokenKind.Digit, Char: '0' })
				{
					zeros++;
				}

				tokens[i] = Raw.Shown(".");
				tokens[i + 1] = new Raw(FormatTokenKind.SubSecond) { Count = zeros };
				tokens.RemoveRange(i + 2, zeros - 1);
				section.SubSecondDigits = Math.Max(section.SubSecondDigits, zeros);
				section.ShowsSeconds = true;

				continue;
			}

			switch (token.Kind)
			{
				case FormatTokenKind.Digit:
					tokens[i] = Raw.Shown(token.Char == '0' ? "0" : "");

					break;
				case FormatTokenKind.Exponent or FormatTokenKind.At or FormatTokenKind.General:
					tokens[i] = Raw.Shown("");

					break;
				case FormatTokenKind.AmPm or FormatTokenKind.ShortAmPm:
					section.TwelveHour = true;

					break;
				case FormatTokenKind.Second or FormatTokenKind.ElapsedSeconds:
					section.ShowsSeconds = true;

					break;
			}
		}

		// Elapsed time after a date part is the time of day in its unit (yyyy-mm-dd [h]:mm shows the hour of the day).
		var afterDate = false;
		var elapsed = 0;
		var others = 0;
		for (var i = 0; i < tokens.Count; i++)
		{
			var kind = tokens[i].Kind;
			afterDate |= kind is FormatTokenKind.Year or FormatTokenKind.Month or FormatTokenKind.Day or FormatTokenKind.EraYear or
				FormatTokenKind.BuddhistYear or FormatTokenKind.WeekDay;
			if (kind is FormatTokenKind.ElapsedHours or FormatTokenKind.ElapsedMinutes or FormatTokenKind.ElapsedSeconds)
			{
				elapsed++;
				if (afterDate)
				{
					tokens[i] = tokens[i] with { OfDay = true };
				}
			}
			else if (kind != FormatTokenKind.Literal)
			{
				others++;
			}
		}

		section.ElapsedAlone = elapsed == 1 && others == 0;
	}

	/// <summary>A text section: <c>@</c> and literals; any other token shows as itself, or nothing.</summary>
	/// <param name="tokens">The tokens, changed in place.</param>
	private static void FinishText(List<Raw> tokens)
	{
		for (var i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			if (token.Kind is FormatTokenKind.Literal or FormatTokenKind.At)
			{
				continue;
			}

			tokens[i] = Raw.Shown(token.Kind == FormatTokenKind.Digit && token.Char == '0' ? "0" : "");
		}
	}

	/// <summary>The kind of the date or time token before a position, past literals.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="i">The position.</param>
	/// <returns>The kind, or <see cref="FormatTokenKind.Literal"/> for none.</returns>
	private static FormatTokenKind Previous(List<Raw> tokens, int i)
	{
		for (var j = i - 1; j >= 0; j--)
		{
			if (tokens[j].Kind is not (FormatTokenKind.Literal or FormatTokenKind.Digit))
			{
				return tokens[j].Kind;
			}
		}

		return FormatTokenKind.Literal;
	}

	/// <summary>The kind of the date or time token after a position, past literals.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="i">The position.</param>
	/// <returns>The kind, or <see cref="FormatTokenKind.Literal"/> for none.</returns>
	private static FormatTokenKind Next(List<Raw> tokens, int i)
	{
		for (var j = i + 1; j < tokens.Count; j++)
		{
			if (tokens[j].Kind is not (FormatTokenKind.Literal or FormatTokenKind.Digit))
			{
				return tokens[j].Kind;
			}
		}

		return FormatTokenKind.Literal;
	}

	/// <summary>Writes a section's tokens' text to the pool, adjacent literals as one and empty ones left out.</summary>
	/// <param name="tokens">The tokens.</param>
	/// <param name="pool">The code's literal text.</param>
	/// <returns>The compiled tokens.</returns>
	private static FormatToken[] Pool(List<Raw> tokens, List<byte> pool)
	{
		var compiled = new List<FormatToken>(tokens.Count);
		var text = new StringBuilder();
		foreach (var token in tokens)
		{
			if (token.Kind == FormatTokenKind.Literal)
			{
				text.Append(token.Text);

				continue;
			}

			AddLiteral(compiled, text, pool);
			var start = pool.Count;
			if (token.Kind is FormatTokenKind.ShortAmPm or FormatTokenKind.FixedDenominator)
			{
				pool.AddRange(Encoding.UTF8.GetBytes(token.Text!));
			}

			compiled.Add(new FormatToken(token.Kind, start, pool.Count - start, token.Count, token.Char, token.Role, token.OfDay));
		}

		AddLiteral(compiled, text, pool);

		return [.. compiled];
	}

	/// <summary>Adds the literal text gathered so far as one token, if there is any.</summary>
	/// <param name="compiled">The compiled tokens.</param>
	/// <param name="text">The text, which is cleared.</param>
	/// <param name="pool">The code's literal text.</param>
	private static void AddLiteral(List<FormatToken> compiled, StringBuilder text, List<byte> pool)
	{
		if (text.Length == 0)
		{
			return;
		}

		var start = pool.Count;
		pool.AddRange(Encoding.UTF8.GetBytes(text.ToString()));
		compiled.Add(new FormatToken(FormatTokenKind.Literal, start, pool.Count - start));
		text.Clear();
	}

	/// <summary>Whether <paramref name="code"/> has <paramref name="word"/> at <paramref name="index"/>, in any case.</summary>
	/// <param name="code">The code.</param>
	/// <param name="index">Where to look.</param>
	/// <param name="word">The word, in lower case.</param>
	/// <returns>Whether the word is there.</returns>
	private static bool Matches(string code, int index, string word) =>
		index + word.Length <= code.Length && string.Compare(code, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;

	/// <summary>A token as read, before its section is finished.</summary>
	/// <param name="Kind">What it is.</param>
	private record struct Raw(FormatTokenKind Kind)
	{
		/// <summary>A literal's text, <c>A/P</c>'s letters, or a fixed denominator's digits.</summary>
		public string? Text { get; set; }

		/// <summary>A placeholder's character, an exponent's sign, or the character of a literal read outside quotes.</summary>
		public char Char { get; set; }

		/// <summary>Whether a literal is a character read outside quotes, which can be a point, a comma, a fraction's bar or a denominator's digit.</summary>
		public bool Bare { get; set; }

		/// <summary>A date or time token's letters, or the digits of fractions of a second.</summary>
		public int Count { get; set; }

		/// <summary>What a placeholder does.</summary>
		public PlaceholderRole Role { get; set; }

		/// <summary>Whether an elapsed time follows a date part.</summary>
		public bool OfDay { get; set; }

		/// <summary>A literal of text that shows as it is.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The token.</returns>
		public static Raw Shown(string text) => new(FormatTokenKind.Literal) { Text = text };
	}

	/// <summary>A section as read: its tokens and what its brackets say.</summary>
	private sealed class RawSection
	{
		/// <summary>The tokens.</summary>
		public List<Raw> Tokens { get; } = [];

		/// <summary>Whether it has a date or time token.</summary>
		public bool IsDate { get; private set; }

		/// <summary>How many <c>@</c> it has.</summary>
		public int Ats { get; private set; }

		/// <summary>Whether it has <c>General</c>.</summary>
		public bool HasGeneral { get; set; }

		/// <summary>Whether a digit placeholder has been read, for an exponent's test.</summary>
		public bool HasPlaceholder { get; private set; }

		/// <summary>The operator of its condition.</summary>
		public ConditionOperator Condition { get; private set; }

		/// <summary>The number its condition compares with.</summary>
		public double Operand { get; private set; }

		/// <summary>Whether it has a color.</summary>
		public bool HasColor { get; private set; }

		/// <summary>Whether it has <c>DBNum</c>.</summary>
		public bool HasDBNum { get; private set; }

		/// <summary>Whether it has <c>B1</c> or <c>B2</c>.</summary>
		public bool HasCalendar { get; private set; }

		/// <summary>Whether it has a locale code.</summary>
		public bool HasLocale { get; private set; }

		/// <summary>The names a locale code's language gives.</summary>
		public DateNames? Names { get; private set; }

		/// <summary>The system format a locale code names.</summary>
		public SystemFormat System { get; private set; }

		/// <summary>Adds a token.</summary>
		/// <param name="token">The token.</param>
		public void Add(Raw token)
		{
			Tokens.Add(token);
			switch (token.Kind)
			{
				case FormatTokenKind.At:
					Ats++;

					break;
				case FormatTokenKind.Digit:
					HasPlaceholder = true;

					break;
				case FormatTokenKind.Literal or FormatTokenKind.Exponent or FormatTokenKind.General:
					break;
				default:
					IsDate = true;

					break;
			}
		}

		/// <summary>Reads a bracket's content: elapsed time, a currency or locale, <c>DBNum</c>, a condition, or a color.</summary>
		/// <param name="content">The text between the brackets.</param>
		/// <returns>Whether the renderer can read it.</returns>
		public bool Bracket(string content)
		{
			if (IsElapsed(content))
			{
				var kind = (content[0] | 0x20) switch
				{
					'h' => FormatTokenKind.ElapsedHours,
					'm' => FormatTokenKind.ElapsedMinutes,
					_ => FormatTokenKind.ElapsedSeconds,
				};
				Add(new Raw(kind) { Count = content.Length });

				return true;
			}

			if (content.StartsWith("$", StringComparison.Ordinal))
			{
				return Locale(content);
			}

			if (content.Length == 6 && content.StartsWith("DBNum", StringComparison.OrdinalIgnoreCase) && content[5] is '1' or '2' or '3')
			{
				HasDBNum = true;

				return true;
			}

			if (content.Length > 0 && content[0] is '<' or '>' or '=')
			{
				return ReadCondition(content);
			}

			return ReadColor(content);
		}

		/// <summary>
		/// Reads the letters at a position: <c>General</c>, an exponent, <c>AM/PM</c> or <c>A/P</c>, a calendar, a date or
		/// time token, or a letter that shows as itself.
		/// </summary>
		/// <param name="code">The code.</param>
		/// <param name="i">The letter's position.</param>
		/// <returns>The position of the token's last character; −1 for an exponent in lower case, which Excel refuses.</returns>
		public int Letters(string code, int i)
		{
			var c = code[i];
			var lower = (char)(c | 0x20);
			if (lower == 'g' && Matches(code, i, "general"))
			{
				Add(new Raw(FormatTokenKind.General));
				HasGeneral = true;

				return i + 6;
			}

			// E+ and E- after a placeholder of a number, or before one, are an exponent; in lower case, Excel refuses them.
			if (lower == 'e' && i + 1 < code.Length && code[i + 1] is '+' or '-' &&
				((HasPlaceholder && !IsDate) || (i + 2 < code.Length && code[i + 2] is '0' or '#' or '?')))
			{
				if (c == 'e')
				{
					return -1;
				}

				Add(new Raw(FormatTokenKind.Exponent) { Char = code[i + 1] });

				return i + 1;
			}

			if (lower == 'a' && Matches(code, i, "am/pm"))
			{
				Add(new Raw(FormatTokenKind.AmPm));

				return i + 4;
			}

			if (lower == 'a' && Matches(code, i, "a/p"))
			{
				Add(new Raw(FormatTokenKind.ShortAmPm) { Text = code[i].ToString() + code[i + 2] });

				return i + 2;
			}

			// B1 and B2 are a calendar, which isn't rendered.
			if (c == 'B' && i + 1 < code.Length && code[i + 1] is '1' or '2')
			{
				HasCalendar = true;

				return i + 1;
			}

			var run = 1;
			while (i + run < code.Length && (code[i + run] | 0x20) == lower)
			{
				run++;
			}

			FormatTokenKind? kind = lower switch
			{
				'y' => FormatTokenKind.Year,
				'm' => FormatTokenKind.Month,
				'd' => FormatTokenKind.Day,
				'h' => FormatTokenKind.Hour,
				's' => FormatTokenKind.Second,
				'e' => FormatTokenKind.EraYear,
				'g' => FormatTokenKind.Era,
				'b' => FormatTokenKind.BuddhistYear,
				'a' when run >= 3 => FormatTokenKind.WeekDay,
				_ => null,
			};
			if (kind is null)
			{
				// A letter that is no token shows as itself (h:mm a shows 0:00 a).
				Add(new Raw(FormatTokenKind.Literal) { Text = c.ToString(), Char = c, Bare = true });

				return i;
			}

			Add(new Raw(kind.Value) { Count = run });

			return i + run - 1;
		}

		/// <summary>
		/// Reads a currency or locale: <c>$</c>, a symbol, and after a <c>-</c> a locale id of 1 to 8 hex digits, whose low
		/// 16 bits are the language (<c>F800</c> and <c>F400</c> the system formats; the calendar and numeral system above
		/// them aren't rendered), or a tag (<c>en-US</c>, <c>x-sysdate</c>, <c>x-systime</c>).
		/// </summary>
		/// <param name="content">The bracket's content, from its <c>$</c>.</param>
		/// <returns>Whether the renderer can read it.</returns>
		private bool Locale(string content)
		{
			HasLocale = true;
			var dash = content.IndexOf('-');
			var symbol = dash < 0 ? content.Substring(1) : content.Substring(1, dash - 1);
			if (symbol.Length > 0)
			{
				Add(Raw.Shown(symbol));
			}

			if (dash < 0 || dash == content.Length - 1)
			{
				return true;
			}

			var id = content.Substring(dash + 1);
			if (id.Length <= 8 && uint.TryParse(id, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var locale))
			{
				var language = (int)(locale & 0xFFFF);
				System = language switch
				{
					0xF800 => SystemFormat.LongDate,
					0xF400 => SystemFormat.LongTime,
					_ => SystemFormat.None,
				};
				Names = language == 0 || System != SystemFormat.None ? null : DateNames.ForLanguage(language);

				return true;
			}

			if (string.Equals(id, "x-sysdate", StringComparison.OrdinalIgnoreCase))
			{
				System = SystemFormat.LongDate;
			}
			else if (string.Equals(id, "x-systime", StringComparison.OrdinalIgnoreCase))
			{
				System = SystemFormat.LongTime;
			}
			else if (IsTag(id))
			{
				Names = DateNames.ForTag(id);
			}
			else
			{
				return false;
			}

			return true;
		}

		/// <summary>Reads a condition: <c>&lt;</c>, <c>&gt;</c>, <c>=</c>, <c>&lt;=</c>, <c>&gt;=</c> or <c>&lt;&gt;</c>, and a number.</summary>
		/// <param name="content">The bracket's content.</param>
		/// <returns>Whether the renderer can read it.</returns>
		private bool ReadCondition(string content)
		{
			var length = 1;
			Condition = content[0] switch
			{
				'=' => ConditionOperator.Equal,
				'<' when content.Length > 1 && content[1] == '=' => ConditionOperator.LessOrEqual,
				'<' when content.Length > 1 && content[1] == '>' => ConditionOperator.NotEqual,
				'<' => ConditionOperator.Less,
				'>' when content.Length > 1 && content[1] == '=' => ConditionOperator.GreaterOrEqual,
				_ => ConditionOperator.Greater,
			};
			if (Condition is ConditionOperator.LessOrEqual or ConditionOperator.NotEqual or ConditionOperator.GreaterOrEqual)
			{
				length = 2;
			}

			var number = content.Substring(length);
			if (number.Length == 0 || !double.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var operand))
			{
				return false;
			}

			Operand = operand;

			return true;
		}

		/// <summary>Reads a color: one of the eight names, or <c>Color1</c> to <c>Color56</c>, in any case and with any spaces.</summary>
		/// <param name="content">The bracket's content.</param>
		/// <returns>Whether it is one.</returns>
		private bool ReadColor(string content)
		{
			var name = content.Replace(" ", "");
			foreach (var color in ColorNames)
			{
				if (string.Equals(name, color, StringComparison.OrdinalIgnoreCase))
				{
					HasColor = true;

					return true;
				}
			}

			if (name.Length is 6 or 7 && name.StartsWith("color", StringComparison.OrdinalIgnoreCase) &&
				int.TryParse(name.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 56)
			{
				HasColor = true;

				return true;
			}

			return false;
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

		/// <summary>Whether a locale code's id is a language tag: letters, digits and hyphens, starting with a letter.</summary>
		/// <param name="id">The id.</param>
		/// <returns>Whether it is.</returns>
		private static bool IsTag(string id)
		{
			if (id.Length == 0 || (uint)((id[0] | 0x20) - 'a') > 'z' - 'a')
			{
				return false;
			}

			foreach (var c in id)
			{
				if (!(c == '-' || (uint)((c | 0x20) - 'a') <= 'z' - 'a' || c is >= '0' and <= '9'))
				{
					return false;
				}
			}

			return true;
		}
	}
}
