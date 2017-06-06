// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Globalization;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Throws an exception when used. This is here so that it's apparent
	/// that there is no support for <see cref="IEnumerable"/> type coversion. A custom
	/// converter will need to be created to have a field convert to and 
	/// from an IEnumerable.
	/// </summary>
	public class EnumerableConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Throws an exception.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="ICsvReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field. " +
			                                     "If you want to do this, create your own ITypeConverter and register " +
												 "it in the TypeConverterFactory by calling AddConverter." );
		}

		/// <summary>
		/// Throws an exception.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row">The <see cref="ICsvWriterRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being written.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString( object value, ICsvWriterRow row, CsvPropertyMapData propertyMapData )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field. " +
												 "If you want to do this, create your own ITypeConverter and register " +
												 "it in the TypeConverterFactory by calling AddConverter." );
		}
	}
}
