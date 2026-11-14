using System;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The types of CSV's fields, for a sheet converted from CSV: which types a field matches, the narrowest type a column's
/// fields share, and a matching field's text in the form a sheet's sink reads, with the type it takes it as.
/// </summary>
/// <remarks>
/// <para>
/// A field matches, by its bytes alone:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ColumnType.Number"/>: <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?([Ee][+-]?[0-9]+)?</c>, the text the sink reads as a
/// number (<see cref="DotNetText.IsInvariantNumber"/>) with <c>e</c> too, in the CSV's culture: its negative and positive
/// signs and its decimal separator, so <c>1,5</c> in <c>de-DE</c>. Zero, or from 1E-307 to below 1E+308, the numbers
/// Excel takes typed in. At most 15 significant digits (from the first digit that isn't 0 to the last, as the sink counts
/// them), unless <see cref="ExcelWriterOptions.LongNumbers"/> isn't <see cref="ExcelLongNumbers.Text"/>, when the sink
/// writes the longer ones as that says.
/// </description></item>
/// <item><description><see cref="ColumnType.Date"/>: <c>yyyy-MM-dd</c>, of a day that exists; and <see cref="ColumnType.DateTime"/> too.</description></item>
/// <item><description>
/// <see cref="ColumnType.DateTime"/>: a date, <c>T</c> or a space, <c>HH:mm</c>, then <c>:ss</c> and a fraction of 1 to 7
/// digits if any.
/// </description></item>
/// <item><description>
/// <see cref="ColumnType.DateTimeOffset"/>: a date and time, then <c>Z</c> or <c>+HH:mm</c> or <c>-HH:mm</c> of at most 14
/// hours; only when <see cref="ExcelWriterOptions.DateTimeOffsets"/> isn't <see cref="ExcelDateTimeOffsets.Text"/>, which
/// would make its cell text of the sink's form rather than of the field's.
/// </description></item>
/// <item><description><see cref="ColumnType.Bool"/>: <c>true</c> or <c>false</c>, in any case.</description></item>
/// </list>
/// <para>
/// A field of a typed column is handed to the sink in the form a record writer gives it for its type, so that the sink
/// writes it as it writes that type: a number's invariant text as a <see cref="decimal"/>'s (so
/// <see cref="ExcelWriterOptions.LongNumbers"/> applies), a date's <c>"O"</c> text as a <c>DateOnly</c>'s, a date and
/// time's as a <see cref="DateTime"/>'s, one with an offset as a <see cref="DateTimeOffset"/>'s, and <c>True</c> or
/// <c>False</c> as a <see cref="bool"/>'s. A date the sink would write as text, one before 1900 with
/// <see cref="ExcelWriterOptions.OutOfRangeDates"/> at <see cref="ExcelOutOfRangeDates.Text"/>, is left as the field's
/// text instead of the sink's form of it.
/// </para>
/// </remarks>
internal sealed class ColumnTypes
{
	/// <summary>The most bytes a field's form for the sink takes: a number's invariant text, which is no longer than the field.</summary>
	public const int MaxFormLength = DotNetText.MaxNumberLength;

	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxSignificantDigits = 15;

	/// <summary>The decimal exponent of the smallest number that isn't 0, 1E-307: Excel takes no smaller one typed in.</summary>
	private const int MinMagnitude = -307;

	/// <summary>The decimal exponent of the largest numbers, below 1E+308: Excel takes no larger one typed in.</summary>
	private const int MaxMagnitude = 307;

	/// <summary>A cap on an exponent's value while it is read, far past the magnitudes taken, so that it can't overflow.</summary>
	private const int ExponentCap = 100_000;

	/// <summary>The length of a date and time's <c>"O"</c> text, without an offset: <c>yyyy-MM-ddTHH:mm:ss.fffffff</c>.</summary>
	private const int DateTimeLength = 27;

	private const long TicksPerSecond = 10_000_000;
	private const long TicksPerMinute = 60 * TicksPerSecond;
	private const long TicksPerHour = 60 * TicksPerMinute;

