// Copyright 2009-2022 Josh Close
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
	/// setting.
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
		/// setting.
		/// </summary>
		/// <param name="culture">The culture.</param>
		public CultureInfoAttribute(string culture)
		{
			CultureInfo = CultureInfo.GetCultureInfo(culture);
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.TypeConverterOptions.CultureInfo = CultureInfo;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="ParameterMap" />.
		/// </summary>
		/// <param name="parameterMap">The parameter map.</param>
		public void ApplyTo(ParameterMap parameterMap)
		{
			parameterMap.Data.TypeConverterOptions.CultureInfo = CultureInfo;
		}
	}
}
