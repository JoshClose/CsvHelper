using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#else
using System.Buffers.Binary;
#endif
using System.Text;

namespace CsvHelper;

public sealed partial class CsvReader
{
	private static readonly string[] asciiStrings = CreateAsciiStrings();

	private static string[] CreateAsciiStrings()
	{
		var strings = new string[128];
		for (var i = 0; i < strings.Length; i++) strings[i] = ((char)i).ToString();

		return strings;
	}

	/// <summary>
	/// Gets the value of field <paramref name="index"/> of the current record as UTF-8 bytes, decoded as the
	/// <see cref="CsvReaderOptions.Mode"/> says: with quotes removed and escaped quotes unescaped, with escape characters
	/// removed, or as the text of its column. The span is valid until the next read.
	/// </summary>
	/// <remarks>
	/// A field past the end of the record reads as empty, unless <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>. The other field accessors behave the same way.
	/// </remarks>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field as UTF-8 bytes; empty for an empty field.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<byte> GetSpan(int index)
	{
		if ((uint)index >= (uint)fieldCount) return MissingField(index);
		ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(ends), (nint)(uint)(fieldBase + index));
		var start = e & int.MaxValue;
		var next = Unsafe.Add(ref e, 1);
		// The scanner flags fields that need decoding (quoted, or trimming enabled).
		if (next < 0) return DecodeField(start, (next & int.MaxValue) - 1 - start);

#if NET
		return MemoryMarshal.CreateReadOnlySpan(
			ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), (nint)(uint)start), next - 1 - start);
#else
		return new ReadOnlySpan<byte>(buffer, start, next - 1 - start);
#endif
	}

	/// <summary>Gets the value of the field with the given header name. See <see cref="GetSpan(int)"/>.</summary>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The value of the field as UTF-8 bytes; empty for an empty field.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public ReadOnlySpan<byte> GetSpan(string name) => GetSpan(GetFieldIndex(name));

	/// <summary>Gets field <paramref name="index"/> of the current record. The field is valid until the next read.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public CsvField this[int index] => new(this, GetSpan(index), index);

	/// <summary>Gets the field with the given header name. The field is valid until the next read.</summary>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public CsvField this[string name]
	{
		get
		{
			var index = GetFieldIndex(name);

			return new(this, GetSpan(index), index);
		}
	}

	/// <summary>The current record as a <see cref="CsvRow"/>. Valid until the next read.</summary>
	public CsvRow Current => new(this);

	/// <summary>
	/// The raw bytes of the current record as they appear in the input (UTF-8), without the line break. Empty before
	/// the first record and at the end of the input. Valid until the next read.
	/// </summary>
	public ReadOnlySpan<byte> RawRecord
	{
		get
		{
			if (fieldCount == 0) return default;
			var start = ends[fieldBase] & int.MaxValue;
			var end = (ends[fieldBase + fieldCount] & int.MaxValue) - 1;

			return new ReadOnlySpan<byte>(buffer, start, end - start);
		}
	}

	/// <summary>Gets the raw bytes of field <paramref name="index"/> exactly as they appear in the input.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>
	/// The bytes of the field (UTF-8), including any quotes, escaped quotes, escape characters, padding and surrounding
	/// whitespace. Valid until the next read.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public ReadOnlySpan<byte> GetRawSpan(int index)
	{
		if ((uint)index >= (uint)fieldCount) return MissingField(index);
		var start = ends[fieldBase + index] & int.MaxValue;
		var length = (ends[fieldBase + index + 1] & int.MaxValue) - 1 - start;
		if (fixedWidth) return FixedWidthField(start, length, trimPadding: false);

		return new ReadOnlySpan<byte>(buffer, start, length);
	}

	/// <summary>
	/// Gets field <paramref name="index"/> as a string. With <see cref="CsvReaderOptions.PoolStrings"/>, a value seen
	/// before in the same column may return the same string instance.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field; <see cref="string.Empty"/> for an empty field.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public string GetString(int index)
	{
		if ((uint)index < (uint)fieldCount)
		{
			ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(ends), (nint)(uint)(fieldBase + index));
			var start = e & int.MaxValue;
			var next = Unsafe.Add(ref e, 1);
			var buffer = this.buffer;
			// Fields that need no decoding, with 16 readable bytes from their start (for the pool's loads).
			if (next >= 0 && start <= buffer.Length - 16)
			{
				var length = next - 1 - start;
				ref var p = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), (nint)(uint)start);
				if (length <= 1)
				{
					if (length == 0) return string.Empty;
					if (p < 128) return asciiStrings[p];
				}

				if (this.pool is { } pool) return pool.Get(index, ref p, length, recordNumber);

#if NET
				return Utf8ToString(MemoryMarshal.CreateReadOnlySpan(ref p, length));
#else
				return Utf8ToString(new ReadOnlySpan<byte>(buffer, start, length));
