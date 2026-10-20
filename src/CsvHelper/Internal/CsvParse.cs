using System;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CsvHelper;

/// <summary>Fast UTF-8 parsing of common field types, falling back to the BCL for anything unusual.</summary>
internal static class CsvParse
{
	private const NumberStyles IntegerStyle = NumberStyles.Integer;
	private const NumberStyles FloatStyle = NumberStyles.Float | NumberStyles.AllowThousands;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsInvariantDecimal(IFormatProvider provider) =>
		ReferenceEquals(provider, CultureInfo.InvariantCulture) || NumberFormatInfo.GetInstance(provider).NumberDecimalSeparator == ".";

	/// <summary>
	/// The exception for a field that is not a valid <paramref name="type"/>, like <paramref name="e"/> but without the
	/// value in its message (<see cref="CsvReaderOptions.IncludeValuesInExceptions"/> is off).
	/// </summary>
	/// <param name="e">The exception of the parser: a <see cref="FormatException"/> or an <see cref="OverflowException"/>.</param>
	/// <param name="type">The name of the type, such as <c>Int32</c>.</param>
	/// <returns>An exception of the same type as <paramref name="e"/>.</returns>
	internal static Exception Hidden(Exception e, string type) => e is OverflowException
		? new OverflowException($"The field is outside the range of {type} (IncludeValuesInExceptions is off, so its value is not shown).")
		: new FormatException($"The field is not a valid {type} (IncludeValuesInExceptions is off, so its value is not shown).");

	/// <summary>Parses UTF-8 text with a format provider, for <see cref="Hide{T}(ReadOnlySpan{byte}, IFormatProvider, SpanParser{T}, string)"/>.</summary>
	/// <typeparam name="T">The type parsed.</typeparam>
	/// <param name="s">The UTF-8 text.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	internal delegate T SpanParser<T>(ReadOnlySpan<byte> s, IFormatProvider provider);

