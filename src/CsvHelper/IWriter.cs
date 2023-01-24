// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to write to a CSV file.
	/// </summary>
	public interface IWriter : IWriterRow, IDisposable
#if !NET462 && !NETSTANDARD2_0
		, IAsyncDisposable
#endif
	{
		/// <summary>
		/// Flushes the internal buffer to the <see cref="TextWriter"/> then
		/// flushes the <see cref="TextWriter"/>.
		/// </summary>
		void Flush();

		/// <summary>
		/// Flushes the internal buffer to the <see cref="TextWriter"/> then
		/// flushes the <see cref="TextWriter"/>.
		/// </summary>
		Task FlushAsync();

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This flushes the buffer to the <see cref="TextWriter"/> but
		/// does not flush the <see cref="TextWriter"/>.
		/// </summary>
		void NextRecord();

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This flushes the buffer to the <see cref="TextWriter"/> but
		/// does not flush the <see cref="TextWriter"/>.
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
		/// <param name="cancellationToken">The cancellation token to stop the writing.</param>
		Task WriteRecordsAsync(IEnumerable records, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">Record type.</typeparam>
		/// <param name="records">The records to write.</param>
		/// <param name="cancellationToken">The cancellation token to stop the writing.</param>
		Task WriteRecordsAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">Record type.</typeparam>
		/// <param name="records">The records to write.</param>
		/// <param name="cancellationToken">The cancellation token to stop the writing.</param>
		Task WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default);
	}
}
