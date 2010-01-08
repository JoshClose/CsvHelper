#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used the parse a CSV file.
	/// </summary>
	public interface ICsvParser : IDisposable
	{
		/// <summary>
		/// Gets the size of the buffer
		/// used when reading the stream.
		/// </summary>
		int BufferSize { get; }

		/// <summary>
		/// Gets the delimiter used to
		/// separate the fields of the CSV records.
		/// </summary>
		char Delimiter { get; }

		/// <summary>
		/// Gets the field count.
		/// </summary>
		int FieldCount { get; }

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="List{String}" /> of fields for the record read.</returns>
		string[] Read();
	}
}
