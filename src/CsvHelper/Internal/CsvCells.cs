using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CsvHelper;

/// <summary>
/// The conversions of typed cells, the fields of a <see cref="CsvRecordSource"/> whose <see cref="CsvCellKind"/> is not
/// text: numbers for <see cref="decimal"/> and integer targets, rounded to significant digits; serial dates and durations
/// for date and time targets, in either of Excel's date systems; booleans; error cells; and the text of dates,
/// durations and booleans. The reader and dynamic records call it for typed cells only, so CSV never runs it.
/// </summary>
/// <remarks>
/// The rules are those of "What a cell reads as" in Phase 2 of docs/plans/excel.md. A conversion either gives the value,
/// fails, or leaves the cell to the caller, which parses its stored text with the invariant culture as Phase 1 did:
/// <see cref="double"/>, <see cref="float"/>, enums, <see cref="Guid"/> and other types, numbers that need no rounding,
/// and a number cell whose text is not a number.
/// </remarks>
internal static class CsvCells
{
	private const long MillisecondsPerDay = 86_400_000;
	private const long MillisecondsPerHour = 3_600_000;
	private const long TicksPerMillisecond = 10_000;
	private const long TicksPerDay = MillisecondsPerDay * TicksPerMillisecond;

	// Past 9999-12-31 in both date systems: a cheap bound before the exact check of the date.
	private const double MaxSerial = 3_000_000;

	// TimeSpan's range in whole days, so that the ticks of any serial within it fit in a long.
	private const double MaxDurationDays = 10_675_199;

	// The digits of a number that conversions keep: more than the 28 of decimal's significant digits, and Excel's 17.
	private const int MaxDigits = 40;

	private const string Hidden = " (IncludeValuesInExceptions is off, so its value is not shown)";

	// Days from 0001-01-01: to the day before serial 1 of the 1900 date system, to the day that serial 61 and later count
	// from (one less, for the 1900-02-29 that Excel counts and that never existed), and to serial 0 of the 1904 system.
	private static readonly long Days1899Dec31 = new DateTime(1899, 12, 31).Ticks / TicksPerDay;
	private static readonly long Days1899Dec30 = new DateTime(1899, 12, 30).Ticks / TicksPerDay;
	private static readonly long Days1904Jan1 = new DateTime(1904, 1, 1).Ticks / TicksPerDay;
	private static readonly long MaxDays = DateTime.MaxValue.Ticks / TicksPerDay;

	/// <summary>Why a conversion did not give a value.</summary>
	private enum Failure : byte
	{
		/// <summary>It gave the value.</summary>
		None,

		/// <summary>The caller parses the stored text with the invariant culture.</summary>
		Text,

		/// <summary>The cell holds an error.</summary>
		ErrorCell,

		/// <summary>An integer target, and the number is not whole.</summary>
		NotWhole,

		/// <summary>The number is outside the range of the target.</summary>
		Overflow,

		/// <summary>A boolean target, and the number is neither 1 nor 0.</summary>
		NotBoolean,

		/// <summary>A date target, and the serial is 60 of the 1900 date system: 1900-02-29, which never existed.</summary>
		NoDay,

		/// <summary>A date target, and the serial is negative, past 9999-12-31, or not a finite number.</summary>
		DateRange,

		/// <summary>A boolean target, and the cell is a date or a duration.</summary>
		DateCell,

		/// <summary>A date or time target, and the cell is a boolean.</summary>
		BoolCell,
	}

	/// <summary>
	/// Converts a typed cell for <typeparamref name="T"/>, as "What a cell reads as" says: the value, an exception, or
	/// <see langword="false"/> for the caller to parse the stored text with the invariant culture.
	/// </summary>
	/// <typeparam name="T">The type to parse the cell as.</typeparam>
	/// <param name="s">The stored text of the cell (UTF-8), such as <c>45306.5</c> for a date.</param>
	/// <param name="kind">The kind of the cell; not text or empty.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <param name="options">The source's options.</param>
	/// <param name="value">The value, when the method returns <see langword="true"/>.</param>
	/// <returns>
	/// <see langword="true"/> with the value; <see langword="false"/> when the caller parses the stored text with the
	/// invariant culture, the built-in boolean values and its own parsers.
	/// </returns>
	/// <exception cref="FormatException">The cell is not a valid <typeparamref name="T"/>, such as an error cell.</exception>
	/// <exception cref="OverflowException">The cell is outside the range of <typeparamref name="T"/>.</exception>
	public static bool Parse<T>(ReadOnlySpan<byte> s, CsvCellKind kind, bool hide, CsvCellOptions options, out T value)
	{
		var failure = Convert(s, kind, options, out value);
		if (failure == Failure.None)
		{
			return true;
		}

		if (failure == Failure.Text)
		{
			return false;
		}

		throw Exception(failure, s, kind, typeof(T), hide, options);
	}

