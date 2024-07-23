// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="object"/> to and from a <see cref="string"/>.
/// </summary>
public class DefaultTypeConverter : ITypeConverter
{
	/// <inheritdoc/>
	public virtual object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
	{
		// Conversion has failed
		// Check if a default value should be returned
		if (!memberMapData.UseDefaultOnConversionFailure || !memberMapData.IsDefaultSet)
		{
			throw CreateTypeConverterException(text, row, memberMapData);
		}

		// Try to get a valid default value from the memberMapData
		var memberType = memberMapData.Member!.MemberType();

		if (memberMapData.Default is null)
		{
			if (TypeAllowsNull(memberType))
			{
				return null;
			}
		}
		else if (memberType.IsAssignableFrom(memberMapData.Default.GetType()))
		{
			return memberMapData.Default;
		}

		// No valid default value was configured, throw a TypeConverterException
		throw CreateTypeConverterException(text, row, memberMapData);
	}

	/// <inheritdoc/>
	public virtual string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		if (value == null)
		{
			if (memberMapData.TypeConverterOptions.NullValues.Count > 0)
			{
				return memberMapData.TypeConverterOptions.NullValues.First();
			}

			return string.Empty;
		}

		if (value is IFormattable formattable)
		{
			var format = memberMapData.TypeConverterOptions.Formats?.FirstOrDefault();
			return formattable.ToString(format, memberMapData.TypeConverterOptions.CultureInfo);
		}

		return value?.ToString() ?? string.Empty;
	}

	private TypeConverterException CreateTypeConverterException(string? text, IReaderRow row, MemberMapData memberMapData)
	{
		if (!row.Configuration.ExceptionMessagesContainRawData)
		{
			text = $"Hidden because {nameof(IParserConfiguration.ExceptionMessagesContainRawData)} is false.";
		}

		text ??= string.Empty;

		var message =
			$"The conversion cannot be performed.{Environment.NewLine}" +
			$"    Text: '{text}'{Environment.NewLine}" +
			$"    MemberName: {memberMapData.Member?.Name}{Environment.NewLine}" +
			$"    MemberType: {memberMapData.Member?.MemberType().FullName}{Environment.NewLine}" +
			$"    TypeConverter: '{memberMapData.TypeConverter?.GetType().FullName}'";

		return new TypeConverterException(this, memberMapData, text, row.Context, message);
	}

	private static bool TypeAllowsNull(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
	}
}
