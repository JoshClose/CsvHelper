// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Sets type converter options on a member map.
	/// </summary>
	public class MapTypeConverterOption
	{
		private readonly MemberMap memberMap;

		/// <summary>
		/// Creates a new instance using the given <see cref="MemberMap"/>.
		/// </summary>
		/// <param name="memberMap">The member map the options are being applied to.</param>
		public MapTypeConverterOption(MemberMap memberMap)
		{
			this.memberMap = memberMap;
		}

		/// <summary>
		/// The <see cref="CultureInfo"/> used when type converting.
		/// This will override the global <see cref="CsvConfiguration.CultureInfo"/>
		/// setting.
		/// </summary>
		/// <param name="cultureInfo">The culture info.</param>
		public virtual MemberMap CultureInfo(CultureInfo cultureInfo)
		{
			memberMap.Data.TypeConverterOptions.CultureInfo = cultureInfo;

			return memberMap;
		}

		/// <summary>
		/// The <see cref="DateTimeStyles"/> to use when type converting.
		/// This is used when doing any <see cref="DateTime"/> conversions.
		/// </summary>
		/// <param name="dateTimeStyle">The date time style.</param>
		public virtual MemberMap DateTimeStyles(DateTimeStyles dateTimeStyle)
		{
			memberMap.Data.TypeConverterOptions.DateTimeStyle = dateTimeStyle;

			return memberMap;
		}

		/// <summary>
		/// The <see cref="TimeSpanStyles"/> to use when type converting.
		/// This is used when doing <see cref="TimeSpan"/> converting.
		/// </summary>
		/// <param name="timeSpanStyles">The time span styles.</param>
		public virtual MemberMap TimespanStyles(TimeSpanStyles timeSpanStyles)
		{
			memberMap.Data.TypeConverterOptions.TimeSpanStyle = timeSpanStyles;

			return memberMap;
		}

		/// <summary>
		/// The <see cref="NumberStyles"/> to use when type converting.
		/// This is used when doing any number conversions.
		/// </summary>
		/// <param name="numberStyle"></param>
		public virtual MemberMap NumberStyles(NumberStyles numberStyle)
		{
			memberMap.Data.TypeConverterOptions.NumberStyle = numberStyle;

			return memberMap;
		}

		/// <summary>
		/// The string format to be used when type converting.
		/// </summary>
		/// <param name="formats">The format.</param>
		public virtual MemberMap Format(params string[] formats)
		{
			memberMap.Data.TypeConverterOptions.Formats = formats;

			return memberMap;
		}

		/// <summary>
		/// The <see cref="UriKind"/> to use when converting.
		/// This is used when doing <see cref="Uri"/> conversions.
		/// </summary>
		/// <param name="uriKind">Kind of the URI.</param>
		public virtual MemberMap UriKind(UriKind uriKind)
		{
			memberMap.Data.TypeConverterOptions.UriKind = uriKind;

			return memberMap;
		}

		/// <summary>
		/// The string values used to represent a boolean when converting.
		/// </summary>
		/// <param name="isTrue">A value indicating whether true values or false values are being set.</param>
		/// <param name="clearValues">A value indication if the current values should be cleared before adding the new ones.</param>
		/// <param name="booleanValues">The string boolean values.</param>
		public virtual MemberMap BooleanValues(bool isTrue, bool clearValues = true, params string[] booleanValues)
		{
			if (isTrue)
			{
				if (clearValues)
				{
					memberMap.Data.TypeConverterOptions.BooleanTrueValues.Clear();
				}

				memberMap.Data.TypeConverterOptions.BooleanTrueValues.AddRange(booleanValues);
			}
			else
			{
				if (clearValues)
				{
					memberMap.Data.TypeConverterOptions.BooleanFalseValues.Clear();
				}

				memberMap.Data.TypeConverterOptions.BooleanFalseValues.AddRange(booleanValues);
			}

			return memberMap;
		}

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="nullValues">The values that represent null.</param>
		/// <returns></returns>
		public virtual MemberMap NullValues(params string[] nullValues)
		{
			return NullValues(true, nullValues);
		}

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="clearValues">A value indication if the current values should be cleared before adding the new ones.</param>
		/// <param name="nullValues">The values that represent null.</param>
		/// <returns></returns>
		public virtual MemberMap NullValues(bool clearValues, params string[] nullValues)
		{
			if (clearValues)
			{
				memberMap.Data.TypeConverterOptions.NullValues.Clear();
			}

			memberMap.Data.TypeConverterOptions.NullValues.AddRange(nullValues);

			return memberMap;
		}
	}
}
