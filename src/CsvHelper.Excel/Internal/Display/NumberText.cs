using System;

namespace CsvHelper.Excel;

/// <summary>
/// The text a number shows in a compiled format code, and the text a text or a boolean shows through its text section
/// (Phase 8's step 3), as Excel 365 shows them in a column wide enough, as UTF-8. A date, time or elapsed-time section,
/// and a system format, are <see cref="DateText"/>'s (step 4).
/// </summary>
/// <remarks>
/// <para>
/// The rules are those Excel showed through COM, in en-US (Phase 8's "Checked on 2026-11-16", and step 3's probes in
/// <c>Csv\_bench\results\phase8\step3</c>):
/// </para>
/// <list type="bullet">
/// <item><description>
/// Sections: without conditions, positive numbers take the first, negative ones the second (without a minus sign) and
/// zero the third; with one section, a negative number shows a minus sign first, before literals, unless it rounds to
/// zero. A code of one text section shows numbers in General. A number below the smallest normal double is 0, as Excel
/// holds it, and takes the zero section.
/// </description></item>
/// <item><description>
/// Conditions: a section without one has its place's: the first <c>[&gt;0]</c>, and the second <c>[&lt;0]</c> of three;
/// the last section takes the rest, unless it has a condition of its own, when a value no section takes can't be shown.
/// A negative number shows a minus sign unless its section's condition takes only negative numbers (<c>[&lt;0]</c>,
/// <c>[=-5]</c>), or its section takes the rest after one condition that is <c>&lt;</c>, <c>&lt;=</c> or
/// <c>&lt;&gt;</c> anything, or <c>&gt;</c> or <c>&gt;=</c> a number not above 0. An empty section shows the sign alone.
/// </description></item>
/// <item><description>
/// A number is taken to 15 significant digits (<see cref="Digits"/>), times 100 for each <c>%</c> (not with an exponent)
/// and divided by 1,000 for each scaling comma, then rounded half away from zero to the places shown. Integer
/// placeholders take the digits from the right, the leftmost the rest; one without a digit shows <c>0</c>, a space or
/// nothing (<c>0</c>, <c>?</c>, <c>#</c>), and grouping puts the culture's separator after the digit of each power of a
/// thousand, a space after a space. Decimal placeholders show the digits, and past the last that isn't 0, <c>0</c>, a
/// space or nothing. Without integer placeholders, the integer part shows before the point all the same.
/// </description></item>
/// <item><description>
/// An exponent is a multiple of the integer placeholders, its digits placed as integers are; zero shows every integer
/// placeholder as <c>0</c>. <c>E-</c> shows no sign for a positive exponent.
/// </description></item>
/// <item><description>
/// Fractions: a denominator of placeholders takes the last continued-fraction convergent, in doubles, whose denominator
/// fits them; a fixed one rounds the numerator half away from zero, which can't pass 32,767. Without a whole number the
/// fraction is improper: a number of 2,147,483,647 or more can't be shown, the convergent's numerator, the whole
/// number's part included, can't pass it either, and one over 1 is the number rounded. With one, a fraction rounding to 1 adds to it, and a zero fraction shows as spaces of its width
/// (a <c>?</c> in the numerator), nothing (only <c>#</c>), or itself (a <c>0</c>), the whole number at least <c>0</c>. A
/// whole number that is 0 and shows nothing hides the literals before the numerator, or shows them as spaces when the
/// numerator starts with <c>?</c>. A numerator's digits are placed as integers are; a denominator's from the left, its
/// leading <c>0</c>s padding it first, its <c>#</c>s then left empty, then its last placeholders.
/// </description></item>
/// <item><description>
/// General: decimals while they show as many significant digits as scientific notation would, else scientific, in at
/// most 11 characters besides the sign.
/// </description></item>
/// <item><description>
/// A number's text can't pass 255 characters (<c>0</c> can't show 1E+255), nor can a text's through a text section
/// unless the section is <c>@</c> alone.
/// </description></item>
/// </list>
/// <para>
/// Nothing allocates: the digits are a struct, and the text goes into the caller's span (<see cref="ShownText"/>).
/// </para>
/// </remarks>
internal static class NumberText
{
	/// <summary>The most characters Excel shows a number in, or a text through a text section.</summary>
	public const int MaxChars = 255;

	/// <summary>The most characters General shows a number in, besides its sign.</summary>
	private const int GeneralLength = 11;

	/// <summary>The smallest normal double: Excel holds a smaller one as 0.</summary>
	private const double MinNormal = 2.2250738585072014E-308;

	/// <summary>The largest numerator, and the bound of the number, of a fraction without a whole number.</summary>
	private const double MaxImproper = int.MaxValue;

	/// <summary>The largest numerator over a fixed denominator.</summary>
	private const double MaxFixedNumerator = short.MaxValue;

