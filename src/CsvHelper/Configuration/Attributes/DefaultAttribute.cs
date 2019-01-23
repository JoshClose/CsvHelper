// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The default value that will be used when reading when
	/// the CSV field is empty.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
	public class DefaultAttribute : Attribute
	{
		/// <summary>
		/// Gets the default value.
		/// </summary>
		public object Default { get; }

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value</param>
		public DefaultAttribute( object defaultValue )
		{
			Default = defaultValue;
		}
	}
}
