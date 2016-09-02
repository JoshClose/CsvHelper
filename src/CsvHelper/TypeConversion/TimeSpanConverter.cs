// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Globalization;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="TimeSpan"/> to and from a <see cref="string"/>.
	/// </summary>
	public class TimeSpanConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="ICsvReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData )
		{
			var formatProvider = (IFormatProvider)propertyMapData.TypeConverterOptions.CultureInfo;

			TimeSpan span;

#if !NET_2_0 && !NET_3_5 && !PCL
			var timeSpanStyle = propertyMapData.TypeConverterOptions.TimeSpanStyle ?? TimeSpanStyles.None;
			if( !string.IsNullOrEmpty( propertyMapData.TypeConverterOptions.Format ) && TimeSpan.TryParseExact( text, propertyMapData.TypeConverterOptions.Format, formatProvider, timeSpanStyle, out span ) )
			{
				return span;
			}

			if( string.IsNullOrEmpty( propertyMapData.TypeConverterOptions.Format ) && TimeSpan.TryParse( text, formatProvider, out span ) )
			{
				return span;
			}
#else
			if( TimeSpan.TryParse( text, out span ) )
			{
				return span;
			}
#endif

			return base.ConvertFromString( text, row, propertyMapData );
		}
	}
}
