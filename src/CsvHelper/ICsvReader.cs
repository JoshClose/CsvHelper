// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to read parsed data
	/// from a CSV file.
	/// </summary>
	public interface ICsvReader : ICsvReaderRow, IDisposable
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Gets the parser.
		/// </summary>
		ICsvParser Parser { get; }

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		string[] FieldHeaders { get; }

		/// <summary>
		/// Reads the header field without reading the first row.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		bool ReadHeader();

		/// <summary>
		/// Advances the reader to the next record. If the header hasn't been read
		/// yet, it'll automatically be read along with the first record.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		bool Read();
	}
}