	/// <summary>Tries to convert a typed cell for <typeparamref name="T"/>, as <see cref="Parse{T}"/> converts it.</summary>
	/// <typeparam name="T">The type to parse the cell as.</typeparam>
	/// <param name="s">The stored text of the cell (UTF-8).</param>
	/// <param name="kind">The kind of the cell; not text or empty.</param>
	/// <param name="options">The source's options.</param>
	/// <param name="value">The value, or the default of <typeparamref name="T"/>.</param>
	/// <returns>
	/// <see langword="true"/> with the value, <see langword="false"/> when the conversion fails, and
	/// <see langword="null"/> when the caller parses the stored text with the invariant culture.
	/// </returns>
	public static bool? TryParse<T>(ReadOnlySpan<byte> s, CsvCellKind kind, CsvCellOptions options, out T value)
	{
		var failure = Convert(s, kind, options, out value);
		if (failure == Failure.Text)
		{
			return null;
		}

		return failure == Failure.None;
	}

	/// <summary>
	/// The text of a date, duration or boolean cell, when it is not the cell's stored text: ISO 8601 for a date
	/// (<c>2024-01-15</c>, <c>13:45:00</c>, <c>2024-01-15T13:45:00.25</c>), <c>[h]:mm:ss</c> for a duration
	/// (<c>36:00:00</c>), and <c>TRUE</c> or <c>FALSE</c>.
	/// </summary>
	/// <remarks>
	/// A date that is not a valid date (negative, past 9999-12-31, not a number) has its stored text, except Excel's
	/// 1900-02-29 (serial 60 of the 1900 date system), whose text is that day. Error cells have their stored text
	/// (<c>#N/A</c>); with <see cref="CsvCellOptions.ThrowOnErrorCells"/>, the caller throws <see cref="ErrorRead"/> instead.
	/// </remarks>
	/// <param name="s">The stored text of the cell (UTF-8).</param>
	/// <param name="kind">The kind of the cell.</param>
	/// <param name="options">The source's options.</param>
	/// <returns>The text, or <see langword="null"/> when the text is the stored text.</returns>
	public static string? Text(ReadOnlySpan<byte> s, CsvCellKind kind, CsvCellOptions options) => kind switch
	{
		CsvCellKind.DateNumber => DateText(s, options.Date1904),
		CsvCellKind.Duration => DurationText(s),
		CsvCellKind.Bool => BooleanText(s),
		_ => null,
	};

	/// <summary>
	/// The exception for reading an error cell as text with <see cref="CsvCellOptions.ThrowOnErrorCells"/>, such as
	/// <c>Data!B3 holds the error #N/A.</c>
	/// </summary>
	/// <param name="s">The stored text of the cell: the error.</param>
	/// <param name="hide">Whether the message leaves the error out.</param>
	/// <param name="location">Where the cell is, such as <c>Data!B3</c>, or <see langword="null"/> when that is not known.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static FormatException ErrorRead(ReadOnlySpan<byte> s, bool hide, string? location)
	{
		var cell = location ?? "The cell";

		return new FormatException(hide ? $"{cell} holds an error{Hidden}." : $"{cell} holds the error {Utf8String.Create(s)}.");
	}

	/// <summary>Converts a typed cell for <typeparamref name="T"/>. See <see cref="Parse{T}"/>.</summary>
	/// <typeparam name="T">The type to parse the cell as.</typeparam>
	/// <param name="s">The stored text of the cell (UTF-8).</param>
	/// <param name="kind">The kind of the cell; not text or empty.</param>
	/// <param name="options">The source's options.</param>
	/// <param name="value">The value, or the default of <typeparamref name="T"/>.</param>
	/// <returns><see cref="Failure.None"/> with the value, or why there is none.</returns>
	private static Failure Convert<T>(ReadOnlySpan<byte> s, CsvCellKind kind, CsvCellOptions options, out T value)
	{
		value = default!;
		if (typeof(T) == typeof(string))
		{
			if (kind == CsvCellKind.Error)
			{
				return options.ThrowOnErrorCells ? Failure.ErrorCell : Failure.Text;
			}

			if (Text(s, kind, options) is not { } text)
			{
				return Failure.Text;
			}

			value = Unsafe.As<string, T>(ref text);

			return Failure.None;
		}

		if (kind == CsvCellKind.Error)
		{
			return Failure.ErrorCell;
		}

		if (typeof(T) == typeof(decimal))
		{
			return ToDecimal(s, options.SignificantDigits, out Unsafe.As<T, decimal>(ref value));
		}

		if (IsInteger<T>())
		{
			var failure = ToWhole(s, options.SignificantDigits, out var negative, out var magnitude);
			if (failure != Failure.None)
			{
				return failure;
			}

			return ToInteger(negative, magnitude, out value) ? Failure.None : Failure.Overflow;
		}

		if (typeof(T) == typeof(bool))
		{
			return ToBoolean(s, kind, out Unsafe.As<T, bool>(ref value));
		}

		if (IsDateOrTime<T>())
		{
			if (kind == CsvCellKind.Bool)
			{
				return Failure.BoolCell;
			}

			if (!CsvParse.TryDouble(s, CultureInfo.InvariantCulture, out var serial))
			{
				return Failure.Text;
			}

			return ToDateOrTime(serial, options.Date1904, out value);
		}

		return Failure.Text;
	}

