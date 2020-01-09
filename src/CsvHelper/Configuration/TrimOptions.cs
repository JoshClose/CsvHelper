// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Options for trimming of fields.
	/// </summary>
	[Flags]
	public enum TrimOptions
	{
		/// <summary>
		/// No trimming.
		/// </summary>
		None = 0,

		/// <summary>
		/// Trims the whitespace around a field.
		/// </summary>
		Trim = 1,

		/// <summary>
		/// Trims the whitespace inside of quotes around a field.
		/// </summary>
		InsideQuotes = 2
	}
}
