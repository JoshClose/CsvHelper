// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Throws an exception when used. This is here so that it's apparent
	/// that there is no support for IEnumerable type coversion. A custom
	/// converter will need to be created to have a field convert to and 
	/// from an IEnumerable.
	/// </summary>
	public class EnumerableConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Throws an exception.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( CultureInfo culture, string text )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field." +
			                                     "If you want to do this, create your own ITypeConverter and register" +
												 "it in the TypeConverterFactory by calling AddConverter. If you were" +
			                                     "trying to read a row you may have called GetRecord instead of GetRecords." );
		}

		/// <summary>
		/// Throws an exception.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="value">The object to convert to a string.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString( CultureInfo culture, object value )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field." +
												 "If you want to do this, create your own ITypeConverter and register" +
												 "it in the TypeConverterFactory by calling AddConverter." );
		}

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertFrom( Type type )
		{
			return false;
		}

		/// <summary>
		/// Determines whether this instance [can convert to] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert to] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertTo( Type type )
		{
			return false;
		}
	}
}
