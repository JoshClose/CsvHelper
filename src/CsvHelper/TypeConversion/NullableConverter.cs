﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Globalization;
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
		/// <exception cref="System.ArgumentException">type is not a nullable type.</exception>
		public NullableConverter( Type type )
		{
			NullableType = type;
			UnderlyingType = Nullable.GetUnderlyingType( type );
			if( UnderlyingType == null )
			{
				throw new ArgumentException( "type is not a nullable type." );
			}

			UnderlyingTypeConverter = TypeConverterFactory.GetConverter( UnderlyingType );
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="ICsvReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData )
		{
			if( string.IsNullOrEmpty( text ) )
			{
				return null;
			}

			foreach( var nullValue in propertyMapData.TypeConverterOptions.NullValues )
			{
				if( text == nullValue )
				{
					return null;
				}
			}

			return UnderlyingTypeConverter.ConvertFromString( text, row, propertyMapData );
		}

		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row"></param>
		/// <param name="propertyMapData"></param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString( object value, ICsvWriterRow row, CsvPropertyMapData propertyMapData )
		{
			return UnderlyingTypeConverter.ConvertToString( value, row, propertyMapData );
		}
	}
}
