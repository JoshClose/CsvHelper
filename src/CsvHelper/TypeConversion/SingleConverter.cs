// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using CsvHelper.Configuration;
using System;
using System.Linq;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="Single"/> to and from a <see cref="string"/>.
	/// </summary>
	public class SingleConverter : DefaultTypeConverter
	{
		private string defaultFormat;

		/// <summary>
		/// Initializes a new instance of the <see cref="SingleConverter"/> class.
		/// </summary>
		public SingleConverter()
		{
			// Have to use a try/catch here because of a bug in .NET that causes a StackOverflowException when calling ToString("R").
			try
			{
				float.Parse(float.MaxValue.ToString("R"));
				defaultFormat = "R";
			}
			catch
			{
				defaultFormat = "G9";
			}
		}

        /// <summary>
        /// Converts the object to a string.
        /// </summary>
        /// <param name="value">The object to convert to a string.</param>
        /// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
        /// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
        /// <returns>The string representation of the object.</returns>
        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is float d && memberMapData.TypeConverterOptions.Formats?.FirstOrDefault() == null)
            {
                return d.ToString(defaultFormat, memberMapData.TypeConverterOptions.CultureInfo);
            }

            return base.ConvertToString(value, row, memberMapData);
        }

        /// <summary>
        /// Converts the string to an object.
        /// </summary>
        /// <param name="text">The string to convert to an object.</param>
        /// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
        /// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
        /// <returns>The object created from the string.</returns>
        public override object ConvertFromString( string text, IReaderRow row, MemberMapData memberMapData )
		{
			var numberStyle = memberMapData.TypeConverterOptions.NumberStyle ?? NumberStyles.Float;

			if( float.TryParse( text, numberStyle, memberMapData.TypeConverterOptions.CultureInfo, out var f ) )
			{
				return f;
			}

			return base.ConvertFromString( text, row, memberMapData );
		}
	}
}
