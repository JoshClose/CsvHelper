// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used the parse a CSV file.
	/// </summary>
	public interface ICsvParser : IDisposable
	{
		/// <summary>
		/// Gets the configuration.
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

		/// <summary>
		/// Ignore no of line(s) in reader
		/// </summary>
		/// <param name="noOfLine"></param>
		/// <returns>string in ignored line(s)</returns>
		string IgnoreLines(int noOfLine);
	}
}
