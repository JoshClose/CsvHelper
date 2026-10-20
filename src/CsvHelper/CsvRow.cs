using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper;

/// <summary>A view of the current record of a <see cref="CsvReader"/>. Valid until the next read.</summary>
public readonly ref struct CsvRow
{
	private readonly CsvReader reader;

	internal CsvRow(CsvReader reader) => this.reader = reader;

	/// <summary>The number of fields in the record.</summary>
	public int FieldCount => reader.FieldCount;

	/// <summary>The 1-based number of the data record.</summary>
	public long RowNumber => reader.RowNumber;

	/// <summary>Gets field <paramref name="index"/>. See <see cref="CsvReader.GetSpan(int)"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public CsvField this[int index] => new(reader, reader.GetSpan(index), index);

	/// <summary>Gets the field with the given header name.</summary>
	/// <param name="name">The header name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public CsvField this[string name] => reader[name];

	/// <summary>The raw bytes of the record (UTF-8), without the line break.</summary>
	public ReadOnlySpan<byte> RawRecord => reader.RawRecord;

	/// <summary>Gets the value of field <paramref name="index"/> as UTF-8 bytes. See <see cref="CsvReader.GetSpan(int)"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field as UTF-8 bytes; empty for an empty field. Valid until the next read.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public ReadOnlySpan<byte> GetSpan(int index) => reader.GetSpan(index);

	/// <summary>Gets field <paramref name="index"/> as a string. See <see cref="CsvReader.GetString(int)"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field; <see cref="string.Empty"/> for an empty field.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public string GetString(int index) => reader.GetString(index);

#if NET
	/// <summary>Parses field <paramref name="index"/> as <typeparamref name="T"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
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
	public T Parse<T>(int index) where T : ISpanParsable<T> => reader.Parse<T>(index);
#endif

	/// <summary>
	/// Parses field <paramref name="index"/> with <paramref name="parser"/> (see
	/// <see cref="CsvReader.Parse{T}(int, Func{string, IFormatProvider, T})"/>).
	/// </summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> returns.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="parser">Parses the value of the field with the reader's format provider.</param>
	/// <returns>The value that <paramref name="parser"/> returns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public T Parse<T>(int index, Func<string, IFormatProvider, T> parser) => reader.Parse(index, parser);

	/// <summary>Copies all fields of the record into a new string array.</summary>
	/// <returns>The values of the fields, one element per field.</returns>
	public string[] ToArray()
	{
		var values = new string[reader.FieldCount];
		for (var i = 0; i < values.Length; i++) values[i] = reader.GetString(i);

		return values;
	}

	/// <summary>
	/// Copies the record into a new dictionary keyed by column name: the header names (each once, with its first column),
	/// or <c>Field1</c>, <c>Field2</c> and so on without a header. A value is <see langword="null"/> when it is one of
	/// <see cref="CsvReaderOptions.NullValues"/>, as for <see cref="CsvReader.GetStringOrNull(int)"/>.
	/// </summary>
	/// <returns>The values of the record by name, in column order.</returns>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has fewer fields than the header and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public Dictionary<string, string?> ToDictionary()
	{
		var headers = reader.Headers;
		var count = headers.Count > 0 ? headers.Count : reader.FieldCount;
		var dictionary = new Dictionary<string, string?>(count);
		for (var i = 0; i < count; i++)
		{
			var name = headers.Count > 0 ? headers[i] : "Field" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
			if (!dictionary.ContainsKey(name))
			{
				dictionary.Add(name, reader.GetStringOrNull(i));
			}
		}

		return dictionary;
	}

	/// <summary>Gets the record as it appears in the input, without the line break.</summary>
	/// <returns>The raw record decoded from UTF-8, quotes and escapes included.</returns>
	public override string ToString() => Encoding.UTF8.GetString(reader.RawRecord);
}

