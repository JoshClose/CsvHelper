// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a DateTime to and from a string.
	/// </summary>
	public class DateTimeConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			try
			{
			if( text == null )
			{
				return base.ConvertFromString( options, null );
			}

			if( text.Trim().Length == 0 )
			{
				return DateTime.MinValue;
			}

			var formatProvider = (IFormatProvider)options.CultureInfo.GetFormat( typeof( DateTimeFormatInfo ) ) ?? options.CultureInfo;
			var dateTimeStyle = options.DateTimeStyle ?? DateTimeStyles.None;

			return string.IsNullOrEmpty( options.Format )
				? DateTime.Parse( text, formatProvider, dateTimeStyle )
				: DateTime.ParseExact( text, options.Format, formatProvider, dateTimeStyle );
			}
			catch (Exception exception)
			{
				string errormessage = string.Format("Exception while parsing value [{0}] to a date. Using Culture [{1}].", text, options.CultureInfo);
				if (!string.IsNullOrEmpty(options.Format))
				{
					errormessage += string.Format(" Using option format [{0}]", options.Format);
				}

				throw new CsvTypeConverterException(errormessage, exception);
			}
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
