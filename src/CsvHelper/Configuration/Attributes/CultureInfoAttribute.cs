// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// When applied to a member, specifies the <see cref="System.Globalization.CultureInfo"/>
	/// used when type converting the member. When applied to a type, the value of
	/// <see cref="CsvConfiguration.CultureInfo"/> in the <see cref="CsvConfiguration"/>
	/// returned by <see cref="CsvConfiguration.FromAttributes{T}()"/>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class CultureInfoAttribute : Attribute, IClassMapper, IMemberMapper, IParameterMapper
	{
		/// <summary>
		/// Gets the culture info.
		/// </summary>
		public CultureInfo CultureInfo { get; private set; }

		/// <summary><inheritdoc cref="CultureInfoAttribute"/></summary>
		/// <param name="name">
		/// The name of a culture (case insensitive), or the literal values <c>"InvariantCulture"</c>,
		/// <c>"CurrentCulture"</c>, <c>"CurrentUICulture"</c>, <c>"InstalledUICulture"</c> to use the
		/// corresponding static properties on <see cref="System.Globalization.CultureInfo"/>.
		/// </param>
		public CultureInfoAttribute(string name)
		{
			switch(name)
			{
				case nameof(CultureInfo.InvariantCulture):
					CultureInfo = CultureInfo.InvariantCulture;
					break;
				case nameof(CultureInfo.CurrentCulture):
					CultureInfo = CultureInfo.CurrentCulture;
					break;
				case nameof(CultureInfo.CurrentUICulture):
					CultureInfo = CultureInfo.CurrentUICulture;
					break;
				case nameof(CultureInfo.InstalledUICulture):
					CultureInfo = CultureInfo.InstalledUICulture;
					break;
				default:
					CultureInfo = CultureInfo.GetCultureInfo(name);
					break;
			}
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.CultureInfo = CultureInfo;
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
