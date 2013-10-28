// Copyright 2009-2013 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a Boolean to and from a string.
	/// </summary>
	public class BooleanConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			bool b;
			if( bool.TryParse( text, out b ) )
			{
				return b;
			}

			short sh;
			if( short.TryParse( text, out sh ) )
			{
				if( sh == 0 )
				{
					return false;
				}
				if( sh == 1 )
				{
					return true;
				}
			}

			var t = ( text ?? string.Empty ).Trim();
			foreach( var trueValue in options.BooleanTrueValues )
			{
				if( options.CultureInfo.CompareInfo.Compare( trueValue, t, CompareOptions.IgnoreCase ) == 0 )
				{
					return true;
				}
			}

			foreach( var falseValue in options.BooleanFalseValues )
			{
				if( options.CultureInfo.CompareInfo.Compare( falseValue, t, CompareOptions.IgnoreCase ) == 0 )
				{
					return false;
				}
			}

			return base.ConvertFromString( options, text );
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
			// We only care about strings.
			return type == typeof( string );
		}
	}
}
