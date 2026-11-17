using System;

namespace CsvHelper.Excel;

/// <summary>
/// The text a number shows in a date, time or elapsed-time section of a compiled format code, or in a system format
/// (Phase 8's step 4), as Excel 365 shows it in a column wide enough, as UTF-8.
/// </summary>
/// <remarks>
/// <para>
/// The rules are those Excel showed through COM, in en-US (Phase 8's "Checked on 2026-11-16", and step 4's probes in
/// <c>Csv\_bench\results\phase8\step4</c>):
/// </para>
/// <list type="bullet">
/// <item><description>
/// The 1900 date system can't show a negative date or time, in any section; the 1904 system shows a minus sign and the
/// absolute value's (−1 is <c>-1904-01-02</c>), but in a section that shows none (a second section's <c>-1</c> is
/// <c>1904-01-02</c>), and even where the value rounds to 0 (−1E-09 is <c>-1904-01-01</c>). Neither shows a date past
/// 9999-12-31, after rounding.
/// </description></item>
/// <item><description>
/// The time is rounded to the precision the section shows, whole seconds or one to three digits of their fractions, before
/// its parts are taken, as Excel's arithmetic does it in doubles (Phase 8's step 7): half the unit shown is added to the
/// serial, in days, a whole day carrying to the date, and the fraction of the day left is taken apart by truncation, its
/// hours times 24, its minutes the rest times 60, its seconds the rest times 60, and their fractions the rest times the
/// precision. So the double nearest a half can go either way: 09:05:07.250 of 2024's days shows <c>05:07.2</c> in <c>mm:ss.0</c>,
/// though each double is a little above the half. Elapsed time is the whole value times 86,400 and the precision, taken
/// to 15 significant digits (<see cref="Digits"/>), rounded half away from zero, while the minutes, seconds and dates
/// beside it are the time of day's: 45306.000005787035, 0.4999998 seconds past midnight, is 3914438400.5 seconds to 15
/// digits, so <c>[s] yyyy</c> shows it as <c>3914438401 2024</c>, where <c>[h]:mm:ss</c> shows <c>1087344:00:00</c>.
/// Minutes and hours aren't rounded: <c>h:mm</c> shows 0:00:59 as <c>0:00</c>. Each group of fractions of a second shows
/// the first digits of the most precise one.
/// </description></item>
/// <item><description>
/// In the 1900 system serial 0 is 1900-01-00 and 60 is 1900-02-29, and the days before 1900-03-01 fall a weekday early
/// (serial 1 is a Sunday). Years show two digits for <c>y</c> and <c>yy</c>, four for more, and so do <c>e</c> (four)
/// and the Buddhist year <c>b</c> (543 more, Phase 8's decision 18; its two digits are its third and fourth, so 10542
/// shows <c>54</c>). Months show their number, two digits, their
/// abbreviated name, their name, their first letter, and for six letters or more their name; days their number, two
/// digits, their day of the week's abbreviated name, and its name. Names and designators are the locale code's
/// language's, or the culture's.
/// </description></item>
/// <item><description>
/// Elapsed time, <c>[h]</c>, <c>[m]</c> and <c>[s]</c>, is the whole hours, minutes or seconds, padded with zeros to its
/// letters; past 15 digits, its 15 significant digits and zeros. Alone among literals it shows any value, a negative one
/// with a minus sign (<c>[h]</c> shows −0.5 as <c>-12</c>, and −1E-09 as <c>-0</c>); beside other tokens it stops at the
/// last date and can't show a negative in the 1900 system.
/// </description></item>
/// <item><description>
/// A system format, <c>[$-F800]</c> or <c>[$-F400]</c>, shows the culture's long date or long time whatever else the
/// section holds, with a minus sign for a negative wherever the section stands.
/// </description></item>
/// <item><description>
/// The text can't pass 255 characters, as a number's can't.
/// </description></item>
/// </list>
/// <para>
/// Nothing allocates: the date's parts come from <see cref="DateTime"/>, a struct, and the text goes into the caller's span
/// (<see cref="ShownText"/>).
/// </para>
/// </remarks>
internal static class DateText
{
	/// <summary>The serial of 9999-12-31, Excel's last date, in the 1900 date system.</summary>
	private const long LastDay1900 = 2958465;

	/// <summary>The serial of 9999-12-31 in the 1904 date system.</summary>
	private const long LastDay1904 = 2957003;