#endif
			}
		}

		return MakeString(GetSpan(index), index);
	}

	/// <summary>Gets the field with the given header name as a string. See <see cref="GetString(int)"/>.</summary>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The value of the field; <see cref="string.Empty"/> for an empty field.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public string GetString(string name) => GetString(GetFieldIndex(name));

	/// <summary>Copies field <paramref name="index"/> into <paramref name="destination"/> as UTF-16.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="destination">Receives the value of the field.</param>
	/// <returns>The number of chars written to <paramref name="destination"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="destination"/> is too small for the value.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public int GetChars(int index, Span<char> destination) => Encoding.UTF8.GetChars(GetSpan(index), destination);

	/// <summary>Whether field <paramref name="index"/> is empty (or missing).</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>
	/// <see langword="true"/> when the value is empty (<c>""</c> included) or the record has no such field;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public bool IsEmpty(int index) => GetSpan(index).IsEmpty;

	/// <summary>Whether field <paramref name="index"/> is null: empty (or missing), or one of <see cref="CsvReaderOptions.NullValues"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>
	/// <see langword="true"/> when the value is empty, the record has no such field or the value is one of
	/// <see cref="CsvReaderOptions.NullValues"/>; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public bool IsNull(int index) => IsNullSpan(GetSpan(index));

	/// <summary>
	/// Gets field <paramref name="index"/> as a string, or <see langword="null"/> when it is one of
	/// <see cref="CsvReaderOptions.NullValues"/>. An empty field is <c>""</c> unless <c>""</c> is one of them.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field, or <see langword="null"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public string? GetStringOrNull(int index) => nullValues is not null && IsNullValue(GetSpan(index)) ? null : GetString(index);

	/// <summary>Gets the field with the given header name as a string, or <see langword="null"/>. See <see cref="GetStringOrNull(int)"/>.</summary>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The value of the field, or <see langword="null"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public string? GetStringOrNull(string name) => GetStringOrNull(GetFieldIndex(name));

	/// <summary>Whether a field value is null for <c>ParseOrNull</c>: empty, or one of <see cref="CsvReaderOptions.NullValues"/>.</summary>
	internal bool IsNullSpan(ReadOnlySpan<byte> s) => s.IsEmpty || (nullValues is not null && IsNullValue(s));

	/// <summary>Whether a field value is one of <see cref="CsvReaderOptions.NullValues"/> (exactly).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool IsNullValue(ReadOnlySpan<byte> s)
	{
		foreach (var value in nullValues!)
		{
			if (s.SequenceEqual(value))
			{
				return true;
			}
		}

		return false;
	}

#if NET
	/// <summary>
	/// Parses field <paramref name="index"/> as <typeparamref name="T"/> using the configured format provider. Common
	/// types (<see cref="int"/>, <see cref="long"/>, <see cref="double"/>, <see cref="float"/>, <see cref="decimal"/>,
	/// <see cref="bool"/>, <see cref="Guid"/>, <see cref="DateTime"/> and <see cref="string"/>) are parsed as by the
	/// typed getters such as <see cref="GetInt32(int)"/>.
	/// </summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a valid <typeparamref name="T"/>.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <typeparamref name="T"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(int index) where T : ISpanParsable<T> => ParseSpan<T>(GetSpan(index), index);

	/// <summary>Parses the field with the given header name as <typeparamref name="T"/>. See <see cref="Parse{T}(int)"/>.</summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a valid <typeparamref name="T"/>.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <typeparamref name="T"/>.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(string name) where T : ISpanParsable<T> => Parse<T>(GetFieldIndex(name));

	/// <summary>
	/// Parses field <paramref name="index"/> as <typeparamref name="T"/> with <paramref name="provider"/> instead of
	/// the configured format provider. See <see cref="Parse{T}(int)"/>.
	/// </summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a valid <typeparamref name="T"/>.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <typeparamref name="T"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(int index, IFormatProvider provider) where T : ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);

		return ParseSpan<T>(GetSpan(index), index, provider);
	}

	/// <summary>Tries to parse field <paramref name="index"/> as <typeparamref name="T"/>. See <see cref="Parse{T}(int)"/>.</summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="value">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
	/// <returns>Whether the field could be parsed.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public bool TryParse<T>(int index, out T value) where T : ISpanParsable<T> => TryParseSpan(GetSpan(index), index, out value);
