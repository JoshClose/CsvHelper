// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Appends a prefix to the header of each field of the reference member.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class HeaderPrefixAttribute : Attribute, IMemberReferenceMapper
	{
		/// <summary>
		/// Gets the prefix.
		/// </summary>
		public string Prefix { get; private set; }

		/// <summary>
		/// Appends a prefix to the header of each field of the reference member.
		/// </summary>
		public HeaderPrefixAttribute() { }

		/// <summary>
		/// Appends a prefix to the header of each field of the reference member.
		/// </summary>
		/// <param name="prefix">The prefix.</param>
		public HeaderPrefixAttribute(string prefix)
		{
			Prefix = prefix;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="referenceMap">The reference map.</param>
		public void ApplyTo(MemberReferenceMap referenceMap)
		{
			referenceMap.Data.Prefix = Prefix ?? referenceMap.Data.Member.Name + ".";
		}
	}
}
