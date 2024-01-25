// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Flags for the type of members that 
	/// can be used for auto mapping.
	/// </summary>
	[Flags]
	public enum MemberTypes
	{
		/// <summary>
		/// No members. This is not a valid value
		/// and will cause an exception if used.
		/// </summary>
		None = 0,

		/// <summary>
		/// Properties on a class.
		/// </summary>
		Properties = 1,

		/// <summary>
		/// Fields on a class.
		/// </summary>
		Fields = 2
	}
}