	/// <summary>The most an offset can be: 14 hours, as <see cref="DateTimeOffset"/> takes.</summary>
	private const long MaxOffset = 14 * TicksPerHour;

	// The CSV's culture's negative and positive signs and decimal separator, as UTF-8.
	private readonly byte[] negative;
	private readonly byte[] positive;
	private readonly byte[] separator;
	// Whether numbers of more than 15 significant digits match (LongNumbers isn't Text).
	private readonly bool longNumbers;
	// Whether dates and times with an offset match (DateTimeOffsets isn't Text), and whether their cells are in UTC.
	private readonly bool offsets;
	private readonly bool utc;
	// Whether a date before 1900 is a text cell (OutOfRangeDates is Text), which keeps the field's text.
	private readonly bool outOfRangeText;

	/// <summary>Creates the types of a CSV's fields.</summary>
	/// <param name="provider">The CSV reader's format provider, whose numbers' signs and separator a number has.</param>
	/// <param name="options">The workbook's options, for what becomes of long numbers, offsets and dates before 1900.</param>
	public ColumnTypes(IFormatProvider provider, ExcelWriterOptions options)
	{
		var format = NumberFormatInfo.GetInstance(provider);
		negative = Encoding.UTF8.GetBytes(format.NegativeSign);
		positive = Encoding.UTF8.GetBytes(format.PositiveSign);
		separator = Encoding.UTF8.GetBytes(format.NumberDecimalSeparator);
		longNumbers = options.LongNumbers != ExcelLongNumbers.Text;
		offsets = options.DateTimeOffsets != ExcelDateTimeOffsets.Text;
		utc = options.DateTimeOffsets == ExcelDateTimeOffsets.Utc;
		outOfRangeText = options.OutOfRangeDates == ExcelOutOfRangeDates.Text;
	}

	/// <summary>The bit of a type in a set of types (<see cref="Matches"/>).</summary>
	/// <param name="type">The type.</param>
	/// <returns>The bit.</returns>
	public static int Bit(ColumnType type) => 1 << (int)type;

	/// <summary>
	/// The narrowest type of a set that a column's fields all match: a number, a date before a date and time, a date and
	/// time with an offset, a boolean; text for an empty set.
	/// </summary>
	/// <param name="types">The set, the bits of the types (<see cref="Bit"/>).</param>
	/// <returns>The type.</returns>
	public static ColumnType Narrowest(int types) =>
		(types & Bit(ColumnType.Number)) != 0 ? ColumnType.Number
		: (types & Bit(ColumnType.Date)) != 0 ? ColumnType.Date
		: (types & Bit(ColumnType.DateTime)) != 0 ? ColumnType.DateTime
		: (types & Bit(ColumnType.DateTimeOffset)) != 0 ? ColumnType.DateTimeOffset
		: (types & Bit(ColumnType.Bool)) != 0 ? ColumnType.Bool
		: ColumnType.Text;

	/// <summary>The type a field of a column's type is handed to the sink as.</summary>
	/// <param name="type">The column's type.</param>
	/// <returns>The type.</returns>
	public static Type CellType(ColumnType type) => type switch
	{
		ColumnType.Number => typeof(decimal),
		ColumnType.Date => CellKinds.Date,
		ColumnType.DateTime => typeof(DateTime),
		ColumnType.DateTimeOffset => typeof(DateTimeOffset),
		ColumnType.Bool => typeof(bool),
		_ => typeof(string),
	};

	/// <summary>The types a field matches.</summary>
	/// <param name="field">The field, which isn't null.</param>
	/// <returns>The set of them (<see cref="Bit"/>): empty for text.</returns>
	public int Matches(ReadOnlySpan<byte> field)
	{
		if (field.IsEmpty)
		{
			return 0;
		}

		if (field[0] is (byte)'t' or (byte)'T' or (byte)'f' or (byte)'F')
		{
			return TryBool(field, out _) ? Bit(ColumnType.Bool) : 0;
		}

		Span<byte> number = stackalloc byte[MaxFormLength];
		if (TryNumber(field, number) >= 0)
		{
			return Bit(ColumnType.Number);
		}

		if (DotNetText.TryDate(field, out _))
		{
			return Bit(ColumnType.Date) | Bit(ColumnType.DateTime);
		}

		if (TryDateTime(field, out _, out _, out var clockEnd))
		{
			return clockEnd == field.Length ? Bit(ColumnType.DateTime) : offsets ? Bit(ColumnType.DateTimeOffset) : 0;
		}

		return 0;
	}

