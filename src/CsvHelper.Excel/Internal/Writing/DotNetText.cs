using System;

namespace CsvHelper.Excel;

/// <summary>
/// Reads back the texts that .NET formats values with by default, which is what a cell sink gets (Phase 0): dates and
/// times in ISO 8601 round-trip format (<c>"O"</c>), durations in the constant format (<c>"c"</c>), and numbers, in the
/// invariant culture or the writer's. The layouts are fixed, so they are read by position. Anything else, such as the
/// text of another format, is not read: the field is then a text cell.
/// </summary>
internal static class DotNetText
{
	/// <summary>The longest number text that is read: longer text, which only a format gives, is a text cell.</summary>
	public const int MaxNumberLength = 128;

	private const long TicksPerSecond = 10_000_000;
	private const long TicksPerMinute = 60 * TicksPerSecond;
	private const long TicksPerHour = 60 * TicksPerMinute;
	private const long TicksPerDay = 24 * TicksPerHour;

	// The most days a TimeSpan holds (TimeSpan.MaxValue is 10675199.02:48:05.4775807).
	private const long MaxDurationDays = 10_675_199;

	/// <summary>
	/// Reads a <see cref="DateTime"/>'s or a <see cref="DateTimeOffset"/>'s <c>"O"</c> text:
	/// <c>yyyy-MM-ddTHH:mm:ss.fffffff</c>, then nothing (unspecified), <c>Z</c> (UTC) or an offset (<c>+01:00</c>).
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="ticks">The clock time's ticks, whatever the offset.</param>
	/// <param name="offset">The offset's ticks: 0 without one or with <c>Z</c>.</param>
	/// <returns>Whether the text is in that layout and a valid date and time.</returns>
	public static bool TryDateTime(ReadOnlySpan<byte> text, out long ticks, out long offset)
	{
		ticks = 0;
		offset = 0;
		if (text.Length < 27 || text[10] != (byte)'T' || !TryDate(text.Slice(0, 10), out var date) || !TryTime(text.Slice(11, 16), out var time))
		{
			return false;
		}

		var rest = text.Slice(27);
		if (rest.Length == 1 && rest[0] == (byte)'Z')
		{
			rest = default;
		}
		else if (rest.Length == 6 && rest[0] is (byte)'+' or (byte)'-' && rest[3] == (byte)':' &&
			TryDigits(rest.Slice(1, 2), out var hours) && hours <= 14 && TryDigits(rest.Slice(4, 2), out var minutes) && minutes <= 59)
		{
			offset = (rest[0] == (byte)'-' ? -1 : 1) * ((hours * TicksPerHour) + (minutes * TicksPerMinute));
			rest = default;
		}

		if (!rest.IsEmpty)
		{
			return false;
		}

		ticks = date + time;

		return true;
	}

	/// <summary>Reads a <c>DateOnly</c>'s <c>"O"</c> text, <c>yyyy-MM-dd</c>, which a date and time starts with too.</summary>
	/// <param name="text">The text.</param>
	/// <param name="ticks">The ticks of the day's start.</param>
	/// <returns>Whether the text is in that layout and a valid date.</returns>
	public static bool TryDate(ReadOnlySpan<byte> text, out long ticks)
	{
		ticks = 0;
		if (text.Length != 10 || text[4] != (byte)'-' || text[7] != (byte)'-' ||
			!TryDigits(text.Slice(0, 4), out var year) || !TryDigits(text.Slice(5, 2), out var month) || !TryDigits(text.Slice(8, 2), out var day) ||
			year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth((int)year, (int)month))
		{
			return false;
		}

		ticks = new DateTime((int)year, (int)month, (int)day).Ticks;

		return true;
	}

