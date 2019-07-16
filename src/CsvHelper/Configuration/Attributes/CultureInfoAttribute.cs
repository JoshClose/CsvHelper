// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The <see cref="CultureInfo"/> used when type converting.
	/// This will override the global <see cref="Configuration.CultureInfo"/>
	/// setting.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
	public class CultureInfoAttribute : Attribute, IMemberMapper
	{
		/// <summary>
		/// Gets the culture info.
		/// </summary>
		public CultureInfo CultureInfo { get; private set; }

		/// <summary>
		/// The <see cref="CultureInfo"/> used when type converting.
		/// This will override the global <see cref="Configuration.CultureInfo"/>
		/// setting.
		/// </summary>
		/// <param name="culture">The culture.</param>
		public CultureInfoAttribute( string culture )
		{
			CultureInfo = CultureInfo.GetCultureInfo( culture );
		}

        public void ApplyTo(MemberMap memberMap)
        {
            memberMap.Data.TypeConverterOptions.CultureInfo = CultureInfo;
        }

    }
}
