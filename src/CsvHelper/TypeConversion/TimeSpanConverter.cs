// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a TimeSpan to and from a string.
	/// </summary>
	public class TimeSpanConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertFrom( Type type )
		{
			return type == typeof( string );
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			var formatProvider = (IFormatProvider)options.CultureInfo;

			TimeSpan span;

#if !NET_2_0 && !NET_3_5 && !PCL
			var timeSpanStyle = options.TimeSpanStyle ?? TimeSpanStyles.None;
			if( !string.IsNullOrEmpty( options.Format ) && TimeSpan.TryParseExact( text, options.Format, formatProvider, timeSpanStyle, out span ) )
			{
				return span;
			}

			if( string.IsNullOrEmpty( options.Format ) && TimeSpan.TryParse( text, formatProvider, out span ) )
			{
				return span;
			}
#else
			if( TimeSpan.TryParse( text, out span ) )
			{
				return span;
			}
#endif

			return base.ConvertFromString( options, text );
		}
	}
}