	/// <summary>Whether <typeparamref name="T"/> is one of the integer types that <see cref="ToInteger{T}"/> converts to.</summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is an integer type.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsInteger<T>() =>
		typeof(T) == typeof(int) || typeof(T) == typeof(long) || typeof(T) == typeof(short) || typeof(T) == typeof(byte) ||
		typeof(T) == typeof(sbyte) || typeof(T) == typeof(ushort) || typeof(T) == typeof(uint) || typeof(T) == typeof(ulong);

	/// <summary>Whether <typeparamref name="T"/> is one of the date and time types that <see cref="ToDateOrTime{T}"/> converts to.</summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is a date or time type.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsDateOrTime<T>() =>
		typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(TimeSpan)
#if NET
		|| typeof(T) == typeof(DateOnly) || typeof(T) == typeof(TimeOnly)
#endif
		;

	/// <summary>
	/// Converts a number cell for a <see cref="decimal"/> target, rounded to <paramref name="significantDigits"/>. Text of
	/// no more bytes than that has no more digits, and is left to the caller's exact parse, as is text that is not a number.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="significantDigits">The digits to round to, or <see langword="null"/> for none.</param>
	/// <param name="value">The value.</param>
	/// <returns><see cref="Failure.None"/>, <see cref="Failure.Text"/> or <see cref="Failure.Overflow"/>.</returns>
	private static Failure ToDecimal(ReadOnlySpan<byte> s, int? significantDigits, out decimal value)
	{
		value = 0;
		if (significantDigits is not { } keep || s.Length <= keep)
		{
			return Failure.Text;
		}

		Span<byte> digits = stackalloc byte[MaxDigits];
		var number = new Number(digits);
		if (!number.Read(s))
		{
			return Failure.Text;
		}

		number.Round(keep);
		number.TrimZeros();
		if (number.Count == 0)
		{
			return Failure.None;
		}

		// decimal keeps 28 digits after the point.
		if (number.Exponent < -28)
		{
			number.Round(number.Count + number.Exponent + 28);
			number.TrimZeros();
			if (number.Count == 0)
			{
				return Failure.None;
			}
		}

		var zeros = Math.Max(number.Exponent, 0);
		if (number.Count + zeros > 29)
		{
			return Failure.Overflow;
		}

		// The digits and the zeros of a positive exponent, as a 96-bit integer, which decimal scales by its negative exponent.
		ulong low = 0;
		uint high = 0;
		for (var i = 0; i < number.Count + zeros; i++)
		{
			if (!MultiplyAdd(ref low, ref high, i < number.Count ? number.Digits[i] : 0u))
			{
				return Failure.Overflow;
			}
		}

		var scale = (byte)Math.Max(-number.Exponent, 0);
		value = new decimal((int)(uint)low, (int)(uint)(low >> 32), (int)high, number.Negative, scale);

		return Failure.None;
	}

	/// <summary>Multiplies a 96-bit integer by 10 and adds <paramref name="digit"/>.</summary>
	/// <param name="low">The low 64 bits.</param>
	/// <param name="high">The high 32 bits.</param>
	/// <param name="digit">The digit to add.</param>
	/// <returns>Whether the result fits in 96 bits.</returns>
	private static bool MultiplyAdd(ref ulong low, ref uint high, uint digit)
	{
		var lo = ((low & 0xFFFF_FFFF) * 10) + digit;
		var mid = ((low >> 32) * 10) + (lo >> 32);
		var hi = ((ulong)high * 10) + (mid >> 32);
		if (hi > uint.MaxValue)
		{
			return false;
		}

		low = (mid << 32) | (lo & 0xFFFF_FFFF);
		high = (uint)hi;

		return true;
	}

