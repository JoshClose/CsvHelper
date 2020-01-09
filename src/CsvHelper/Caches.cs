// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper
{
	/// <summary>
	/// Types of caches.
	/// </summary>
	[Flags]
	[Serializable]
	public enum Caches
	{
		/// <summary>
		/// None.
		/// </summary>
		None = 0,

		/// <summary>
		/// Named index.
		/// </summary>
		NamedIndex = 1,

		/// <summary>
		/// Delegate that creates objects when reading.
		/// </summary>
		ReadRecord = 2,

		/// <summary>
		/// Delegate that writes objects to strings when writing.
		/// </summary>
		WriteRecord = 4,

		/// <summary>
		/// Type converter options.
		/// </summary>
		TypeConverterOptions = 8,

		/// <summary>
		/// Raw record.
		/// </summary>
		RawRecord = 16
	}
}
