using System;

namespace CsvHelper.Excel;

/// <summary>
/// The width of a value's text in a number format code, for auto-fit and a column's width (Phase 5's decision 5): the text
/// Excel shows, from Phase 8's renderer (<see cref="NumberText"/>, <see cref="DateText"/>), measured as Excel's AutoFit
/// measures it, so that a typed cell doesn't show <c>########</c>. It replaces Phase 5's estimate (Phase 8's decision 16).
/// </summary>
/// <remarks>
/// <para>
/// A text is measured in Excel en-US, Excel's own text, with the widest separators of the common cultures (<c>.</c> for
/// both, as Phase 5 measured them), so that a workbook opened elsewhere shows its numbers too. The code is compiled to
/// measure (<see cref="FormatCode.Compile(string, bool)"/>): a blank Excel leaves a digit's width for (<c>?</c>, a
/// fraction's spaces) is a digit, <c>_x</c> the width of <c>x</c>, and General's every character a digit, as Excel's
/// AutoFit counts them (Phase 5's step 3).
/// </para>
/// <para>
/// A date or time, in a section without elapsed time, is as wide as its format's widest text over a leap year's days at
/// 11:59:59 and 23:59:59, measured once a section, so that later dates fit: the widest month and day names, two-digit
/// days, months and hours, and the wider designator. Elapsed time is its own value's text, as numbers are.
/// </para>
/// <para>
/// What the renderer leaves out is estimated wide, as Phase 5 did: <c>[DBNum]</c>'s numerals, which an East Asian locale
/// shows, a numeral and a unit for each digit; an era's name one or two East Asian characters; and elapsed time after a
/// date part, whose text no rule was found for, its widest time of day. A value Excel can't show (a negative date, one
/// past the last, a fraction too large) has no width: Excel shows <c>#</c> in any column.
/// </para>
/// </remarks>
internal sealed class FormatWidth
{
	/// <summary>The serial of 2024-01-01, the first day of the leap year a date's widest text is found in.</summary>
	private const double LeapYear = 45292;

	/// <summary>The days of a leap year.</summary>
	private const int LeapYearDays = 366;

	/// <summary>The room a text takes at most: 255 characters, each up to four bytes.</summary>
	private const int MaxBytes = 1024;

	/// <summary>The times of day a date's widest text is found at: 11:59:59 and 23:59:59, both designators and two-digit hours.</summary>
	private static readonly double[] Times = [43199.0 / 86400, 86399.0 / 86400];

	/// <summary>Excel en-US, with the widest separators of the common cultures.</summary>
	private static readonly DisplayCulture Measuring = MeasuringCulture();

	/// <summary>General, compiled to measure.</summary>
	private static readonly FormatCode General = FormatCode.Compile("General", true);

	/// <summary>The width of a numeral in <c>[DBNum]</c>: two East Asian characters, a numeral and a unit.</summary>
	private static readonly double NumeralPixels = 2 * ColumnWidths.CharPixels((char)0x5E74, false);

	private readonly FormatCode code;
	// By number section, the widest text of a date or time section over a leap year; NaN until measured.
	private readonly double[] dates;

	/// <summary>Reads a format code.</summary>
	/// <param name="code">The code, which <see cref="FormatCheck"/> passes.</param>
	public FormatWidth(string code)
	{
		this.code = FormatCode.Compile(code, true);
		dates = new double[this.code.Numbers.Length];
		for (var i = 0; i < dates.Length; i++)
		{
			dates[i] = double.NaN;
		}
	}

	/// <summary>The pixels of a number's text in the code (<see cref="ColumnWidths.TextPixels"/>).</summary>
	/// <param name="value">The number: a date's, time's or duration's serial in days too.</param>
	/// <returns>The pixels, without the padding; 0 for a value Excel can't show.</returns>
	public double NumberPixels(double value)
	{
		if (!code.IsReadable)
		{
			// The writer's codes all read (elapsed time after a date part is measured as its time of day): General's width
			// errs least for any other.
			return GeneralPixels(value);
		}

		Span<byte> room = stackalloc byte[MaxBytes];
		var text = new ShownText(room, true);
		if (!NumberText.TryWrite(code, value, Measuring, false, ref text) || !text.Fits)
		{
			return 0;
		}

		var section = NumberText.SectionOf(code, value);
		if (section is not null && IsClock(section))
		{
			return DatePixels(section);
		}

		return Pixels(text.Written, section?.HasDBNum == true);
	}