	/// <summary>
	/// Reads a number cell as a whole number for an integer target, after rounding it to
	/// <paramref name="significantDigits"/>: <c>42</c>, <c>42.0</c>, <c>1E3</c>, and <c>3.0000000000000004</c> at 15 digits.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="significantDigits">The digits to round to, or <see langword="null"/> for none.</param>
	/// <param name="negative">Whether the number is negative.</param>
	/// <param name="magnitude">The absolute value.</param>
	/// <returns>
	/// <see cref="Failure.None"/>, <see cref="Failure.NotWhole"/>, <see cref="Failure.Overflow"/> (more than
	/// <see cref="ulong"/> holds), or <see cref="Failure.Text"/> for text that is not a number.
	/// </returns>
	private static Failure ToWhole(ReadOnlySpan<byte> s, int? significantDigits, out bool negative, out ulong magnitude)
	{
		if (TryPlainInteger(s, significantDigits ?? 19, out negative, out magnitude))
		{
			return Failure.None;
		}

		Span<byte> digits = stackalloc byte[MaxDigits];
		var number = new Number(digits);
		if (!number.Read(s))
		{
			return Failure.Text;
		}

		if (significantDigits is { } keep)
		{
			number.Round(keep);
		}

		number.TrimZeros();
		negative = number.Negative;
		if (number.Count == 0)
		{
			return Failure.None;
		}

		if (number.Exponent < 0 || number.Inexact)
		{
			return Failure.NotWhole;
		}

		if (number.Count + number.Exponent > 20)
		{
			return Failure.Overflow;
		}

		for (var i = 0; i < number.Count + number.Exponent; i++)
		{
			var digit = i < number.Count ? number.Digits[i] : 0u;
			if (magnitude > (ulong.MaxValue - digit) / 10)
			{
				return Failure.Overflow;
			}

			magnitude = (magnitude * 10) + digit;
		}

		return Failure.None;
	}

	/// <summary>
	/// Reads <c>[-]digits</c> of at most <paramref name="maxDigits"/> digits (and 19), which needs no rounding: the common
	/// case of an integer cell.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="maxDigits">The most digits to read.</param>
	/// <param name="negative">Whether the text starts with <c>-</c>; <see langword="false"/> when the text doesn't have the form.</param>
	/// <param name="magnitude">The absolute value; 0 when the text doesn't have the form.</param>
	/// <returns>Whether the text has that form.</returns>
	private static bool TryPlainInteger(ReadOnlySpan<byte> s, int maxDigits, out bool negative, out ulong magnitude)
	{
		negative = false;
		magnitude = 0;
		var minus = s.Length > 0 && s[0] == (byte)'-';
		var start = minus ? 1 : 0;
		var count = s.Length - start;
		if (count == 0 || count > maxDigits || count > 19)
		{
			return false;
		}

		ulong value = 0;
		for (var i = start; i < s.Length; i++)
		{
			var digit = (uint)(s[i] - '0');
			if (digit > 9)
			{
				return false;
			}

			value = (value * 10) + digit;
		}

		negative = minus;
		magnitude = value;

		return true;
	}

	/// <summary>Converts a whole number to the integer type <typeparamref name="T"/>, if it is in its range.</summary>
	/// <typeparam name="T">The integer type, one of <see cref="IsInteger{T}"/>.</typeparam>
	/// <param name="negative">Whether the number is negative.</param>
	/// <param name="magnitude">The absolute value.</param>
	/// <param name="value">The value.</param>
	/// <returns>Whether the number is in the range of <typeparamref name="T"/>.</returns>
	private static bool ToInteger<T>(bool negative, ulong magnitude, out T value)
	{
		value = default!;
		if (typeof(T) == typeof(int))
		{
			var fits = TrySigned(negative, magnitude, int.MaxValue, out var signed);
			Unsafe.As<T, int>(ref value) = (int)signed;

			return fits;
		}

		if (typeof(T) == typeof(long))
		{
			return TrySigned(negative, magnitude, long.MaxValue, out Unsafe.As<T, long>(ref value));
		}

		if (typeof(T) == typeof(short))
		{
			var fits = TrySigned(negative, magnitude, short.MaxValue, out var signed);
			Unsafe.As<T, short>(ref value) = (short)signed;

			return fits;
		}

		if (typeof(T) == typeof(sbyte))
		{
			var fits = TrySigned(negative, magnitude, sbyte.MaxValue, out var signed);
			Unsafe.As<T, sbyte>(ref value) = (sbyte)signed;

			return fits;
		}

		if (typeof(T) == typeof(byte))
		{
			var fits = TryUnsigned(negative, magnitude, byte.MaxValue);
			Unsafe.As<T, byte>(ref value) = (byte)magnitude;

			return fits;
		}

		if (typeof(T) == typeof(ushort))
		{
			var fits = TryUnsigned(negative, magnitude, ushort.MaxValue);
			Unsafe.As<T, ushort>(ref value) = (ushort)magnitude;

			return fits;
		}

		if (typeof(T) == typeof(uint))
		{
			var fits = TryUnsigned(negative, magnitude, uint.MaxValue);
			Unsafe.As<T, uint>(ref value) = (uint)magnitude;

			return fits;
		}

		var inRange = TryUnsigned(negative, magnitude, ulong.MaxValue);
		Unsafe.As<T, ulong>(ref value) = magnitude;

		return inRange;
	}

