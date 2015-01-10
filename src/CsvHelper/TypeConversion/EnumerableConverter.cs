// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
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
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field. " +
			                                     "If you want to do this, create your own ITypeConverter and register " +
												 "it in the TypeConverterFactory by calling AddConverter." );
		}

		/// <summary>
		/// Throws an exception.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="value">The object to convert to a string.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString( TypeConverterOptions options, object value )
		{
			throw new CsvTypeConverterException( "Converting IEnumerable types is not supported for a single field. " +
												 "If you want to do this, create your own ITypeConverter and register " +
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
			return true;
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
			return true;
		}
	}
}