	/// <summary>Reads a <c>TimeOnly</c>'s <c>"O"</c> text, <c>HH:mm:ss.fffffff</c>, which a date and time ends with too.</summary>
	/// <param name="text">The text.</param>
	/// <param name="ticks">The ticks since midnight.</param>
	/// <returns>Whether the text is in that layout and a valid time of day.</returns>
	public static bool TryTime(ReadOnlySpan<byte> text, out long ticks)
	{
		ticks = 0;
		if (text.Length != 16 || text[8] != (byte)'.' || !TryClock(text.Slice(0, 8), out var clock) || !TryDigits(text.Slice(9, 7), out var fraction))
		{
			return false;
		}

		ticks = clock + fraction;

		return true;
	}

	/// <summary>
	/// Reads a <see cref="TimeSpan"/>'s <c>"c"</c> text: <c>[-][d.]hh:mm:ss[.fffffff]</c>, with the days and the fraction
	/// only when they aren't 0.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="ticks">The duration's ticks.</param>
	/// <returns>Whether the text is in that layout and within <see cref="TimeSpan"/>'s range.</returns>
	public static bool TryDuration(ReadOnlySpan<byte> text, out long ticks)
	{
		ticks = 0;
		var negative = !text.IsEmpty && text[0] == (byte)'-';
		var rest = negative ? text.Slice(1) : text;
		ulong days = 0;
		var dot = rest.IndexOf((byte)'.');
		var colon = rest.IndexOf((byte)':');
		if (dot >= 0 && colon > dot)
		{
			if (dot > 8 || !TryDigits(rest.Slice(0, dot), out var value) || value > MaxDurationDays)
			{
				return false;
			}

			days = (ulong)value;
			rest = rest.Slice(dot + 1);
		}

		if (rest.Length < 8 || !TryClock(rest.Slice(0, 8), out var clock))
		{
			return false;
		}

		long fraction = 0;
		if (rest.Length != 8 && (rest.Length != 16 || rest[8] != (byte)'.' || !TryDigits(rest.Slice(9, 7), out fraction)))
		{
			return false;
		}

		var magnitude = (days * (ulong)TicksPerDay) + (ulong)clock + (ulong)fraction;
		if (magnitude > (negative ? (ulong)long.MaxValue + 1 : long.MaxValue))
		{
			return false;
		}

		ticks = negative ? unchecked((long)(0 - magnitude)) : (long)magnitude;

		return true;
	}

	/// <summary>
	/// Reads a number written in .NET's default format, in the invariant culture or another, as the invariant text Excel
	/// stores: <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?(E[+-]?[0-9]+)?</c>, nothing else. That is the text of every integer,
	/// <see cref="decimal"/> and floating-point value in the invariant culture, and a culture's is the same with its signs and
	/// decimal separator. Leading zeros and group separators are not read, so <c>0042</c> and <c>1,234.50</c>, whose look a
	/// number cell would lose, stay text.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="culture">The writer's culture's signs and separator, or <see langword="null"/> for the invariant ones.</param>
	/// <param name="destination">The invariant text, of at most <see cref="MaxNumberLength"/> bytes.</param>
	/// <param name="significant">
	/// The number of significant digits: from the first digit that isn't 0 to the last, so that <c>0.00120</c> has 2 and
	/// <c>10000000000000000000</c> has 1.
	/// </param>
	/// <returns>The invariant text's length, or -1 when the text isn't a number in that form.</returns>
	public static int ToInvariantNumber(ReadOnlySpan<byte> text, NumberCulture? culture, Span<byte> destination, out int significant)
	{
		significant = 0;
		if (text.Length > MaxNumberLength)
		{
			return -1;
		}

		int length;
		if (culture is null)
		{
			text.CopyTo(destination);
			length = text.Length;
		}
		else
		{
			length = culture.ToInvariant(text, destination);
			if (length < 0)
			{
				return -1;
			}
		}

		return IsInvariantNumber(destination.Slice(0, length), out significant) ? length : -1;
	}

