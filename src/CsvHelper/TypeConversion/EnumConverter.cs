// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace CsvHelper.TypeConversion
{
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
		public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
		{
			var ignoreCase = memberMapData.TypeConverterOptions.EnumIgnoreCase ?? false;

			if (text != null)
			{
				var dict = ignoreCase
					? enumNamesByAttributeNamesIgnoreCase
					: enumNamesByAttributeNames;
				if (dict.TryGetValue(text, out var name))
				{
					return Enum.Parse(type, name);
				}
			}

#if NET45 || NET47 || NETSTANDARD2_0
			try
			{
				return Enum.Parse(type, text, ignoreCase);
			}
			catch
			{
				return base.ConvertFromString(text, row, memberMapData);
			}
#else
			if (Enum.TryParse(type, text, ignoreCase, out var value))
			{
				return value;
			}
			else
			{
				return base.ConvertFromString(text, row, memberMapData);
			}
#endif
		}

		/// <inheritdoc/>
		public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
		{
			if (value != null && attributeNamesByEnumValues.TryGetValue(value, out var name))
			{
				return name;
			}

			return base.ConvertToString(value, row, memberMapData);
		}
	}
}
