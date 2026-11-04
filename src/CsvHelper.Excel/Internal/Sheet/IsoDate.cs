using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// Converts the ISO 8601 text of a <c>t="d"</c> cell to the serial number Excel gives it, so that it reads as a date cell:
/// a date (<c>2024-01-02</c>), a date and time (<c>2024-01-02T03:04:05.00000006519257800</c>, as Excel's Strict files
/// write them, to the bit of the serial), or a time alone (<c>03:04:05</c>), each with an optional <c>Z</c>; or a
/// duration (<c>PT36H0M0.000S</c>), as Excel's Strict files write a cell in an elapsed-time format.
/// </summary>
internal static class IsoDate
{
	/// <summary>The fraction digits read: past 20, a digit can't change a <see cref="decimal"/> of seconds.</summary>
	private const int MaxFractionDigits = 20;

	/// <summary>The most digits of a duration's days, hours, minutes or seconds, which a <see cref="decimal"/> of seconds holds.</summary>
	private const int MaxDurationDigits = 15;

	private const decimal SecondsPerDay = 86_400m;

	private static readonly DateTime Day1899Dec30 = new(1899, 12, 30);
	private static readonly DateTime Day1899Dec31 = new(1899, 12, 31);
	private static readonly DateTime Day1900Mar1 = new(1900, 3, 1);
	private static readonly DateTime Day1904Jan1 = new(1904, 1, 1);

	/// <summary>
	/// Converts the text to a serial. A date counts in the workbook's date system: in the 1900 system, 1899-12-31 is day
	/// 0 and 1900-03-01 is 61, past Excel's 1900-02-29; in the 1904 system, 1904-01-01 is day 0. A time alone is on day 0.
	/// The time is the serial's fraction, worked out in <see cref="decimal"/> and rounded once to a <see cref="double"/>. A
	/// duration is its days, in the same way, whatever the date system.
	/// </summary>
	/// <remarks>
	/// Not dates: offsets other than <c>Z</c>, week and ordinal dates, the basic format (<c>20240102</c>), <c>24:00</c>,
	/// leap seconds, a date before the date system's day 0, and anything else. Their cells stay text. A duration has days
	/// (<c>D</c>), and after <c>T</c> hours, minutes and seconds (<c>H</c>, <c>M</c>, <c>S</c>), each a whole number of up
	/// to 15 digits, in that order, at least one of them, and a fraction only in the seconds; years, months, weeks, a sign
	/// and a <c>Z</c> are not taken.
	/// </remarks>
	/// <param name="text">The text, decoded; XML white space around it is ignored.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="serial">The serial number.</param>
	/// <returns>Whether the text is a date, a time or a duration of those forms, a date within the date system.</returns>
	public static bool TryParse(ReadOnlySpan<byte> text, bool date1904, out double serial)
	{
		serial = 0;
		text = Trim(text);
		if (text.Length > 0 && text[0] == (byte)'P')
		{
			if (!TryDuration(text, out var days))
			{
				return false;
			}

			serial = ToDouble(days);

			return true;
		}

		if (text.Length > 0 && text[text.Length - 1] == (byte)'Z')
		{
			text = text.Slice(0, text.Length - 1);
		}

		long day = 0;
		var position = 0;
		if (text.Length >= 10 && text[4] == (byte)'-' && text[7] == (byte)'-')
		{
			if (!TryDay(text, date1904, out day))
			{
				return false;
			}

			position = 10;
			if (position == text.Length)
			{
				serial = day;

				return true;
			}

			if (text[position] != (byte)'T')
			{
				return false;
			}

			position++;
		}

		if (!TrySeconds(text.Slice(position), out var seconds))
		{
			return false;
		}

		serial = ToDouble(day + (seconds / SecondsPerDay));

		return true;
	}

	/// <summary>
	/// Reads an ISO 8601 duration as days: <c>P</c>, then days (<c>1D</c>), then <c>T</c> and hours, minutes and seconds
	/// (<c>36H</c>, <c>0M</c>, <c>0.000S</c>), as Excel writes <c>PT36H0M0.000S</c>, <c>PT2160M0.000S</c> or <c>PT129600.000S</c>.
	/// </summary>
	/// <param name="text">The text, from its <c>P</c>.</param>
	/// <param name="days">The days.</param>
	/// <returns>Whether the text is such a duration.</returns>
	private static bool TryDuration(ReadOnlySpan<byte> text, out decimal days)
	{
		days = 0;
		decimal seconds = 0;
		var inTime = false;
		var last = 0;
		var i = 1;
		while (i < text.Length)
		{
			if (text[i] == (byte)'T')
			{
				// Once, and with a part after it.
				if (inTime || i + 1 == text.Length)
				{
					return false;
				}

				inTime = true;
				i++;

				continue;
			}

			var start = i;
			while (i < text.Length && (uint)(text[i] - '0') <= 9)
			{
				i++;
			}

			if (i == start || i - start > MaxDurationDigits || !TryLongDigits(text.Slice(start, i - start), out var whole))
			{
				return false;
			}

			decimal fraction = 0;
			var hasFraction = i < text.Length && text[i] == (byte)'.';
			if (hasFraction)
			{
				var fractionStart = ++i;
				while (i < text.Length && (uint)(text[i] - '0') <= 9)
				{
					i++;
				}

				if (!TryFraction(text.Slice(fractionStart, i - fractionStart), out fraction))
				{
					return false;
				}
			}

			if (i == text.Length)
			{
				return false;
			}

			// Each part in its place: days before T, then hours, minutes and seconds; a fraction only in the seconds.
			var (order, unit) = (text[i], inTime) switch
			{
				((byte)'D', false) => (1, 86_400m),
				((byte)'H', true) => (2, 3_600m),
				((byte)'M', true) => (3, 60m),
				((byte)'S', true) => (4, 1m),
				_ => (0, 0m),
			};
			if (order <= last || (hasFraction && order != 4))
			{
				return false;
			}

			last = order;
			seconds += (whole + fraction) * unit;
			i++;
		}

		days = seconds / SecondsPerDay;

		return last > 0;
	}

