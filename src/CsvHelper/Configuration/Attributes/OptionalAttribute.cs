// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Ignore the member when reading if no matching field name can be found.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
	public class OptionalAttribute : Attribute, IMemberMapper, IParameterMapper
	{
		/// <inheritdoc />
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.IsOptional = true;
		}

		/// <inheritdoc />
		public void ApplyTo(ParameterMap parameterMap)
		{
			parameterMap.Data.IsOptional = true;
		}
	}
}
