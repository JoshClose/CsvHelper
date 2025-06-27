// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Reflection;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="Enum"/> to and from a <see cref="string"/>.
/// </summary>
public class EnumConverter : DefaultTypeConverter
{
	private readonly Type type;
	private readonly Dictionary<string, string> enumNamesByAttributeNames = new Dictionary<string, string>();
	private readonly Dictionary<string, string> enumNamesByAttributeNamesIgnoreCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<object, string> attributeNamesByEnumValues = new Dictionary<object, string>();

	// enumNamesByAttributeNames
	// enumNamesByAttributeNamesIgnoreCase
	// [Name("Foo")]:One

	// attributeNamesByEnumValues
	// 1:[Name("Foo")]

	/// <summary>
	/// Creates a new <see cref="EnumConverter"/> for the given <see cref="Enum"/> <see cref="System.Type"/>.
	/// </summary>
	/// <param name="type">The type of the Enum.</param>
	public EnumConverter(Type type)
	{
		if (!typeof(Enum).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			throw new ArgumentException($"'{type.FullName}' is not an Enum.");
		}

		this.type = type;

		foreach (var value in Enum.GetValues(type))
		{
			var enumName = Enum.GetName(type, value) ?? string.Empty;

			var nameAttribute = type.GetField(enumName)?.GetCustomAttribute<NameAttribute>();
			if (nameAttribute != null && nameAttribute.Names.Length > 0)
			{
				foreach (var attributeName in nameAttribute.Names)
				{
					if (!enumNamesByAttributeNames.ContainsKey(attributeName))
					{
						enumNamesByAttributeNames.Add(attributeName, enumName);
					}

					if (!enumNamesByAttributeNamesIgnoreCase.ContainsKey(attributeName))
					{
						enumNamesByAttributeNamesIgnoreCase.Add(attributeName, enumName);
					}

					if (!attributeNamesByEnumValues.ContainsKey(value))
					{
						attributeNamesByEnumValues.Add(value, attributeName);
					}
				}
			}
		}
	}

	/// <inheritdoc/>
	public override object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		var ignoreCase = memberMapData.TypeConverterOptions.EnumIgnoreCase ?? false;

		text = text.Trim();

		if (text.Length > 0)
		{
			var dict = ignoreCase
				? enumNamesByAttributeNamesIgnoreCase
				: enumNamesByAttributeNames;
			if (dict.TryGetValue(text.ToString(), out var name))
			{
				return Enum.Parse(type, name);
			}
		}

#if NETSTANDARD2_1_OR_GREATER || NET8_0_OR_GREATER
		if (Enum.TryParse(type,
#if NET8_0_OR_GREATER
			text
#else
			text.ToString()
#endif
			, ignoreCase, out var value))
		{
			return value;
		}
		else
		{
			return base.ConvertFromString(text, row, memberMapData);
		}
#else
		try
		{
			return Enum.Parse(type, text.ToString(), ignoreCase);
		}
		catch
		{
			return base.ConvertFromString(text, row, memberMapData);
		}
#endif
	}

	/// <inheritdoc/>
	public override ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		if (value != null && attributeNamesByEnumValues.TryGetValue(value, out var name))
		{
			return name.AsSpan();
		}

		return base.ConvertToString(value, row, memberMapData);
	}
}
