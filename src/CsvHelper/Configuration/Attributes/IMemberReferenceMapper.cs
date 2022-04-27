// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Defines methods to enable pluggable configuration of member reference mapping.
	/// </summary>
	public interface IMemberReferenceMapper
    {
		/// <summary>
		/// Applies configuration to the given <see cref="MemberReferenceMap" />.
		/// </summary>
		/// <param name="referenceMap">The reference map.</param>
		void ApplyTo(MemberReferenceMap referenceMap);
    }
}
