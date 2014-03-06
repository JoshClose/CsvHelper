// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
#if NET_2_0
using CsvHelper.MissingFrom20;
#else
using System.Linq;
#endif

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Options used when doing type conversion.
	/// </summary>
	public class TypeConverterOptions
	{
		private readonly List<string> booleanTrueValues = new List<string> { "yes", "y" };
		private readonly List<string> booleanFalseValues = new List<string> { "no", "n" };

		/// <summary>
		/// Gets or sets the culture info.
		/// </summary>
		public CultureInfo CultureInfo { get; set; }

		/// <summary>
		/// Gets or sets the date time style.
		/// </summary>
		public DateTimeStyles? DateTimeStyle { get; set; }

#if !NET_2_0 && !NET_3_5
		/// <summary>
		/// Gets or sets the time span style.
		/// </summary>
		public TimeSpanStyles? TimeSpanStyle { get; set; }
#endif

		/// <summary>
		/// Gets or sets the number style.
		/// </summary>
		public NumberStyles? NumberStyle { get; set; }

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of true.
		/// </summary>
		public List<string> BooleanTrueValues
		{
			get { return booleanTrueValues; }
		}

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of false.
		/// </summary>
		public List<string> BooleanFalseValues
		{
			get { return booleanFalseValues; }
		}

		/// <summary>
		/// Gets or sets the string format.
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// Merges TypeConverterOptions by applying the values of sources in order to a
		/// new TypeConverterOptions instance.
		/// </summary>
		/// <param name="sources">The sources that will be applied.</param>
		/// <returns>A new instance of TypeConverterOptions with the source applied to it.</returns>
		public static TypeConverterOptions Merge( params TypeConverterOptions[] sources )
		{
			var options = new TypeConverterOptions();
			foreach( var source in sources )
			{
				if( source == null )
				{
					continue;
				}

				if( source.CultureInfo != null )
				{
					options.CultureInfo = source.CultureInfo;
				}

				if( source.DateTimeStyle != null )
				{
					options.DateTimeStyle = source.DateTimeStyle;
				}

#if !NET_2_0 && !NET_3_5
				if( source.TimeSpanStyle != null )
				{
					options.TimeSpanStyle = source.TimeSpanStyle;
				}
#endif

				if( source.NumberStyle != null )
				{
					options.NumberStyle = source.NumberStyle;
				}

				if( source.Format != null )
				{
					options.Format = source.Format;
				}

#if NET_2_0
				if( !EnumerableHelper.SequenceEqual( options.booleanTrueValues, source.booleanTrueValues ) )
#else
				if( !options.booleanTrueValues.SequenceEqual( source.booleanTrueValues ) )
#endif
				{
					options.booleanTrueValues.Clear();
					options.booleanTrueValues.AddRange( source.booleanTrueValues );
				}

#if NET_2_0
				if( !EnumerableHelper.SequenceEqual( options.booleanFalseValues, source.booleanFalseValues ) )
#else
				if( !options.booleanFalseValues.SequenceEqual( source.booleanFalseValues ) )
#endif
				{
					options.booleanFalseValues.Clear();
					options.booleanFalseValues.AddRange( source.booleanFalseValues );
				}
			}

			return options;
		}
	}
}