	/// <summary>A serial worked out in <see cref="decimal"/>, rounded once to a <see cref="double"/>.</summary>
	/// <param name="value">The serial.</param>
	/// <returns>The nearest <see cref="double"/>.</returns>
	private static double ToDouble(decimal value) => double.Parse(value.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

	/// <summary>Reads <c>yyyy-MM-dd</c> as a day of the date system.</summary>
	/// <param name="text">The text, whose first 10 bytes are the date.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="day">The day's number.</param>
	/// <returns>Whether the date is valid and on or after the date system's day 0.</returns>
	private static bool TryDay(ReadOnlySpan<byte> text, bool date1904, out long day)
	{
		day = 0;
		if (!TryDigits(text.Slice(0, 4), out var year) || !TryDigits(text.Slice(5, 2), out var month) || !TryDigits(text.Slice(8, 2), out var dayOfMonth)
			|| year < 1 || month is < 1 or > 12 || dayOfMonth < 1 || dayOfMonth > DateTime.DaysInMonth(year, month))
		{
			return false;
		}

		var date = new DateTime(year, month, dayOfMonth);
		var epoch = date1904 ? Day1904Jan1 : date < Day1900Mar1 ? Day1899Dec31 : Day1899Dec30;
		if (date < (date1904 ? Day1904Jan1 : Day1899Dec31))
		{
			return false;
		}

		day = (date - epoch).Days;

		return true;
	}

	/// <summary>Reads <c>HH:mm</c>, <c>HH:mm:ss</c> or <c>HH:mm:ss.fff</c> as seconds of the day.</summary>
	/// <param name="text">The time, and nothing after it.</param>
	/// <param name="seconds">The seconds since midnight.</param>
	/// <returns>Whether the text is such a time, from 00:00 to 23:59:59.999.</returns>
	private static bool TrySeconds(ReadOnlySpan<byte> text, out decimal seconds)
	{
		seconds = 0;
		if (text.Length < 5 || text[2] != (byte)':' || !TryDigits(text.Slice(0, 2), out var hours) || !TryDigits(text.Slice(3, 2), out var minutes)
			|| hours > 23 || minutes > 59)
		{
			return false;
		}

		var whole = (hours * 3600) + (minutes * 60);
		if (text.Length == 5)
		{
			seconds = whole;

			return true;
		}

		if (text.Length < 8 || text[5] != (byte)':' || !TryDigits(text.Slice(6, 2), out var secondsOfMinute) || secondsOfMinute > 59)
		{
			return false;
		}

		seconds = whole + secondsOfMinute;
		if (text.Length == 8)
		{
			return true;
		}

		if (text[8] != (byte)'.' || !TryFraction(text.Slice(9), out var fraction))
		{
			return false;
		}

		seconds += fraction;

		return true;
	}

	/// <summary>Reads the digits after a point as a fraction: the first 20, which a <see cref="decimal"/> holds; the rest must be digits too.</summary>
	/// <param name="digits">The digits, at least one.</param>
	/// <param name="fraction">The fraction, from 0 to less than 1.</param>
	/// <returns>Whether there are digits, and only digits.</returns>
	private static bool TryFraction(ReadOnlySpan<byte> digits, out decimal fraction)
	{
		fraction = 0;
		if (digits.IsEmpty)
		{
			return false;
		}

		decimal value = 0;
		decimal scale = 1;
		for (var i = 0; i < digits.Length; i++)
		{
			var digit = digits[i] - '0';
			if ((uint)digit > 9)
			{
				return false;
			}

			if (i < MaxFractionDigits)
			{
				value = (value * 10) + digit;
				scale *= 10;
			}
		}

		fraction = value / scale;

		return true;
	}

	/// <summary>Reads ASCII digits as a number.</summary>
	/// <param name="digits">The digits.</param>
	/// <param name="value">The number.</param>
	/// <returns>Whether every byte is a digit.</returns>
	private static bool TryDigits(ReadOnlySpan<byte> digits, out int value)
	{
		var valid = TryLongDigits(digits, out var wide);
		value = (int)wide;

		return valid;
	}

	/// <summary>Reads ASCII digits as a number; at most 18 of them, so that it can't overflow.</summary>
	/// <param name="digits">The digits.</param>
	/// <param name="value">The number.</param>
	/// <returns>Whether every byte is a digit.</returns>
	private static bool TryLongDigits(ReadOnlySpan<byte> digits, out long value)
	{
		value = 0;
		foreach (var b in digits)
		{
			var digit = b - '0';
			if ((uint)digit > 9)
			{
				return false;
			}

			value = (value * 10) + digit;
		}

		return true;
	}

	/// <summary>The text without XML white space (space, tab, CR, LF) around it.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The trimmed text.</returns>
	private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
	{
		var start = 0;
		var end = text.Length;
		while (start < end && text[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
		{
			start++;
		}

		while (end > start && text[end - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
		{
			end--;
		}

		return text.Slice(start, end - start);
	}
}
