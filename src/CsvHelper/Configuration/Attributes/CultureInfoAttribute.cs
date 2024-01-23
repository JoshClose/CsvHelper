// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The <see cref="CultureInfo"/> used when type converting.
	/// This will override the global <see cref="CsvConfiguration.CultureInfo"/>
	/// setting. Or set the same if the attribute is specified on class level.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
	public class CultureInfoAttribute : Attribute, IMemberMapper, IParameterMapper
	{
		/// <summary>
		/// Gets the culture info.
		/// </summary>
		public CultureInfo CultureInfo { get; private set; }

		/// <summary>
		/// The <see cref="CultureInfo"/> used when type converting.
		/// This will override the global <see cref="CsvConfiguration.CultureInfo"/>
		/// setting. Or set the same if the attribute is specified on class level.
		/// </summary>
		/// <param name="culture">The culture.</param>
		public CultureInfoAttribute(string culture)
		{
			CultureInfo = CultureInfo.GetCultureInfo(culture);
		}

		/// <inheritdoc />
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.TypeConverterOptions.CultureInfo = CultureInfo;
		}

		/// <inheritdoc />
		public void ApplyTo(ParameterMap parameterMap)
		{
			parameterMap.Data.TypeConverterOptions.CultureInfo = CultureInfo;
		}
	}
}