	/// <summary>The most steps of a continued fraction: a double's ends well before.</summary>
	private const int MaxFractionSteps = 64;

	/// <summary>How literals between a fraction's whole number and its numerator show.</summary>
	private enum Between
	{
		/// <summary>As they are.</summary>
		Shown,

		/// <summary>As spaces, one a character: the whole number is hidden and the numerator starts with <c>?</c>.</summary>
		Spaces,

		/// <summary>Not at all: the whole number is hidden.</summary>
		Hidden,
	}

	/// <summary>Writes a number's text in a code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="culture">The culture's separators, names, long date and long time.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system, for a date or time section.</param>
	/// <param name="text">An empty text, which receives the number's.</param>
	/// <returns>
	/// Whether Excel can show the number in the code: <see langword="false"/> for a code the renderer can't read, a value
	/// that isn't finite, one no section takes, a fraction too large, a date or time Excel can't show
	/// (<see cref="DateText.TryWrite"/>), and text past 255 characters.
	/// </returns>
	public static bool TryWrite(FormatCode code, double value, DisplayCulture culture, bool date1904, ref ShownText text)
	{
		if (!code.IsReadable || double.IsNaN(value) || double.IsInfinity(value))
		{
			return false;
		}

		// Excel holds a number below the smallest normal double as 0, so it takes the zero section.
		if (Math.Abs(value) < MinNormal)
		{
			value = 0;
		}

		// A code of one text section shows numbers in General, which Excel's AutoFit measures as they are, not as General's
		// digits (@ shows 0.5 as 18.5 points; Phase 5's probe).
		if (code.Numbers.Length == 0)
		{
			if (value < 0)
			{
				text.Append('-');
			}

			WriteGeneralText(Math.Abs(value), culture, ref text);

			return true;
		}

		if (!Choose(code, value, out var section, out var minus))
		{
			return false;
		}

		if (section.Kind == FormatSectionKind.Date || section.System != SystemFormat.None)
		{
			return DateText.TryWrite(code, section, value, minus, culture, date1904, ref text);
		}

		var shown = WriteSection(code, section, Math.Abs(value), minus, culture, ref text);

		return shown && text.Chars <= MaxChars;
	}

	/// <summary>The number section a number shows in, as <see cref="TryWrite"/> chooses it, for auto-fit's widths.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <returns>The section; <see langword="null"/> for a code the renderer can't read or of a text section alone, and for a value no section takes.</returns>
	public static FormatSection? SectionOf(FormatCode code, double value)
	{
		if (!code.IsReadable || double.IsNaN(value) || double.IsInfinity(value) || code.Numbers.Length == 0)
		{
			return null;
		}

		return Choose(code, Math.Abs(value) < MinNormal ? 0 : value, out var section, out _) ? section : null;
	}

	/// <summary>Writes a text's, or a boolean's <c>TRUE</c> or <c>FALSE</c>, through a code's text section.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The text, in UTF-8.</param>
	/// <param name="text">An empty text, which receives the shown text.</param>
	/// <returns>
	/// Whether the text section shows it: <see langword="false"/> when the code has none, or can't be read, so that the
	/// text shows as it is, and when the shown text passes 255 characters, which Excel can't show.
	/// </returns>
	public static bool TryWriteText(FormatCode code, ReadOnlySpan<byte> value, ref ShownText text)
	{
		var section = code.Text;
		if (!code.IsReadable || section is null)
		{
			return false;
		}

		// @ alone shows text as it is, however long (step 3's probes showed 1,100 characters).
		var tokens = section.Tokens;
		if (tokens.Length == 1 && tokens[0].Kind == FormatTokenKind.At)
		{
			text.Append(value);

			return true;
		}

		foreach (var token in tokens)
		{
			if (token.Kind == FormatTokenKind.Literal)
			{
				text.Append(code.Literal(token));
			}
			else if (token.Kind == FormatTokenKind.At)
			{
				text.Append(value);
			}
		}

		return text.Chars <= MaxChars;
	}

