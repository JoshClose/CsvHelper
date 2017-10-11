// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The string values used to represent null when converting.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
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
			NullValues = new string[] { nullValue };
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
