// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// When reading, is used to get the field
	/// at the index of the name if there was a
	/// header specified. It will look for the
	/// first name match in the order listed.
	/// When writing, sets the name of the
	/// field in the header record.
	/// The first name will be used.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
	public class NameAttribute : Attribute
	{
		/// <summary>
		/// Gets the names.
		/// </summary>
		public string[] Names { get; }

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. It will look for the
		/// first name match in the order listed.
		/// When writing, sets the name of the
		/// field in the header record.
		/// The first name will be used.
		/// </summary>
		/// <param name="name">The name</param>
		public NameAttribute( string name )
		{
			Names = new[] { name };
		}

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. It will look for the
		/// first name match in the order listed.
		/// When writing, sets the name of the
		/// field in the header record.
		/// The first name will be used.
		/// </summary>
		/// <param name="names">The names.</param>
		public NameAttribute( params string[] names )
		{
			if( names == null || names.Length == 0 )
			{
				throw new ArgumentNullException( nameof( names ) );
			}

			Names = names;
		}
	}
}
