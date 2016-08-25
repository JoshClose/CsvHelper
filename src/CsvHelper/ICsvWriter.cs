// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to write to a CSV file.
	/// </summary>
	public interface ICsvWriter : ICsvWriterRow, IDisposable
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Ends writing of the current record
		/// and starts a new record. This is used
		/// when manually writing records with <see cref="WriteField{T}( T )"/>
		/// </summary>
		void NextRecord();

		/// <summary>
		/// Write the Excel seperator record.
		/// </summary>
		void WriteExcelSeparator();

#if !NET_2_0

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The list of records to write.</param>
		void WriteRecords( IEnumerable records );

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache{T}"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		void ClearRecordCache<T>();

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache( Type )"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <param name="type">The record type.</param>
		void ClearRecordCache( Type type );

		/// <summary>
		/// Clears the record cache for all types. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache()"/> needs to be called to update the
		/// record cache.
		/// </summary>
		void ClearRecordCache();

#endif
	}
}
