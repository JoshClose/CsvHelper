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
	/// The string format to be used when type converting.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
	public class FormatAttribute : Attribute
    {
		/// <summary>
		/// Gets the formats.
		/// </summary>
		public string[] Formats { get; private set; }

		/// <summary>
		/// The string format to be used when type converting.
		/// </summary>
		/// <param name="format">The format.</param>
		public FormatAttribute( string format )
		{
			Formats = new string[] { format };
		}

		/// <summary>
		/// The string format to be used when type converting.
		/// </summary>
		/// <param name="formats">The formats.</param>
		public FormatAttribute( params string[] formats )
		{
			Formats = formats;
		}
    }
}