	/// <summary>Chooses the section a number shows in, and whether it shows a minus sign.</summary>
	/// <param name="code">The code, with at least one number section.</param>
	/// <param name="value">The number; −0 is 0.</param>
	/// <param name="section">The section.</param>
	/// <param name="minus">Whether a negative number shows a minus sign, unless it rounds to zero.</param>
	/// <returns>Whether a section takes the number.</returns>
	private static bool Choose(FormatCode code, double value, out FormatSection section, out bool minus)
	{
		var numbers = code.Numbers;
		var negative = value < 0;
		section = numbers[0];
		minus = false;
		if (!code.IsConditional || numbers.Length == 1)
		{
			if (negative)
			{
				section = numbers.Length >= 2 ? numbers[1] : numbers[0];
				minus = numbers.Length == 1;
			}
			else if (value == 0 && numbers.Length >= 3)
			{
				section = numbers[2];
			}

			return true;
		}

		// A section without a condition has its place's: the first > 0, the second of three < 0.
		var first = numbers[0];
		var firstOperator = first.Condition != ConditionOperator.None ? first.Condition : ConditionOperator.Greater;
		var firstOperand = first.Condition != ConditionOperator.None ? first.Operand : 0;
		if (Takes(firstOperator, firstOperand, value))
		{
			minus = negative && !OnlyNegative(firstOperator, firstOperand);

			return true;
		}

		var second = numbers[1];
		if (numbers.Length >= 3)
		{
			var secondOperator = second.Condition != ConditionOperator.None ? second.Condition : ConditionOperator.Less;
			var secondOperand = second.Condition != ConditionOperator.None ? second.Operand : 0;
			if (Takes(secondOperator, secondOperand, value))
			{
				section = second;
				minus = negative && !OnlyNegative(secondOperator, secondOperand);

				return true;
			}

			// The third takes the rest, with a minus sign.
			section = numbers[2];
			minus = negative;

			return true;
		}

		section = second;
		if (second.Condition != ConditionOperator.None)
		{
			minus = negative && !OnlyNegative(second.Condition, second.Operand);

			return second.Takes(value);
		}

		// The second of two takes the rest: without a minus sign after < anything, or > a number not above 0.
		minus = negative && !(firstOperator is ConditionOperator.Less or ConditionOperator.LessOrEqual or ConditionOperator.NotEqual ||
			(firstOperator is ConditionOperator.Greater or ConditionOperator.GreaterOrEqual && firstOperand <= 0));

		return true;
	}

	/// <summary>Whether a condition takes a number.</summary>
	/// <param name="op">The operator.</param>
	/// <param name="operand">The number it compares with.</param>
	/// <param name="value">The number.</param>
	/// <returns>Whether it does.</returns>
	private static bool Takes(ConditionOperator op, double operand, double value) => op switch
	{
		ConditionOperator.Equal => value == operand,
		ConditionOperator.NotEqual => value != operand,
		ConditionOperator.Less => value < operand,
		ConditionOperator.LessOrEqual => value <= operand,
		ConditionOperator.Greater => value > operand,
		ConditionOperator.GreaterOrEqual => value >= operand,
		_ => true,
	};

	/// <summary>Whether a condition takes only negative numbers: <c>&lt;</c> a number not above 0, <c>&lt;=</c> or <c>=</c> one below 0.</summary>
	/// <param name="op">The operator.</param>
	/// <param name="operand">The number it compares with.</param>
	/// <returns>Whether it does.</returns>
	private static bool OnlyNegative(ConditionOperator op, double operand) =>
		(op == ConditionOperator.Less && operand <= 0) || (op is ConditionOperator.LessOrEqual or ConditionOperator.Equal && operand < 0);

	/// <summary>Writes a number in a section that isn't a date's.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first, unless the number rounds to zero.</param>
	/// <param name="culture">The culture's separators.</param>
	/// <param name="text">The text.</param>
	/// <returns>Whether the number can be shown: a fraction can be too large.</returns>
	private static bool WriteSection(FormatCode code, FormatSection section, double abs, bool minus, DisplayCulture culture, ref ShownText text)
	{
		// Brackets alone: a color, DBNum or a condition shows General ([Red] is [Red]General); a locale alone, or nothing,
		// shows nothing but the sign.
		if (section.Tokens.Length == 0)
		{
			if (minus)
			{
				text.Append('-');
			}

			if (section.HasColor || section.HasDBNum || section.Condition != ConditionOperator.None)
			{
				WriteGeneral(abs, culture, ref text);
			}

			return true;
		}

		if (section.HasGeneral)
		{
			WriteGeneralSection(code, section, abs, minus, culture, ref text);

			return true;
		}

		if (section.Kind == FormatSectionKind.Fraction)
		{
			return WriteFraction(code, section, abs, minus, culture, ref text);
		}

		if (section.Placeholders == 0 && !HasPoint(section))
		{
			// Literals alone don't round: "x" shows −0.001 as -x.
			if (minus)
			{
				text.Append('-');
			}

			foreach (var token in section.Tokens)
			{
				if (token.Kind == FormatTokenKind.Literal)
				{
					text.Append(code.Literal(token));
				}
			}

			return true;
		}

		WriteNumber(code, section, abs, minus, culture, ref text);

		return true;
	}

