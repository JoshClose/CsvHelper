#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CsvHelper;

/// <summary>
/// The overloads of <c>Parse&lt;T&gt;</c>, <c>TryParse&lt;T&gt;</c> and <c>ParseOrNull&lt;T&gt;</c> for enums, whose
/// types are not <c>ISpanParsable&lt;T&gt;</c>: <c>reader.Parse&lt;Status&gt;(2)</c> calls the method of this class.
/// An enum field is read as the generated mappers of <see cref="CsvRecordAttribute"/> types read enum members: the names
/// of the members and their <see cref="CsvEnumNameAttribute"/> names, ignoring ASCII case unless two names of different
/// members differ only in case, then numbers and lists of flags (<c>Read, Write</c>) as <c>Enum.TryParse</c> reads them,
/// ignoring case.
/// </summary>
/// <remarks>
/// On .NET Framework and .NET Standard, where <c>Parse&lt;T&gt;</c> has no constraint, the methods of the reader, its rows
/// and fields, and dynamic records parse enums themselves, the same way, and this class does not exist.
/// </remarks>
public static class CsvEnumExtensions
{
	/// <summary>The members of an enum type that reading it uses: its fields, for their names and <see cref="CsvEnumNameAttribute"/>.</summary>
	internal const DynamicallyAccessedMemberTypes EnumMembers = DynamicallyAccessedMemberTypes.PublicFields;

	/// <summary>Parses field <paramref name="index"/> as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The member of <typeparamref name="TEnum"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">The field is not a value of <typeparamref name="TEnum"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="CsvReader.FieldCount"/> and
	/// <see cref="CsvReaderOptions.MissingField"/> is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvReader reader, int index)
		where TEnum : struct, Enum
	{
		ArgumentNullException.ThrowIfNull(reader);

		return CsvEnum<TEnum>.Parse(reader.GetSpan(index), reader.HideValues);
	}

	/// <summary>Parses the field with the given header name as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="name">The header name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The member of <typeparamref name="TEnum"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a value of <typeparamref name="TEnum"/>.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// The record has no field in that column and <see cref="CsvReaderOptions.MissingField"/> is
	/// <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvReader reader, string name)
		where TEnum : struct, Enum
	{
		ArgumentNullException.ThrowIfNull(reader);

		return reader.Parse<TEnum>(reader.GetFieldIndex(name));
	}

	/// <summary>Tries to parse field <paramref name="index"/> as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="value">The member, or the default of <typeparamref name="TEnum"/> when the field is not a value of it.</param>
	/// <returns>Whether the field could be parsed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="CsvReader.FieldCount"/> and
	/// <see cref="CsvReaderOptions.MissingField"/> is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public static bool TryParse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvReader reader, int index, out TEnum value)
		where TEnum : struct, Enum
	{
		ArgumentNullException.ThrowIfNull(reader);

		return CsvEnum<TEnum>.TryParse(reader.GetSpan(index), out value);
	}

	/// <summary>Parses field <paramref name="index"/> of the row as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="row">The row.</param>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The member of <typeparamref name="TEnum"/>.</returns>
	/// <exception cref="FormatException">The field is not a value of <typeparamref name="TEnum"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="CsvRow.FieldCount"/> and
	/// <see cref="CsvReaderOptions.MissingField"/> is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvRow row, int index)
		where TEnum : struct, Enum
		=> row.Reader.Parse<TEnum>(index);

	/// <summary>Parses the value of the field as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="field">The field.</param>
	/// <returns>The member of <typeparamref name="TEnum"/>.</returns>
	/// <exception cref="FormatException">The value is not a value of <typeparamref name="TEnum"/>.</exception>
	public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvField field)
		where TEnum : struct, Enum
		=> CsvEnum<TEnum>.Parse(field.Span, field.Reader.HideValues);

	/// <summary>Tries to parse the value of the field as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="field">The field.</param>
	/// <param name="value">The member, or the default of <typeparamref name="TEnum"/> when the value is not a value of it.</param>
	/// <returns>Whether the value could be parsed.</returns>
	public static bool TryParse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvField field, out TEnum value)
		where TEnum : struct, Enum
		=> CsvEnum<TEnum>.TryParse(field.Span, out value);

	/// <summary>
	/// Parses the value of the field as the enum <typeparamref name="TEnum"/>, or returns <see langword="null"/> if it is
	/// empty, one of <see cref="CsvReaderOptions.NullValues"/> or not a value of <typeparamref name="TEnum"/>.
	/// </summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="field">The field.</param>
	/// <returns>The member, or <see langword="null"/> when the value is null or not a value of <typeparamref name="TEnum"/>.</returns>
	public static TEnum? ParseOrNull<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvField field)
		where TEnum : struct, Enum
		=> !field.Reader.IsNullSpan(field.Span) && CsvEnum<TEnum>.TryParse(field.Span, out var value) ? value : null;

	/// <summary>Parses the field of the column called <paramref name="name"/> as the enum <typeparamref name="TEnum"/>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="record">The dynamic record.</param>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The member of <typeparamref name="TEnum"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="record"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a value of <typeparamref name="TEnum"/>.</exception>
	/// <exception cref="IndexOutOfRangeException">The record has no field in that column and missing fields throw.</exception>
	public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(this CsvDynamicRecord record, string name)
		where TEnum : struct, Enum
	{
		ArgumentNullException.ThrowIfNull(record);

		return CsvEnum<TEnum>.Parse(record.FieldSpan(name), record.HideValues);
	}
}
#endif