	/// <summary>A signed whole number from its sign and magnitude, if it is from <c>-max - 1</c> to <paramref name="max"/>.</summary>
	/// <param name="negative">Whether the number is negative.</param>
	/// <param name="magnitude">The absolute value.</param>
	/// <param name="max">The largest value of the type.</param>
	/// <param name="value">The number, or 0 when it is out of range.</param>
	/// <returns>Whether it is in range.</returns>
	private static bool TrySigned(bool negative, ulong magnitude, long max, out long value)
	{
		value = 0;
		if (magnitude > (ulong)max + (negative ? 1UL : 0UL))
		{
			return false;
		}

		// The magnitude of the smallest value is one more than the largest, so negate one less, then take one more away.
		value = negative ? (magnitude == 0 ? 0 : -(long)(magnitude - 1) - 1) : (long)magnitude;

		return true;
	}

	/// <summary>Whether an unsigned type up to <paramref name="max"/> holds the number: -0 is 0.</summary>
	/// <param name="negative">Whether the number is negative.</param>
	/// <param name="magnitude">The absolute value.</param>
	/// <param name="max">The largest value of the type.</param>
	/// <returns>Whether it is in range.</returns>
	private static bool TryUnsigned(bool negative, ulong magnitude, ulong max) => (!negative || magnitude == 0) && magnitude <= max;

	/// <summary>
	/// Converts a cell for a boolean target: a boolean cell is left to the caller's parse of its <c>1</c> or <c>0</c>, a
	/// number cell is true when it is 1 and false when it is 0, and dates and durations fail.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="kind">The kind of the cell.</param>
	/// <param name="value">The value.</param>
	/// <returns>Why there is no value, if there is none.</returns>
	private static Failure ToBoolean(ReadOnlySpan<byte> s, CsvCellKind kind, out bool value)
	{
		value = false;
		if (kind is CsvCellKind.DateNumber or CsvCellKind.Duration)
		{
			return Failure.DateCell;
		}

		if (kind != CsvCellKind.Number || !CsvParse.TryDouble(s, CultureInfo.InvariantCulture, out var number))
		{
			return Failure.Text;
		}

		if (number == 1)
		{
			value = true;

			return Failure.None;
		}

		return number == 0 ? Failure.None : Failure.NotBoolean;
	}

	/// <summary>Converts a serial number for a date or time target: <see cref="DateTime"/> and the others of <see cref="IsDateOrTime{T}"/>.</summary>
	/// <typeparam name="T">The target type.</typeparam>
	/// <param name="serial">The serial number: days, with the time of day as the fraction.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="value">The value.</param>
	/// <returns>Why there is no value, if there is none.</returns>
	private static Failure ToDateOrTime<T>(double serial, bool date1904, out T value)
	{
		value = default!;
		if (typeof(T) == typeof(TimeSpan))
		{
			if (!TryMilliseconds(serial, out var milliseconds))
			{
				return Failure.Overflow;
			}

			Unsafe.As<T, TimeSpan>(ref value) = new TimeSpan(milliseconds * TicksPerMillisecond);

			return Failure.None;
		}

#if NET
		var timeOfDay = typeof(T) == typeof(TimeOnly);
#else
		var timeOfDay = false;
#endif
		var failure = ToDateTime(serial, date1904, timeOfDay, out var date);
		if (failure != Failure.None)
		{
			return failure;
		}

		if (typeof(T) == typeof(DateTime))
		{
			Unsafe.As<T, DateTime>(ref value) = date;
		}
		else if (typeof(T) == typeof(DateTimeOffset))
		{
			Unsafe.As<T, DateTimeOffset>(ref value) = new DateTimeOffset(date, TimeSpan.Zero);
		}
#if NET
		else if (typeof(T) == typeof(DateOnly))
		{
			Unsafe.As<T, DateOnly>(ref value) = DateOnly.FromDateTime(date);
		}
		else if (typeof(T) == typeof(TimeOnly))
		{
			Unsafe.As<T, TimeOnly>(ref value) = TimeOnly.FromDateTime(date);
		}
#endif

		return Failure.None;
	}

	/// <summary>
	/// Converts a serial number to a date and time, rounded to the millisecond. In the 1900 date system, serial 1 is
	/// 1900-01-01, serials below 1 are times on 1899-12-31 (Excel's day 0), and serial 60 is a 1900-02-29 that never
	/// existed, so serial 61 is 1900-03-01. In the 1904 system, serial 0 is 1904-01-01.
	/// </summary>
	/// <param name="serial">The serial number.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="timeOfDay">Whether only the time of day is wanted, which serial 60 has too.</param>
	/// <param name="date">The date and time, of <see cref="DateTimeKind.Unspecified"/>.</param>
	/// <returns><see cref="Failure.None"/>, <see cref="Failure.NoDay"/> or <see cref="Failure.DateRange"/>.</returns>
	private static Failure ToDateTime(double serial, bool date1904, bool timeOfDay, out DateTime date)
	{
		date = default;
		if (!(serial >= 0 && serial < MaxSerial))
		{
			return Failure.DateRange;
		}

		var milliseconds = Milliseconds(serial);
		var day = milliseconds / MillisecondsPerDay;
		var time = milliseconds - (day * MillisecondsPerDay);
		long epoch;
		if (date1904)
		{
			epoch = Days1904Jan1;
		}
		else if (day < 60)
		{
			epoch = Days1899Dec31;
		}
		else if (day > 60)
		{
			epoch = Days1899Dec30;
		}
		else if (timeOfDay)
		{
			// Any day will do for its time.
			epoch = Days1899Dec31;
		}
		else
		{
			return Failure.NoDay;
		}

		if (epoch + day > MaxDays)
		{
			return Failure.DateRange;
		}

		date = new DateTime(((epoch + day) * TicksPerDay) + (time * TicksPerMillisecond), DateTimeKind.Unspecified);

		return Failure.None;
	}

