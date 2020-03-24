// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="Nullable{T}"/> to and from a <see cref="string"/>.
	/// </summary>
	public class NullableConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Gets the type of the nullable.
		/// </summary>
		/// <value>
		/// The type of the nullable.
		/// </value>
		public Type NullableType { get; private set; }

		/// <summary>
		/// Gets the underlying type of the nullable.
		/// </summary>
		/// <value>
		/// The underlying type.
		/// </value>
		public Type UnderlyingType { get; private set; }

		/// <summary>
		/// Gets the type converter for the underlying type.
		/// </summary>
		/// <value>
		/// The type converter.
		/// </value>
		public ITypeConverter UnderlyingTypeConverter { get; private set; }

		/// <summary>
		/// Creates a new <see cref="NullableConverter"/> for the given <see cref="Nullable{T}"/> <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The nullable type.</param>
		/// <param name="typeConverterFactory">The type converter factory.</param>
		/// <exception cref="System.ArgumentException">type is not a nullable type.</exception>
		public NullableConverter(Type type, TypeConverterCache typeConverterFactory)
		{
			NullableType = type;
			UnderlyingType = Nullable.GetUnderlyingType(type);
			if (UnderlyingType == null)
			{
				throw new ArgumentException("type is not a nullable type.");
			}

			UnderlyingTypeConverter = typeConverterFactory.GetConverter(UnderlyingType);
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
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}

			foreach (var nullValue in memberMapData.TypeConverterOptions.NullValues)
			{
				if (text == nullValue)
				{
					return null;
				}
			}

			return UnderlyingTypeConverter.ConvertFromString(text, row, memberMapData);
		}

		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row"></param>
		/// <param name="memberMapData"></param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
		{
			return UnderlyingTypeConverter.ConvertToString(value, row, memberMapData);
		}
	}
}
