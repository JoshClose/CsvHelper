// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used the parse a CSV file.
	/// </summary>
	public interface IParser : IDisposable
	{
		/// <summary>
		/// Gets the configuration.
		/// </summary>
		ICsvParserConfiguration Configuration { get; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		int Row { get; }

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		string[] Read();
	}
}
