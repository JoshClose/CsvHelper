// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
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
		/// Gets the parser.
		/// </summary>
		ICsvParser Parser { get; }

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

#if !NET_2_0

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		IEnumerable<T> GetRecords<T>();

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the record.</param>
		/// <returns>An <see cref="IList{Object}" /> of records.</returns>
		IEnumerable<object> GetRecords( Type type );

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache{T}"/> needs to be called to update the
		/// record cache.
		/// </summary>
		void ClearRecordCache<T>();

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache( Type )"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <param name="type">The type to invalidate.</param>
		void ClearRecordCache( Type type );

		/// <summary>
		/// Clears the record cache for all types. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache()"/> needs to be called to update the
		/// record cache.
		/// </summary>
		void ClearRecordCache();

#endif
	}
}
