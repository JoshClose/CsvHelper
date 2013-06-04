// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used the parse a CSV file.
	/// </summary>
	public interface ICsvParser : IDisposable
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Gets the field count.
		/// </summary>
		int FieldCount { get; }

		/// <summary>
		/// Gets the character position that the parser is currently on.
		/// </summary>
		long CharPosition { get; }

		/// <summary>
		/// Gets the byte position that the parser is currently on.
		/// </summary>
		long BytePosition { get; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		int Row { get; }

		/// <summary>
		/// Gets the raw row for the current record that was parsed.
		/// </summary>
		string RawRecord { get; }

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		string[] Read();
	}
}
