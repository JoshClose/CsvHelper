// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that prepares the header field for matching against a member name.
	/// The header field and the member name are both ran through this function.
	/// You should do things like trimming, removing whitespace, removing underscores,
	/// and making casing changes to ignore case.
	/// </summary>
	public delegate string PrepareHeaderForMatch(PrepareHeaderForMatchArgs args);

	/// <summary>
	/// PrepareHeaderForMatch args.
	/// </summary>
	public readonly struct PrepareHeaderForMatchArgs
	{
		/// <summary>
		/// The header.
		/// </summary>
		public readonly string Header;

		/// <summary>
		/// The field index.
		/// </summary>
		public readonly int FieldIndex;

		/// <summary>
		/// Creates a new instance of PrepareHeaderForMatchArgs.
		/// </summary>
		/// <param name="header">The header.</param>
		/// <param name="fieldIndex">The field index.</param>
		public PrepareHeaderForMatchArgs(string header, int fieldIndex)
		{
			Header = header;
			FieldIndex = fieldIndex;
		}
	}
}
