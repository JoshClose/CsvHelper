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
	/// Defines methods used to write to a CSV file.
	/// </summary>
	public interface ICsvWriter : IDisposable
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Writes the field to the CSV file. The field
		/// may get quotes added to it.
		/// When all fields are written for a record,
		/// <see cref="NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		void WriteField( string field );

		/// <summary>
		/// Writes the field to the CSV file. This will
		/// ignore any need to quote and ignore the
		/// <see cref="CsvConfiguration.QuoteAllFields"/>
		/// and just quote based on the shouldQuote
		/// parameter.
		/// When all fields are written for a record,
		/// <see cref="NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		/// <param name="shouldQuote">True to quote the field, otherwise false.</param>
		void WriteField( string field, bool shouldQuote );

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		void WriteField<T>( T field );

		/// <summary>
		/// Ends writing of the current record
		/// and starts a new record. This is used
		/// when manually writing records with <see cref="WriteField{T}( T )"/>
		/// </summary>
		void NextRecord();

#if !NET_2_0
		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		void WriteRecord<T>( T record ) where T : class;

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		/// <param name="record">The record to write.</param>
		void WriteRecord( Type type, object record );

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="records">The list of records to write.</param>
		void WriteRecords<T>( IEnumerable<T> records ) where T : class;

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		/// <param name="records">The list of records to write.</param>
		void WriteRecords( Type type, IEnumerable<object> records );

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		void InvalidateRecordCache<T>() where T : class;

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="InvalidateRecordCache"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		/// <param name="type">The record type.</param>
		void InvalidateRecordCache( Type type );
#endif
	}
}