	/// <summary>
	/// Writes a field of a column of a type in the form the sink reads for it (see <see cref="ColumnTypes"/>), when it
	/// matches the type.
	/// </summary>
	/// <param name="field">The field, which isn't null.</param>
	/// <param name="type">The column's type, not <see cref="ColumnType.Text"/>.</param>
	/// <param name="destination">The form, of at most <see cref="MaxFormLength"/> bytes.</param>
	/// <returns>The form's length, or -1 for a field to write as text: one that doesn't match, or a date before 1900 that is text.</returns>
	public int Format(ReadOnlySpan<byte> field, ColumnType type, Span<byte> destination)
	{
		switch (type)
		{
			case ColumnType.Number:
				return TryNumber(field, destination);
			case ColumnType.Bool:
				if (!TryBool(field, out var value))
				{
					return -1;
				}

				var text = value ? "True"u8 : "False"u8;
				text.CopyTo(destination);

				return text.Length;
			case ColumnType.Date:
				if (!DotNetText.TryDate(field, out var day) || IsText(day))
				{
					return -1;
				}

				field.CopyTo(destination);

				return field.Length;
			default:
				return FormatDateTime(field, type == ColumnType.DateTimeOffset, destination);
		}
	}

	/// <summary>Reads <c>true</c> or <c>false</c>, in any case.</summary>
	/// <param name="field">The field.</param>
	/// <param name="value">Its value.</param>
	/// <returns>Whether it is one.</returns>
	private static bool TryBool(ReadOnlySpan<byte> field, out bool value)
	{
		value = field.Length == 4;
		var word = value ? "true"u8 : "false"u8;
		if (field.Length != word.Length)
		{
			return false;
		}

		for (var i = 0; i < word.Length; i++)
		{
			// Only a letter's case differs by 0x20 from these letters.
			if ((field[i] | 0x20) != word[i])
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Reads a date and time: <c>yyyy-MM-dd</c>, <c>T</c> or a space, <c>HH:mm</c>, then <c>:ss</c> and <c>.</c> and 1 to 7
	/// digits if any, then <c>Z</c>, <c>+HH:mm</c> or <c>-HH:mm</c> if any.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="ticks">The clock time's ticks.</param>
	/// <param name="offset">The offset's ticks: 0 without one or with <c>Z</c>.</param>
	/// <param name="clockEnd">Where the time ends and an offset starts: the field's length without one.</param>
	/// <returns>Whether the field is one.</returns>
	private static bool TryDateTime(ReadOnlySpan<byte> field, out long ticks, out long offset, out int clockEnd)
	{
		ticks = 0;
		offset = 0;
		clockEnd = 0;
		if (field.Length < 16 || field[10] is not ((byte)'T' or (byte)' ') || field[13] != (byte)':' ||
			!DotNetText.TryDate(field.Slice(0, 10), out var date) || !TryTwoDigits(field, 11, 23, out var hours) || !TryTwoDigits(field, 14, 59, out var minutes))
		{
			return false;
		}

		var i = 16;
		long seconds = 0;
		long fraction = 0;
		if (i < field.Length && field[i] == (byte)':')
		{
			if (field.Length < 19 || !TryTwoDigits(field, 17, 59, out seconds))
			{
				return false;
			}

			i = 19;
			if (i < field.Length && field[i] == (byte)'.')
			{
				var start = ++i;
				long scale = TicksPerSecond;
				while (i < field.Length && IsDigit(field[i]) && i - start < 7)
				{
					scale /= 10;
					fraction += (field[i++] - '0') * scale;
				}

				// An eighth digit is no offset's start, so the offset's check below refuses it.
				if (i == start)
				{
					return false;
				}
			}
		}

		clockEnd = i;
		if (i < field.Length)
		{
			if (field[i] == (byte)'Z' && i + 1 == field.Length)
			{
				// UTC: an offset of 0.
			}
			else if (field.Length - i == 6 && field[i] is (byte)'+' or (byte)'-' && field[i + 3] == (byte)':' &&
				TryTwoDigits(field, i + 1, 14, out var offsetHours) && TryTwoDigits(field, i + 4, 59, out var offsetMinutes) &&
				(offsetHours * TicksPerHour) + (offsetMinutes * TicksPerMinute) <= MaxOffset)
			{
				offset = (field[i] == (byte)'-' ? -1 : 1) * ((offsetHours * TicksPerHour) + (offsetMinutes * TicksPerMinute));
			}
			else
			{
				return false;
			}
		}

		ticks = date + (hours * TicksPerHour) + (minutes * TicksPerMinute) + (seconds * TicksPerSecond) + fraction;

		return true;
	}

	/// <summary>Reads two digits at a position, of a value up to a limit.</summary>
	/// <param name="field">The field, at least as long as the position and 2.</param>
	/// <param name="at">The position.</param>
	/// <param name="max">The largest value.</param>
	/// <param name="value">The value.</param>
	/// <returns>Whether they are two digits of a value up to the limit.</returns>
	private static bool TryTwoDigits(ReadOnlySpan<byte> field, int at, int max, out long value)
	{
		value = 0;
		if (!IsDigit(field[at]) || !IsDigit(field[at + 1]))
		{
			return false;
		}

		value = ((field[at] - '0') * 10) + (field[at + 1] - '0');

		return value <= max;
	}

	/// <summary>Whether a byte is an ASCII digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is <c>0</c> to <c>9</c>.</returns>
	private static bool IsDigit(byte b) => (uint)(b - '0') <= 9;

	/// <summary>
	/// Reads a number in the CSV's culture (see <see cref="ColumnTypes"/>) and writes its invariant text, the form the sink
	/// reads: <c>-</c>, <c>.</c>, and <c>E</c> with <c>+</c> or <c>-</c>.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="destination">The invariant text, which is no longer than the field.</param>
	/// <returns>The invariant text's length, or -1 when the field isn't a number that matches.</returns>
	private int TryNumber(ReadOnlySpan<byte> field, Span<byte> destination)
	{
		if (field.Length > MaxFormLength)
		{
			return -1;
		}

		var i = 0;
		var n = 0;
		if (negative.Length > 0 && field.StartsWith(negative))
		{
			destination[n++] = (byte)'-';
			i = negative.Length;
		}

		// The mantissa's digits, without its point: how many come before the point, and the first and last that aren't 0.
		var integer = 0;
		var digits = 0;
		var first = -1;
		var last = -1;
		if (i >= field.Length || !IsDigit(field[i]))
		{
			return -1;
		}

		var leadingZero = field[i] == (byte)'0';
		while (i < field.Length && IsDigit(field[i]) && !(leadingZero && integer == 1))
		{
			Digit(field[i++], destination, ref n, ref digits, ref first, ref last);
			integer++;
		}

		if (i < field.Length && separator.Length > 0 && field.Slice(i).StartsWith(separator))
		{
			destination[n++] = (byte)'.';
			i += separator.Length;
			var fraction = digits;
			while (i < field.Length && IsDigit(field[i]))
			{
				Digit(field[i++], destination, ref n, ref digits, ref first, ref last);
			}

			if (digits == fraction)
			{
				return -1;
			}
		}

		var exponent = 0;
		if (i < field.Length && field[i] is (byte)'E' or (byte)'e')
		{
			destination[n++] = (byte)'E';
			i++;
			var negativeExponent = false;
			if (negative.Length > 0 && field.Slice(i).StartsWith(negative))
			{
				destination[n++] = (byte)'-';
				i += negative.Length;
				negativeExponent = true;
			}
			else if (positive.Length > 0 && field.Slice(i).StartsWith(positive))
			{
				destination[n++] = (byte)'+';
				i += positive.Length;
			}

			var start = i;
			while (i < field.Length && IsDigit(field[i]))
			{
				exponent = Math.Min((exponent * 10) + (field[i] - '0'), ExponentCap);
				destination[n++] = field[i++];
			}

			if (i == start)
			{
				return -1;
			}

			exponent = negativeExponent ? -exponent : exponent;
		}

		if (i != field.Length)
		{
			return -1;
		}

		if (first < 0)
		{
			// Zero, of any exponent.
			return n;
		}

		var magnitude = integer - 1 - first + exponent;
		if ((last - first + 1 > MaxSignificantDigits && !longNumbers) || magnitude is < MinMagnitude or > MaxMagnitude)
		{
			return -1;
		}

		return n;
	}

	/// <summary>Copies a mantissa's digit, and counts it.</summary>
	/// <param name="digit">The digit.</param>
	/// <param name="destination">The invariant text.</param>
	/// <param name="n">The invariant text's length.</param>
	/// <param name="digits">The mantissa's digits so far.</param>
	/// <param name="first">The index of its first digit that isn't 0, or -1.</param>
	/// <param name="last">The index of its last digit that isn't 0, or -1.</param>
	private static void Digit(byte digit, Span<byte> destination, ref int n, ref int digits, ref int first, ref int last)
	{
		destination[n++] = digit;
		if (digit != (byte)'0')
		{
			if (first < 0)
			{
				first = digits;
			}

			last = digits;
		}

		digits++;
	}

	/// <summary>
	/// Writes a date, or a date and time, as a date and time's <c>"O"</c> text, with its offset when it has one:
	/// <c>yyyy-MM-ddTHH:mm:ss.fffffff</c>, then <c>Z</c> or <c>+HH:mm</c>.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="withOffset">Whether the column's dates and times have an offset; otherwise they have none, and dates alone match too.</param>
	/// <param name="destination">The text, of 27 bytes and the offset's.</param>
	/// <returns>The text's length, or -1 for a field to write as text.</returns>
	private int FormatDateTime(ReadOnlySpan<byte> field, bool withOffset, Span<byte> destination)
	{
		long ticks;
		long offset = 0;
		var clockEnd = 0;
		var dateAlone = false;
		if (!withOffset && DotNetText.TryDate(field, out ticks))
		{
			dateAlone = true;
		}
		else if (!TryDateTime(field, out ticks, out offset, out clockEnd) || (clockEnd < field.Length) != withOffset)
		{
			return -1;
		}

		if (IsText(utc ? ticks - offset : ticks))
		{
			return -1;
		}

		field.Slice(0, 10).CopyTo(destination);
		destination[10] = (byte)'T';
		if (dateAlone)
		{
			"00:00:00.0000000"u8.CopyTo(destination.Slice(11));

			return DateTimeLength;
		}

		field.Slice(11, 5).CopyTo(destination.Slice(11));
		var seconds = clockEnd >= 19 ? field.Slice(16, 3) : ":00"u8;
		seconds.CopyTo(destination.Slice(16));
		destination[19] = (byte)'.';
		var fraction = clockEnd > 20 ? field.Slice(20, clockEnd - 20) : default;
		fraction.CopyTo(destination.Slice(20));
		destination.Slice(20 + fraction.Length, 7 - fraction.Length).Fill((byte)'0');
		var zone = field.Slice(clockEnd);
		zone.CopyTo(destination.Slice(DateTimeLength));

		return DateTimeLength + zone.Length;
	}

	/// <summary>Whether a date and time is a text cell: before 1900, Excel's first date, when the options say so.</summary>
	/// <param name="ticks">The date and time's ticks, as its cell would hold it.</param>
	/// <returns>Whether it is.</returns>
	private bool IsText(long ticks) => outOfRangeText && Serial.DateMilliseconds(ticks) < 0;
}
