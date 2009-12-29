#region License
// Copyright 2009 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;

namespace CsvHelper
{
	public interface ICsvParser : IDisposable
	{
		/// <summary>
		/// Gets or sets the delimiter used to
		/// separate the fields of the CSV records.
		/// </summary>
		char Delimiter { get; set; }

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="List{String}" /> of fields for the record read.</returns>
		IList<string> Read();
	}
}