	/// <summary>
	/// Runs <paramref name="parse"/>, and replaces its <see cref="FormatException"/> or <see cref="OverflowException"/> with
	/// one whose message leaves the value out.
	/// </summary>
	/// <typeparam name="T">The type parsed.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="parse">The parser.</param>
	/// <param name="type">The name of the type, for the message.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static T Hide<T>(ReadOnlySpan<byte> s, IFormatProvider provider, SpanParser<T> parse, string type)
	{
		try
		{
			return parse(s, provider);
		}
		catch (Exception e) when (e is FormatException or OverflowException)
		{
			throw Hidden(e, type);
		}
	}

#if NET
	/// <summary>
	/// Parses <paramref name="s"/> as an <see cref="int"/> in <see cref="NumberStyles.Integer"/> style. Up to 9 ASCII
	/// digits with an optional leading <c>-</c> are parsed directly; anything else (more digits, whitespace, a <c>+</c>,
	/// the provider's signs) goes to the BCL's <c>int.Parse</c>, which parses the UTF-8 bytes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not an integer.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="int"/>.</exception>
	public static int Int32(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		TryInt32Fast(s, out var v) ? v : Int32Slow(s, provider, hide);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Int32Slow(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		try
		{
			return int.Parse(s, IntegerStyle, provider);
		}
		catch (Exception e) when (hide && e is FormatException or OverflowException)
		{
			throw Hidden(e, "Int32");
		}
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as an <see cref="int"/>, as <see cref="Int32(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="int"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryInt32(ReadOnlySpan<byte> s, IFormatProvider provider, out int value) =>
		TryInt32Fast(s, out value) || int.TryParse(s, IntegerStyle, provider, out value);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="long"/> in <see cref="NumberStyles.Integer"/> style. Up to 18 ASCII
	/// digits with an optional leading <c>-</c> are parsed directly; anything else (more digits, whitespace, a <c>+</c>,
	/// the provider's signs) goes to the BCL's <c>long.Parse</c>, which parses the UTF-8 bytes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not an integer.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="long"/>.</exception>
	public static long Int64(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		TryInt64Fast(s, out var v) ? v : Int64Slow(s, provider, hide);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static long Int64Slow(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		try
		{
			return long.Parse(s, IntegerStyle, provider);
		}
		catch (Exception e) when (hide && e is FormatException or OverflowException)
		{
			throw Hidden(e, "Int64");
		}
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="long"/>, as <see cref="Int64(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="long"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryInt64(ReadOnlySpan<byte> s, IFormatProvider provider, out long value) =>
		TryInt64Fast(s, out value) || long.TryParse(s, IntegerStyle, provider, out value);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="double"/>, exactly rounded, in <see cref="NumberStyles.Float"/> style
	/// with thousands separators allowed. When the provider's decimal separator is <c>.</c>,
	/// <see cref="FastFloat.TryParseDouble"/> parses plain decimal and exponent notation; anything else goes to the BCL's
	/// <c>double.Parse</c>, which parses the UTF-8 bytes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="double"/> is an infinity.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	public static double Double(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseDouble(s, out var v)) return v;

		return hide ? Hide(s, provider, static (x, p) => double.Parse(x, FloatStyle, p), "Double") : double.Parse(s, FloatStyle, provider);
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="double"/>, as <see cref="Double(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a number.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryDouble(ReadOnlySpan<byte> s, IFormatProvider provider, out double value)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseDouble(s, out value)) return true;

		return double.TryParse(s, FloatStyle, provider, out value);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="float"/>, exactly rounded, in <see cref="NumberStyles.Float"/> style
	/// with thousands separators allowed. When the provider's decimal separator is <c>.</c>,
	/// <see cref="FastFloat.TryParseSingle"/> parses plain decimal and exponent notation; anything else goes to the BCL's
	/// <c>float.Parse</c>, which parses the UTF-8 bytes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="float"/> is an infinity.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	public static float Single(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseSingle(s, out var v)) return v;

		return hide ? Hide(s, provider, static (x, p) => float.Parse(x, FloatStyle, p), "Single") : float.Parse(s, FloatStyle, provider);
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="float"/>, as <see cref="Single(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a number.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TrySingle(ReadOnlySpan<byte> s, IFormatProvider provider, out float value)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseSingle(s, out value)) return true;

		return float.TryParse(s, FloatStyle, provider, out value);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="decimal"/> in <see cref="NumberStyles.Number"/> style with exponents
	/// allowed, with the BCL's <c>decimal.Parse</c>, which parses the UTF-8 bytes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="decimal"/>.</exception>
	public static decimal Decimal(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		hide
			? Hide(s, provider, static (x, p) => decimal.Parse(x, NumberStyles.Number | NumberStyles.AllowExponent, p), "Decimal")
			: decimal.Parse(s, NumberStyles.Number | NumberStyles.AllowExponent, provider);

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="decimal"/>, as <see cref="Decimal(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="decimal"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryDecimal(ReadOnlySpan<byte> s, IFormatProvider provider, out decimal value) =>
		decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowExponent, provider, out value);
#else
	/// <summary>
	/// Parses <paramref name="s"/> as an <see cref="int"/> in <see cref="NumberStyles.Integer"/> style. Up to 9 ASCII
	/// digits with an optional leading <c>-</c> are parsed directly; anything else (more digits, whitespace, a <c>+</c>,
	/// the provider's signs) is decoded to a string for the BCL's <c>int.Parse</c>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not an integer.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="int"/>.</exception>
	public static int Int32(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		TryInt32Fast(s, out var v) ? v : Int32Slow(s, provider, hide);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Int32Slow(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		try
		{
			return int.Parse(Text(s), IntegerStyle, provider);
		}
		catch (Exception e) when (hide && e is FormatException or OverflowException)
		{
			throw Hidden(e, "Int32");
		}
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as an <see cref="int"/>, as <see cref="Int32(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="int"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryInt32(ReadOnlySpan<byte> s, IFormatProvider provider, out int value) =>
		TryInt32Fast(s, out value) || int.TryParse(Text(s), IntegerStyle, provider, out value);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="long"/> in <see cref="NumberStyles.Integer"/> style. Up to 18 ASCII
	/// digits with an optional leading <c>-</c> are parsed directly; anything else (more digits, whitespace, a <c>+</c>,
	/// the provider's signs) is decoded to a string for the BCL's <c>long.Parse</c>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not an integer.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="long"/>.</exception>
	public static long Int64(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		TryInt64Fast(s, out var v) ? v : Int64Slow(s, provider, hide);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static long Int64Slow(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		try
		{
			return long.Parse(Text(s), IntegerStyle, provider);
		}
		catch (Exception e) when (hide && e is FormatException or OverflowException)
		{
			throw Hidden(e, "Int64");
		}
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="long"/>, as <see cref="Int64(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="long"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryInt64(ReadOnlySpan<byte> s, IFormatProvider provider, out long value) =>
		TryInt64Fast(s, out value) || long.TryParse(Text(s), IntegerStyle, provider, out value);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="double"/>, exactly rounded, in <see cref="NumberStyles.Float"/> style
	/// with thousands separators allowed. When the provider's decimal separator is <c>.</c>,
	/// <see cref="FastFloat.TryParseDouble"/> parses plain decimal and exponent notation; anything else is decoded to a
	/// string for the BCL's <c>double.Parse</c>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>
	/// The parsed value; a value beyond the range of <see cref="double"/> is an infinity (on .NET Framework, only when
	/// <see cref="FastFloat"/> parses it).
	/// </returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, <paramref name="s"/> is beyond the range of <see cref="double"/> and is parsed by the
	/// .NET Framework parser (with a format provider whose decimal separator is not <c>.</c>, for example).
	/// </exception>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	public static double Double(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseDouble(s, out var v)) return v;

		return hide ? Hide(s, provider, static (x, p) => double.Parse(Text(x), FloatStyle, p), "Double") : double.Parse(Text(s), FloatStyle, provider);
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="double"/>, as <see cref="Double(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a number.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryDouble(ReadOnlySpan<byte> s, IFormatProvider provider, out double value)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseDouble(s, out value)) return true;

		return double.TryParse(Text(s), FloatStyle, provider, out value);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="float"/>, exactly rounded, in <see cref="NumberStyles.Float"/> style
	/// with thousands separators allowed. When the provider's decimal separator is <c>.</c>,
	/// <see cref="FastFloat.TryParseSingle"/> parses plain decimal and exponent notation; anything else is decoded to a
	/// string for the BCL's <c>float.Parse</c>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>
	/// The parsed value; a value beyond the range of <see cref="float"/> is an infinity (on .NET Framework, only when
	/// <see cref="FastFloat"/> parses it).
	/// </returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, <paramref name="s"/> is beyond the range of <see cref="float"/> and is parsed by the
	/// .NET Framework parser (with a format provider whose decimal separator is not <c>.</c>, for example).
	/// </exception>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	public static float Single(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseSingle(s, out var v)) return v;

		return hide ? Hide(s, provider, static (x, p) => float.Parse(Text(x), FloatStyle, p), "Single") : float.Parse(Text(s), FloatStyle, provider);
	}

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="float"/>, as <see cref="Single(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a number.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TrySingle(ReadOnlySpan<byte> s, IFormatProvider provider, out float value)
	{
		if (IsInvariantDecimal(provider) && FastFloat.TryParseSingle(s, out value)) return true;

		return float.TryParse(Text(s), FloatStyle, provider, out value);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="decimal"/> in <see cref="NumberStyles.Number"/> style with exponents
	/// allowed: decoded to a string for the BCL's <c>decimal.Parse</c>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a number.</exception>
	/// <exception cref="OverflowException"><paramref name="s"/> is outside the range of <see cref="decimal"/>.</exception>
	public static decimal Decimal(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		hide
			? Hide(s, provider, static (x, p) => decimal.Parse(Text(x), NumberStyles.Number | NumberStyles.AllowExponent, p), "Decimal")
			: decimal.Parse(Text(s), NumberStyles.Number | NumberStyles.AllowExponent, provider);

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="decimal"/>, as <see cref="Decimal(ReadOnlySpan{byte}, IFormatProvider, bool)"/>
	/// parses it.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="value">The parsed value, or 0 when <paramref name="s"/> is not a valid <see cref="decimal"/>.</param>
	/// <returns>Whether <paramref name="s"/> could be parsed.</returns>
	public static bool TryDecimal(ReadOnlySpan<byte> s, IFormatProvider provider, out decimal value) =>
		decimal.TryParse(Text(s), NumberStyles.Number | NumberStyles.AllowExponent, provider, out value);
#endif

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="bool"/>, accepting the values that
	/// <see cref="TryBoolean(ReadOnlySpan{byte}, out bool)"/> accepts.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message of the exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is none of the accepted values.</exception>
	public static bool Boolean(ReadOnlySpan<byte> s, bool hide) => TryBoolean(s, out var v) ? v : throw NotBoolean(s, hide);

	/// <summary>The exception for a field that is not a boolean, with its value in the message unless <paramref name="hide"/> is set.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message leaves the value out.</param>
	/// <returns>The exception to throw.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static FormatException NotBoolean(ReadOnlySpan<byte> s, bool hide) => hide
		? new FormatException("The field is not a valid boolean (IncludeValuesInExceptions is off, so its value is not shown).")
		: new FormatException($"'{Encoding.UTF8.GetString(s)}' is not a valid boolean.");

	/// <summary>
	/// Tries to parse <paramref name="s"/> as a <see cref="bool"/>: true/false, yes/no, y/n, t/f or 1/0, in any ASCII
	/// case, with surrounding spaces and tabs ignored.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="value">The parsed value, or <see langword="false"/> when <paramref name="s"/> is none of the accepted values.</param>
	/// <returns>Whether <paramref name="s"/> is one of the accepted values.</returns>
	public static bool TryBoolean(ReadOnlySpan<byte> s, out bool value)
	{
		s = s.Trim(" \t"u8);
		switch (s.Length)
		{
			case 1:
				switch (s[0] | 0x20)
				{
					case '1' | 0x20: case 't': case 'y': value = true; return true;
					case '0' | 0x20: case 'f': case 'n': value = false; return true;
				}

				break;
			case 2 when Ascii.EqualsIgnoreCase(s, "no"u8): value = false; return true;
			case 3 when Ascii.EqualsIgnoreCase(s, "yes"u8): value = true; return true;
			case 4 when Ascii.EqualsIgnoreCase(s, "true"u8): value = true; return true;
			case 5 when Ascii.EqualsIgnoreCase(s, "false"u8): value = false; return true;
		}

		value = false;

		return false;
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/>. A UTC value in the round-trip format (<c>"O"</c>, such as
	/// <c>2024-03-01T10:20:30.0000000Z</c>) is parsed directly; anything else goes to the BCL's <c>DateTime.Parse</c>
	/// with the provider and <see cref="DateTimeStyles.RoundtripKind"/> (on .NET from a stack copy when the field has at
	/// most 128 bytes, else from a string).
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider of the BCL fallback.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a date and time.</exception>
	private static DateTime DateTimeCore(ReadOnlySpan<byte> s, IFormatProvider provider)
	{
		if (s.Length == 28 && Utf8Parser.TryParse(s, out DateTime v, out var used, 'O') && used == s.Length) return v;

#if NET
		Span<char> chars = stackalloc char[Math.Min(s.Length, 128)];
		if (s.Length <= 128)
		{
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTime.Parse(chars.Slice(0, n), provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTime.Parse(Encoding.UTF8.GetString(s), provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> with the BCL's <c>DateTimeOffset.Parse</c>, the
	/// provider and <see cref="DateTimeStyles.RoundtripKind"/>; a value without an offset gets the local time zone's. A
	/// field of at most 128 bytes is decoded on the stack, a longer one to a string.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a date and time.</exception>
	private static DateTimeOffset DateTimeOffsetCore(ReadOnlySpan<byte> s, IFormatProvider provider)
	{
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTimeOffset.Parse(chars.Slice(0, n), provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTimeOffset.Parse(Encoding.UTF8.GetString(s), provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly <paramref name="format"/>, with the BCL's
	/// <c>DateTime.ParseExact</c>, the provider and <see cref="DateTimeStyles.RoundtripKind"/>. A field of at most
	/// 128 bytes is decoded on the stack, a longer one to a string.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field, as <c>DateTime.ParseExact</c> takes it.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	private static DateTime DateTimeExactCore(ReadOnlySpan<byte> s, string format, IFormatProvider provider)
	{
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTime.ParseExact(chars.Slice(0, n), format, provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTime.ParseExact(Encoding.UTF8.GetString(s), format, provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly <paramref name="format"/>, with the BCL's
	/// <c>DateTimeOffset.ParseExact</c>, the provider and <see cref="DateTimeStyles.RoundtripKind"/>; a value without an
	/// offset gets the local time zone's. A field of at most 128 bytes is decoded on the stack, a longer one to a string.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field, as <c>DateTimeOffset.ParseExact</c> takes it.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	private static DateTimeOffset DateTimeOffsetExactCore(ReadOnlySpan<byte> s, string format, IFormatProvider provider)
	{
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTimeOffset.ParseExact(chars.Slice(0, n), format, provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTimeOffset.ParseExact(Encoding.UTF8.GetString(s), format, provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly one of <paramref name="formats"/>, tried in
	/// order, as <see cref="DateTimeExactCore(ReadOnlySpan{byte}, string, IFormatProvider)"/> parses one format.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have, as <c>DateTime.ParseExact</c> takes them.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	private static DateTime DateTimeExactCore(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider)
	{
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTime.ParseExact(chars.Slice(0, n), formats, provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTime.ParseExact(Encoding.UTF8.GetString(s), formats, provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly one of <paramref name="formats"/>,
	/// tried in order, as <see cref="DateTimeOffsetExactCore(ReadOnlySpan{byte}, string, IFormatProvider)"/> parses one
	/// format.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have, as <c>DateTimeOffset.ParseExact</c> takes them.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	private static DateTimeOffset DateTimeOffsetExactCore(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider)
	{
		if (s.Length <= 128)
		{
			Span<char> chars = stackalloc char[128];
			var n = Encoding.UTF8.GetChars(s, chars);

			return System.DateTimeOffset.ParseExact(chars.Slice(0, n), formats, provider, DateTimeStyles.RoundtripKind);
		}

		return System.DateTimeOffset.ParseExact(Encoding.UTF8.GetString(s), formats, provider, DateTimeStyles.RoundtripKind);
	}
#else
		return System.DateTime.Parse(Text(s), provider, DateTimeStyles.RoundtripKind);
	}

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/>: decoded to a string for the BCL's
	/// <c>DateTimeOffset.Parse</c> with the provider and <see cref="DateTimeStyles.RoundtripKind"/>. A value without an
	/// offset gets the local time zone's.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a date and time.</exception>
	private static DateTimeOffset DateTimeOffsetCore(ReadOnlySpan<byte> s, IFormatProvider provider) =>
		System.DateTimeOffset.Parse(Text(s), provider, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly <paramref name="format"/>: decoded to a string
	/// for the BCL's <c>DateTime.ParseExact</c> with the provider and <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field, as <c>DateTime.ParseExact</c> takes it.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	private static DateTime DateTimeExactCore(ReadOnlySpan<byte> s, string format, IFormatProvider provider) =>
		System.DateTime.ParseExact(Text(s), format, provider, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly <paramref name="format"/>: decoded to a
	/// string for the BCL's <c>DateTimeOffset.ParseExact</c> with the provider and <see cref="DateTimeStyles.RoundtripKind"/>.
	/// A value without an offset gets the local time zone's.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field, as <c>DateTimeOffset.ParseExact</c> takes it.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	private static DateTimeOffset DateTimeOffsetExactCore(ReadOnlySpan<byte> s, string format, IFormatProvider provider) =>
		System.DateTimeOffset.ParseExact(Text(s), format, provider, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly one of <paramref name="formats"/>, tried in
	/// order: decoded to a string for the BCL's <c>DateTime.ParseExact</c> with the provider and
	/// <see cref="DateTimeStyles.RoundtripKind"/>.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have, as <c>DateTime.ParseExact</c> takes them.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	private static DateTime DateTimeExactCore(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider) =>
		System.DateTime.ParseExact(Text(s), formats, provider, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly one of <paramref name="formats"/>,
	/// tried in order: decoded to a string for the BCL's <c>DateTimeOffset.ParseExact</c> with the provider and
	/// <see cref="DateTimeStyles.RoundtripKind"/>. A value without an offset gets the local time zone's.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have, as <c>DateTimeOffset.ParseExact</c> takes them.</param>
	/// <param name="provider">The format provider.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	private static DateTimeOffset DateTimeOffsetExactCore(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider) =>
		System.DateTimeOffset.ParseExact(Text(s), formats, provider, DateTimeStyles.RoundtripKind);
#endif

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.Guid"/>. The default <c>"D"</c> format
	/// (<c>0f8fad5b-d9cb-469f-a165-70867728950e</c>) is parsed directly with <see cref="Utf8Parser"/>; any other format
	/// that <see cref="System.Guid.Parse(string)"/> accepts goes to it as a string.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a GUID.</exception>
	private static Guid GuidCore(ReadOnlySpan<byte> s)
	{
		if (Utf8Parser.TryParse(s, out Guid g, out var used) && used == s.Length) return g;

		return System.Guid.Parse(Encoding.UTF8.GetString(s));
	}

	/// <summary>Parses <paramref name="s"/> as a <see cref="System.DateTime"/>, as <see cref="DateTimeCore"/> describes.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a date and time.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DateTime DateTime(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		hide ? Hide(s, provider, static (x, p) => DateTimeCore(x, p), "DateTime") : DateTimeCore(s, provider);

	/// <summary>Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/>, as <see cref="DateTimeOffsetCore"/> describes.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a date and time.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DateTimeOffset DateTimeOffset(ReadOnlySpan<byte> s, IFormatProvider provider, bool hide) =>
		hide ? Hide(s, provider, static (x, p) => DateTimeOffsetCore(x, p), "DateTimeOffset") : DateTimeOffsetCore(s, provider);

	/// <summary>Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly <paramref name="format"/>, as <see cref="DateTimeExactCore(ReadOnlySpan{byte}, string, IFormatProvider)"/> describes.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DateTime DateTimeExact(ReadOnlySpan<byte> s, string format, IFormatProvider provider, bool hide) =>
		hide ? DateTimeExactHidden(s, format, provider) : DateTimeExactCore(s, format, provider);

	// Separate, so that the closure over the format is only created when it is used.
	private static DateTime DateTimeExactHidden(ReadOnlySpan<byte> s, string format, IFormatProvider provider) =>
		Hide(s, provider, (x, p) => DateTimeExactCore(x, format, p), "DateTime");

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly <paramref name="format"/>, as
	/// <see cref="DateTimeOffsetExactCore(ReadOnlySpan{byte}, string, IFormatProvider)"/> describes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="format">The exact format of the field.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> does not have the format, or the format is not valid.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DateTimeOffset DateTimeOffsetExact(ReadOnlySpan<byte> s, string format, IFormatProvider provider, bool hide) =>
		hide ? DateTimeOffsetExactHidden(s, format, provider) : DateTimeOffsetExactCore(s, format, provider);

	private static DateTimeOffset DateTimeOffsetExactHidden(ReadOnlySpan<byte> s, string format, IFormatProvider provider) =>
		Hide(s, provider, (x, p) => DateTimeOffsetExactCore(x, format, p), "DateTimeOffset");

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTime"/> in exactly one of <paramref name="formats"/>, tried in
	/// order, as <see cref="DateTimeExactCore(ReadOnlySpan{byte}, string[], IFormatProvider)"/> describes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	public static DateTime DateTimeExact(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider, bool hide) =>
		hide ? DateTimeExactHidden(s, formats, provider) : DateTimeExactCore(s, formats, provider);

	private static DateTime DateTimeExactHidden(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider) =>
		Hide(s, provider, (x, p) => DateTimeExactCore(x, formats, p), "DateTime");

	/// <summary>
	/// Parses <paramref name="s"/> as a <see cref="System.DateTimeOffset"/> in exactly one of <paramref name="formats"/>,
	/// tried in order, as <see cref="DateTimeOffsetExactCore(ReadOnlySpan{byte}, string[], IFormatProvider)"/> describes.
	/// </summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="formats">The formats the field may have.</param>
	/// <param name="provider">The format provider.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> has none of the formats, or a format is not valid.</exception>
	public static DateTimeOffset DateTimeOffsetExact(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider, bool hide) =>
		hide ? DateTimeOffsetExactHidden(s, formats, provider) : DateTimeOffsetExactCore(s, formats, provider);

	private static DateTimeOffset DateTimeOffsetExactHidden(ReadOnlySpan<byte> s, string[] formats, IFormatProvider provider) =>
		Hide(s, provider, (x, p) => DateTimeOffsetExactCore(x, formats, p), "DateTimeOffset");

	/// <summary>Parses <paramref name="s"/> as a <see cref="System.Guid"/>, as <see cref="GuidCore"/> describes.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message of an exception leaves the value out.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is not a GUID.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Guid Guid(ReadOnlySpan<byte> s, bool hide) =>
		hide ? Hide(s, CultureInfo.InvariantCulture, static (x, _) => GuidCore(x), "Guid") : GuidCore(s);

#if !NET
	// Without span overloads the BCL parses a string.
	private static string Text(ReadOnlySpan<byte> s) => Encoding.UTF8.GetString(s);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryInt32Fast(ReadOnlySpan<byte> s, out int value)
	{
		var len = s.Length;
		ref var p = ref MemoryMarshal.GetReference(s);
		var i = 0;
		var negative = false;
		if (len != 0 && p == (byte)'-')
		{
			negative = true;
			i = 1;
		}

		// Up to 9 digits cannot overflow.
		if ((uint)(len - i - 1) < 9u)
		{
			var v = 0U;
			for (; i < len; i++)
			{
				var d = (uint)(Unsafe.Add(ref p, i) - '0');
				if (d > 9) goto Fail;
				v = v * 10 + d;
			}

			value = negative ? -(int)v : (int)v;

			return true;
		}

	Fail:
		value = 0;

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryInt64Fast(ReadOnlySpan<byte> s, out long value)
	{
		var len = s.Length;
		ref var p = ref MemoryMarshal.GetReference(s);
		var i = 0;
		var negative = false;
		if (len != 0 && p == (byte)'-')
		{
			negative = true;
			i = 1;
		}

		// Up to 18 digits cannot overflow.
		if ((uint)(len - i - 1) < 18u)
		{
			var v = 0UL;
			for (; i < len; i++)
			{
				var d = (uint)(Unsafe.Add(ref p, i) - '0');
				if (d > 9) goto Fail;
				v = v * 10 + d;
			}

			value = negative ? -(long)v : (long)v;

			return true;
		}

	Fail:
		value = 0;

		return false;
	}
}
