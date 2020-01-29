// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to write to a CSV file.
	/// </summary>
	public interface IWriter : IWriterRow, IDisposable
#if NET47 || NETSTANDARD2_1
		, IAsyncDisposable
#endif
	{
		/// <summary>
		/// Serializes the row to the <see cref="TextWriter"/>.
		/// </summary>
		void Flush();

		/// <summary>
		/// Serializes the row to the <see cref="TextWriter"/>.
		/// </summary>
		Task FlushAsync();

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This automatically flushes the writer.
		/// </summary>
		void NextRecord();

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This automatically flushes the writer.
		/// </summary>
		Task NextRecordAsync();

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The records to write.</param>
		void WriteRecords(IEnumerable records);

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">Record type.</typeparam>
		/// <param name="records">The records to write.</param>
		void WriteRecords<T>(IEnumerable<T> records);

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The records to write.</param>
		Task WriteRecordsAsync(IEnumerable records);

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">Record type.</typeparam>
		/// <param name="records">The records to write.</param>
		Task WriteRecordsAsync<T>(IEnumerable<T> records);
	}
}