	/// <summary>A serial number in milliseconds, rounded half away from zero, as Excel shows times and <c>DateTime.FromOADate</c> rounds them.</summary>
	/// <param name="serial">The serial number, of at most <see cref="MaxDurationDays"/> days either way.</param>
	/// <returns>The milliseconds.</returns>
	private static long Milliseconds(double serial) => (long)Math.Round(serial * MillisecondsPerDay, MidpointRounding.AwayFromZero);

	/// <summary>A number of days in milliseconds, if <see cref="TimeSpan"/> holds it.</summary>
	/// <param name="serial">The number of days.</param>
	/// <param name="milliseconds">The milliseconds.</param>
	/// <returns>Whether the days are finite and within <see cref="TimeSpan"/>'s range.</returns>
	private static bool TryMilliseconds(double serial, out long milliseconds)
	{
		milliseconds = 0;
		if (!(serial >= -MaxDurationDays && serial <= MaxDurationDays))
		{
			return false;
		}

		milliseconds = Milliseconds(serial);

		return true;
	}

	/// <summary>
	/// The text of a date cell, from its value: <c>yyyy-MM-dd</c> for a whole day, <c>HH:mm:ss</c> for a serial below 1
	/// (a time alone), else <c>yyyy-MM-ddTHH:mm:ss</c>, each with <c>.fff</c> when there are milliseconds, without
	/// trailing zeros.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The text, or <see langword="null"/> for a cell that is not a valid date: its text is the stored text.</returns>
	private static string? DateText(ReadOnlySpan<byte> s, bool date1904)
	{
		if (!CsvParse.TryDouble(s, CultureInfo.InvariantCulture, out var serial) || !(serial >= 0 && serial < MaxSerial))
		{
			return null;
		}

		var milliseconds = Milliseconds(serial);
		var day = milliseconds / MillisecondsPerDay;
		var time = milliseconds - (day * MillisecondsPerDay);
		Span<char> text = stackalloc char[32];
		var length = 0;
		if (day > 0)
		{
			if (!date1904 && day == 60)
			{
				// Excel's 1900-02-29, which a date target can't hold.
				Append(text, ref length, 1900, 4);
				"-02-29".AsSpan().CopyTo(text.Slice(length));
				length += 6;
			}
			else
			{
				var epoch = date1904 ? Days1904Jan1 : day < 60 ? Days1899Dec31 : Days1899Dec30;
				if (epoch + day > MaxDays)
				{
					return null;
				}

				var date = new DateTime((epoch + day) * TicksPerDay);
				Append(text, ref length, date.Year, 4);
				text[length++] = '-';
				Append(text, ref length, date.Month, 2);
				text[length++] = '-';
				Append(text, ref length, date.Day, 2);
			}

			if (time == 0)
			{
				return text.Slice(0, length).ToString();
			}

			text[length++] = 'T';
		}

		AppendTime(text, ref length, time / MillisecondsPerHour, time % MillisecondsPerHour);

		return text.Slice(0, length).ToString();
	}

	/// <summary>
	/// The text of a duration cell, as Excel's <c>[h]:mm:ss</c> shows it: hours of at least two digits, with no day part
	/// (<c>36:00:00</c>, <c>-01:30:00</c>), and <c>.fff</c> when there are milliseconds, without trailing zeros.
	/// </summary>
	/// <param name="s">The stored text of the cell.</param>
	/// <returns>The text, or <see langword="null"/> for a cell beyond <see cref="TimeSpan"/>'s range: its text is the stored text.</returns>
	private static string? DurationText(ReadOnlySpan<byte> s)
	{
		if (!CsvParse.TryDouble(s, CultureInfo.InvariantCulture, out var serial) || !TryMilliseconds(serial, out var milliseconds))
		{
			return null;
		}

		Span<char> text = stackalloc char[32];
		var length = 0;
		if (milliseconds < 0)
		{
			text[length++] = '-';
			milliseconds = -milliseconds;
		}

		AppendTime(text, ref length, milliseconds / MillisecondsPerHour, milliseconds % MillisecondsPerHour);

		return text.Slice(0, length).ToString();
	}

