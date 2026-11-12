using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The text Excel shows for a value in a number format code, and its width (Phase 5's decision 5): a column's
/// <see cref="ExcelColumnOptions.Format"/> sizes its numbers, booleans, dates, times and durations, so that a typed cell
/// doesn't show <c>########</c>, and its text for auto-fit. <see cref="General"/> is the text Excel shows a number in
/// without a format.
/// </summary>
/// <remarks>
/// <para>
/// The rules are those Excel 365 showed through COM, in en-US (checked on 2026-11-09; <c>Csv\_bench\results\phase5\step3</c>):
/// </para>
/// <list type="bullet">
/// <item><description>
/// A code has up to four sections, for positive numbers, negative ones, zero and text. With one section, a negative
/// number shows a minus sign first, unless it rounds to zero; with two or more, the negative section shows the number
/// without one. A last section with <c>@</c> is the text section, which booleans take too; a code of one section with
/// <c>@</c> shows numbers in General. An empty section shows nothing.
/// </description></item>
/// <item><description>
/// A number is rounded on its 15 significant digits, half away from zero (<c>999.995</c> shows <c>1000.00</c> in
/// <c>0.00</c>). Each <c>%</c> multiplies it by 100, and each comma right after the last digit placeholder divides it
/// by 1,000; a comma between integer placeholders groups the digits by three.
/// </description></item>
/// <item><description>
/// Integer placeholders take the digits from the right, the leftmost the rest; one without a digit shows <c>0</c>, nothing
/// or a space as a digit's width (<c>0</c>, <c>#</c>, <c>?</c>). Fraction placeholders show the rounded digits, and past
/// the last one that isn't 0, <c>0</c>, nothing or a space.
/// </description></item>
/// <item><description>
/// An exponent (<c>E+</c>, <c>E-</c>) is a multiple of the number of integer placeholders (<c>##0.0E+0</c> shows
/// <c>123.5E+12</c>), with at least as many digits as its zeros; <c>E-</c> shows no sign for a positive exponent.
/// </description></item>
/// <item><description>
/// <c>_x</c> leaves the width of <c>x</c>, <c>*x</c> repeats <c>x</c> to fill the cell (which auto-fit counts as
/// nothing), <c>[$€-407]</c> shows <c>€</c>, and colors show nothing.
/// </description></item>
/// <item><description>
/// Dates show month and day names (<c>aaa</c> and <c>aaaa</c> too), in English or in the language of a locale
/// (<c>[$-407]</c>), and the days before 1900-03-01 a day of the week earlier, as Excel counts them. Elapsed time
/// (<c>[h]</c>) shows the whole hours, minutes or seconds, but after a date part, where it is the time of day in its unit;
/// alone, it shows any serial, where any other token stops at 9999-12-31. A time is taken to the millisecond, and rounds
/// to the second unless the section shows fractions of one; fractions of a second and minutes are cut.
/// </description></item>
/// </list>
/// <para>
/// A width errs wide: group separators and decimal points are measured as the widest Excel shows in common locales
/// (<c>.</c>), a date's names as its language's widest, <c>AM/PM</c> as the wider designator, and its days, months and
/// hours as two digits, so that the dates after those measured fit too. Codes it doesn't model are estimated, never
/// narrower than Excel's text (checked against Phase 4's answers too): conditions (<c>[&gt;=1000]</c>) take their widest
/// section's text; fractions (<c>?/?</c>) a digit for each placeholder besides their whole number's digits;
/// <c>[DBNum1]</c> to <c>[DBNum3]</c>, whose East Asian numerals an East Asian locale shows, a numeral and a unit for each
/// digit; and an era's name (<c>g</c>) one or two East Asian characters.
/// </para>
/// </remarks>
internal sealed class NumberFormatWidth
{
	/// <summary>The most characters General shows a number in, besides its sign.</summary>
	private const int GeneralLength = 11;

	/// <summary>The most significant digits of a number Excel keeps.</summary>
	private const int MaxDigits = 15;

	/// <summary>A date's serial past Excel's last, 9999-12-31.</summary>
	private const double PastLastDate = 2958466;

	/// <summary>East Asian characters, as wide as each other (<see cref="ColumnWidths"/>): what an era's name, a Japanese day's, or a numeral is measured as.</summary>
	private const char Wide = (char)0x5E74;

	/// <summary>A digit's widest form in <c>[DBNum]</c>: a numeral and a unit.</summary>
	private static readonly string WideNumeral = new(Wide, 2);

	// The sections numbers take, in order (positive, negative, zero); the text section, if any; and whether a section has
	// a condition, which chooses its section by the value rather than by its sign.
	private readonly List<Section> numbers = [];
	private readonly Section? text;
	private readonly bool conditional;

	/// <summary>Reads a format code.</summary>
	/// <param name="code">The code, which <see cref="FormatCheck"/> passes.</param>
	public NumberFormatWidth(string code)
	{
		var sections = Parse(code);
		var last = sections[sections.Count - 1];
		if (sections.Count == 4 || (sections.Count >= 2 && last.HasAt))
		{
			text = last;
			sections.RemoveAt(sections.Count - 1);
		}
		else if (sections.Count == 1 && last.HasAt)
		{
			text = last;
			sections.Clear();
		}

		numbers.AddRange(sections);
		conditional = numbers.Exists(s => s.Condition);
	}

	/// <summary>The pixels of a number's widest text in the code (<see cref="ColumnWidths.TextPixels"/>).</summary>
	/// <param name="value">The number: a date's, time's or duration's serial in days too.</param>
	/// <returns>The pixels, without the padding.</returns>
	public double NumberPixels(double value) => ColumnWidths.TextPixels(Number(value, widest: true).AsSpan());

	/// <summary>The pixels of a text, or a boolean's <c>TRUE</c> or <c>FALSE</c>, in the code: in its text section, if it has one.</summary>
	/// <param name="textPixels">The text's own pixels.</param>
	/// <returns>The pixels, without the padding.</returns>
	public double TextPixels(double textPixels)
	{
		if (text is null)
		{
			return textPixels;
		}

		var literals = ColumnWidths.TextPixels(RenderText(text, "", widest: true).AsSpan());

		return literals + (text.Ats * textPixels);
	}

	/// <summary>
	/// A number's text in the code: as Excel shows it in en-US (<paramref name="widest"/> off), or its widest form, which
	/// <see cref="NumberPixels"/> measures. Conditions and fractions are only estimated.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <param name="widest">Whether to give the widest form rather than Excel's text.</param>
	/// <returns>The text.</returns>
	public string Number(double value, bool widest)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			return "";
		}

		if (numbers.Count == 0)
		{
			return General(value);
		}

		var abs = Math.Abs(value);
		if (conditional)
		{
			// The widest of the sections a condition could choose.
			var widestText = "";
			var widestPixels = -1.0;
			foreach (var section in numbers)
			{
				var shown = Render(section, value, abs, value < 0, widest);
				var pixels = ColumnWidths.TextPixels(shown.AsSpan());
				if (pixels > widestPixels)
				{
					widestText = shown;
					widestPixels = pixels;
				}
			}

			return widestText;
		}

		if (value > 0)
		{
			return Render(numbers[0], value, abs, false, widest);
		}

		if (value < 0)
		{
			return numbers.Count >= 2 ? Render(numbers[1], value, abs, false, widest) : Render(numbers[0], value, abs, true, widest);
		}

		return Render(numbers.Count >= 3 ? numbers[2] : numbers[0], value, 0, false, widest);
	}

	/// <summary>A text's, or a boolean's, text in the code: in its text section, if it has one.</summary>
	/// <param name="value">The text.</param>
	/// <returns>The text Excel shows.</returns>
	public string Text(string value) => text is null ? value : RenderText(text, value, widest: false);

	/// <summary>
	/// A number's text in General, as Excel shows it in a column wide enough: at most 11 characters besides its sign, in
	/// decimals while they show as many significant digits as scientific notation would, and in scientific notation
	/// otherwise (<c>1.23457E+11</c>, <c>1.23457E-06</c>); integers of up to 11 digits whole.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The text.</returns>
	public static string General(double value)
	{
		if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
		{
			return "0";
		}

		var sign = value < 0 ? "-" : "";
		var digits = Digits.Of(value);
		if (digits.Exponent >= 0)
		{
			var integerLength = digits.Exponent + 1;
			if (integerLength <= GeneralLength)
			{
				var decimals = Math.Max(0, GeneralLength - integerLength - 1);
				var rounded = digits.Round(decimals);
				if (rounded.IntegerLength <= GeneralLength)
				{
					return sign + Plain(rounded, decimals);
				}
			}
		}
		else
		{
			// After "0.", the leading zeros and the significant digits that fit.
			var zeros = -digits.Exponent - 1;
			var shown = Math.Min(digits.Text.Length, GeneralLength - 2 - zeros);
			if (shown > 0 && shown >= Math.Min(digits.Text.Length, ScientificDecimals(digits.Exponent) + 1))
			{
				return sign + Plain(digits.Round(GeneralLength - 2), GeneralLength - 2);
			}
		}

		return sign + Scientific(digits);
	}

	/// <summary>
	/// The pixels of a number's text in General, as Excel's AutoFit measures it: every character but a leading minus sign
	/// as wide as a digit (<c>0.5</c> as <c>123</c>, <c>1E-10</c> as <c>12345</c>; checked on 2026-11-09), since General
	/// counts in digits how many characters a column has room for.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The pixels, without the padding.</returns>
	public static double GeneralPixels(double value) => ColumnWidths.TextPixels(AsDigits(General(value)).AsSpan());

	/// <summary>General's text with every character but a leading minus sign as a digit, to measure as Excel's AutoFit does.</summary>
	/// <param name="text">General's text.</param>
	/// <returns>The text of digits.</returns>
	private static string AsDigits(string text)
	{
		var chars = text.ToCharArray();
		for (var i = chars.Length > 0 && chars[0] == '-' ? 1 : 0; i < chars.Length; i++)
		{
			chars[i] = '0';
		}

		return new string(chars);
	}

	/// <summary>A rounded number in decimals, without trailing zeros.</summary>
	/// <param name="digits">The number.</param>
	/// <param name="decimals">The decimals it was rounded to.</param>
	/// <returns>The text.</returns>
	private static string Plain(Digits digits, int decimals)
	{
		var integer = digits.Integer();
		var fraction = digits.Fraction(decimals).TrimEnd('0');
		if (integer.Length == 0)
		{
			integer = "0";
		}

		return fraction.Length == 0 ? integer : integer + "." + fraction;
	}

	/// <summary>A number in General's scientific notation: as many decimals as 11 characters hold, without trailing zeros.</summary>
	/// <param name="digits">The number, not zero.</param>
	/// <returns>The text, such as <c>1.23457E+11</c> or <c>1E-10</c>.</returns>
	private static string Scientific(Digits digits)
	{
		var exponent = digits.Exponent;
		var mantissa = new Digits(digits.Text, 0).Round(ScientificDecimals(exponent));
		if (mantissa.Exponent > 0)
		{
			exponent += mantissa.Exponent;
			mantissa = new Digits(mantissa.Text, 0).Round(ScientificDecimals(exponent));
		}

		var text = mantissa.Text.Length > 1 ? mantissa.Text.Substring(0, 1) + "." + mantissa.Text.Substring(1) : mantissa.Text;

		return text + (exponent < 0 ? "E-" : "E+") + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
	}

	/// <summary>The decimals of General's scientific notation: 11 characters less <c>d.</c> and the exponent's.</summary>
	/// <param name="exponent">The exponent.</param>
	/// <returns>5 for an exponent of two digits, 4 for three.</returns>
	private static int ScientificDecimals(int exponent) => GeneralLength - 2 - (Math.Abs(exponent) >= 100 ? 5 : 4);

	/// <summary>A section's text for a number.</summary>
	/// <param name="section">The section.</param>
	/// <param name="value">The number, with its sign, for dates.</param>
	/// <param name="abs">Its absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first, unless it rounds to zero.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string Render(Section section, double value, double abs, bool minus, bool widest)
	{
		// A section of a color, DBNum or a condition alone shows the number in General ([Red] is [Red]General); one of a
		// locale alone, or of nothing, shows nothing (checked through COM on 2026-11-11, after step 7's format fuzzer).
		var bracketsAlone = section.Tokens.Count == 0;
		if (bracketsAlone && !(section.Color || section.DBNum || section.Condition))
		{
			return "";
		}

		if (section.IsDate)
		{
			return RenderDate(section, value, widest);
		}

		var shown = bracketsAlone || section.HasGeneral || section.Placeholders == 0 ? RenderGeneral(section, abs, minus, widest) :
			section.IsFraction ? RenderFraction(section, abs, minus, widest) : RenderNumber(section, abs, minus, widest);
		if (section.DBNum && widest)
		{
			// East Asian numerals, which an East Asian locale shows (四万五千三百六 for 45306), and Excel shows as digits
			// elsewhere: each digit as wide as a numeral and a unit, East Asian characters both.
			var numerals = new StringBuilder(shown.Length * 2);
			foreach (var c in shown)
			{
				numerals.Append(c is >= '0' and <= '9' ? WideNumeral : c.ToString());
			}

			return numerals.ToString();
		}

		return shown;
	}

	/// <summary>
	/// A section's text for a number in General, with its literals: a section with <c>General</c>, one of literals alone
	/// (<c>"zero"</c>), one of brackets alone (<c>[Red]</c>), and, in its widest form, one with <c>[DBNum]</c>.
	/// </summary>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string RenderGeneral(Section section, double abs, bool minus, bool widest)
	{
		var builder = new StringBuilder();
		var general = section.HasGeneral || section.Placeholders > 0 || section.Tokens.Count == 0 ? General(abs) : "";
		if (widest)
		{
			general = AsDigits(general);
		}

		if (section.Tokens.Count == 0)
		{
			return (minus && abs != 0 ? "-" : "") + general;
		}

		foreach (var token in section.Tokens)
		{
			if (token.Kind is TokenKind.General or TokenKind.Digit)
			{
				builder.Append(general);
				general = "";
			}
			else
			{
				AppendLiteral(builder, token, widest);
			}
		}

		return (minus && abs != 0 ? "-" : "") + builder;
	}

	/// <summary>
	/// A fraction section's estimated text: the whole number's digits, rounded, at least one; and a digit for each
	/// placeholder of the numerator and the denominator, with the literals.
	/// </summary>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string RenderFraction(Section section, double abs, bool minus, bool widest)
	{
		var builder = new StringBuilder();
		var whole = Digits.Of(abs).Round(0).Integer();
		if (whole.Length == 0)
		{
			whole = "0";
		}

		var wholeShown = false;
		foreach (var token in section.Tokens)
		{
			if (token.Kind != TokenKind.Digit)
			{
				AppendLiteral(builder, token, widest);

				continue;
			}

			// The whole number's digits, at its first placeholder, or before the numerator's when it has none.
			if (!wholeShown)
			{
				builder.Append(whole);
				wholeShown = true;
			}

			if (token.Role == DigitRole.Fraction)
			{
				builder.Append('0');
			}
		}

		return (minus && abs != 0 ? "-" : "") + builder;
	}

	/// <summary>A number section's text.</summary>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first, unless it rounds to zero.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string RenderNumber(Section section, double abs, bool minus, bool widest)
	{
		var digits = Digits.Of(abs).Scale((2 * section.Percents) - (3 * section.Scale));
		var exponent = 0;
		if (section.HasExponent)
		{
			var step = Math.Max(section.IntegerPlaceholders, 1);
			if (!digits.IsZero)
			{
				exponent = FloorDivide(digits.Exponent, step) * step;
				var mantissa = digits.Scale(-exponent).Round(section.FractionPlaceholders);
				if (mantissa.IntegerLength > step)
				{
					exponent += step;
					mantissa = digits.Scale(-exponent).Round(section.FractionPlaceholders);
				}

				digits = mantissa;
			}
		}
		else
		{
			digits = digits.Round(section.FractionPlaceholders);
		}

		var integer = digits.Integer();
		var fraction = digits.Fraction(section.FractionPlaceholders);
		var lastFractionDigit = fraction.TrimEnd('0').Length - 1;
		var integerTexts = IntegerTexts(section, integer, section.HasExponent && digits.IsZero, widest);
		var builder = new StringBuilder();
		var integerIndex = 0;
		var fractionIndex = 0;
		var exponentShown = false;
		foreach (var token in section.Tokens)
		{
			switch (token.Kind)
			{
				case TokenKind.Digit when token.Role == DigitRole.Integer:
					builder.Append(integerTexts[integerIndex++]);

					break;
				case TokenKind.Digit when token.Role == DigitRole.Fraction:
					if (fractionIndex <= lastFractionDigit)
					{
						builder.Append(fraction[fractionIndex]);
					}
					else
					{
						AppendEmpty(builder, token.Char, widest);
					}

					fractionIndex++;

					break;
				case TokenKind.Digit:
					// The exponent's digits, all at its first placeholder.
					if (!exponentShown)
					{
						builder.Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture).PadLeft(section.ExponentZeros, '0'));
						exponentShown = true;
					}

					break;
				case TokenKind.Point:
					// Without integer placeholders, the integer part shows before the point all the same.
					builder.Append(section.IntegerPlaceholders == 0 ? integer : "");
					builder.Append('.');

					break;
				case TokenKind.Exponent:
					builder.Append('E');
					builder.Append(exponent < 0 ? "-" : token.Char == '+' ? "+" : "");

					break;
				case TokenKind.Percent:
					builder.Append('%');

					break;
				case TokenKind.Comma when token.Role == DigitRole.None:
					builder.Append(',');

					break;
				case TokenKind.Comma:
					break;
				default:
					AppendLiteral(builder, token, widest);

					break;
			}
		}

		return (minus && !digits.IsZero ? "-" : "") + builder;
	}

	/// <summary>
	/// The text of each integer placeholder: the digits from the right, the leftmost one the digits left over; or, when the
	/// section groups digits, the whole grouped number at the first.
	/// </summary>
	/// <param name="section">The section.</param>
	/// <param name="integer">The integer part's digits, empty when it is 0.</param>
	/// <param name="zeroExponent">Whether the number is 0 in scientific notation, which shows every placeholder as <c>0</c>.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The texts, in the placeholders' order.</returns>
	private static string[] IntegerTexts(Section section, string integer, bool zeroExponent, bool widest)
	{
		var count = section.IntegerPlaceholders;
		var texts = new string[count];
		var kinds = section.IntegerKinds;
		if (section.Grouping)
		{
			// Padded to the placeholders from the first 0, grouped by three, and spaces for the ? before them.
			var zeros = kinds.IndexOf('0');
			var padded = zeros < 0 ? integer : integer.PadLeft(count - zeros, '0');
			var grouped = new StringBuilder();
			for (var i = 0; i < padded.Length; i++)
			{
				if (i > 0 && (padded.Length - i) % 3 == 0)
				{
					grouped.Append(widest ? '.' : ',');
				}

				grouped.Append(padded[i]);
			}

			for (var i = padded.Length; i < count; i++)
			{
				if (kinds[count - 1 - i] == '?')
				{
					grouped.Insert(0, widest ? "0" : " ");
				}
			}

			texts[0] = grouped.ToString();
			for (var i = 1; i < count; i++)
			{
				texts[i] = "";
			}

			return texts;
		}

		for (var j = 0; j < count; j++)
		{
			var fromRight = count - 1 - j;
			if (zeroExponent)
			{
				texts[j] = "0";
			}
			else if (fromRight < integer.Length)
			{
				var digit = integer[integer.Length - 1 - fromRight].ToString();
				texts[j] = j == 0 && integer.Length > count ? integer.Substring(0, integer.Length - count) + digit : digit;
			}
			else
			{
				texts[j] = kinds[j] == '0' ? "0" : kinds[j] == '?' ? (widest ? "0" : " ") : "";
			}
		}

		return texts;
	}

	/// <summary>A date or time section's text.</summary>
	/// <param name="section">The section.</param>
	/// <param name="value">The serial, in days.</param>
	/// <param name="widest">Whether to give the widest form: names as their widest, and days, months and hours in two digits.</param>
	/// <returns>The text, or nothing for a serial Excel shows as <c>########</c>.</returns>
	private static string RenderDate(Section section, double value, bool widest)
	{
		// Elapsed time with nothing else of a date or time but literals counts past Excel's last date ([h] shows 2,958,466
		// days as 71003184), and any other token stops there ([h]:mm shows ########): checked through COM on 2026-11-11.
		var elapsedAlone = IsElapsedAlone(section);
		if (value < 0 || (value >= PastLastDate && !elapsedAlone))
		{
			return "";
		}

		if (elapsedAlone && value >= PastLastDate)
		{
			return RenderElapsed(section, value);
		}

		// To the millisecond; then whole seconds round unless the section shows fractions of a second, and fractions of a
		// second and minutes are cut (09:05:07.250 shows 07.2 in ss.0, 23:59:59 shows 23:59 in hh:mm, and 23:59:59.6 shows
		// the next day in yyyy-mm-dd and 00:00 in hh:mm: step 7's probe).
		var milliseconds = (long)Math.Round(value * Serial.MillisecondsPerDay, MidpointRounding.AwayFromZero);
		if (section.SubSecondDigits == 0)
		{
			milliseconds = (milliseconds + 500) / 1000 * 1000;
		}

		// A serial that rounds up to the last one shows as ######## too: 9999-12-31 23:59:59.999 in any format without
		// fractions of a second (step 6's durations.txt, 2,958,465.9999999884 in [h]:mm:ss; step 7's, in yyyy and d).
		if (milliseconds >= Serial.PastLast && !elapsedAlone)
		{
			return "";
		}

		var days = milliseconds / Serial.MillisecondsPerDay;
		var ofDay = milliseconds % Serial.MillisecondsPerDay;
		var (year, month, day, weekday) = elapsedAlone ? (0, 0, 0, 0) : DateOf((int)days);
		var hour = (int)(ofDay / 3_600_000);
		var minute = (int)(ofDay / 60_000 % 60);
		var second = (int)(ofDay / 1000 % 60);
		var names = section.Names;
		var builder = new StringBuilder();
		foreach (var token in section.Tokens)
		{
			switch (token.Kind)
			{
				case TokenKind.Year:
					builder.Append(token.Count <= 2 ? (year % 100).ToString("00", CultureInfo.InvariantCulture) : year.ToString("0000", CultureInfo.InvariantCulture));

					break;
				case TokenKind.EraYear:
					builder.Append(year.ToString("0000", CultureInfo.InvariantCulture));

					break;
				case TokenKind.BuddhistYear:
					builder.Append(token.Count <= 2 ? ((year + 543) % 100).ToString("00", CultureInfo.InvariantCulture) : (year + 543).ToString(CultureInfo.InvariantCulture));

					break;
				case TokenKind.Month:
					builder.Append(token.Count switch
					{
						<= 2 => TwoDigits(month, token.Count, widest),
						3 => Name(names.AbbreviatedMonthNames, month - 1, widest, 12),
						5 => Name(Array.ConvertAll(names.MonthNames, n => n.Length > 0 ? n.Substring(0, 1) : n), month - 1, widest, 12),
						_ => Name(names.MonthNames, month - 1, widest, 12),
					});

					break;
				case TokenKind.Day:
					builder.Append(token.Count switch
					{
						<= 2 => TwoDigits(day, token.Count, widest),
						3 => Name(names.AbbreviatedDayNames, weekday, widest, 7),
						_ => Name(names.DayNames, weekday, widest, 7),
					});

					break;
				case TokenKind.JapaneseDay:
					// The day of the week's name, as ddd and dddd show it: Excel shows English names without a locale.
					builder.Append(token.Count == 3 ? Name(names.AbbreviatedDayNames, weekday, widest, 7) : Name(names.DayNames, weekday, widest, 7));

					break;
				case TokenKind.Hour:
					var shownHour = section.TwelveHour ? (hour % 12 == 0 ? 12 : hour % 12) : hour;
					builder.Append(TwoDigits(shownHour, token.Count, widest));

					break;
				case TokenKind.Minute:
					builder.Append(TwoDigits(minute, token.Count, widest));

					break;
				case TokenKind.Second:
					builder.Append(TwoDigits(second, token.Count, widest));

					break;
				case TokenKind.SubSecond:
					builder.Append((ofDay % 1000).ToString("000", CultureInfo.InvariantCulture).Substring(0, Math.Min(token.Count, 3)).PadRight(token.Count, '0'));

					break;
				case TokenKind.ElapsedHour:
					builder.Append(((token.OfDay ? ofDay : milliseconds) / 3_600_000).ToString(CultureInfo.InvariantCulture).PadLeft(token.Count, '0'));

					break;
				case TokenKind.ElapsedMinute:
					builder.Append(((token.OfDay ? ofDay : milliseconds) / 60_000).ToString(CultureInfo.InvariantCulture).PadLeft(token.Count, '0'));

					break;
				case TokenKind.ElapsedSecond:
					builder.Append(((token.OfDay ? ofDay : milliseconds) / 1000).ToString(CultureInfo.InvariantCulture).PadLeft(token.Count, '0'));

					break;
				case TokenKind.AmPm:
					var am = token.Count == 2 ? "AM" : "A";
					var pm = token.Count == 2 ? "PM" : "P";
					if (char.IsLower(token.Char))
					{
						am = am.ToLowerInvariant();
						pm = pm.ToLowerInvariant();
					}

					// The wider of the two: AM in capitals, p in lower case.
					builder.Append(widest ? (ColumnWidths.TextPixels(am.AsSpan()) >= ColumnWidths.TextPixels(pm.AsSpan()) ? am : pm) : hour < 12 ? am : pm);

					break;
				case TokenKind.Era:
					// An era's name, which an East Asian locale shows (令和) and others don't: one or two East Asian characters.
					builder.Append(Wide, token.Count >= 3 ? 2 : 1);

					break;
				default:
					AppendLiteral(builder, token, widest);

					break;
			}
		}

		return builder.ToString();
	}

	/// <summary>Whether a section's only date or time token is one elapsed time, beside literals.</summary>
	/// <param name="section">The section, which has a date or time.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsElapsedAlone(Section section)
	{
		var elapsed = 0;
		foreach (var token in section.Tokens)
		{
			switch (token.Kind)
			{
				case TokenKind.ElapsedHour or TokenKind.ElapsedMinute or TokenKind.ElapsedSecond:
					elapsed++;

					break;
				case TokenKind.Year or TokenKind.EraYear or TokenKind.BuddhistYear or TokenKind.Month or TokenKind.Day or TokenKind.JapaneseDay or
					TokenKind.Hour or TokenKind.Minute or TokenKind.Second or TokenKind.SubSecond or TokenKind.AmPm or TokenKind.Era:
					return false;
			}
		}

		return elapsed == 1;
	}

	/// <summary>
	/// The text of a section of elapsed time alone for a serial past Excel's last date: its hours, minutes or seconds in
	/// whole numbers, rounded to the second, with its literals; in the double's digits, past a long's milliseconds.
	/// </summary>
	/// <param name="section">The section, whose only date or time token is one elapsed time.</param>
	/// <param name="value">The serial, 2,958,466 days or more.</param>
	/// <returns>The text.</returns>
	private static string RenderElapsed(Section section, double value)
	{
		var seconds = Math.Round(value * 86_400, MidpointRounding.AwayFromZero);
		var builder = new StringBuilder();
		foreach (var token in section.Tokens)
		{
			var units = token.Kind switch
			{
				TokenKind.ElapsedHour => Math.Floor(seconds / 3600),
				TokenKind.ElapsedMinute => Math.Floor(seconds / 60),
				TokenKind.ElapsedSecond => seconds,
				_ => -1,
			};
			if (units < 0)
			{
				AppendLiteral(builder, token, widest: false);

				continue;
			}

			builder.Append(units.ToString("0", CultureInfo.InvariantCulture).PadLeft(token.Count, '0'));
		}

		return builder.ToString();
	}

	/// <summary>
	/// A day of the 1900 date system: 60 is Excel's 1900-02-29, which never existed, and the days before it are a day of
	/// the week earlier than they were, as Excel counts them (it shows 1900-01-01 as a Sunday).
	/// </summary>
	/// <param name="days">The serial's whole days, 0 to 2,958,465.</param>
	/// <returns>The year, month, day and day of the week (0 for Sunday).</returns>
	private static (int Year, int Month, int Day, int Weekday) DateOf(int days)
	{
		if (days == 60)
		{
			return (1900, 2, 29, 3);
		}

		if (days < 60)
		{
			var early = new DateTime(1899, 12, 31).AddDays(days);

			return (early.Year, early.Month, early.Day, ((int)early.DayOfWeek + 6) % 7);
		}

		var date = new DateTime(1899, 12, 30).AddDays(days);

		return (date.Year, date.Month, date.Day, (int)date.DayOfWeek);
	}

	/// <summary>A day, month or time's number: in two digits for the widest form, else padded to the token's length.</summary>
	/// <param name="value">The number.</param>
	/// <param name="count">The token's letters: 1 for no padding.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string TwoDigits(int value, int count, bool widest) =>
		value.ToString(widest || count >= 2 ? "00" : "0", CultureInfo.InvariantCulture);

	/// <summary>A month's or a day's name, or the widest of them.</summary>
	/// <param name="names">The names.</param>
	/// <param name="index">The month's (from 0) or the day of the week's.</param>
	/// <param name="widest">Whether to give the widest.</param>
	/// <param name="count">How many names there are: 12 months or 7 days (a month's list has a 13th, empty).</param>
	/// <returns>The name.</returns>
	private static string Name(string[] names, int index, bool widest, int count)
	{
		if (!widest)
		{
			return names[index];
		}

		var name = "";
		var pixels = -1.0;
		for (var i = 0; i < count; i++)
		{
			var width = ColumnWidths.TextPixels(names[i].AsSpan());
			if (width > pixels)
			{
				name = names[i];
				pixels = width;
			}
		}

		return name;
	}

	/// <summary>A text section's text: its literals, and the text at each <c>@</c>.</summary>
	/// <param name="section">The section.</param>
	/// <param name="value">The text.</param>
	/// <param name="widest">Whether to give the widest form.</param>
	/// <returns>The text.</returns>
	private static string RenderText(Section section, string value, bool widest)
	{
		var builder = new StringBuilder();
		foreach (var token in section.Tokens)
		{
			if (token.Kind == TokenKind.At)
			{
				builder.Append(value);
			}
			else
			{
				AppendLiteral(builder, token, widest);
			}
		}

		return builder.ToString();
	}

	/// <summary>
	/// Appends a token that shows as itself: a literal, a space the width of a character (<c>_x</c>), a fill (<c>*x</c>,
	/// which shows as many times as the column has room for, and auto-fit counts as nothing), and the characters of tokens
	/// out of place.
	/// </summary>
	/// <param name="builder">The text.</param>
	/// <param name="token">The token.</param>
	/// <param name="widest">Whether this is the widest form: a space shows the character whose width it leaves.</param>
	private static void AppendLiteral(StringBuilder builder, Token token, bool widest)
	{
		switch (token.Kind)
		{
			case TokenKind.Literal:
				builder.Append(token.Text);

				break;
			case TokenKind.Space:
				builder.Append(widest ? token.Char : ' ');

				break;
			case TokenKind.Fill:
				break;
			case TokenKind.Point:
				builder.Append('.');

				break;
			case TokenKind.Comma:
				builder.Append(',');

				break;
			case TokenKind.Percent:
				builder.Append('%');

				break;
			case TokenKind.Digit:
				builder.Append(token.Char == '0' ? "0" : "");

				break;
		}
	}

	/// <summary>Appends what a placeholder without a digit shows: <c>0</c>, nothing (<c>#</c>), or a space as wide as a digit (<c>?</c>).</summary>
	/// <param name="builder">The text.</param>
	/// <param name="placeholder">The placeholder.</param>
	/// <param name="widest">Whether this is the widest form, where a space shows as a digit.</param>
	private static void AppendEmpty(StringBuilder builder, char placeholder, bool widest)
	{
		if (placeholder == '0')
		{
			builder.Append('0');
		}
		else if (placeholder == '?')
		{
			builder.Append(widest ? '0' : ' ');
		}
	}

	/// <summary>Divides, rounding toward negative infinity.</summary>
	/// <param name="value">The dividend.</param>
	/// <param name="divisor">The divisor, positive.</param>
	/// <returns>The quotient.</returns>
	private static int FloorDivide(int value, int divisor) => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

	/// <summary>Reads a code into its sections' tokens.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The sections, one or more.</returns>
	private static List<Section> Parse(string code)
	{
		var sections = new List<Section>();
		var section = new Section();
		for (var i = 0; i < code.Length; i++)
		{
			var c = code[i];
			switch (c)
			{
				case ';':
					sections.Add(section.Finish());
					section = new Section();

					continue;
				case '"':
					var close = code.IndexOf('"', i + 1);
					if (close < 0)
					{
						close = code.Length;
					}

					section.Add(new Token(TokenKind.Literal, code.Substring(i + 1, close - i - 1)));
					i = close;

					continue;
				case '\\' when i + 1 < code.Length:
					section.Add(new Token(TokenKind.Literal, code[++i].ToString()));

					continue;
				case '_' when i + 1 < code.Length:
					section.Add(new Token(TokenKind.Space, Char: code[++i]));

					continue;
				case '*' when i + 1 < code.Length:
					section.Add(new Token(TokenKind.Fill, Char: code[++i]));

					continue;
				case '[':
					var end = code.IndexOf(']', i + 1);
					if (end < 0)
					{
						end = code.Length;
					}

					section.Bracket(code.Substring(i + 1, end - i - 1));
					i = end;

					continue;
				case '0' or '#' or '?':
					section.Add(new Token(TokenKind.Digit, Char: c));

					continue;
				case '.':
					section.Add(new Token(TokenKind.Point));

					continue;
				case ',':
					section.Add(new Token(TokenKind.Comma));

					continue;
				case '%':
					section.Add(new Token(TokenKind.Percent));

					continue;
				case '@':
					section.Add(new Token(TokenKind.At));

					continue;
				case '/':
					section.Add(new Token(TokenKind.Slash, "/"));

					continue;
			}

			if ((uint)((c | 0x20) - 'a') <= 'z' - 'a')
			{
				i = section.Letters(code, i);

				continue;
			}

			section.Add(new Token(TokenKind.Literal, c.ToString()));
		}

		sections.Add(section.Finish());

		return sections;
	}

	/// <summary>Whether <paramref name="code"/> has <paramref name="word"/> at <paramref name="index"/>, in any case.</summary>
	/// <param name="code">The code.</param>
	/// <param name="index">Where to look.</param>
	/// <param name="word">The word, in lower case.</param>
	/// <returns>Whether the word is there.</returns>
	private static bool Matches(string code, int index, string word) =>
		index + word.Length <= code.Length && string.Compare(code, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;

	/// <summary>What a token of a code is.</summary>
	private enum TokenKind
	{
		/// <summary>Text shown as it is: quoted, escaped, or a character that needs neither.</summary>
		Literal,

		/// <summary>A <c>/</c> outside quotes: a fraction's bar between placeholders, else itself.</summary>
		Slash,

		/// <summary>A digit placeholder, <c>0</c>, <c>#</c> or <c>?</c>.</summary>
		Digit,

		/// <summary>A decimal point.</summary>
		Point,

		/// <summary>A comma: grouping, scaling, or itself.</summary>
		Comma,

		/// <summary>A percent sign.</summary>
		Percent,

		/// <summary>An exponent, <c>E+</c> or <c>E-</c>.</summary>
		Exponent,

		/// <summary>The text, <c>@</c>.</summary>
		At,

		/// <summary><c>General</c>.</summary>
		General,

		/// <summary>A space the width of a character, <c>_x</c>.</summary>
		Space,

		/// <summary>A character repeated to fill the cell, <c>*x</c>.</summary>
		Fill,

		/// <summary>A year, <c>yy</c> or <c>yyyy</c>.</summary>
		Year,

		/// <summary>A month: its number, or its name.</summary>
		Month,

		/// <summary>A minute: <c>m</c> or <c>mm</c> after an hour or before a second.</summary>
		Minute,

		/// <summary>A day: its number, or its day of the week's name.</summary>
		Day,

		/// <summary>An hour.</summary>
		Hour,

		/// <summary>A second.</summary>
		Second,

		/// <summary>Fractions of a second, the <c>0</c>s after a <c>.</c> in a date or time.</summary>
		SubSecond,

		/// <summary>Elapsed hours, <c>[h]</c>.</summary>
		ElapsedHour,

		/// <summary>Elapsed minutes, <c>[m]</c>.</summary>
		ElapsedMinute,

		/// <summary>Elapsed seconds, <c>[s]</c>.</summary>
		ElapsedSecond,

		/// <summary><c>AM/PM</c> or <c>A/P</c>.</summary>
		AmPm,

		/// <summary>An era's year, <c>e</c>.</summary>
		EraYear,

		/// <summary>An era's name, <c>g</c>.</summary>
		Era,

		/// <summary>A Buddhist year, <c>b</c>.</summary>
		BuddhistYear,

		/// <summary>A Japanese day of the week, <c>aaa</c>.</summary>
		JapaneseDay,
	}

	/// <summary>What a digit placeholder or a comma does in a number section.</summary>
	private enum DigitRole
	{
		/// <summary>Neither: a comma that shows as itself.</summary>
		None,

		/// <summary>An integer placeholder, or a comma that groups its digits.</summary>
		Integer,

		/// <summary>A fraction placeholder, after the decimal point.</summary>
		Fraction,

		/// <summary>An exponent's placeholder.</summary>
		Exponent,

		/// <summary>A comma that scales the number by 1,000.</summary>
		Scale,
	}

	/// <summary>A token of a code.</summary>
	/// <param name="Kind">What it is.</param>
	/// <param name="Text">A literal's text.</param>
	/// <param name="Char">A placeholder's character, an exponent's sign, a space's or fill's character, or <c>A</c>'s case of <c>AM/PM</c>.</param>
	/// <param name="Count">A date token's letters.</param>
	/// <param name="Role">What a placeholder or a comma does.</param>
	/// <param name="OfDay">Whether an elapsed time comes after a date part, where it is the time of day in its unit.</param>
	private readonly record struct Token(TokenKind Kind, string Text = "", char Char = '\0', int Count = 0, DigitRole Role = DigitRole.None, bool OfDay = false);

	/// <summary>A section of a code: its tokens, and what they make of it.</summary>
	private sealed class Section
	{
		// The digit placeholders read so far, for an exponent's test, as FormatCheck makes it.
		private int digitsRead;

		/// <summary>The tokens, in order.</summary>
		public List<Token> Tokens { get; } = [];

		/// <summary>Whether the section has a date or time token.</summary>
		public bool IsDate { get; private set; }

		/// <summary>Whether it has <c>@</c>.</summary>
		public bool HasAt { get; private set; }

		/// <summary>How many <c>@</c> it has.</summary>
		public int Ats { get; private set; }

		/// <summary>Whether it has <c>General</c>.</summary>
		public bool HasGeneral { get; private set; }

		/// <summary>Whether it has <c>[DBNum1]</c> to <c>[DBNum3]</c>.</summary>
		public bool DBNum { get; private set; }

		/// <summary>Whether it has a condition.</summary>
		public bool Condition { get; private set; }

		/// <summary>Whether the section has a color.</summary>
		public bool Color { get; private set; }

		/// <summary>The names of its months and days: English, or a locale's (<c>[$-407]</c>).</summary>
		public DateTimeFormatInfo Names { get; private set; } = CultureInfo.InvariantCulture.DateTimeFormat;

		/// <summary>How many digit placeholders it has.</summary>
		public int Placeholders { get; private set; }

		/// <summary>How many integer placeholders it has.</summary>
		public int IntegerPlaceholders { get; private set; }

		/// <summary>The integer placeholders' characters, in order.</summary>
		public string IntegerKinds { get; private set; } = "";

		/// <summary>How many fraction placeholders it has.</summary>
		public int FractionPlaceholders { get; private set; }

		/// <summary>The zeros of its exponent's placeholders: the fewest digits the exponent shows.</summary>
		public int ExponentZeros { get; private set; }

		/// <summary>Whether it has an exponent.</summary>
		public bool HasExponent { get; private set; }

		/// <summary>Whether it groups its integer digits by three.</summary>
		public bool Grouping { get; private set; }

		/// <summary>How many commas scale it by 1,000.</summary>
		public int Scale { get; private set; }

		/// <summary>How many percent signs it has.</summary>
		public int Percents { get; private set; }

		/// <summary>Whether it is a fraction.</summary>
		public bool IsFraction { get; private set; }

		/// <summary>Whether a fraction has a whole number before its numerator.</summary>
		public bool FractionHasInteger { get; private set; }

		/// <summary>Whether a time is in 12 hours, with <c>AM/PM</c> or <c>A/P</c>.</summary>
		public bool TwelveHour { get; private set; }

		/// <summary>Whether a time shows seconds, to which it rounds.</summary>
		public bool ShowsSeconds { get; private set; }

		/// <summary>The digits of fractions of a second it shows.</summary>
		public int SubSecondDigits { get; private set; }

		/// <summary>Adds a token.</summary>
		/// <param name="token">The token.</param>
		public void Add(Token token)
		{
			Tokens.Add(token);
			if (token.Kind == TokenKind.At)
			{
				HasAt = true;
				Ats++;
			}
			else if (token.Kind == TokenKind.Digit)
			{
				digitsRead++;
			}
		}

		/// <summary>Reads a bracket's content: elapsed time, a currency or locale, <c>DBNum</c>, a condition, or a color.</summary>
		/// <param name="content">The text between the brackets.</param>
		public void Bracket(string content)
		{
			if (IsElapsed(content))
			{
				var kind = (content[0] | 0x20) switch
				{
					'h' => TokenKind.ElapsedHour,
					'm' => TokenKind.ElapsedMinute,
					_ => TokenKind.ElapsedSecond,
				};
				Add(new Token(kind, Count: content.Length));
				IsDate = true;

				return;
			}

			if (content.StartsWith("$", StringComparison.Ordinal))
			{
				var dash = content.IndexOf('-');
				var symbol = dash < 0 ? content.Substring(1) : content.Substring(1, dash - 1);
				if (symbol.Length > 0)
				{
					Add(new Token(TokenKind.Literal, symbol));
				}

				if (dash >= 0 && int.TryParse(content.Substring(dash + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var locale))
				{
					Names = NamesOf(locale & 0xFFFF);
				}

				return;
			}

			if (content.StartsWith("DBNum", StringComparison.OrdinalIgnoreCase))
			{
				DBNum = true;
			}
			else if (content.Length > 0 && content[0] is '<' or '>' or '=')
			{
				Condition = true;
			}
			else
			{
				Color = true;
			}
		}

		/// <summary>Reads the letters at a position, as <see cref="FormatCheck"/> does.</summary>
		/// <param name="code">The code.</param>
		/// <param name="i">The letter's position.</param>
		/// <returns>The position of the token's last character.</returns>
		public int Letters(string code, int i)
		{
			var c = code[i];
			var lower = (char)(c | 0x20);
			if (lower == 'g' && Matches(code, i, "general"))
			{
				Add(new Token(TokenKind.General));
				HasGeneral = true;

				return i + 6;
			}

			if (lower == 'e' && i + 1 < code.Length && code[i + 1] is '+' or '-' &&
				((digitsRead > 0 && !IsDate) || (i + 2 < code.Length && code[i + 2] is '0' or '#' or '?')))
			{
				Add(new Token(TokenKind.Exponent, Char: code[i + 1]));

				return i + 1;
			}

			if (lower == 'a' && Matches(code, i, "am/pm"))
			{
				Add(new Token(TokenKind.AmPm, Char: c, Count: 2));
				IsDate = true;

				return i + 4;
			}

			if (lower == 'a' && Matches(code, i, "a/p"))
			{
				Add(new Token(TokenKind.AmPm, Char: c, Count: 1));
				IsDate = true;

				return i + 2;
			}

			var run = 1;
			while (i + run < code.Length && (code[i + run] | 0x20) == lower)
			{
				run++;
			}

			var kind = lower switch
			{
				'y' => TokenKind.Year,
				'm' => TokenKind.Month,
				'd' => TokenKind.Day,
				'h' => TokenKind.Hour,
				's' => TokenKind.Second,
				'e' => TokenKind.EraYear,
				'g' => TokenKind.Era,
				'b' => TokenKind.BuddhistYear,
				'a' => TokenKind.JapaneseDay,
				_ => TokenKind.Literal,
			};
			if (kind == TokenKind.Literal)
			{
				Add(new Token(TokenKind.Literal, c.ToString()));

				return i;
			}

			Add(new Token(kind, Count: run));
			IsDate = true;

			return i + run - 1;
		}

		/// <summary>Works out what the tokens make of the section, once it is read.</summary>
		/// <returns>The section.</returns>
		public Section Finish()
		{
			if (IsDate)
			{
				FinishDate();
			}
			else
			{
				FinishNumber();
			}

			return this;
		}

		/// <summary>
		/// A date section: <c>m</c> and <c>mm</c> after an hour or before a second are minutes, the <c>0</c>s after a
		/// <c>.</c> are fractions of a second, and seconds round to what it shows.
		/// </summary>
		private void FinishDate()
		{
			for (var i = 0; i < Tokens.Count; i++)
			{
				var token = Tokens[i];
				if (token.Kind == TokenKind.Month && token.Count <= 2 && (Previous(i) is TokenKind.Hour or TokenKind.ElapsedHour || Next(i) is TokenKind.Second or TokenKind.ElapsedSecond))
				{
					Tokens[i] = token with { Kind = TokenKind.Minute };
				}
				else if (token.Kind == TokenKind.Point && i + 1 < Tokens.Count && Tokens[i + 1] is { Kind: TokenKind.Digit, Char: '0' })
				{
					var zeros = 0;
					while (i + 1 + zeros < Tokens.Count && Tokens[i + 1 + zeros] is { Kind: TokenKind.Digit, Char: '0' })
					{
						zeros++;
					}

					Tokens[i] = new Token(TokenKind.Literal, ".");
					Tokens[i + 1] = new Token(TokenKind.SubSecond, Count: zeros);
					Tokens.RemoveRange(i + 2, zeros - 1);
					SubSecondDigits = Math.Max(SubSecondDigits, zeros);
					ShowsSeconds = true;
				}
				else if (token.Kind == TokenKind.Point)
				{
					Tokens[i] = new Token(TokenKind.Literal, ".");
				}
				else if (token.Kind == TokenKind.Slash)
				{
					Tokens[i] = new Token(TokenKind.Literal, "/");
				}
				else if (token.Kind == TokenKind.AmPm)
				{
					TwelveHour = true;
				}

				ShowsSeconds |= token.Kind is TokenKind.Second or TokenKind.ElapsedSecond;
			}

			// Elapsed time after a date part is the time of day in its unit: yyyy-mm-dd [h]:mm shows the hour of the day (Phase
			// 4's answers), d[ss] the seconds since midnight, and mmmm[mm] the minutes; before one, it is the whole time ([ss]d
			// shows every second, then the day): checked through COM on 2026-11-11, after step 7's fuzzers.
			var afterDate = false;
			for (var i = 0; i < Tokens.Count; i++)
			{
				var kind = Tokens[i].Kind;
				afterDate |= kind is TokenKind.Year or TokenKind.Month or TokenKind.Day or TokenKind.EraYear or TokenKind.BuddhistYear or TokenKind.JapaneseDay;
				if (afterDate && kind is TokenKind.ElapsedHour or TokenKind.ElapsedMinute or TokenKind.ElapsedSecond)
				{
					Tokens[i] = Tokens[i] with { OfDay = true };
				}
			}
		}

		/// <summary>The kind of the date token before a position, past literals.</summary>
		/// <param name="i">The position.</param>
		/// <returns>The kind, or <see cref="TokenKind.Literal"/> for none.</returns>
		private TokenKind Previous(int i)
		{
			for (var j = i - 1; j >= 0; j--)
			{
				if (Tokens[j].Kind is not (TokenKind.Literal or TokenKind.Slash or TokenKind.Point or TokenKind.Space or TokenKind.Fill or TokenKind.Comma))
				{
					return Tokens[j].Kind;
				}
			}

			return TokenKind.Literal;
		}

		/// <summary>The kind of the date token after a position, past literals.</summary>
		/// <param name="i">The position.</param>
		/// <returns>The kind, or <see cref="TokenKind.Literal"/> for none.</returns>
		private TokenKind Next(int i)
		{
			for (var j = i + 1; j < Tokens.Count; j++)
			{
				if (Tokens[j].Kind is not (TokenKind.Literal or TokenKind.Slash or TokenKind.Point or TokenKind.Space or TokenKind.Fill or TokenKind.Comma))
				{
					return Tokens[j].Kind;
				}
			}

			return TokenKind.Literal;
		}

		/// <summary>
		/// A number section: each placeholder's role (integer, fraction or exponent), each comma's (grouping, scaling or
		/// itself), the percent signs, and whether it is a fraction.
		/// </summary>
		private void FinishNumber()
		{
			if (FinishFraction())
			{
				return;
			}

			var exponentAt = Tokens.FindIndex(t => t.Kind == TokenKind.Exponent);
			var mantissaEnd = exponentAt < 0 ? Tokens.Count : exponentAt;
			var pointAt = Tokens.FindIndex(0, mantissaEnd, t => t.Kind == TokenKind.Point);
			var integerEnd = pointAt < 0 ? mantissaEnd : pointAt;
			var firstInteger = -1;
			var lastInteger = -1;
			var lastMantissa = -1;
			var kinds = new StringBuilder();
			for (var i = 0; i < Tokens.Count; i++)
			{
				var token = Tokens[i];
				if (token.Kind == TokenKind.Percent)
				{
					Percents++;
				}

				if (token.Kind != TokenKind.Digit)
				{
					continue;
				}

				Placeholders++;
				if (i < integerEnd)
				{
					Tokens[i] = token with { Role = DigitRole.Integer };
					IntegerPlaceholders++;
					kinds.Append(token.Char);
					firstInteger = firstInteger < 0 ? i : firstInteger;
					lastInteger = i;
					lastMantissa = i;
				}
				else if (i < mantissaEnd)
				{
					Tokens[i] = token with { Role = DigitRole.Fraction };
					FractionPlaceholders++;
					lastMantissa = i;
				}
				else
				{
					Tokens[i] = token with { Role = DigitRole.Exponent };
					ExponentZeros += token.Char == '0' ? 1 : 0;
				}
			}

			IntegerKinds = kinds.ToString();
			HasExponent = exponentAt >= 0;
			ExponentZeros = Math.Max(ExponentZeros, 1);
			for (var i = 0; i < mantissaEnd; i++)
			{
				if (Tokens[i].Kind != TokenKind.Comma)
				{
					continue;
				}

				if (i > firstInteger && i < lastInteger)
				{
					Tokens[i] = Tokens[i] with { Role = DigitRole.Integer };
					Grouping = true;
				}
				else if (lastMantissa >= 0 && i > lastMantissa && Tokens.FindIndex(lastMantissa + 1, i - lastMantissa - 1, t => t.Kind != TokenKind.Comma) < 0)
				{
					Tokens[i] = Tokens[i] with { Role = DigitRole.Scale };
					Scale++;
				}
			}

			SlashesToLiterals();
		}

		/// <summary>
		/// Whether the section is a fraction: a <c>/</c> with a placeholder before it and a placeholder or a digit after it.
		/// Its placeholders before the numerator's are the whole number's (integer), and the numerator's and the
		/// denominator's are the fraction's.
		/// </summary>
		/// <returns>Whether it is.</returns>
		private bool FinishFraction()
		{
			var bar = -1;
			for (var i = 1; i + 1 < Tokens.Count && bar < 0; i++)
			{
				var after = Tokens[i + 1];
				if (Tokens[i].Kind == TokenKind.Slash && Tokens[i - 1].Kind == TokenKind.Digit &&
					(after.Kind == TokenKind.Digit || (after.Kind == TokenKind.Literal && after.Text.Length == 1 && after.Text[0] is >= '1' and <= '9')))
				{
					bar = i;
				}
			}

			if (bar < 0)
			{
				return false;
			}

			IsFraction = true;
			var numerator = bar - 1;
			while (numerator > 0 && Tokens[numerator - 1].Kind == TokenKind.Digit)
			{
				numerator--;
			}

			for (var i = 0; i < Tokens.Count; i++)
			{
				if (Tokens[i].Kind == TokenKind.Digit)
				{
					Placeholders++;
					Tokens[i] = Tokens[i] with { Role = i < numerator ? DigitRole.Integer : DigitRole.Fraction };
				}
			}

			SlashesToLiterals();

			return true;
		}

		/// <summary>Makes every <c>/</c> a literal, once a fraction's has been found.</summary>
		private void SlashesToLiterals()
		{
			for (var i = 0; i < Tokens.Count; i++)
			{
				if (Tokens[i].Kind == TokenKind.Slash)
				{
					Tokens[i] = new Token(TokenKind.Literal, "/");
				}
			}
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

		/// <summary>The month and day names of a locale's language, or English when .NET doesn't know it.</summary>
		/// <param name="locale">The locale's id, such as 0x407.</param>
		/// <returns>The names.</returns>
		private static DateTimeFormatInfo NamesOf(int locale)
		{
			if (locale == 0)
			{
				return CultureInfo.InvariantCulture.DateTimeFormat;
			}

			try
			{
				return CultureInfo.GetCultureInfo(locale).DateTimeFormat;
			}
			catch (CultureNotFoundException)
			{
				return CultureInfo.InvariantCulture.DateTimeFormat;
			}
			catch (ArgumentException)
			{
				return CultureInfo.InvariantCulture.DateTimeFormat;
			}
		}
	}

	/// <summary>
	/// A number's decimal digits as Excel keeps them: at most 15 significant ones, without leading or trailing zeros, the
	/// first of them at a power of ten.
	/// </summary>
	/// <param name="Text">The significant digits; empty for zero.</param>
	/// <param name="Exponent">The power of ten of the first digit.</param>
	private readonly record struct Digits(string Text, int Exponent)
	{
		/// <summary>Whether the number is zero.</summary>
		public bool IsZero => Text.Length == 0;

		/// <summary>The digits of its integer part.</summary>
		public int IntegerLength => IsZero || Exponent < 0 ? 0 : Exponent + 1;

		/// <summary>A number's absolute value's digits, rounded to 15 significant ones.</summary>
		/// <param name="value">The number, finite.</param>
		/// <returns>Its digits.</returns>
		public static Digits Of(double value)
		{
			if (value == 0)
			{
				return new Digits("", 0);
			}

			// d.dddddddddddddd E+ddd: 15 significant digits, correctly rounded.
			var scientific = Math.Abs(value).ToString("E" + (MaxDigits - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
			var e = scientific.IndexOf('E');
			var digits = (scientific.Substring(0, 1) + scientific.Substring(2, e - 2)).TrimEnd('0');

			return new Digits(digits, int.Parse(scientific.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
		}

		/// <summary>The number times a power of ten.</summary>
		/// <param name="power">The power.</param>
		/// <returns>The number.</returns>
		public Digits Scale(int power) => IsZero ? this : this with { Exponent = Exponent + power };

		/// <summary>The number rounded to some decimals, half away from zero.</summary>
		/// <param name="decimals">The decimals, 0 or more.</param>
		/// <returns>The number.</returns>
		public Digits Round(int decimals)
		{
			var keep = Exponent + 1 + decimals;
			if (IsZero || keep >= Text.Length)
			{
				return this;
			}

			if (keep < 0)
			{
				return new Digits("", 0);
			}

			var up = Text[keep] >= '5';
			var kept = Text.Substring(0, keep).ToCharArray();
			if (!up)
			{
				var down = new string(kept).TrimEnd('0');

				return down.Length == 0 ? new Digits("", 0) : new Digits(down, Exponent);
			}

			var i = kept.Length - 1;
			while (i >= 0 && kept[i] == '9')
			{
				kept[i--] = '0';
			}

			if (i < 0)
			{
				// All nines (or none kept): one more digit.
				return new Digits("1", Exponent + 1);
			}

			kept[i]++;

			return new Digits(new string(kept).TrimEnd('0'), Exponent);
		}

		/// <summary>The digits of the integer part.</summary>
		/// <returns>They, or nothing when it is 0.</returns>
		public string Integer()
		{
			var length = IntegerLength;
			if (length == 0)
			{
				return "";
			}

			return Text.Length >= length ? Text.Substring(0, length) : Text + new string('0', length - Text.Length);
		}

		/// <summary>The first digits after the decimal point.</summary>
		/// <param name="count">How many.</param>
		/// <returns>They, padded with zeros.</returns>
		public string Fraction(int count)
		{
			var chars = new char[count];
			for (var i = 0; i < count; i++)
			{
				var k = Exponent + 1 + i;
				chars[i] = !IsZero && k >= 0 && k < Text.Length ? Text[k] : '0';
			}

			return new string(chars);
		}
	}
}