	/// <summary>
	/// Whether text is a number in the invariant grammar, <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?(E[+-]?[0-9]+)?</c>, and its
	/// significant digits.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="significant">The significant digits of its mantissa.</param>
	/// <returns>Whether it is.</returns>
	public static bool IsInvariantNumber(ReadOnlySpan<byte> text, out int significant)
	{
		significant = 0;
		var i = text.Length > 0 && text[0] == (byte)'-' ? 1 : 0;
		var start = i;
		if (i >= text.Length || !IsDigit(text[i]))
		{
			return false;
		}

		if (text[i] == (byte)'0')
		{
			i++;
		}
		else
		{
			i = SkipDigits(text, i);
		}

		if (i < text.Length && text[i] == (byte)'.')
		{
			var fraction = ++i;
			i = SkipDigits(text, i);
			if (i == fraction)
			{
				return false;
			}
		}

		var end = i;
		if (i < text.Length && text[i] == (byte)'E')
		{
			i++;
			if (i < text.Length && text[i] is (byte)'+' or (byte)'-')
			{
				i++;
			}

			var exponent = i;
			i = SkipDigits(text, i);
			if (i == exponent)
			{
				return false;
			}
		}

		if (i != text.Length)
		{
			return false;
		}

		significant = Significant(text.Slice(start, end - start));

		return true;
	}

	/// <summary>The significant digits of a mantissa: from its first digit that isn't 0 to its last, the point not counted.</summary>
	/// <param name="mantissa">Digits and at most one point.</param>
	/// <returns>The number of significant digits, 0 for a zero.</returns>
	private static int Significant(ReadOnlySpan<byte> mantissa)
	{
		var first = -1;
		var last = -1;
		var digits = 0;
		foreach (var b in mantissa)
		{
			if (b == (byte)'.')
			{
				continue;
			}

			if (b != (byte)'0')
			{
				if (first < 0)
				{
					first = digits;
				}

				last = digits;
			}

			digits++;
		}

		return first < 0 ? 0 : last - first + 1;
	}

	/// <summary>Reads <c>HH:mm:ss</c>, with hours to 23 and minutes and seconds to 59.</summary>
	/// <param name="text">Eight bytes.</param>
	/// <param name="ticks">The ticks of that time of day.</param>
	/// <returns>Whether it is a valid clock time.</returns>
	private static bool TryClock(ReadOnlySpan<byte> text, out long ticks)
	{
		ticks = 0;
		if (text[2] != (byte)':' || text[5] != (byte)':' ||
			!TryDigits(text.Slice(0, 2), out var hours) || !TryDigits(text.Slice(3, 2), out var minutes) || !TryDigits(text.Slice(6, 2), out var seconds) ||
			hours > 23 || minutes > 59 || seconds > 59)
		{
			return false;
		}

		ticks = (hours * TicksPerHour) + (minutes * TicksPerMinute) + (seconds * TicksPerSecond);

		return true;
	}

	/// <summary>Reads decimal digits, all of them.</summary>
	/// <param name="text">The digits, at most 18 of them.</param>
	/// <param name="value">Their value.</param>
	/// <returns>Whether the text is one or more digits and nothing else.</returns>
	private static bool TryDigits(ReadOnlySpan<byte> text, out long value)
	{
		value = 0;
		if (text.IsEmpty)
		{
			return false;
		}

		foreach (var b in text)
		{
			if (!IsDigit(b))
			{
				return false;
			}

			value = (value * 10) + (b - '0');
		}

		return true;
	}

	/// <summary>The position after the digits that start at a position.</summary>
	/// <param name="text">The text.</param>
	/// <param name="i">The position.</param>
	/// <returns>The position of the first byte that isn't a digit, or the text's length.</returns>
	private static int SkipDigits(ReadOnlySpan<byte> text, int i)
	{
		while (i < text.Length && IsDigit(text[i]))
		{
			i++;
		}

		return i;
	}

	/// <summary>Whether a byte is an ASCII digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is <c>0</c> to <c>9</c>.</returns>
	private static bool IsDigit(byte b) => (uint)(b - '0') <= 9;
}
