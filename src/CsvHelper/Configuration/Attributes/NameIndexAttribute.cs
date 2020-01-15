// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// When reading, is used to get the 
	/// index of the name used when there 
	/// are multiple names that are the same.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class NameIndexAttribute : Attribute, IMemberMapper
	{
		/// <summary>
		/// The name index.
		/// </summary>
		public int NameIndex { get; private set; }

		/// <summary>
		/// When reading, is used to get the 
		/// index of the name used when there 
		/// are multiple names that are the same.
		/// </summary>
		/// <param name="nameIndex">The name index.</param>
		public NameIndexAttribute(int nameIndex)
		{
			NameIndex = nameIndex;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.NameIndex = NameIndex;
		}
	}
}