	/// <summary>The serial of 1900-02-29, the day the 1900 date system has that the calendar doesn't.</summary>
	private const long LeapDay1900 = 60;

	/// <summary>The seconds in a day.</summary>
	private const long SecondsPerDay = 86400;

	/// <summary>The seconds in an hour.</summary>
	private const long SecondsPerHour = 3600;

	/// <summary>The seconds in a minute.</summary>
	private const long SecondsPerMinute = 60;

	/// <summary>The years a Buddhist year is ahead of the Gregorian one.</summary>
	private const int BuddhistYears = 543;

	/// <summary>The day before 1900-01-01: serials 1 to 59 count from it.</summary>
	private static readonly DateTime Before1900 = new(1899, 12, 31);

	/// <summary>Two days before 1900-01-01: serials from 61 count from it, past the 1900 system's 1900-02-29.</summary>
	private static readonly DateTime Epoch1900 = new(1899, 12, 30);

	/// <summary>1904-01-01, serial 0 of the 1904 date system.</summary>
	private static readonly DateTime Epoch1904 = new(1904, 1, 1);

	/// <summary>The units a second is shown in, by the digits of its fractions: 1, 10, 100 or 1,000.</summary>
	private static readonly long[] Units = [1, 10, 100, 1000];

	/// <summary>Writes a number in a date, time or elapsed-time section, or a system format.</summary>
	/// <param name="code">The code, which holds the section's literals.</param>
	/// <param name="section">The section.</param>
	/// <param name="value">The number, finite, with a number below the smallest normal double already 0.</param>
	/// <param name="minus">Whether the section shows a negative number's minus sign.</param>
	/// <param name="culture">The culture's names, and its long date and long time.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="text">The text.</param>
	/// <returns>
	/// Whether Excel can show the number: <see langword="false"/> for a negative date or time in the 1900 system, one past
	/// 9999-12-31, and text past its limit.
	/// </returns>
	public static bool TryWrite(FormatCode code, FormatSection section, double value, bool minus, DisplayCulture culture, bool date1904, ref ShownText text)
	{
		var negative = value < 0;
		if (section.System != SystemFormat.None)
		{
			// The culture's long date or long time, with a minus sign wherever the section stands.
			code = section.System == SystemFormat.LongDate ? culture.LongDateFormat : culture.LongTimeFormat;
			if (code.Numbers.Length == 0 || code.Numbers[0].Kind != FormatSectionKind.Date)
			{
				return false;
			}

			section = code.Numbers[0];
			minus = true;
		}

		if (negative && !date1904 && !section.ElapsedAlone)
		{
			return false;
		}

		var abs = Math.Abs(value);
		var unit = Units[section.SubSecondDigits];
		var perDay = SecondsPerDay * unit;
		var lastDay = date1904 ? LastDay1904 : LastDay1900;
		long day = 0;
		long within = 0;

		// Elapsed time: the whole value in the section's units, to 15 digits; past them, the 15 digits are what show.
		var scaled = abs * perDay;
		var whole = Digits.Of(scaled).Round(0);
		var total = whole.IntegerLength <= Digits.MaxSignificant ? Whole(whole) : -1;
		if (!section.ElapsedAlone)
		{
			// A date, a time of day, and the parts beside elapsed time: half the unit shown added to the serial, a whole day
			// carrying to the date, up to the last date, and the time of day taken apart by truncation.
			if (abs >= lastDay + 1)
			{
				return false;
			}

			var rounded = abs + (0.5 / perDay);
			day = (long)Math.Floor(rounded);
			within = TimeOfDay(rounded - day, unit);
		}

		if (day > lastDay)
		{
			return false;
		}

		if (minus && negative)
		{
			text.Append('-');
		}

		var names = section.Names ?? culture.Names;
		Date(day, date1904, out var year, out var month, out var dayOfMonth, out var weekDay);
		var hour = within / (SecondsPerHour * unit);
		foreach (var token in section.Tokens)
		{
			switch (token.Kind)
			{
				case FormatTokenKind.Literal:
					text.Append(code.Literal(token));

					break;
				case FormatTokenKind.Year or FormatTokenKind.EraYear:
					WriteYear(year, token.Kind == FormatTokenKind.Year && token.Count <= 2, ref text);

					break;
				case FormatTokenKind.BuddhistYear:
					// Two digits are the year's third and fourth: 10542, 9999's, shows 54.
					var buddhist = year + BuddhistYears;
					WriteYear(token.Count <= 2 && buddhist >= 10000 ? buddhist / 10 : buddhist, token.Count <= 2, ref text);

					break;
				case FormatTokenKind.Month:
					WriteMonth(month, token.Count, names, ref text);

					break;
				case FormatTokenKind.Day:
					if (token.Count <= 2)
					{
						WriteNumber(dayOfMonth, token.Count, ref text);
					}
					else
					{
						text.Append(token.Count == 3 ? names.AbbreviatedDays[weekDay] : names.Days[weekDay]);
					}

					break;
				case FormatTokenKind.WeekDay:
					text.Append(token.Count == 3 ? names.AbbreviatedDays[weekDay] : names.Days[weekDay]);

					break;
				case FormatTokenKind.Hour:
					var shown = section.TwelveHour ? ((hour + 11) % 12) + 1 : hour;
					WriteNumber(shown, token.Count, ref text);

					break;
				case FormatTokenKind.Minute:
					WriteNumber(within / (SecondsPerMinute * unit) % 60, token.Count, ref text);

					break;
				case FormatTokenKind.Second:
					WriteNumber(within / unit % 60, token.Count, ref text);

					break;
				case FormatTokenKind.SubSecond:
					WriteFraction(within % unit, section.SubSecondDigits, token.Count, ref text);

					break;
				case FormatTokenKind.ElapsedHours:
					WriteElapsed(total, scaled, SecondsPerHour * unit, token.Count, ref text);

					break;
				case FormatTokenKind.ElapsedMinutes:
					WriteElapsed(total, scaled, SecondsPerMinute * unit, token.Count, ref text);

					break;
				case FormatTokenKind.ElapsedSeconds:
					WriteElapsed(total, scaled, unit, token.Count, ref text);

					break;
				case FormatTokenKind.AmPm:
					text.Append(hour < 12 ? names.AM : names.PM);

					break;
				case FormatTokenKind.ShortAmPm:
					text.Append(code.Literal(token).Slice(hour < 12 ? 0 : 1, 1));

					break;
			}
		}

		return text.Chars <= NumberText.MaxChars;
	}