/// <summary>One field of the current record. The value is already unquoted and unescaped.</summary>
public readonly ref struct CsvField
{
	private readonly CsvReader reader;
	private readonly int index;

	internal CsvField(CsvReader reader, ReadOnlySpan<byte> span, int index)
	{
		this.reader = reader;
		this.index = index;
		Span = span;
	}

	/// <summary>The field value as UTF-8 bytes. Valid until the next read.</summary>
	public ReadOnlySpan<byte> Span { get; }

	/// <summary>The length of the value in bytes.</summary>
	public int Length => Span.Length;

	/// <summary>Whether the value is empty.</summary>
	public bool IsEmpty => Span.IsEmpty;

	/// <summary>Gets the value as a string. See <see cref="CsvReader.GetString(int)"/>.</summary>
	/// <returns>The value; <see cref="string.Empty"/> for an empty field.</returns>
	public override string ToString() => reader.MakeString(Span, index);

#if NET
	/// <summary>Parses the value as <typeparamref name="T"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <typeparam name="T">The type to parse the value as.</typeparam>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The value is not a valid <typeparamref name="T"/>.</exception>
	/// <exception cref="OverflowException">The value is outside the range of <typeparamref name="T"/>.</exception>
	public T Parse<T>() where T : ISpanParsable<T> => reader.ParseSpan<T>(Span, index);

	/// <summary>Tries to parse the value as <typeparamref name="T"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <typeparam name="T">The type to parse the value as.</typeparam>
	/// <param name="value">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
	/// <returns>Whether the value could be parsed.</returns>
	public bool TryParse<T>(out T value) where T : ISpanParsable<T> => reader.TryParseSpan(Span, index, out value);

	/// <summary>
	/// Parses the value as <typeparamref name="T"/>, or returns <see langword="null"/> if it is empty, one of
	/// <see cref="CsvReaderOptions.NullValues"/> or invalid.
	/// </summary>
	/// <typeparam name="T">The type to parse the value as.</typeparam>
	/// <returns>The parsed value, or <see langword="null"/> when the value is null or not a valid <typeparamref name="T"/>.</returns>
	public T? ParseOrNull<T>() where T : struct, ISpanParsable<T> =>
		!reader.IsNullSpan(Span) && reader.TryParseSpan(Span, index, out T value) ? value : null;
#endif

	/// <summary>Parses the value with <paramref name="parser"/>, which gets it as a string and the reader's format provider.</summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> returns.</typeparam>
	/// <param name="parser">Parses the value with the given format provider.</param>
	/// <returns>The value that <paramref name="parser"/> returns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
	public T Parse<T>(Func<string, IFormatProvider, T> parser) => reader.ParseWith(Span, index, parser);

	/// <summary>Tries to parse the value with <paramref name="parser"/>, which gets it as a string and the reader's format provider.</summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> parses.</typeparam>
	/// <param name="parser">Tries to parse the value with the given format provider.</param>
	/// <param name="value">The value that <paramref name="parser"/> produced.</param>
	/// <returns>The result of <paramref name="parser"/>: whether the value could be parsed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
	public bool TryParse<T>(CsvTryParser<T> parser, out T value) => reader.TryParseWith(Span, index, parser, out value);

	/// <summary>
	/// Parses the value with <paramref name="parser"/>, or returns <see langword="null"/> if it is empty, one of
	/// <see cref="CsvReaderOptions.NullValues"/>, or <paramref name="parser"/> fails.
	/// </summary>
	/// <typeparam name="T">The type that <paramref name="parser"/> parses.</typeparam>
	/// <param name="parser">Tries to parse the value with the reader's format provider. It is not called for a null value.</param>
	/// <returns>The parsed value, or <see langword="null"/> when the value is null or <paramref name="parser"/> fails.</returns>
	/// <exception cref="ArgumentNullException">The value is not null and <paramref name="parser"/> is <see langword="null"/>.</exception>
	public T? ParseOrNull<T>(CsvTryParser<T> parser) where T : struct =>
		!reader.IsNullSpan(Span) && reader.TryParseWith(Span, index, parser, out var value) ? value : null;
}
