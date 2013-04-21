// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an object to and from a string.
	/// </summary>
	public class DefaultTypeConverter : ITypeConverter
	{
		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="value">The object to convert to a string.</param>
		/// <returns>The string representation of the object.</returns>
		public virtual string ConvertToString( CultureInfo culture, object value )
		{
			if( value == null )
			{
				return string.Empty;
			}

			if( culture != null && !Equals( culture, CultureInfo.CurrentCulture ) )
			{
				var formattable = value as IFormattable;
				if( formattable != null )
				{
					return formattable.ToString( null, culture );
				}
			}

			return value.ToString();
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public virtual object ConvertFromString( CultureInfo culture, string text )
		{
			throw new CsvTypeConverterException( "The conversion cannot be performed." );
		}

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public virtual bool CanConvertFrom( Type type )
		{
			// The default convert doesn't know how to
			// convert from any type.
			return false;
		}

		/// <summary>
		/// Determines whether this instance [can convert to] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert to] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public virtual bool CanConvertTo( Type type )
		{
			// We only care about strings.
			return type == typeof( string );
		}
	}
}