	/// <summary>A day's date in a date system, and its day of the week.</summary>
	/// <param name="serial">The day's serial, 0 to the last date.</param>
	/// <param name="date1904">Whether the 1904 date system counts it.</param>
	/// <param name="year">The year.</param>
	/// <param name="month">The month, 1 to 12.</param>
	/// <param name="day">The day of the month: 0 for the 1900 system's serial 0.</param>
	/// <param name="weekDay">The day of the week, 0 for Sunday.</param>
	private static void Date(long serial, bool date1904, out int year, out int month, out int day, out int weekDay)
	{
		if (date1904)
		{
			// 1904-01-01 was a Friday.
			weekDay = (int)((serial + 5) % 7);
			var date = Epoch1904.AddDays(serial);
			(year, month, day) = (date.Year, date.Month, date.Day);

			return;
		}

		// Serial 1, 1900-01-01, is a Sunday in Excel, a day early until 1900-03-01, past its 1900-02-29.
		weekDay = (int)((serial + 6) % 7);
		if (serial == 0)
		{
			(year, month, day) = (1900, 1, 0);
		}
		else if (serial == LeapDay1900)
		{
			(year, month, day) = (1900, 2, 29);
		}
		else
		{
			var date = (serial < LeapDay1900 ? Before1900 : Epoch1900).AddDays(serial);
			(year, month, day) = (date.Year, date.Month, date.Day);
		}
	}

	/// <summary>
	/// The time of a day's fraction, rounded already by half the unit shown, in the section's units of a second, as Excel
	/// takes it apart in doubles: the hours are the fraction times 24, truncated; the minutes the rest times 60, truncated;
	/// the seconds the rest times 60, truncated; and their fractions the rest times the units, truncated. It fits all but
	/// eight of 2,277 of Excel's answers about times shown to the second or its fractions (Phase 8's step 7; four doubles),
	/// where step 4's rule, the day's fraction times the units to 15 digits rounded half away from zero, missed 345 (94
	/// doubles), and the seconds times the units, truncated at once, 25 (22).
	/// </summary>
	/// <param name="fraction">The day's fraction, from 0 to below 1.</param>
	/// <param name="unit">The units a second is shown in: 1, 10, 100 or 1,000.</param>
	/// <returns>The time in those units, less than a day's.</returns>
	private static long TimeOfDay(double fraction, long unit)
	{
		var hours = fraction * 24;
		var hour = Math.Floor(hours);
		var minutes = (hours - hour) * 60;
		var minute = Math.Floor(minutes);
		var seconds = (minutes - minute) * 60;
		var second = Math.Floor(seconds);

		return ((((((long)hour * 60) + (long)minute) * SecondsPerMinute) + (long)second) * unit) + (long)Math.Floor((seconds - second) * unit);
	}