	/// <summary>The pixels of a text, or a boolean's <c>TRUE</c> or <c>FALSE</c>, in the code: in its text section, if it has one.</summary>
	/// <param name="textPixels">The text's own pixels.</param>
	/// <returns>The pixels, without the padding.</returns>
	public double TextPixels(double textPixels)
	{
		if (!code.IsReadable || code.Text is not { } section)
		{
			return textPixels;
		}

		var pixels = 0.0;
		foreach (var token in section.Tokens)
		{
			if (token.Kind == FormatTokenKind.Literal)
			{
				pixels += AutoFit.TextPixels(code.Literal(token), false, false);
			}
			else if (token.Kind == FormatTokenKind.At)
			{
				pixels += textPixels;
			}
		}

		return pixels;
	}

	/// <summary>
	/// The pixels of a number's text in General, as Excel's AutoFit measures it: every character but a leading minus sign
	/// as wide as a digit (<c>0.5</c> as <c>123</c>, <c>1E-10</c> as <c>12345</c>; Phase 5's step 3), since General counts
	/// in digits how many characters a column has room for.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The pixels, without the padding.</returns>
	public static double GeneralPixels(double value)
	{
		Span<byte> room = stackalloc byte[MaxBytes];
		var text = new ShownText(room, true);

		// General shows any finite number; an infinity or NaN, which a cell can't hold, as 0.
		return NumberText.TryWrite(General, double.IsNaN(value) || double.IsInfinity(value) ? 0 : value, Measuring, false, ref text) ?
			Pixels(text.Written, false) : 0;
	}

	/// <summary>Whether a section shows a date or a time of day, measured over a leap year: a date or system format's, without elapsed time.</summary>
	/// <param name="section">The section.</param>
	/// <returns>Whether it does.</returns>
	private static bool IsClock(FormatSection section)
	{
		if (section.Kind != FormatSectionKind.Date && section.System == SystemFormat.None)
		{
			return false;
		}

		foreach (var token in section.Tokens)
		{
			if (token.Kind is FormatTokenKind.ElapsedHours or FormatTokenKind.ElapsedMinutes or FormatTokenKind.ElapsedSeconds)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>The pixels of a text measured: as its characters, or with each digit a numeral and a unit in <c>[DBNum]</c>.</summary>
	/// <param name="utf8">The text.</param>
	/// <param name="numerals">Whether its digits are East Asian numerals.</param>
	/// <returns>The pixels.</returns>
	private static double Pixels(ReadOnlySpan<byte> utf8, bool numerals)
	{
		var pixels = AutoFit.TextPixels(utf8, false, false);
		if (numerals)
		{
			foreach (var b in utf8)
			{
				if (b is >= (byte)'0' and <= (byte)'9')
				{
					pixels += NumeralPixels - ColumnWidths.CharPixels((char)b, false);
				}
			}
		}

		return pixels;
	}

	/// <summary>The widest text of a date or time section over a leap year's days at 11:59:59 and 23:59:59, measured once.</summary>
	/// <param name="section">The section.</param>
	/// <returns>The pixels.</returns>
	private double DatePixels(FormatSection section)
	{
		var index = Array.IndexOf(code.Numbers, section);
		if (!double.IsNaN(dates[index]))
		{
			return dates[index];
		}

		Span<byte> room = stackalloc byte[MaxBytes];
		var widest = 0.0;
		for (var day = 0; day < LeapYearDays; day++)
		{
			foreach (var time in Times)
			{
				var text = new ShownText(room, true);
				if (DateText.TryWrite(code, section, LeapYear + day + time, false, Measuring, false, ref text) && text.Fits)
				{
					widest = Math.Max(widest, Pixels(text.Written, section.HasDBNum));
				}
			}
		}

		dates[index] = widest;

		return widest;
	}

	/// <summary>Excel en-US with the widest of the common cultures' decimal and group separators as both.</summary>
	/// <returns>The culture.</returns>
	private static DisplayCulture MeasuringCulture()
	{
		// A period, a comma, a no-break space and a narrow one (fr-FR), and an apostrophe (de-CH).
		var widest = '.';
		foreach (var separator in new[] { ',', (char)0x00A0, (char)0x202F, '\'' })
		{
			if (ColumnWidths.CharPixels(separator, false) > ColumnWidths.CharPixels(widest, false))
			{
				widest = separator;
			}
		}

		var text = widest.ToString();

		return DisplayCulture.Invariant.WithSeparators(text, text);
	}
}
