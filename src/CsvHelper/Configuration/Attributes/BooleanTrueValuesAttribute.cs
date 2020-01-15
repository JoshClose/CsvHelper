// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The string values used to represent a boolean true when converting.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class BooleanTrueValuesAttribute : Attribute, IMemberMapper
	{
		/// <summary>
		/// Gets the true values.
		/// </summary>
		public string[] TrueValues { get; private set; }

		/// <summary>
		/// The string values used to represent a boolean true when converting.
		/// </summary>
		/// <param name="trueValue"></param>
		public BooleanTrueValuesAttribute(string trueValue)
		{
			TrueValues = new string[] { trueValue };
		}

		/// <summary>
		/// The string values used to represent a boolean true when converting.
		/// </summary>
		/// <param name="trueValues"></param>
		public BooleanTrueValuesAttribute(params string[] trueValues)
		{
			TrueValues = trueValues;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.TypeConverterOptions.BooleanTrueValues.Clear();
			memberMap.Data.TypeConverterOptions.BooleanTrueValues.AddRange(TrueValues);
		}
	}
}
