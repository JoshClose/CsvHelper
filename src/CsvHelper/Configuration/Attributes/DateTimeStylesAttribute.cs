// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The <see cref="DateTimeStyles"/> to use when type converting.
	/// This is used when doing any <see cref="DateTime"/> conversions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class DateTimeStylesAttribute : Attribute, IMemberMapper
	{
		/// <summary>
		/// Gets the date time styles.
		/// </summary>
		public DateTimeStyles DateTimeStyles { get; private set; }

		/// <summary>
		/// The <see cref="DateTimeStyles"/> to use when type converting.
		/// This is used when doing any <see cref="DateTime"/> conversions.
		/// </summary>
		/// <param name="dateTimeStyles">The date time styles.</param>
		public DateTimeStylesAttribute(DateTimeStyles dateTimeStyles)
		{
			DateTimeStyles = dateTimeStyles;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.TypeConverterOptions.DateTimeStyle = DateTimeStyles;
		}
	}
}