	/// <summary>An integer's value, from its digits.</summary>
	/// <param name="digits">The digits, whole and below 10^15.</param>
	/// <returns>The value.</returns>
	private static long Whole(Digits digits)
	{
		var whole = (long)digits.Significand;
		for (var i = digits.Exponent - digits.Count + 1; i > 0; i--)
		{
			whole *= 10;
		}

		return digits.IsZero ? 0 : whole;
	}

	/// <summary>Writes a year: its last two digits, or all of them.</summary>
	/// <param name="year">The year.</param>
	/// <param name="twoDigits">Whether to write the last two digits.</param>
	/// <param name="text">The text.</param>
	private static void WriteYear(int year, bool twoDigits, ref ShownText text)
	{
		if (twoDigits)
		{
			WriteNumber(year % 100, 2, ref text);
		}
		else
		{
			WriteNumber(year, 1, ref text);
		}
	}

	/// <summary>Writes a month: its number for one or two letters, its abbreviated name for three, its name for four or six and more, its first letter for five.</summary>
	/// <param name="month">The month, 1 to 12.</param>
	/// <param name="letters">The token's letters.</param>
	/// <param name="names">The language's names.</param>
	/// <param name="text">The text.</param>
	private static void WriteMonth(int month, int letters, DateNames names, ref ShownText text)
	{
		switch (letters)
		{
			case 1 or 2:
				WriteNumber(month, letters, ref text);

				break;
			case 3:
				text.Append(names.AbbreviatedMonths[month - 1]);

				break;
			case 5:
				text.Append(names.MonthLetters[month - 1]);

				break;
			default:
				text.Append(names.Months[month - 1]);

				break;
		}
	}

	/// <summary>Writes a number, padded with zeros to two digits for a token of two letters or more.</summary>
	/// <param name="number">The number, not negative.</param>
	/// <param name="letters">The token's letters.</param>
	/// <param name="text">The text.</param>
	private static void WriteNumber(long number, int letters, ref ShownText text)
	{
		if (letters >= 2 && number < 10)
		{
			text.Append('0');
		}

		Span<byte> digits = stackalloc byte[20];
		var count = 0;
		do
		{
			digits[count++] = (byte)('0' + (number % 10));
			number /= 10;
		}
		while (number > 0);

		while (count > 0)
		{
			text.Append((char)digits[--count]);
		}
	}

	/// <summary>Writes the first digits of a second's fraction.</summary>
	/// <param name="fraction">The fraction, in the section's units.</param>
	/// <param name="precision">The digits the section rounds to, 1 to 3.</param>
	/// <param name="digits">The digits to write, at most the precision.</param>
	/// <param name="text">The text.</param>
	private static void WriteFraction(long fraction, int precision, int digits, ref ShownText text)
	{
		var divisor = Units[precision - 1];
		for (var i = 0; i < digits; i++)
		{
			text.AppendDigit((int)(fraction / divisor % 10));
			divisor /= 10;
		}
	}

	/// <summary>Writes an elapsed time: the whole time in a unit, padded with zeros to the token's letters.</summary>
	/// <param name="total">The whole time in the section's units of a second, to 15 digits and rounded; −1 past 15 digits.</param>
	/// <param name="scaled">The whole time in those units, not rounded, for one past 15 digits.</param>
	/// <param name="divisor">The units in the token's unit.</param>
	/// <param name="letters">The token's letters.</param>
	/// <param name="text">The text.</param>
	private static void WriteElapsed(long total, double scaled, long divisor, int letters, ref ShownText text)
	{
		// Exact in integers to 15 digits; past them, the quotient's 15 significant digits (Excel shows [h] of
		// 1.23456789012345E+20 as 2962962936296280000000).
		var digits = Digits.Of(total >= 0 ? total / divisor : scaled / divisor);
		var length = Math.Max(digits.IntegerLength, 1);
		for (var i = length; i < letters; i++)
		{
			text.Append('0');
		}

		for (var p = length - 1; p >= 0; p--)
		{
			text.AppendDigit(digits.DigitAt(p));
		}
	}
}
