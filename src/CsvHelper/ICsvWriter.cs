// Copyright 2009-2012 Josh Close
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
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		void WriteField( string field );

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

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		void WriteRecord<T>( T record ) where T : class;

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="records">The list of records to write.</param>
		void WriteRecords<T>( IEnumerable<T> records ) where T : class;

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		void InvalidateRecordCache<T>() where T : class;
	}
}
