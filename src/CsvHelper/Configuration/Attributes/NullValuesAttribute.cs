// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The string values used to represent null when converting.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
	public class NullValuesAttribute : Attribute
	{
		/// <summary>
		/// Gets the null values.
		/// </summary>
		public string[] NullValues { get; private set; }

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="nullValue">The null values.</param>
		public NullValuesAttribute( string nullValue )
		{
			NullValues = new[] { nullValue };
		}

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="nullValues">The null values.</param>
		public NullValuesAttribute( params string[] nullValues )
		{
			NullValues = nullValues;
		}
	}
}
