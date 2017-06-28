// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="DateTime"/> to and from a <see cref="string"/>.
	/// </summary>
	public class DateTimeConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, IReaderRow row, CsvPropertyMapData propertyMapData )
		{
			if( text == null )
			{
				return base.ConvertFromString( null, row, propertyMapData );
			}

			var formatProvider = (IFormatProvider)propertyMapData.TypeConverterOptions.CultureInfo.GetFormat( typeof( DateTimeFormatInfo ) ) ?? propertyMapData.TypeConverterOptions.CultureInfo;
			var dateTimeStyle = propertyMapData.TypeConverterOptions.DateTimeStyle ?? DateTimeStyles.None;

			return propertyMapData.TypeConverterOptions.Formats == null || propertyMapData.TypeConverterOptions.Formats.Length == 0
				? DateTime.Parse( text, formatProvider, dateTimeStyle )
				: DateTime.ParseExact( text, propertyMapData.TypeConverterOptions.Formats, formatProvider, dateTimeStyle );
		}
	}
}