#endif

	/// <summary>
	/// Parses field <paramref name="index"/> with <paramref name="parser"/>, which gets the field as a string and the
	/// configured format provider, for example <c>reader.Parse(3, TimeSpan.Parse)</c>.
	/// </summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> returns.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="parser">Parses the value of the field with the given format provider.</param>
	/// <returns>The value that <paramref name="parser"/> returns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(int index, Func<string, IFormatProvider, T> parser)
	{
		ArgumentNullException.ThrowIfNull(parser);

		return parser(GetString(index), provider);
	}

	/// <summary>
	/// Parses the field with the given header name with <paramref name="parser"/>. See
	/// <see cref="Parse{T}(int, Func{string, IFormatProvider, T})"/>.
	/// </summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> returns.</typeparam>
	/// <param name="name">The header name of the column, matched as by <see cref="TryGetFieldIndex(string, out int)"/>.</param>
	/// <param name="parser">Parses the value of the field with the given format provider.</param>
	/// <returns>The value that <paramref name="parser"/> returns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="parser"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(string name, Func<string, IFormatProvider, T> parser) => Parse(GetFieldIndex(name), parser);

	/// <summary>
	/// Tries to parse field <paramref name="index"/> with <paramref name="parser"/>, which gets the field as a string
	/// and the configured format provider.
	/// </summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> parses.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="parser">Tries to parse the value of the field with the given format provider.</param>
	/// <param name="value">The value that <paramref name="parser"/> produced.</param>
	/// <returns>The result of <paramref name="parser"/>: whether the field could be parsed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public bool TryParse<T>(int index, CsvTryParser<T> parser, out T value)
	{
		ArgumentNullException.ThrowIfNull(parser);

		return parser(GetString(index), provider, out value);
	}

	internal T ParseWith<T>(ReadOnlySpan<byte> s, int column, Func<string, IFormatProvider, T> parser)
	{
		ArgumentNullException.ThrowIfNull(parser);

		return parser(MakeString(s, column), provider);
	}

	internal bool TryParseWith<T>(ReadOnlySpan<byte> s, int column, CsvTryParser<T> parser, out T value)
	{
		ArgumentNullException.ThrowIfNull(parser);

		return parser(MakeString(s, column), provider, out value);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as an <see cref="int"/>, in <see cref="System.Globalization.NumberStyles.Integer"/>
	/// style with the configured format provider.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="int"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public int GetInt32(int index) => CsvParse.Int32(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as an <see cref="int"/> with <paramref name="provider"/> instead of the
	/// configured format provider. See <see cref="GetInt32(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="int"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public int GetInt32(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.Int32(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="long"/>, in <see cref="System.Globalization.NumberStyles.Integer"/>
	/// style with the configured format provider.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="long"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public long GetInt64(int index) => CsvParse.Int64(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="long"/> with <paramref name="provider"/> instead of the
	/// configured format provider. See <see cref="GetInt64(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="long"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public long GetInt64(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.Int64(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="double"/> (exactly rounded), in
	/// <see cref="System.Globalization.NumberStyles.Float"/> style with thousands separators allowed, with the
	/// configured format provider.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="double"/> is an infinity.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, the field is beyond the range of <see cref="double"/> and is parsed by the .NET
	/// Framework parser (with a format provider whose decimal separator is not ".", for example).
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public double GetDouble(int index) => CsvParse.Double(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="double"/> with <paramref name="provider"/> instead of the
	/// configured format provider. See <see cref="GetDouble(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="double"/> is an infinity.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, the field is beyond the range of <see cref="double"/> and is parsed by the .NET
	/// Framework parser (with a format provider whose decimal separator is not ".", for example).
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public double GetDouble(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.Double(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="float"/> (exactly rounded), in
	/// <see cref="System.Globalization.NumberStyles.Float"/> style with thousands separators allowed, with the
	/// configured format provider.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="float"/> is an infinity.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, the field is beyond the range of <see cref="float"/> and is parsed by the .NET
	/// Framework parser (with a format provider whose decimal separator is not ".", for example).
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public float GetSingle(int index) => CsvParse.Single(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="float"/> with <paramref name="provider"/> instead of the
	/// configured format provider. See <see cref="GetSingle(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value; a value beyond the range of <see cref="float"/> is an infinity.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">
	/// On .NET Framework only, the field is beyond the range of <see cref="float"/> and is parsed by the .NET
	/// Framework parser (with a format provider whose decimal separator is not ".", for example).
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public float GetSingle(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.Single(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="decimal"/>, in <see cref="System.Globalization.NumberStyles.Number"/>
	/// style with exponents allowed, with the configured format provider.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="decimal"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public decimal GetDecimal(int index) => CsvParse.Decimal(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="decimal"/> with <paramref name="provider"/> instead of the
	/// configured format provider. See <see cref="GetDecimal(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="decimal"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public decimal GetDecimal(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.Decimal(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="bool"/>: true/false, yes/no, y/n, t/f or 1/0 (or
	/// <see cref="CsvReaderOptions.TrueValues"/> and <see cref="CsvReaderOptions.FalseValues"/>), in any case, with
	/// surrounding spaces and tabs ignored.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is none of the accepted values.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public bool GetBoolean(int index) => ParseBoolean(GetSpan(index));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ParseBoolean(ReadOnlySpan<byte> s) => booleans is null ? CsvParse.Boolean(s, hideValues) : booleans.Parse(s, hideValues);

	private bool TryParseBoolean(ReadOnlySpan<byte> s, out bool value) =>
		booleans is null ? CsvParse.TryBoolean(s, out value) : booleans.TryParse(s, out value);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> (ISO 8601 or the format provider's formats).
	/// A time zone offset or <c>Z</c> in the field gives a <see cref="DateTimeKind.Local"/> or
	/// <see cref="DateTimeKind.Utc"/> value as <see cref="System.Globalization.DateTimeStyles.RoundtripKind"/> describes.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index) => CsvParse.DateTime(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> with <paramref name="provider"/> instead of
	/// the configured format provider. See <see cref="GetDateTime(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTime(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> in exactly the given format, such as
	/// <c>"yyyyMMdd"</c>, with the configured format provider. A time zone offset or <c>Z</c> in the field gives a
	/// <see cref="DateTimeKind.Local"/> or <see cref="DateTimeKind.Utc"/> value as
	/// <see cref="DateTimeStyles.RoundtripKind"/> describes.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="format">The format of the field, as <see cref="DateTime.ParseExact(string, string, IFormatProvider)"/> takes it.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="format"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field does not have the format, or the format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index, string format)
	{
		ArgumentNullException.ThrowIfNull(format);

		return CsvParse.DateTimeExact(GetSpan(index), format, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> in exactly the given format with
	/// <paramref name="provider"/>. See <see cref="GetDateTime(int, string)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="format">The format of the field, as <see cref="DateTime.ParseExact(string, string, IFormatProvider)"/> takes it.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="format"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field does not have the format, or the format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index, string format, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(format);
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTimeExact(GetSpan(index), format, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> in exactly one of the given formats, tried in
	/// order, such as <c>"yyyy-MM-dd"</c> and <c>"dd/MM/yyyy"</c> for a file that mixes them, with the configured format
	/// provider. See <see cref="GetDateTime(int, string)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="formats">The formats the field may have, as <see cref="DateTime.ParseExact(string, string[], IFormatProvider, DateTimeStyles)"/> takes them.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field has none of the formats, or a format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index, string[] formats)
	{
		ArgumentNullException.ThrowIfNull(formats);

		return CsvParse.DateTimeExact(GetSpan(index), formats, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTime"/> in exactly one of the given formats, tried in
	/// order, with <paramref name="provider"/>. See <see cref="GetDateTime(int, string[])"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="formats">The formats the field may have, as <see cref="DateTime.ParseExact(string, string[], IFormatProvider, DateTimeStyles)"/> takes them.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field has none of the formats, or a format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTime GetDateTime(int index, string[] formats, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(formats);
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTimeExact(GetSpan(index), formats, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> (ISO 8601 or the format provider's
	/// formats). A field without an offset gets the local time zone's offset.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index) => CsvParse.DateTimeOffset(GetSpan(index), provider, hideValues);

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> with <paramref name="provider"/>
	/// instead of the configured format provider. See <see cref="GetDateTimeOffset(int)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTimeOffset(GetSpan(index), provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> in exactly the given format, such as
	/// <c>"yyyyMMddHHmmsszzz"</c>, with the configured format provider. A field without an offset gets the local time
	/// zone's offset.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="format">The format of the field, as <see cref="DateTimeOffset.ParseExact(string, string, IFormatProvider)"/> takes it.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="format"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field does not have the format, or the format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index, string format)
	{
		ArgumentNullException.ThrowIfNull(format);

		return CsvParse.DateTimeOffsetExact(GetSpan(index), format, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> in exactly the given format with
	/// <paramref name="provider"/>. See <see cref="GetDateTimeOffset(int, string)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="format">The format of the field, as <see cref="DateTimeOffset.ParseExact(string, string, IFormatProvider)"/> takes it.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="format"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field does not have the format, or the format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index, string format, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(format);
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTimeOffsetExact(GetSpan(index), format, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> in exactly one of the given formats, tried
	/// in order, with the configured format provider. See <see cref="GetDateTimeOffset(int, string)"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="formats">The formats the field may have, as <see cref="DateTimeOffset.ParseExact(string, string[], IFormatProvider, DateTimeStyles)"/> takes them.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field has none of the formats, or a format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index, string[] formats)
	{
		ArgumentNullException.ThrowIfNull(formats);

		return CsvParse.DateTimeOffsetExact(GetSpan(index), formats, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="DateTimeOffset"/> in exactly one of the given formats, tried
	/// in order, with <paramref name="provider"/>. See <see cref="GetDateTimeOffset(int, string[])"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="formats">The formats the field may have, as <see cref="DateTimeOffset.ParseExact(string, string[], IFormatProvider, DateTimeStyles)"/> takes them.</param>
	/// <param name="provider">The format provider to parse with.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field has none of the formats, or a format is not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public DateTimeOffset GetDateTimeOffset(int index, string[] formats, IFormatProvider provider)
	{
		ArgumentNullException.ThrowIfNull(formats);
		ArgumentNullException.ThrowIfNull(provider);

		return CsvParse.DateTimeOffsetExact(GetSpan(index), formats, provider, hideValues);
	}

	/// <summary>
	/// Parses field <paramref name="index"/> as a <see cref="Guid"/>, in any of the formats that
	/// <see cref="Guid.Parse(string)"/> accepts.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a GUID.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public Guid GetGuid(int index) => CsvParse.Guid(GetSpan(index), hideValues);

	/// <summary>Gets the index of the column with the given header name. See <see cref="TryGetFieldIndex(string, out int)"/>.</summary>
	/// <param name="name">The header name of the column.</param>
	/// <returns>The 0-based index of the column.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	public int GetFieldIndex(string name)
	{
		if (TryGetFieldIndex(name, out var index)) return index;
		throw new KeyNotFoundException($"The CSV header has no column named '{name}'.");
	}

	/// <summary>
	/// Finds the index of the column with the given header name. An exact match wins; otherwise the
	/// name is matched ignoring case and surrounding whitespace. Both names are changed by
	/// <see cref="CsvReaderOptions.NormalizeHeaderName"/> first, if it is set. Reads the header first if it has not been
	/// read (see <see cref="ReadHeader"/>).
	/// </summary>
	/// <remarks>
	/// When several columns have the same name, the first one wins; <see cref="TryGetFieldIndex(string, int, out int)"/>
	/// finds the others. Without a header (<see cref="CsvReaderOptions.HasHeader"/> is <see langword="false"/>), no name
	/// is found.
	/// </remarks>
	/// <param name="name">The header name of the column.</param>
	/// <param name="index">The 0-based index of the column, or 0 when it is not found.</param>
	/// <returns>Whether a column has that name.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	public bool TryGetFieldIndex(string name, out int index)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (!headerDone) EnsureHeader();
		var key = HeaderKey(name);
		if (headerIndex!.TryGetValue(key, out index)) return true;

		return headerIndexLoose!.TryGetValue(key.Trim(), out index);
	}

	/// <summary>
	/// Finds the index of a column whose name several columns have: the one at <paramref name="nameIndex"/> among them, in
	/// header order. Names are matched as by <see cref="TryGetFieldIndex(string, out int)"/>: among the exact matches
	/// when there are enough, else among those that match ignoring case and surrounding whitespace.
	/// </summary>
	/// <param name="name">The header name of the column.</param>
	/// <param name="nameIndex">0 for the first column with that name, 1 for the second, and so on.</param>
	/// <param name="index">The 0-based index of the column, or 0 when it is not found.</param>
	/// <returns>Whether the header has that many columns with the name.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="nameIndex"/> is negative.</exception>
	public bool TryGetFieldIndex(string name, int nameIndex, out int index)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (nameIndex < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(nameIndex), nameIndex, "nameIndex cannot be negative.");
		}

		if (nameIndex == 0)
		{
			return TryGetFieldIndex(name, out index);
		}

		if (!headerDone) EnsureHeader();
		var key = HeaderKey(name);

		return FindNthHeader(key, nameIndex, StringComparison.Ordinal, false, out index) ||
			   FindNthHeader(key.Trim(), nameIndex, StringComparison.OrdinalIgnoreCase, true, out index);
	}

	/// <summary>Gets the index of the column at <paramref name="nameIndex"/> among those with the name. See <see cref="TryGetFieldIndex(string, int, out int)"/>.</summary>
	/// <param name="name">The header name of the column.</param>
	/// <param name="nameIndex">0 for the first column with that name, 1 for the second, and so on.</param>
	/// <returns>The 0-based index of the column.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="nameIndex"/> is negative.</exception>
	/// <exception cref="KeyNotFoundException">The header does not have that many columns with the name.</exception>
	public int GetFieldIndex(string name, int nameIndex)
	{
		if (TryGetFieldIndex(name, nameIndex, out var index))
		{
			return index;
		}

		throw new KeyNotFoundException($"The CSV header has no column number {nameIndex + 1} named '{name}'.");
	}

	private bool FindNthHeader(string key, int nth, StringComparison comparison, bool trim, out int index)
	{
		var keys = headerKeys;
		var seen = 0;
		for (var i = 0; i < keys.Length; i++)
		{
			if (string.Equals(trim ? keys[i].Trim() : keys[i], key, comparison) && seen++ == nth)
			{
				index = i;

				return true;
			}
		}

		index = 0;

		return false;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ReadOnlySpan<byte> MissingField(int index)
	{
		if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Field index cannot be negative.");
		if (throwOnMissing)
			throw new IndexOutOfRangeException($"Record {recordNumber} has {fieldCount} fields; field {index} does not exist.");

		return default;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ReadOnlySpan<byte> DecodeField(int start, int length)
	{
		var buffer = this.buffer;
		// Common case: "value" of up to 64 bytes with no quotes inside (the field starts with its opening quote).
		// One 64-byte read, which may run past the field but not past the buffer, gives every quote position
		// without a loop or length-dependent branches; bits beyond the field are masked off.
		if ((uint)(length - 2) <= 62 && start <= buffer.Length - 64 && quickUnquote)
		{
			ref var p = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), (nint)(uint)start);
			var quotes = QuoteBits64(ref p) & (ulong.MaxValue >> (64 - length));
			if (quotes == (1UL | (1UL << (length - 1))))
#if NET
				return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref p, 1), length - 2);
#else
				return new ReadOnlySpan<byte>(buffer, start + 1, length - 2);
#endif
		}

		return DecodeFieldSlow(start, length);
	}

	/// <summary>Bit i is set when byte i of the 64 bytes at <paramref name="p"/> is the quote character.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ulong QuoteBits64(ref byte p)
	{
#if NET
		if (Vector512.IsHardwareAccelerated)
			return Vector512.Equals(Vector512.LoadUnsafe(ref p), Vector512.Create(quoteByte)).ExtractMostSignificantBits();
		if (Vector256.IsHardwareAccelerated)
		{
			var q = Vector256.Create(quoteByte);

			return Vector256.Equals(Vector256.LoadUnsafe(ref p), q).ExtractMostSignificantBits() |
				   ((ulong)Vector256.Equals(Vector256.LoadUnsafe(ref p, 32), q).ExtractMostSignificantBits() << 32);
		}

		var q128 = Vector128.Create(quoteByte);

		return Vector128.Equals(Vector128.LoadUnsafe(ref p), q128).ExtractMostSignificantBits() |
			   ((ulong)Vector128.Equals(Vector128.LoadUnsafe(ref p, 16), q128).ExtractMostSignificantBits() << 16) |
			   ((ulong)Vector128.Equals(Vector128.LoadUnsafe(ref p, 32), q128).ExtractMostSignificantBits() << 32) |
			   ((ulong)Vector128.Equals(Vector128.LoadUnsafe(ref p, 48), q128).ExtractMostSignificantBits() << 48);
#else
		return Portable.MatchBits64(ref p, Portable.Repeat(quoteByte));
#endif
	}

	// Out of line, so that DecodeField's fast path keeps a minimal frame (no zeroed span locals).
	[MethodImpl(MethodImplOptions.NoInlining)]
	private ReadOnlySpan<byte> DecodeFieldSlow(int start, int length)
	{
		if (!quotes) return DecodeUnquoted(start, length);
		var raw = new ReadOnlySpan<byte>(buffer, start, length);
		if (trim)
		{
			raw = TrimWhitespace(raw);
			if (raw.IsEmpty || raw[0] != quoteInt) return raw;
		}

		// A field that needs decoding and is not trimmed whitespace starts with its opening quote.
		var value = Unescape(raw);

		return trimInsideQuotes ? TrimWhitespace(value) : value;
	}

	/// <summary>Decodes a field of the modes other than Rfc4180 (where a fixed-width length can be -1, see <see cref="FixedWidthField"/>).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private ReadOnlySpan<byte> DecodeUnquoted(int start, int length)
	{
		if (fixedWidth) return FixedWidthField(start, length, trim);
		var raw = new ReadOnlySpan<byte>(buffer, start, length);
		if (escapes) return UnescapeEscaped(raw);

		return TrimWhitespace(raw);
	}

	/// <summary>
	/// Removes the escape characters of an Escape mode field: each one is dropped and the byte after it kept as text. An
	/// escape at the very end (the input ended) is kept. Trimming keeps escaped whitespace.
	/// </summary>
	private ReadOnlySpan<byte> UnescapeEscaped(ReadOnlySpan<byte> raw)
	{
		if (trim)
		{
			// Leading whitespace is never escaped: its escape would come first.
			var a = 0;
			while (a < raw.Length && whitespace[raw[a]]) a++;
			raw = raw.Slice(a);
		}

		var escape = escapeByte;
		var e = raw.IndexOf(escape);
		if (e < 0) return trim ? TrimWhitespace(raw) : raw;
		var output = RentScratch(raw.Length);
		raw.Slice(0, e).CopyTo(output);
		var d = e;
		var kept = 0;   // the output up to here ends with an escaped byte, which trimming keeps
		var i = e;
		while (true)
		{
			// raw[i] is an escape character.
			if (i + 1 == raw.Length)
			{
				output[d++] = escape;

				break;
			}

			output[d++] = raw[i + 1];
			kept = d;
			i += 2;
			var next = raw.Slice(i).IndexOf(escape);
			var run = next < 0 ? raw.Length - i : next;
			raw.Slice(i, run).CopyTo(output.Slice(d));
			d += run;
			i += run;
			if (next < 0) break;
		}

		if (trim)
		{
			while (d > kept && whitespace[output[d - 1]]) d--;
		}

		scratchPos += d;

		return output.Slice(0, d);
	}

	private ReadOnlySpan<byte> TrimWhitespace(ReadOnlySpan<byte> s) => customWhitespace ? TrimWith(s, whitespace) : TrimSpacesAndTabs(s);

	/// <summary>Trims the bytes that <paramref name="whitespace"/> marks (<see cref="CsvReaderOptions.WhitespaceCharacters"/>).</summary>
	private static ReadOnlySpan<byte> TrimWith(ReadOnlySpan<byte> s, bool[] whitespace)
	{
		int a = 0, b = s.Length;
		while (a < b && whitespace[s[a]])
		{
			a++;
		}

		while (b > a && whitespace[s[b - 1]])
		{
			b--;
		}

		return s.Slice(a, b - a);
	}

	private static ReadOnlySpan<byte> TrimSpacesAndTabs(ReadOnlySpan<byte> s)
	{
		int a = 0, b = s.Length;
#if NET
		// Most fields have no whitespace at either end; padded ones (fixed width) can have a lot, searched with vectors.
		if (b > 0 && (s[0] == (byte)' ' || s[0] == (byte)'\t'))
		{
			a = s.IndexOfAnyExcept((byte)' ', (byte)'\t');
			if (a < 0) return default;
		}

		if (b > a && (s[b - 1] == (byte)' ' || s[b - 1] == (byte)'\t')) b = s.LastIndexOfAnyExcept((byte)' ', (byte)'\t') + 1;
#else
		while (a < b && (s[a] == (byte)' ' || s[a] == (byte)'\t')) a++;
		while (b > a && (s[b - 1] == (byte)' ' || s[b - 1] == (byte)'\t')) b--;
#endif

		return s.Slice(a, b - a);
	}

	/// <summary>Unescapes a field that starts with a quote.</summary>
	private ReadOnlySpan<byte> Unescape(ReadOnlySpan<byte> raw)
	{
		// Common case: "value" with no quotes inside, which needs no copy.
		var n = raw.Length;
		if (n >= 2 && raw[n - 1] == quoteByte)
		{
			var middle = raw.Slice(1, n - 2);
			if (!ContainsByte(middle, quoteByte)) return middle;
		}

		var inner = raw.Slice(1);
		var q = inner.IndexOf(quoteByte);
		if (q < 0) return inner;                               // never closed: runs to the end
		if (q == inner.Length - 1) return inner.Slice(0, q);   // "value": no copy needed

		return UnescapeSlow(inner, q);
	}

	/// <summary>
	/// Like <c>span.Contains(value)</c>, but without a loop for the short spans typical of fields: overlapping
	/// loads cover any length up to 64 bytes.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool ContainsByte(ReadOnlySpan<byte> span, byte value)
	{
		var len = span.Length;
		ref var p = ref MemoryMarshal.GetReference(span);
#if NET
		if (len >= 16)
		{
			if (len > 64) return span.Contains(value);
			var v = Vector128.Create(value);
			var eq = Vector128.Equals(Vector128.LoadUnsafe(ref p), v) |
								 Vector128.Equals(Vector128.LoadUnsafe(ref p, (nuint)(len - 16)), v);
			if (len > 32)
			{
				eq |= Vector128.Equals(Vector128.LoadUnsafe(ref p, 16), v);
				if (len > 48) eq |= Vector128.Equals(Vector128.LoadUnsafe(ref p, 32), v);
			}

			return eq != Vector128<byte>.Zero;
		}
#else
		if (len >= 16)
		{
			if (len > 64) return span.Contains(value);
			// 8-byte words, the last one overlapping the one before.
			var pattern = Portable.Repeat(value);
			for (var i = 0; i < len - 8; i += 8)
			{
				if (Portable.MatchBytes(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, i)), pattern) != 0) return true;
			}

			return Portable.MatchBytes(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, len - 8)), pattern) != 0;
		}
#endif

		const ulong Ones = 0x0101_0101_0101_0101UL;
		const ulong Highs = 0x8080_8080_8080_8080UL;
		if (len >= 8)
		{
			// Two overlapping 8-byte words; (x - 1) & ~x has a high bit set in some byte iff x has a zero byte.
			var pattern = Ones * value;
			var a = Unsafe.ReadUnaligned<ulong>(ref p) ^ pattern;
			var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, len - 8)) ^ pattern;

			return ((((a - Ones) & ~a) | ((b - Ones) & ~b)) & Highs) != 0;
		}

		for (var i = 0; i < len; i++)
		{
			if (Unsafe.Add(ref p, i) == value) return true;
		}

		return false;
	}

	/// <summary>
	/// Unescapes the field content after the opening quote, whose first quote is at <paramref name="q"/>:
	/// <c>""</c> becomes <c>"</c>; text after the closing quote (malformed) is kept.
	/// </summary>
	private ReadOnlySpan<byte> UnescapeSlow(ReadOnlySpan<byte> inner, int q)
	{
		var quote = quoteByte;
		var n = inner.Length;
		var output = RentScratch(n);
		ref var src = ref MemoryMarshal.GetReference(inner);
		ref var dst = ref MemoryMarshal.GetReference(output);
		inner.Slice(0, q).CopyTo(output);
		// Unescaping only shrinks the text, so d <= i: a 16-byte store at d never passes the end when the
		// 16-byte load at i does not.
		int i = q, d = q;
#if NET
		var quotes = Vector128.Create(quote);
#else
		var quotes = Portable.Repeat(quote);
#endif
		while (true)
		{
			// inner[i] is a quote.
			i++;
			if (i >= n || Unsafe.Add(ref src, (nint)(uint)i) != quote)
			{
				// Closing quote. Anything after it is malformed; keep it as text.
				inner.Slice(i).CopyTo(output.Slice(d));
				d += n - i;
				break;
			}

			Unsafe.Add(ref dst, (nint)(uint)d++) = quote;   // "" is an escaped quote
			i++;
#if NET
			// Copy up to the next quote, 16 bytes at a time.
			while (i <= n - 16)
			{
				var v = Vector128.LoadUnsafe(ref src, (nuint)(uint)i);
				v.StoreUnsafe(ref dst, (nuint)(uint)d);
				var m = Vector128.Equals(v, quotes).ExtractMostSignificantBits();
				if (m != 0)
				{
					var k = BitOperations.TrailingZeroCount(m);
					i += k;
					d += k;
					goto NextQuote;
				}

				i += 16;
				d += 16;
			}
#else
			// Copy up to the next quote, 8 bytes at a time.
			while (i <= n - 8)
			{
				var v = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, (nint)(uint)i));
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, (nint)(uint)d), v);
				var m = Portable.MatchBytes(BitConverter.IsLittleEndian ? v : BinaryPrimitives.ReverseEndianness(v), quotes);
				if (m != 0)
				{
					var k = BitOperations.TrailingZeroCount(m) >> 3;
					i += k;
					d += k;
					goto NextQuote;
				}

				i += 8;
				d += 8;
			}
#endif

			while (i < n && Unsafe.Add(ref src, (nint)(uint)i) != quote)
			{
				Unsafe.Add(ref dst, (nint)(uint)d++) = Unsafe.Add(ref src, (nint)(uint)i++);
			}

			if (i == n) break;   // never closed: runs to the end
		NextQuote:;
		}

		scratchPos += d;

		return output.Slice(0, d);
	}

	private Span<byte> RentScratch(int length)
	{
		if (scratch.Length - scratchPos < length)
		{
			// Spans handed out earlier keep the old array alive, so just start a new one.
			scratch = new byte[Math.Max(Math.Max(scratch.Length * 2, length), 256)];
			scratchPos = 0;
		}

		return scratch.AsSpan(scratchPos, length);
	}

	internal string MakeString(ReadOnlySpan<byte> utf8, int column)
	{
		if (utf8.Length <= 1)
		{
			if (utf8.Length == 0) return string.Empty;
			var b = utf8[0];
			if (b < 128) return asciiStrings[b];
		}

		return pool is null ? Utf8ToString(utf8) : pool.Get(column, utf8, recordNumber);
	}

	internal static string Utf8ToString(ReadOnlySpan<byte> utf8) => Utf8String.Create(utf8);

#if NET
	internal T ParseSpan<T>(ReadOnlySpan<byte> s, int column) where T : ISpanParsable<T> => ParseSpan<T>(s, column, provider);

	internal T ParseSpan<T>(ReadOnlySpan<byte> s, int column, IFormatProvider provider) where T : ISpanParsable<T>
	{
		if (typeof(T) == typeof(int)) { var v = CsvParse.Int32(s, provider, hideValues); return Unsafe.As<int, T>(ref v); }
		if (typeof(T) == typeof(long)) { var v = CsvParse.Int64(s, provider, hideValues); return Unsafe.As<long, T>(ref v); }
		if (typeof(T) == typeof(double)) { var v = CsvParse.Double(s, provider, hideValues); return Unsafe.As<double, T>(ref v); }
		if (typeof(T) == typeof(float)) { var v = CsvParse.Single(s, provider, hideValues); return Unsafe.As<float, T>(ref v); }
		if (typeof(T) == typeof(decimal)) { var v = CsvParse.Decimal(s, provider, hideValues); return Unsafe.As<decimal, T>(ref v); }
		if (typeof(T) == typeof(bool)) { var v = ParseBoolean(s); return Unsafe.As<bool, T>(ref v); }
		if (typeof(T) == typeof(short) || typeof(T) == typeof(byte) || typeof(T) == typeof(uint) || typeof(T) == typeof(ulong))
		{
			// The BCL parsers of UTF-8, with the value in their messages unless it is hidden.
			return hideValues ? CsvParse.Hide(s, provider, static (x, p) => ParseUtf8<T>(x, p), typeof(T).Name) : ParseUtf8<T>(s, provider);
		}
		if (typeof(T) == typeof(Guid)) { var v = CsvParse.Guid(s, hideValues); return Unsafe.As<Guid, T>(ref v); }
		if (typeof(T) == typeof(DateTime)) { var v = CsvParse.DateTime(s, provider, hideValues); return Unsafe.As<DateTime, T>(ref v); }
		if (typeof(T) == typeof(string)) { var v = MakeString(s, column); return Unsafe.As<string, T>(ref v); }

		// The BCL parsers put the value in their messages: without values, their exceptions are replaced.
		return hideValues ? CsvParse.Hide(s, provider, static (x, p) => ParseViaChars<T>(x, p), typeof(T).Name) : ParseViaChars<T>(s, provider);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static T ParseUtf8<T>(ReadOnlySpan<byte> s, IFormatProvider provider)
	{
		if (typeof(T) == typeof(short)) { var v = short.Parse(s, NumberStyles.Integer, provider); return Unsafe.As<short, T>(ref v); }
		if (typeof(T) == typeof(byte)) { var v = byte.Parse(s, NumberStyles.Integer, provider); return Unsafe.As<byte, T>(ref v); }
		if (typeof(T) == typeof(uint)) { var v = uint.Parse(s, NumberStyles.Integer, provider); return Unsafe.As<uint, T>(ref v); }

		var u = ulong.Parse(s, NumberStyles.Integer, provider);

		return Unsafe.As<ulong, T>(ref u);
	}

	private static T ParseViaChars<T>(ReadOnlySpan<byte> s, IFormatProvider provider) where T : ISpanParsable<T>
	{
		char[]? rented = null;
		var chars = s.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(s.Length));
		try
		{
			var n = Encoding.UTF8.GetChars(s, chars);

			return T.Parse(chars.Slice(0, n), provider);
		}
		finally
		{
			if (rented is not null) ArrayPool<char>.Shared.Return(rented);
		}
	}

	internal bool TryParseSpan<T>(ReadOnlySpan<byte> s, int column, out T value) where T : ISpanParsable<T>
	{
		if (typeof(T) == typeof(int)) { var ok = CsvParse.TryInt32(s, provider, out var v); value = Unsafe.As<int, T>(ref v); return ok; }
		if (typeof(T) == typeof(long)) { var ok = CsvParse.TryInt64(s, provider, out var v); value = Unsafe.As<long, T>(ref v); return ok; }
		if (typeof(T) == typeof(double)) { var ok = CsvParse.TryDouble(s, provider, out var v); value = Unsafe.As<double, T>(ref v); return ok; }
		if (typeof(T) == typeof(float)) { var ok = CsvParse.TrySingle(s, provider, out var v); value = Unsafe.As<float, T>(ref v); return ok; }
		if (typeof(T) == typeof(decimal)) { var ok = CsvParse.TryDecimal(s, provider, out var v); value = Unsafe.As<decimal, T>(ref v); return ok; }
		if (typeof(T) == typeof(bool)) { var ok = TryParseBoolean(s, out var v); value = Unsafe.As<bool, T>(ref v); return ok; }
		if (typeof(T) == typeof(string)) { var v = MakeString(s, column); value = Unsafe.As<string, T>(ref v); return true; }
		char[]? rented = null;
		var chars = s.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(s.Length));
		try
		{
			var n = Encoding.UTF8.GetChars(s, chars);

			return T.TryParse(chars.Slice(0, n), provider, out value!);
		}
		finally
		{
			if (rented is not null) ArrayPool<char>.Shared.Return(rented);
		}
	}
#endif
}