	/// <summary>Appends <c>HH:mm:ss</c> and the milliseconds, if any, as <c>.fff</c> without trailing zeros.</summary>
	/// <param name="text">The text.</param>
	/// <param name="length">The length of the text so far.</param>
	/// <param name="hours">The hours: at least two digits.</param>
	/// <param name="rest">The milliseconds within the hour.</param>
	private static void AppendTime(Span<char> text, ref int length, long hours, long rest)
	{
		Append(text, ref length, hours, 2);
		text[length++] = ':';
		Append(text, ref length, rest / 60_000, 2);
		text[length++] = ':';
		Append(text, ref length, rest / 1000 % 60, 2);
		var fraction = (int)(rest % 1000);
		if (fraction == 0)
		{
			return;
		}

		text[length++] = '.';
		text[length++] = (char)('0' + (fraction / 100));
		if (fraction % 100 != 0)
		{
			text[length++] = (char)('0' + (fraction / 10 % 10));
			if (fraction % 10 != 0)
			{
				text[length++] = (char)('0' + (fraction % 10));
			}
		}
	}

	/// <summary>Appends a non-negative number in at least <paramref name="width"/> digits, with leading zeros.</summary>
	/// <param name="text">The text.</param>
	/// <param name="length">The length of the text so far.</param>
	/// <param name="number">The number.</param>
	/// <param name="width">The fewest digits.</param>
	private static void Append(Span<char> text, ref int length, long number, int width)
	{
		var digits = 1;
		for (var rest = number / 10; rest > 0; rest /= 10)
		{
			digits++;
		}

		digits = Math.Max(digits, width);
		for (var i = length + digits - 1; i >= length; i--)
		{
			text[i] = (char)('0' + (number % 10));
			number /= 10;
		}

		length += digits;
	}

	/// <summary>The text of a boolean cell: <c>TRUE</c> or <c>FALSE</c>.</summary>
	/// <param name="s">The stored text of the cell, <c>1</c> or <c>0</c>.</param>
	/// <returns>The text, or <see langword="null"/> when the stored text is not a boolean: its text is the stored text.</returns>
	private static string? BooleanText(ReadOnlySpan<byte> s)
	{
		if (s.Length == 1 && s[0] is (byte)'0' or (byte)'1')
		{
			return s[0] == (byte)'1' ? "TRUE" : "FALSE";
		}

		if (!CsvParse.TryBoolean(s, out var value))
		{
			return null;
		}

		return value ? "TRUE" : "FALSE";
	}

	/// <summary>The exception for a failed conversion.</summary>
	/// <param name="failure">Why the conversion failed.</param>
	/// <param name="s">The stored text of the cell.</param>
	/// <param name="kind">The kind of the cell.</param>
	/// <param name="type">The target type.</param>
	/// <param name="hide">Whether the message leaves the value out.</param>
	/// <param name="options">The source's options, for the date system.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception Exception(Failure failure, ReadOnlySpan<byte> s, CsvCellKind kind, Type type, bool hide, CsvCellOptions options)
	{
		var name = type.Name;
		var text = hide ? "" : Utf8String.Create(s);
		switch (failure)
		{
			case Failure.ErrorCell when type == typeof(string):
				return ErrorRead(s, hide, null);
			case Failure.ErrorCell:
				return new FormatException(hide
					? $"The cell holds an error, so it is not a valid {name}{Hidden}."
					: $"The cell holds the error {text}, so it is not a valid {name}.");
			case Failure.NotWhole:
				return new FormatException(hide
					? $"The cell is not a whole number, so it is not a valid {name}{Hidden}."
					: $"'{text}' is not a whole number, so it is not a valid {name}.");
			case Failure.Overflow:
				return hide ? CsvParse.Hidden(new OverflowException(), name) : new OverflowException($"'{text}' is outside the range of {name}.");
			case Failure.NotBoolean:
				return new FormatException(hide
					? $"The cell is not a valid boolean: a number cell is true when it is 1 and false when it is 0{Hidden}."
					: $"'{text}' is not a valid boolean: a number cell is true when it is 1 and false when it is 0.");
			case Failure.NoDay:
				return new FormatException(hide
					? $"The cell is not a valid {name}: its serial is a day that doesn't exist in Excel's 1900 date system{Hidden}."
					: $"'{text}' is 1900-02-29 in Excel's 1900 date system, a day that doesn't exist, so it is not a valid {name}.");
			case Failure.DateRange:
				var range = options.Date1904
					? "from 0 to 2957003.99999999 (9999-12-31) in the 1904 date system"
					: "from 0 to 2958465.99999999 (9999-12-31)";

				return new FormatException(hide
					? $"The cell is not a valid {name}: Excel's dates are serials {range}{Hidden}."
					: $"'{text}' is not a valid {name}: Excel's dates are serials {range}.");
			case Failure.DateCell:
				return new FormatException($"The cell is a {(kind == CsvCellKind.Duration ? "duration" : "date")}, so it is not a valid {name}.");
			default:
				return new FormatException($"The cell is a boolean, so it is not a valid {name}.");
		}
	}