	/// <summary>Whether a section has a decimal point.</summary>
	/// <param name="section">The section.</param>
	/// <returns>Whether it has.</returns>
	private static bool HasPoint(FormatSection section)
	{
		foreach (var token in section.Tokens)
		{
			if (token.Kind == FormatTokenKind.Point)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Writes a number in a section of General among literals: <c>"Total: "General</c> shows −0.5 as <c>-Total: 0.5</c>.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first.</param>
	/// <param name="culture">The culture's separators.</param>
	/// <param name="text">The text.</param>
	private static void WriteGeneralSection(FormatCode code, FormatSection section, double abs, bool minus, DisplayCulture culture, ref ShownText text)
	{
		if (minus)
		{
			text.Append('-');
		}

		foreach (var token in section.Tokens)
		{
			if (token.Kind == FormatTokenKind.Literal)
			{
				text.Append(code.Literal(token));
			}
			else if (token.Kind == FormatTokenKind.General)
			{
				WriteGeneral(abs, culture, ref text);
			}
		}
	}

	/// <summary>Writes a number in a section of placeholders: integers, decimals, grouping, scaling, percents and an exponent.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first, unless the number rounds to zero.</param>
	/// <param name="culture">The culture's separators.</param>
	/// <param name="text">The text.</param>
	private static void WriteNumber(FormatCode code, FormatSection section, double abs, bool minus, DisplayCulture culture, ref ShownText text)
	{
		// Percents don't multiply beside an exponent (step 3's probes: 0.0E+0% and %0.0E+0 show 1234.5 as 1.2E+3).
		var power = (section.HasExponent ? 0 : 2 * section.Percents) - (3 * section.Scale);
		var digits = Digits.Of(abs).Scale(power);
		var decimals = section.DecimalPlaceholders;
		var exponent = 0;
		var zeroMantissa = false;
		if (section.HasExponent)
		{
			if (digits.IsZero)
			{
				zeroMantissa = true;
			}
			else
			{
				var step = Math.Max(section.IntegerPlaceholders, 1);
				exponent = FloorDivide(digits.Exponent, step) * step;
				var mantissa = digits.Scale(-exponent).Round(decimals);
				if (mantissa.IntegerLength > step)
				{
					exponent += step;
					mantissa = digits.Scale(-exponent).Round(decimals);
				}

				digits = mantissa;
			}
		}
		else
		{
			digits = digits.Round(decimals);
		}

		if (minus && !digits.IsZero)
		{
			text.Append('-');
		}

		var lastDecimal = -1;
		for (var i = 0; i < decimals; i++)
		{
			if (digits.DigitAt(-(i + 1)) != 0)
			{
				lastDecimal = i;
			}
		}

		var exponentDigits = Digits.Of(Math.Abs(exponent));
		var integers = new PlaceholderRun(digits, digits.IntegerLength, section.IntegerPlaceholders, section.Grouping, zeroMantissa);
		var exponents = new PlaceholderRun(exponentDigits, Math.Max(exponentDigits.IntegerLength, 1), section.ExponentPlaceholders, false, false);
		var place = 0;
		foreach (var token in section.Tokens)
		{
			switch (token.Kind)
			{
				case FormatTokenKind.Literal:
					text.Append(code.Literal(token));

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Integer:
					integers.Write(token.Placeholder, culture, ref text);

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Decimal:
					if (place <= lastDecimal)
					{
						text.AppendDigit(digits.DigitAt(-(place + 1)));
					}
					else
					{
						Pad(token.Placeholder, ref text);
					}

					place++;

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Exponent:
					exponents.Write(token.Placeholder, culture, ref text);

					break;
				case FormatTokenKind.Point:
					// Without integer placeholders, the integer part shows before the point all the same (.00 shows 12 as 12.00).
					if (section.IntegerPlaceholders == 0)
					{
						for (var p = digits.IntegerLength - 1; p >= 0; p--)
						{
							text.AppendDigit(digits.DigitAt(p));
						}
					}

					text.Append(culture.DecimalSeparator);

					break;
				case FormatTokenKind.Exponent:
					text.Append('E');
					if (exponent < 0)
					{
						text.Append('-');
					}
					else if (token.Placeholder == '+')
					{
						text.Append('+');
					}

					break;
			}
		}
	}

	/// <summary>Writes a placeholder without a digit: <c>0</c> shows 0, <c>?</c> a space, <c>#</c> nothing.</summary>
	/// <param name="kind">The placeholder.</param>
	/// <param name="text">The text.</param>
	private static void Pad(char kind, ref ShownText text)
	{
		if (kind == '0')
		{
			text.Append('0');
		}
		else if (kind == '?')
		{
			text.AppendBlank();
		}
	}

	/// <summary>Writes an integer's digits, at least one.</summary>
	/// <param name="digits">The integer.</param>
	/// <param name="text">The text.</param>
	private static void WriteInteger(Digits digits, ref ShownText text)
	{
		for (var p = Math.Max(digits.IntegerLength, 1) - 1; p >= 0; p--)
		{
			text.AppendDigit(digits.DigitAt(p));
		}
	}

	/// <summary>A division rounded toward negative infinity.</summary>
	/// <param name="dividend">The dividend.</param>
	/// <param name="divisor">The divisor, positive.</param>
	/// <returns>The quotient.</returns>
	private static int FloorDivide(int dividend, int divisor) => dividend >= 0 ? dividend / divisor : -((-dividend + divisor - 1) / divisor);

	/// <summary>Writes a number in a fraction section.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="minus">Whether to show a minus sign first: with a whole number always, without one unless the numerator is 0.</param>
	/// <param name="culture">The culture's separators.</param>
	/// <param name="text">The text.</param>
	/// <returns>Whether the number can be shown.</returns>
	private static bool WriteFraction(FormatCode code, FormatSection section, double abs, bool minus, DisplayCulture culture, ref ShownText text)
	{
		for (var i = 0; i < section.Percents; i++)
		{
			abs *= 100;
		}

		var hasWhole = section.IntegerPlaceholders > 0;
		var floor = Math.Floor(abs);
		var fraction = abs - floor;
		double numerator;
		double denominator;
		if (section.Denominator > 0)
		{
			denominator = section.Denominator;
			numerator = Math.Round((hasWhole ? fraction : abs) * denominator, MidpointRounding.AwayFromZero);
			if (numerator > MaxFixedNumerator)
			{
				return false;
			}
		}
		else
		{
			if (!hasWhole && floor >= MaxImproper)
			{
				return false;
			}

			// Without a whole number, the convergent's numerator, the whole number's part included, must fit too; one over
			// 1 is the number rounded (step 3's probes: ?/? shows 1073741824.5 as 1073741825/1, and ??/?? 123456789.123 as
			// 987654313/8, whose next convergent's numerator, 8024691293, is past the bound).
			Convergent(fraction, MaxDenominatorOf(section.DenominatorPlaceholders), hasWhole ? -1 : floor, out numerator, out denominator);
			if (!hasWhole)
			{
				numerator = denominator == 1 ? Math.Round(abs, MidpointRounding.AwayFromZero) : numerator + (floor * denominator);
			}
		}

		// A fraction rounding to a whole number adds to it. The whole number is the number's 15 digits' integer part:
		// 123456789012345.67 shows 123456789012346 2/3 in # ?/? (step 3's probes).
		var carry = hasWhole && numerator == denominator;
		var whole = !hasWhole ? default : carry ? Digits.Of(floor + 1) : Digits.Of(abs);
		if (carry)
		{
			numerator = 0;
		}

		var zero = numerator == 0;
		var wholeLength = whole.IntegerLength;
		var blank = hasWhole && zero && section.NumeratorKinds.IndexOf('0') < 0;
		if (blank && wholeLength == 0)
		{
			// A blanked fraction's whole number shows at least 0 (# ?/? shows 0 as 0 and four spaces).
			wholeLength = 1;
		}

		var hidden = hasWhole && wholeLength == 0 && section.IntegerKinds.IndexOf('0') < 0;
		var between = !hidden ? Between.Shown : section.NumeratorKinds.Length > 0 && section.NumeratorKinds[0] == '?' ? Between.Spaces : Between.Hidden;
		if (minus && (hasWhole || !zero))
		{
			text.Append('-');
		}

		var parts = new FractionParts(new PlaceholderRun(whole, wholeLength, section.IntegerPlaceholders, section.Grouping, false), numerator, denominator, section, between);
		if (!blank)
		{
			WriteFractionTokens(code, section, 0, section.Tokens.Length, ref parts, culture, ref text);

			return true;
		}

		// A zero fraction after a whole number shows as spaces of its width with a ? in its numerator, else as nothing.
		// Measured, it is the fraction's characters, as Excel's AutoFit measures them: # ?/? shows 1 as 1 and four spaces,
		// as wide as 1 0/1 (Phase 5's probe).
		WriteFractionTokens(code, section, 0, section.WholeEnd, ref parts, culture, ref text);
		if (section.NumeratorKinds.IndexOf('?') >= 0)
		{
			var zeroParts = new FractionParts(default, 0, section.Denominator > 0 ? section.Denominator : 1, section, Between.Shown);
			if (text.Measuring)
			{
				WriteFractionTokens(code, section, section.WholeEnd, section.FractionEnd, ref zeroParts, culture, ref text);
			}
			else
			{
				var measure = new ShownText(Span<byte>.Empty);
				WriteFractionTokens(code, section, section.WholeEnd, section.FractionEnd, ref zeroParts, culture, ref measure);
				text.AppendBlanks(measure.Chars);
			}
		}

		WriteFractionTokens(code, section, section.FractionEnd, section.Tokens.Length, ref parts, culture, ref text);

		return true;
	}

	/// <summary>Writes some of a fraction section's tokens.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="from">The first token's position.</param>
	/// <param name="to">The position after the last.</param>
	/// <param name="parts">The fraction's parts, which advance as their placeholders are written.</param>
	/// <param name="culture">The culture's separators.</param>
	/// <param name="text">The text.</param>
	private static void WriteFractionTokens(FormatCode code, FormatSection section, int from, int to, ref FractionParts parts, DisplayCulture culture, ref ShownText text)
	{
		var tokens = section.Tokens;
		for (var i = from; i < to; i++)
		{
			var token = tokens[i];
			switch (token.Kind)
			{
				case FormatTokenKind.Literal:
					var literal = code.Literal(token);
					var isBetween = i >= section.WholeEnd && i < section.NumeratorStart;
					if (!isBetween || parts.Between == Between.Shown)
					{
						text.Append(literal);
					}
					else if (parts.Between == Between.Spaces)
					{
						// Measured, the literals themselves, whose width Excel's AutoFit gives the spaces (# ?/? shows 0.5 as
						// " 1/2", as wide as its space; Phase 5's probe).
						if (text.Measuring)
						{
							text.Append(literal);
						}
						else
						{
							text.AppendBlanks(ShownText.CountChars(literal));
						}
					}

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Integer:
					parts.Whole.Write(token.Placeholder, culture, ref text);

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Numerator:
					parts.Numerator.Write(token.Placeholder, culture, ref text);

					break;
				case FormatTokenKind.Digit when token.Role == PlaceholderRole.Denominator:
					parts.WriteDenominator(token.Placeholder, ref text);

					break;
				case FormatTokenKind.Bar:
					text.Append('/');

					break;
				case FormatTokenKind.FixedDenominator:
					text.Append(code.Literal(token));

					break;
			}
		}
	}

	/// <summary>
	/// The last continued-fraction convergent of a number in [0, 1) whose denominator is at most a bound, computed in
	/// doubles as Excel computes it: 0.3 is 2/7 over one digit, since its double's expansion is [0; 3, 2, 1, …].
	/// </summary>
	/// <param name="x">The number, from 0 up to 1.</param>
	/// <param name="maxDenominator">The largest denominator.</param>
	/// <param name="whole">
	/// The whole number of an improper fraction, whose numerator (the whole number times the denominator, and the
	/// convergent's numerator) must stay within 2,147,483,647 too; −1 for a fraction after a whole number.
	/// </param>
	/// <param name="numerator">The convergent's numerator; 0 for 0/1.</param>
	/// <param name="denominator">The convergent's denominator.</param>
	private static void Convergent(double x, double maxDenominator, double whole, out double numerator, out double denominator)
	{
		double h = 0;
		double k = 1;
		double previousH = 1;
		double previousK = 0;
		for (var step = 0; step < MaxFractionSteps && x != 0; step++)
		{
			var y = 1 / x;
			var a = Math.Floor(y);
			var nextH = (a * h) + previousH;
			var nextK = (a * k) + previousK;
			if (nextK > maxDenominator || (whole >= 0 && (whole * nextK) + nextH > MaxImproper))
			{
				break;
			}

			previousH = h;
			previousK = k;
			h = nextH;
			k = nextK;
			x = y - a;
		}

		numerator = h;
		denominator = k;
	}

	/// <summary>The largest denominator some placeholders hold: 9 for one, 99 for two.</summary>
	/// <param name="placeholders">The placeholders.</param>
	/// <returns>10 to their power, less 1.</returns>
	private static double MaxDenominatorOf(int placeholders)
	{
		var bound = 1.0;
		for (var i = 0; i < placeholders; i++)
		{
			bound *= 10;
		}

		return bound - 1;
	}

	/// <summary>
	/// Writes a number in General: at most 11 characters besides its sign, in decimals or in scientific notation. Measured,
	/// every character is a digit, as Excel's AutoFit counts General's characters (Phase 5's step 3: <c>0.5</c> as wide as
	/// <c>123</c>).
	/// </summary>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="culture">The culture's decimal separator.</param>
	/// <param name="text">The text.</param>
	private static void WriteGeneral(double abs, DisplayCulture culture, ref ShownText text)
	{
		if (text.Measuring)
		{
			var counted = new ShownText(Span<byte>.Empty);
			WriteGeneralText(abs, culture, ref counted);
			text.AppendBlanks(counted.Chars);

			return;
		}

		WriteGeneralText(abs, culture, ref text);
	}

	/// <summary>Writes a number's General text (<see cref="WriteGeneral"/>), as it shows.</summary>
	/// <param name="abs">The number's absolute value.</param>
	/// <param name="culture">The culture's decimal separator.</param>
	/// <param name="text">The text.</param>
	private static void WriteGeneralText(double abs, DisplayCulture culture, ref ShownText text)
	{
		var digits = Digits.Of(abs);
		if (digits.IsZero)
		{
			text.Append('0');

			return;
		}

		if (digits.Exponent >= 0)
		{
			var integerLength = digits.Exponent + 1;
			if (integerLength <= GeneralLength)
			{
				var decimals = Math.Max(0, GeneralLength - integerLength - 1);
				var rounded = digits.Round(decimals);
				if (rounded.IntegerLength <= GeneralLength)
				{
					WritePlain(rounded, culture, ref text);

					return;
				}
			}
		}
		else
		{
			// After "0.", the leading zeros and the significant digits that fit, while they show as many as scientific
			// notation would.
			var zeros = -digits.Exponent - 1;
			var shown = Math.Min(digits.Count, GeneralLength - 2 - zeros);
			if (shown > 0 && shown >= Math.Min(digits.Count, ScientificDecimals(digits.Exponent) + 1))
			{
				WritePlain(digits.Round(GeneralLength - 2), culture, ref text);

				return;
			}
		}

		WriteScientific(digits, culture, ref text);
	}

	/// <summary>Writes a rounded number in decimals, without trailing zeros: 0 for an integer part of 0.</summary>
	/// <param name="digits">The number.</param>
	/// <param name="culture">The culture's decimal separator.</param>
	/// <param name="text">The text.</param>
	private static void WritePlain(Digits digits, DisplayCulture culture, ref ShownText text)
	{
		WriteInteger(digits, ref text);
		var last = digits.IsZero ? 0 : digits.Exponent - digits.Count + 1;
		if (last < 0)
		{
			text.Append(culture.DecimalSeparator);
			for (var p = -1; p >= last; p--)
			{
				text.AppendDigit(digits.DigitAt(p));
			}
		}
	}

	/// <summary>Writes a number in General's scientific notation: as many decimals as 11 characters hold, without trailing zeros (<c>1.23457E+11</c>).</summary>
	/// <param name="digits">The number, not zero.</param>
	/// <param name="culture">The culture's decimal separator.</param>
	/// <param name="text">The text.</param>
	private static void WriteScientific(Digits digits, DisplayCulture culture, ref ShownText text)
	{
		var exponent = digits.Exponent;
		var mantissa = digits.Scale(-exponent).Round(ScientificDecimals(exponent));
		if (mantissa.Exponent > 0)
		{
			exponent++;
			mantissa = digits.Scale(-exponent).Round(ScientificDecimals(exponent));
		}

		WritePlain(mantissa, culture, ref text);
		text.Append('E');
		text.Append(exponent < 0 ? '-' : '+');
		var e = Math.Abs(exponent);
		if (e >= 100)
		{
			text.AppendDigit(e / 100);
		}

		text.AppendDigit(e / 10 % 10);
		text.AppendDigit(e % 10);
	}

	/// <summary>The decimals of General's scientific notation: 11 characters less <c>d.</c> and the exponent's.</summary>
	/// <param name="exponent">The exponent.</param>
	/// <returns>5 for an exponent of two digits, 4 for three.</returns>
	private static int ScientificDecimals(int exponent) => GeneralLength - 2 - (Math.Abs(exponent) >= 100 ? 5 : 4);

	/// <summary>
	/// Placeholders that show an integer's digits, as integer placeholders do: each from the right takes a digit, the
	/// leftmost the rest, and one without a digit shows <c>0</c>, a space or nothing; grouping puts a separator after the
	/// digit of each power of a thousand.
	/// </summary>
	private struct PlaceholderRun
	{
		private readonly Digits digits;
		private readonly int length;
		private readonly int count;
		private readonly bool grouping;
		private readonly bool zeros;
		private int index;

		/// <summary>Creates a run.</summary>
		/// <param name="digits">The integer's digits, at powers 0 and up.</param>
		/// <param name="length">How many digits it shows: 0 for an integer part of 0, at least 1 for a numerator or an exponent.</param>
		/// <param name="count">How many placeholders there are.</param>
		/// <param name="grouping">Whether to group the digits by three.</param>
		/// <param name="zeros">Whether a placeholder without a digit shows 0 whatever it is, as for zero beside an exponent.</param>
		public PlaceholderRun(Digits digits, int length, int count, bool grouping, bool zeros)
		{
			this.digits = digits;
			this.length = length;
			this.count = count;
			this.grouping = grouping;
			this.zeros = zeros;
			index = 0;
		}

		/// <summary>Writes the next placeholder's text.</summary>
		/// <param name="kind">The placeholder: <c>0</c>, <c>#</c> or <c>?</c>.</param>
		/// <param name="culture">The culture's group separator.</param>
		/// <param name="text">The text.</param>
		public void Write(char kind, DisplayCulture culture, ref ShownText text)
		{
			var power = count - 1 - index;
			if (index == 0)
			{
				for (var p = length - 1; p > power; p--)
				{
					WriteDigit(p, culture, ref text);
				}
			}

			index++;
			if (power < length)
			{
				WriteDigit(power, culture, ref text);
			}
			else if (zeros || kind == '0')
			{
				text.Append('0');
				Separate(power, culture, false, ref text);
			}
			else if (kind == '?')
			{
				text.AppendBlank();
				Separate(power, culture, true, ref text);
			}
		}

		/// <summary>Writes the digit at a power, and a separator after it.</summary>
		/// <param name="power">The power.</param>
		/// <param name="culture">The culture's group separator.</param>
		/// <param name="text">The text.</param>
		private readonly void WriteDigit(int power, DisplayCulture culture, ref ShownText text)
		{
			text.AppendDigit(digits.DigitAt(power));
			Separate(power, culture, false, ref text);
		}

		/// <summary>Writes a group separator after the place of a power of a thousand, when grouping.</summary>
		/// <param name="power">The place's power.</param>
		/// <param name="culture">The culture's group separator.</param>
		/// <param name="space">Whether the place showed a space, which a space follows.</param>
		/// <param name="text">The text.</param>
		private readonly void Separate(int power, DisplayCulture culture, bool space, ref ShownText text)
		{
			if (!grouping || power <= 0 || power % 3 != 0)
			{
				return;
			}

			if (space)
			{
				text.AppendBlank();
			}
			else
			{
				text.Append(culture.GroupSeparator);
			}
		}
	}

	/// <summary>A fraction's parts as its placeholders write them: the whole number, the numerator and the denominator.</summary>
	private struct FractionParts
	{
		private readonly Digits denominator;
		private readonly int denominatorLength;
		private readonly string denominatorKinds;
		private int denominatorIndex;
		private int denominatorDigit;

		/// <summary>The whole number's placeholders.</summary>
		public PlaceholderRun Whole;

		/// <summary>The numerator's placeholders.</summary>
		public PlaceholderRun Numerator;

		/// <summary>How the literals between the whole number and the numerator show.</summary>
		public Between Between { get; }

		/// <summary>Creates the parts.</summary>
		/// <param name="whole">The whole number's placeholders.</param>
		/// <param name="numerator">The numerator.</param>
		/// <param name="denominator">The denominator.</param>
		/// <param name="section">The section.</param>
		/// <param name="between">How the literals between the whole number and the numerator show.</param>
		public FractionParts(PlaceholderRun whole, double numerator, double denominator, FormatSection section, Between between)
		{
			Whole = whole;
			var numeratorDigits = Digits.Of(numerator);
			Numerator = new PlaceholderRun(numeratorDigits, Math.Max(numeratorDigits.IntegerLength, 1), section.NumeratorPlaceholders, false, false);
			this.denominator = Digits.Of(denominator);
			denominatorLength = Math.Max(this.denominator.IntegerLength, 1);
			denominatorKinds = section.DenominatorKinds;
			denominatorIndex = 0;
			denominatorDigit = 0;
			Between = between;
		}

		/// <summary>
		/// Writes the next denominator placeholder's text. The digits go from the left: of the placeholders without one,
		/// the leading <c>0</c>s pad first (<c>?/0?</c> shows 1/02), then the <c>#</c>s are left empty, then the last of the
		/// others (<c>?/?0</c> shows 1/20, <c>?/??0</c> 1/2 0; step 3's probes).
		/// </summary>
		/// <param name="kind">The placeholder.</param>
		/// <param name="text">The text.</param>
		public void WriteDenominator(char kind, ref ShownText text)
		{
			var kinds = denominatorKinds;
			var count = kinds.Length;
			var j = denominatorIndex++;
			var pads = Math.Max(count - denominatorLength, 0);
			var leading = 0;
			while (leading < count && kinds[leading] == '0')
			{
				leading++;
			}

			var zeroPads = Math.Min(leading, pads);
			if (j < zeroPads)
			{
				text.Append('0');

				return;
			}

			var rest = pads - zeroPads;
			var hashes = 0;
			var hashesBefore = 0;
			var othersAfter = 0;
			for (var i = zeroPads; i < count; i++)
			{
				if (kinds[i] == '#')
				{
					hashes++;
					hashesBefore += i < j ? 1 : 0;
				}
				else if (i > j)
				{
					othersAfter++;
				}
			}

			var emptyHashes = Math.Min(hashes, rest);
			var empty = kind == '#' ? hashesBefore < emptyHashes : othersAfter < rest - emptyHashes;
			if (empty)
			{
				Pad(kind, ref text);

				return;
			}

			// The last placeholder takes any digits left.
			do
			{
				text.AppendDigit(denominator.DigitAt(denominatorLength - 1 - denominatorDigit));
				denominatorDigit++;
			}
			while (j == count - 1 && denominatorDigit < denominatorLength);
		}
	}
}
