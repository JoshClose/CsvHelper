using System;
using System.Buffers.Text;
using System.Globalization;
#if !NET
using System.Net;
using System.Numerics;
#endif
using System.Runtime.CompilerServices;
using System.Text;

namespace CsvHelper;

/// <summary>
/// Parses UTF-8 fields as the built-in types, chosen by a type parameter: the code behind the <c>Parse&lt;T&gt;</c>,
/// <c>TryParse&lt;T&gt;</c> and <c>ParseExact&lt;T&gt;</c> methods of the reader, its fields and dynamic records. The
/// JIT specializes the methods for each value type, which leaves only the parser of that type.
/// </summary>
internal static class CsvValue
{
	/// <summary>
	/// Whether <see cref="Parse{T}"/> and <see cref="TryParse{T}"/> parse <typeparamref name="T"/>: <see cref="int"/>,
	/// <see cref="long"/>, <see cref="double"/>, <see cref="float"/>, <see cref="decimal"/>, <see cref="bool"/>,
	/// <see cref="Guid"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, the integer types of
	/// <see cref="IsBclInteger{T}"/> and <see cref="string"/>, and on .NET Framework and .NET Standard also the types of
	/// <see cref="IsBclString{T}"/> and enums.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is one of the built-in types.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBuiltIn<T>() =>
		typeof(T) == typeof(int) || typeof(T) == typeof(long) || typeof(T) == typeof(double) || typeof(T) == typeof(float) ||
		typeof(T) == typeof(decimal) || typeof(T) == typeof(bool) || typeof(T) == typeof(Guid) || typeof(T) == typeof(DateTime) ||
		typeof(T) == typeof(DateTimeOffset) || IsBclInteger<T>() || typeof(T) == typeof(string) || IsBclString<T>() || IsEnum<T>();

