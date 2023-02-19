﻿// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Options used when doing type conversion.
	/// </summary>
	public class TypeConverterOptions
	{
		private static readonly string[] defaultBooleanTrueValues = { };
		private static readonly string[] defaultBooleanFalseValues = { };
		private static readonly string[] defaultNullValues = { };

		/// <summary>
		/// Gets or sets the culture info.
		/// </summary>
		public CultureInfo? CultureInfo { get; set; }

		/// <summary>
		/// Gets or sets the date time style.
		/// </summary>
		public DateTimeStyles? DateTimeStyle { get; set; }

		/// <summary>
		/// Gets or sets the time span style.
		/// </summary>
		public TimeSpanStyles? TimeSpanStyle { get; set; }

		/// <summary>
		/// Gets or sets the number style.
		/// </summary>
		public NumberStyles? NumberStyles { get; set; }

		/// <summary>
		/// Gets or sets the string format.
		/// </summary>
		public string[]? Formats { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="UriKind"/>.
		/// </summary>
		public UriKind? UriKind { get; set; }

		/// <summary>
		/// Ingore case when parsing enums. Default is false.
		/// </summary>
		public bool? EnumIgnoreCase { get; set; }

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of true.
		/// </summary>
		public List<string> BooleanTrueValues { get; } = new List<string>(defaultBooleanTrueValues);

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of false.
		/// </summary>
		public List<string> BooleanFalseValues { get; } = new List<string>(defaultBooleanFalseValues);

		/// <summary>
		/// Gets the list of values that can be used to represent a null value.
		/// </summary>
		public List<string> NullValues { get; } = new List<string>(defaultNullValues);

		/// <summary>
		/// Merges <see cref="TypeConverterOptions"/> by applying the values of <paramref name="sources"/>
		/// in order on to <paramref name="options"/>.
		/// </summary>
		/// <param name="options">The <see cref="TypeConverterOptions"/> value to mutate</param>
		/// <param name="sources">The sources that will be applied.</param>
		/// <returns>The mutated <paramref name="options"/> object.</returns>
		public static TypeConverterOptions Merge(TypeConverterOptions options, params TypeConverterOptions[] sources)
		{
			if (options is null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			if (sources is null)
			{
				throw new ArgumentNullException(nameof(sources));
			}

			for (var i = 0; i < sources.Length; i++)
			{
				var source = sources[i];

				if (source == null)
				{
					continue;
				}

				if (source.CultureInfo != null)
				{
					options.CultureInfo = source.CultureInfo;
				}

				if (source.DateTimeStyle != null)
				{
					options.DateTimeStyle = source.DateTimeStyle;
				}

				if (source.TimeSpanStyle != null)
				{
					options.TimeSpanStyle = source.TimeSpanStyle;
				}

				if (source.NumberStyles != null)
				{
					options.NumberStyles = source.NumberStyles;
				}

				if (source.Formats != null)
				{
					options.Formats = source.Formats;
				}

				if (source.UriKind != null)
				{
					options.UriKind = source.UriKind;
				}

				if (source.EnumIgnoreCase != null)
				{
					options.EnumIgnoreCase = source.EnumIgnoreCase;
				}

				// Only change the values if they are different than the defaults.
				// This means there were explicit changes made to the options.

				if (!defaultBooleanTrueValues.SequenceEqual(source.BooleanTrueValues))
				{
					options.BooleanTrueValues.Clear();
					options.BooleanTrueValues.AddRange(source.BooleanTrueValues);
				}

				if (!defaultBooleanFalseValues.SequenceEqual(source.BooleanFalseValues))
				{
					options.BooleanFalseValues.Clear();
					options.BooleanFalseValues.AddRange(source.BooleanFalseValues);
				}

				if (!defaultNullValues.SequenceEqual(source.NullValues))
				{
					options.NullValues.Clear();
					options.NullValues.AddRange(source.NullValues);
				}
			}

			return options;
		}
	}
}