	/// <summary>
	/// The significant digits of a number in invariant form (<c>-1.5E-05</c>): the number is
	/// <c>Digits[0..Count)</c> times <c>10^Exponent</c>, with no leading zeros, and is negative when <see cref="Negative"/> is set.
	/// </summary>
	private ref struct Number
	{
		/// <summary>Creates a number of no digits that keeps up to <c>digits.Length</c> of them.</summary>
		/// <param name="digits">Room for the digits.</param>
		public Number(Span<byte> digits)
		{
			Digits = digits;
			Count = 0;
			Exponent = 0;
			Negative = false;
			Inexact = false;
		}

		/// <summary>The digits, 0 to 9, most significant first.</summary>
		public Span<byte> Digits { get; }

		/// <summary>The number of digits.</summary>
		public int Count { get; private set; }

		/// <summary>The power of ten of the last digit.</summary>
		public int Exponent { get; private set; }

		/// <summary>Whether the number is negative.</summary>
		public bool Negative { get; private set; }

		/// <summary>Whether digits past the room for them that were not zero were dropped, so the digits are less than the number.</summary>
		public bool Inexact { get; private set; }

		/// <summary>Reads <c>[+-]digits[.digits][(e|E)[+-]digits]</c>, with at least one digit before the exponent.</summary>
		/// <param name="s">The text.</param>
		/// <returns>Whether all of <paramref name="s"/> has that form.</returns>
		public bool Read(ReadOnlySpan<byte> s)
		{
			var i = 0;
			if (s.Length > 0 && s[0] is (byte)'-' or (byte)'+')
			{
				Negative = s[0] == (byte)'-';
				i++;
			}

			var any = false;
			for (; i < s.Length && IsDigit(s[i]); i++)
			{
				any = true;
				Add(s[i] - '0', fraction: false);
			}

			if (i < s.Length && s[i] == (byte)'.')
			{
				for (i++; i < s.Length && IsDigit(s[i]); i++)
				{
					any = true;
					Add(s[i] - '0', fraction: true);
				}
			}

			if (!any)
			{
				return false;
			}

			if (i < s.Length && (s[i] | 0x20) == 'e')
			{
				i++;
				var negative = false;
				if (i < s.Length && s[i] is (byte)'-' or (byte)'+')
				{
					negative = s[i] == (byte)'-';
					i++;
				}

				var start = i;
				var exponent = 0;
				for (; i < s.Length && IsDigit(s[i]); i++)
				{
					// Far past any type's range; more digits change nothing.
					if (exponent < 100_000)
					{
						exponent = (exponent * 10) + (s[i] - '0');
					}
				}

				if (i == start)
				{
					return false;
				}

				Exponent += negative ? -exponent : exponent;
			}

			return i == s.Length;
		}

		/// <summary>
		/// Rounds to <paramref name="keep"/> significant digits, half away from zero; to 0 when <paramref name="keep"/> is
		/// negative (the number is below half of the place it would round at).
		/// </summary>
		/// <param name="keep">The digits to keep.</param>
		public void Round(int keep)
		{
			if (Count <= keep)
			{
				return;
			}

			Inexact = false;
			if (keep < 0)
			{
				Count = 0;
				Exponent = 0;

				return;
			}

			var up = Digits[keep] >= 5;
			Exponent += Count - keep;
			Count = keep;
			if (!up)
			{
				return;
			}

			var i = Count - 1;
			while (i >= 0 && Digits[i] == 9)
			{
				Digits[i] = 0;
				i--;
			}

			if (i >= 0)
			{
				Digits[i]++;

				return;
			}

			// All nines (or no digits): one more place.
			Digits[0] = 1;
			Exponent += Count;
			Count = 1;
		}

		/// <summary>Moves trailing zeros into the exponent.</summary>
		public void TrimZeros()
		{
			while (Count > 0 && Digits[Count - 1] == 0)
			{
				Count--;
				Exponent++;
			}

			if (Count == 0)
			{
				Exponent = 0;
			}
		}

		private static bool IsDigit(byte c) => (uint)(c - '0') <= 9;

		/// <summary>Adds a digit: leading zeros only move the point, and digits past the room for them only the exponent.</summary>
		/// <param name="digit">The digit.</param>
		/// <param name="fraction">Whether the digit is after the point.</param>
		private void Add(int digit, bool fraction)
		{
			if (Count == 0 && digit == 0)
			{
				if (fraction)
				{
					Exponent--;
				}

				return;
			}

			if (Count < Digits.Length)
			{
				Digits[Count++] = (byte)digit;
				if (fraction)
				{
					Exponent--;
				}

				return;
			}

			if (!fraction)
			{
				Exponent++;
			}

			if (digit != 0)
			{
				Inexact = true;
			}
		}
	}
}