	/// <summary>
	/// Parses <paramref name="s"/> as <typeparamref name="T"/>, one of the built-in types of <see cref="IsBuiltIn{T}"/>,
	/// as <see cref="CsvReader.Parse{T}(int)"/> describes.
	/// </summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <param name="booleans">The true and false values of the options, or <see langword="null"/> for the built-in ones.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a valid <typeparamref name="T"/>.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <typeparamref name="T"/>.</exception>
	/// <exception cref="NotSupportedException"><typeparamref name="T"/> is not one of the built-in types.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Parse<T>(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide, CsvBooleanText? booleans)
	{
		if (typeof(T) == typeof(int))
		{
			return As<int, T>(CsvParse.Int32(s, provider, hide));
		}

		if (typeof(T) == typeof(long))
		{
			return As<long, T>(CsvParse.Int64(s, provider, hide));
		}

		if (typeof(T) == typeof(double))
		{
			return As<double, T>(CsvParse.Double(s, provider, hide));
		}

		if (typeof(T) == typeof(float))
		{
			return As<float, T>(CsvParse.Single(s, provider, hide));
		}

		if (typeof(T) == typeof(decimal))
		{
			return As<decimal, T>(CsvParse.Decimal(s, provider, hide));
		}

		if (typeof(T) == typeof(bool))
		{
			return As<bool, T>(booleans is null ? CsvParse.Boolean(s, hide) : booleans.Parse(s, hide));
		}

		if (typeof(T) == typeof(Guid))
		{
			return As<Guid, T>(CsvParse.Guid(s, hide));
		}

		if (typeof(T) == typeof(DateTime))
		{
			return As<DateTime, T>(CsvParse.DateTime(s, provider, hide));
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			return As<DateTimeOffset, T>(CsvParse.DateTimeOffset(s, provider, hide));
		}

		if (IsBclInteger<T>())
		{
			// The BCL parsers, with the value in their messages unless it is hidden.
			return hide ? CsvParse.Hide(s, provider, static (x, p) => BclInteger<T>(x, p), typeof(T).Name) : BclInteger<T>(s, provider);
		}

		if (typeof(T) == typeof(string))
		{
			return As<string, T>(Utf8String.Create(s));
		}

#if !NET
		if (IsBclString<T>())
		{
			// The BCL parsers of strings, with the value in their messages unless it is hidden.
			return hide ? CsvParse.Hide(s, provider, static (x, p) => BclString<T>(x, p), typeof(T).Name) : BclString<T>(s, provider);
		}

		if (typeof(T).IsEnum)
		{
			return CsvEnum<T>.Parse(s, hide);
		}
#endif

		throw NotSupported(typeof(T));
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as <typeparamref name="T"/>, one of the built-in types of
	/// <see cref="IsBuiltIn{T}"/>, as <see cref="Parse{T}"/> parses it.
	/// </summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="booleans">The true and false values of the options, or <see langword="null"/> for the built-in ones.</param>
	/// <param name="value">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	/// <exception cref="NotSupportedException"><typeparamref name="T"/> is not one of the built-in types.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryParse<T>(ReadOnlySpan<byte> s, IFormatProvider provider, CsvBooleanText? booleans, out T value)
	{
		value = default!;
		if (typeof(T) == typeof(int))
		{
			return CsvParse.TryInt32(s, provider, out Unsafe.As<T, int>(ref value));
		}

		if (typeof(T) == typeof(long))
		{
			return CsvParse.TryInt64(s, provider, out Unsafe.As<T, long>(ref value));
		}

		if (typeof(T) == typeof(double))
		{
			return CsvParse.TryDouble(s, provider, out Unsafe.As<T, double>(ref value));
		}

		if (typeof(T) == typeof(float))
		{
			return CsvParse.TrySingle(s, provider, out Unsafe.As<T, float>(ref value));
		}

		if (typeof(T) == typeof(decimal))
		{
			return CsvParse.TryDecimal(s, provider, out Unsafe.As<T, decimal>(ref value));
		}

		if (typeof(T) == typeof(bool))
		{
			return booleans is null
				? CsvParse.TryBoolean(s, out Unsafe.As<T, bool>(ref value))
				: booleans.TryParse(s, out Unsafe.As<T, bool>(ref value));
		}

		if (typeof(T) == typeof(Guid))
		{
			return TryGuid(s, out Unsafe.As<T, Guid>(ref value));
		}

		if (typeof(T) == typeof(DateTime))
		{
			return TryDateTime(s, provider, out Unsafe.As<T, DateTime>(ref value));
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			return TryDateTimeOffset(s, provider, out Unsafe.As<T, DateTimeOffset>(ref value));
		}

		if (IsBclInteger<T>())
		{
			return TryBclInteger(s, provider, out value);
		}

		if (typeof(T) == typeof(string))
		{
			Unsafe.As<T, string>(ref value) = Utf8String.Create(s);

			return true;
		}

#if !NET
		if (IsBclString<T>())
		{
			return TryBclString(s, provider, out value);
		}

		if (typeof(T).IsEnum)
		{
			return CsvEnum<T>.TryParse(s, out value);
		}
#endif

		throw NotSupported(typeof(T));
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> in exactly
	/// <paramref name="format"/>, with <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <typeparam name="T"><see cref="DateTime"/> or <see cref="DateTimeOffset"/>.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field, as <c>ParseExact</c> of the type takes it.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	/// <exception cref="NotSupportedException"><typeparamref name="T"/> is neither <see cref="DateTime"/> nor <see cref="DateTimeOffset"/>.</exception>
	public static T ParseExact<T>(ReadOnlySpan<byte> s, string format, IFormatProvider provider, bool hide)
	{
		if (typeof(T) == typeof(DateTime))
		{
			return As<DateTime, T>(CsvParse.DateTimeExact(s, format, provider, hide));
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			return As<DateTimeOffset, T>(CsvParse.DateTimeOffsetExact(s, format, provider, hide));
		}

		throw NotSupportedExact(typeof(T));
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> in exactly one of
	/// <paramref name="formats"/>, tried in order, with <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <typeparam name="T"><see cref="DateTime"/> or <see cref="DateTimeOffset"/>.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have, as <c>ParseExact</c> of the type takes them.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	/// <exception cref="NotSupportedException"><typeparamref name="T"/> is neither <see cref="DateTime"/> nor <see cref="DateTimeOffset"/>.</exception>
	public static T ParseExact<T>(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider, bool hide)
	{
		if (typeof(T) == typeof(DateTime))
		{
			return As<DateTime, T>(CsvParse.DateTimeExact(s, formats, provider, hide));
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			return As<DateTimeOffset, T>(CsvParse.DateTimeOffsetExact(s, formats, provider, hide));
		}

		throw NotSupportedExact(typeof(T));
	}

	/// <summary>The exception for a type that <c>Parse&lt;T&gt;</c> does not parse (on .NET Framework and .NET Standard).</summary>
	/// <param name="type">The type.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static NotSupportedException NotSupported(Type type) => new(
		$"Parse<{type.Name}> is not supported: on .NET Framework and .NET Standard, Parse<T> parses int, long, short, byte, uint, " +
		"ulong, sbyte, ushort, double, float, decimal, bool, char, Guid, DateTime, DateTimeOffset, TimeSpan, BigInteger, IPAddress, " +
		"string and enums. Parse other types with Parse(index, parser), which gives the parser the field as a string.");

	/// <summary>The exception for a type that <c>ParseExact&lt;T&gt;</c> does not parse.</summary>
	/// <param name="type">The type.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotSupportedException NotSupportedExact(Type type) => new(
		$"ParseExact<{type.Name}> is not supported: ParseExact<T> parses DateTime and DateTimeOffset. Parse other types with " +
		"Parse(index, parser), which gives the parser the field as a string.");

#if NET
	/// <summary>
	/// Parses <paramref name="chars"/> with <typeparamref name="T"/>'s own parser. When <paramref name="hide"/> is set,
	/// its <see cref="FormatException"/> or <see cref="OverflowException"/> is replaced with one whose message leaves the
	/// value out.
	/// </summary>
	/// <typeparam name="T">The type parsed.</typeparam>
	/// <param name="chars">The decoded field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ParseChars<T>(ReadOnlySpan<char> chars, IFormatProvider provider, bool hide) where T : ISpanParsable<T> =>
		hide ? ParseCharsHidden<T>(chars, provider) : T.Parse(chars, provider);

	/// <summary>
	/// Parses <paramref name="s"/> with <typeparamref name="T"/>'s own parser of chars, as <see cref="ParseChars{T}"/>
	/// does: a field of at most 128 bytes is decoded on the stack, a longer one to a string.
	/// </summary>
	/// <typeparam name="T">The type parsed.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static T ParseDecoded<T>(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) where T : ISpanParsable<T>
	{
		if (s.Length > 128)
		{
			return ParseChars<T>(Encoding.UTF8.GetString(s).AsSpan(), provider, hide);
		}

		Span<char> chars = stackalloc char[128];
		var n = Encoding.UTF8.GetChars(s, chars);

		return ParseChars<T>(chars.Slice(0, n), provider, hide);
	}

	/// <summary>
	/// Parses <paramref name="chars"/> with <typeparamref name="T"/>'s own parser, and replaces its
	/// <see cref="FormatException"/> or <see cref="OverflowException"/> with one whose message leaves the value out.
	/// </summary>
	/// <typeparam name="T">The type parsed.</typeparam>
	/// <param name="chars">The decoded field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static T ParseCharsHidden<T>(ReadOnlySpan<char> chars, IFormatProvider provider) where T : ISpanParsable<T>
	{
		try
		{
			return T.Parse(chars, provider);
		}
		catch (Exception e) when (e is FormatException or OverflowException)
		{
			throw CsvParse.Hidden(e, typeof(T).Name);
		}
	}
#endif

	/// <summary>Reinterprets a value as <typeparamref name="TTo"/>, which is the same type (checked by the caller with <c>typeof</c>).</summary>
	/// <typeparam name="TFrom">The type of the value.</typeparam>
	/// <typeparam name="TTo">The type parameter of the caller.</typeparam>
	/// <param name="value">The value.</param>
	/// <returns>The value as <typeparamref name="TTo"/>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TTo As<TFrom, TTo>(TFrom value) => Unsafe.As<TFrom, TTo>(ref value);

	/// <summary>
	/// The built-in integer types that the BCL's parsers parse: <see cref="short"/>, <see cref="byte"/>, <see cref="sbyte"/>,
	/// <see cref="ushort"/>, <see cref="uint"/> and <see cref="ulong"/>.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is one of them.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsBclInteger<T>() =>
		typeof(T) == typeof(short) || typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(ushort) ||
		typeof(T) == typeof(uint) || typeof(T) == typeof(ulong);

	/// <summary>
	/// Parses <paramref name="s"/> as one of the integer types of <see cref="IsBclInteger{T}"/>, in
	/// <see cref="NumberStyles.Integer"/> style: on .NET with the BCL's parser of UTF-8, else decoded to a string.
	/// </summary>
	/// <typeparam name="T">The integer type.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	private static T BclInteger<T>(ReadOnlySpan<byte> s, IFormatProvider provider)
	{
#if NET
		if (typeof(T) == typeof(short))
		{
			return As<short, T>(CsvParse.Bcl<short>(s, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(byte))
		{
			return As<byte, T>(CsvParse.Bcl<byte>(s, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(sbyte))
		{
			return As<sbyte, T>(CsvParse.Bcl<sbyte>(s, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(ushort))
		{
			return As<ushort, T>(CsvParse.Bcl<ushort>(s, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(uint))
		{
			return As<uint, T>(CsvParse.Bcl<uint>(s, NumberStyles.Integer, provider));
		}

		return As<ulong, T>(CsvParse.Bcl<ulong>(s, NumberStyles.Integer, provider));
#else
		var text = Encoding.UTF8.GetString(s);
		if (typeof(T) == typeof(short))
		{
			return As<short, T>(short.Parse(text, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(byte))
		{
			return As<byte, T>(byte.Parse(text, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(sbyte))
		{
			return As<sbyte, T>(sbyte.Parse(text, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(ushort))
		{
			return As<ushort, T>(ushort.Parse(text, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(uint))
		{
			return As<uint, T>(uint.Parse(text, NumberStyles.Integer, provider));
		}

		return As<ulong, T>(ulong.Parse(text, NumberStyles.Integer, provider));
#endif
	}

	/// <summary>Tries to parse <paramref name="s"/> as one of the integer types of <see cref="IsBclInteger{T}"/>, as <see cref="BclInteger{T}"/> parses it.</summary>
	/// <typeparam name="T">The integer type.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	private static bool TryBclInteger<T>(ReadOnlySpan<byte> s, IFormatProvider provider, out T value)
	{
		value = default!;
#if NET
		if (typeof(T) == typeof(short))
		{
			return short.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, short>(ref value));
		}

		if (typeof(T) == typeof(byte))
		{
			return byte.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, byte>(ref value));
		}

		if (typeof(T) == typeof(sbyte))
		{
			return sbyte.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, sbyte>(ref value));
		}

		if (typeof(T) == typeof(ushort))
		{
			return ushort.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, ushort>(ref value));
		}

		if (typeof(T) == typeof(uint))
		{
			return uint.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, uint>(ref value));
		}

		return ulong.TryParse(s, NumberStyles.Integer, provider, out Unsafe.As<T, ulong>(ref value));
#else
		var text = Encoding.UTF8.GetString(s);
		if (typeof(T) == typeof(short))
		{
			return short.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, short>(ref value));
		}

		if (typeof(T) == typeof(byte))
		{
			return byte.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, byte>(ref value));
		}

		if (typeof(T) == typeof(sbyte))
		{
			return sbyte.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, sbyte>(ref value));
		}

		if (typeof(T) == typeof(ushort))
		{
			return ushort.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, ushort>(ref value));
		}

		if (typeof(T) == typeof(uint))
		{
			return uint.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, uint>(ref value));
		}

		return ulong.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, ulong>(ref value));
#endif
	}

	/// <summary>
	/// The built-in types of .NET Framework and .NET Standard that the BCL's parsers of strings parse: <see cref="TimeSpan"/>,
	/// <see cref="System.Numerics.BigInteger"/>, <see cref="char"/> and <see cref="System.Net.IPAddress"/>. On .NET,
	/// <c>Parse&lt;T&gt;</c> parses them with their own <c>ISpanParsable&lt;T&gt;</c> parsers, as any other type.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is one of them; never on .NET.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsBclString<T>() =>
#if NET
		false;
#else
		typeof(T) == typeof(TimeSpan) || typeof(T) == typeof(BigInteger) || typeof(T) == typeof(char) || typeof(T) == typeof(IPAddress);
#endif

	/// <summary>
	/// Whether <typeparamref name="T"/> is an enum, which <see cref="CsvEnum{T}"/> parses, on .NET Framework and .NET
	/// Standard. On .NET, where <c>Parse&lt;T&gt;</c> takes only <c>ISpanParsable&lt;T&gt;</c> types, the enum overloads of
	/// <c>CsvEnumExtensions</c> parse enums.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <returns>Whether <typeparamref name="T"/> is an enum; never on .NET.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsEnum<T>() =>
#if NET
		false;
#else
		typeof(T).IsEnum;
#endif

#if !NET
	/// <summary>
	/// Parses <paramref name="s"/> as one of the types of <see cref="IsBclString{T}"/>, decoded to a string, as their
	/// <c>ISpanParsable&lt;T&gt;</c> parsers on .NET do: <see cref="TimeSpan"/> with
	/// <see cref="TimeSpan.Parse(string, IFormatProvider)"/>, <see cref="BigInteger"/> in <see cref="NumberStyles.Integer"/>
	/// style, <see cref="char"/> as exactly one character and <see cref="IPAddress"/> with <see cref="IPAddress.Parse(string)"/>.
	/// </summary>
	/// <typeparam name="T"><see cref="TimeSpan"/>, <see cref="BigInteger"/>, <see cref="char"/> or <see cref="IPAddress"/>.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	private static T BclString<T>(ReadOnlySpan<byte> s, IFormatProvider provider)
	{
		var text = Encoding.UTF8.GetString(s);
		if (typeof(T) == typeof(TimeSpan))
		{
			return As<TimeSpan, T>(TimeSpan.Parse(text, provider));
		}

		if (typeof(T) == typeof(BigInteger))
		{
			return As<BigInteger, T>(BigInteger.Parse(text, NumberStyles.Integer, provider));
		}

		if (typeof(T) == typeof(char))
		{
			return As<char, T>(char.Parse(text));
		}

		return As<IPAddress, T>(IPAddress.Parse(text));
	}

	/// <summary>Tries to parse <paramref name="s"/> as one of the types of <see cref="IsBclString{T}"/>, as <see cref="BclString{T}"/> parses it.</summary>
	/// <typeparam name="T"><see cref="TimeSpan"/>, <see cref="BigInteger"/>, <see cref="char"/> or <see cref="IPAddress"/>.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or the default when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	private static bool TryBclString<T>(ReadOnlySpan<byte> s, IFormatProvider provider, out T value)
	{
		value = default!;
		var text = Encoding.UTF8.GetString(s);
		if (typeof(T) == typeof(TimeSpan))
		{
			return TimeSpan.TryParse(text, provider, out Unsafe.As<T, TimeSpan>(ref value));
		}

		if (typeof(T) == typeof(BigInteger))
		{
			return BigInteger.TryParse(text, NumberStyles.Integer, provider, out Unsafe.As<T, BigInteger>(ref value));
		}

		if (typeof(T) == typeof(char))
		{
			return char.TryParse(text, out Unsafe.As<T, char>(ref value));
		}

		return IPAddress.TryParse(text, out Unsafe.As<T, IPAddress>(ref value));
	}
#endif

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="Guid"/>: the default <c>"D"</c> format with
	/// <see cref="Utf8Parser"/>, any other format that <see cref="Guid.Parse(string)"/> accepts as a string.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="value">The parsed value, or <see cref="Guid.Empty"/> when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	private static bool TryGuid(ReadOnlySpan<byte> s, out Guid value) =>
		(Utf8Parser.TryParse(s, out value, out var used) && used == s.Length) || Guid.TryParse(Encoding.UTF8.GetString(s), out value);

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="DateTime"/>, as <see cref="CsvParse.DateTime"/> parses it: a UTC
	/// value in the round-trip format directly, anything else with the BCL's <c>DateTime.TryParse</c>, the provider and
	/// <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or the default when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TryDateTime(ReadOnlySpan<byte> s, IFormatProvider provider, out DateTime value)
	{
		if (s.Length == 28 && Utf8Parser.TryParse(s, out value, out var used, 'O') && used == s.Length)
		{
			return true;
		}

#if NET
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return DateTime.TryParse(chars.Slice(0, n), provider, DateTimeStyles.RoundtripKind, out value);
		}
#endif

		return DateTime.TryParse(Encoding.UTF8.GetString(s), provider, DateTimeStyles.RoundtripKind, out value);
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="DateTimeOffset"/>, as <see cref="CsvParse.DateTimeOffset"/> parses
	/// it: with the BCL's <c>DateTimeOffset.TryParse</c>, the provider and <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or the default when parsing fails.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TryDateTimeOffset(ReadOnlySpan<byte> s, IFormatProvider provider, out DateTimeOffset value)
	{
#if NET
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return DateTimeOffset.TryParse(chars.Slice(0, n), provider, DateTimeStyles.RoundtripKind, out value);
		}
#endif

		return DateTimeOffset.TryParse(Encoding.UTF8.GetString(s), provider, DateTimeStyles.RoundtripKind, out value);
	}
}
