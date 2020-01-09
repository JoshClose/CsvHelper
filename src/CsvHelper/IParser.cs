// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using CsvHelper.Configuration;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used the parse a CSV file.
	/// </summary>
	public interface IParser : IDisposable
	{
		/// <summary>
		/// Gets the reading context.
		/// </summary>
		ReadingContext Context { get; }

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		IParserConfiguration Configuration { get; }

		/// <summary>
		/// Gets the <see cref="FieldReader"/>.
		/// </summary>
		IFieldReader FieldReader { get; }

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		string[] Read();

		/// <summary>
		/// Reads a record from the CSV file asynchronously.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		Task<string[]> ReadAsync();
	}
}
