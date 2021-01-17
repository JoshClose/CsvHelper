// Copyright 2009-2021 Josh Close
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
		private readonly Dictionary<string, object> nameValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<object, string> valueNames = new Dictionary<object, string>();

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
				var name = Enum.GetName(type, value);

				var nameAttribute = type.GetField(name).GetCustomAttribute<NameAttribute>();
				if (nameAttribute != null && nameAttribute.Names.Length > 0)
				{
					foreach (var n in nameAttribute.Names)
					{
						nameValues.Add(n, value);
					}
				}

				nameValues.Add(name, value);
				valueNames.Add(value, nameValues.First().Key);
			}
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
		{
			if (text != null && nameValues.ContainsKey(text))
			{
				return nameValues[text];
			}

#if NET45 || NET47 || NETSTANDARD2_0
			try
			{
				return Enum.Parse(type, text);
			}
			catch
			{
				return base.ConvertFromString(text, row, memberMapData);
			}
#else
			if (Enum.TryParse(type, text, out var value))
			{
				return value;
			}
			else
			{
				return base.ConvertFromString(text, row, memberMapData);
			}
#endif
		}

		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
		{
			if (nameValues.ContainsValue(value))
			{
				return nameValues.First(pair => Equals(pair.Value, value)).Key;
			}

			return base.ConvertToString(value, row, memberMapData);
		}
	}
}
