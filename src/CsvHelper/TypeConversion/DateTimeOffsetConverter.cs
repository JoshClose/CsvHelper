﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a DateTimeOffset to and from a string.
	/// </summary>
	public class DateTimeOffsetConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			if( text == null )
			{
				return base.ConvertFromString( options, null );
			}

			if( text.Trim().Length == 0 )
			{
				return DateTimeOffset.MinValue;
			}

			var formatProvider = (IFormatProvider)options.CultureInfo.GetFormat( typeof( DateTimeFormatInfo ) ) ?? options.CultureInfo;
			var dateTimeStyle = options.DateTimeStyle ?? DateTimeStyles.None;

			return string.IsNullOrEmpty( options.Format )
				? DateTimeOffset.Parse( text, formatProvider, dateTimeStyle )
				: DateTimeOffset.ParseExact( text, options.Format, formatProvider, dateTimeStyle );
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
