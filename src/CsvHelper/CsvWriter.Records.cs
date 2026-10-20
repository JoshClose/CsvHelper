using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

// Records of [CsvRecord] types, written by the record writers the source generator writes (see CsvRecordWriter).
public sealed partial class CsvWriter
{
	// The record writer the record methods used last.
	private object? recordWriter;

	/// <summary>
	/// Writes the header of <typeparamref name="T"/>, a <see cref="CsvRecordAttribute"/> type: a record with the names
	/// of its columns, which a reader maps back to its members.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or the
	/// header is not a valid record (see <see cref="EndRecord"/>). The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteHeader<T>()
	{
		if (DynamicRecords<T>.Apply)
		{
			throw new InvalidOperationException(
				"Dynamic records have no columns before the first record: WriteRecords writes the header of the first one, or write it with WriteHeader(names).");
		}

		WriteHeaderFields(RecordWriter<T>());
		EndRecord();
	}

	/// <summary>
	/// Writes <paramref name="record"/>, of a <see cref="CsvRecordAttribute"/> type, as a complete record: a field per
	/// column, formatted as its member's attributes say, then <see cref="EndRecord"/>. The record writer is created on the
	/// first call and reused while <typeparamref name="T"/> stays the same.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="record">The record.</param>
	/// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or the
	/// record is not valid (see <see cref="EndRecord"/>). The record is discarded.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteRecord<T>(T record)
	{
		if (record is null)
		{
			throw new ArgumentNullException(nameof(record));
		}

		WriteRecordFields(record);
		EndRecord();
	}

	/// <summary>
	/// Writes <paramref name="record"/> as a complete record and flushes asynchronously when the buffer is mostly full.
	/// See <see cref="WriteRecord{T}(T)"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="record">The record.</param>
	/// <param name="cancellationToken">Cancels writing to the destination.</param>
	/// <returns>A task that completes when the record is in the buffer and, if the buffer was mostly full, written out.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or the
	/// record is not valid (see <see cref="EndRecord"/>). The record is discarded.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteRecordAsync<T>(T record, CancellationToken cancellationToken = default)
	{
		if (record is null)
		{
			throw new ArgumentNullException(nameof(record));
		}

		WriteRecordFields(record);

		return EndRecordAsync(cancellationToken);
	}

	/// <summary>
	/// Writes <paramref name="records"/>, of a <see cref="CsvRecordAttribute"/> type: first the header of
	/// <typeparamref name="T"/> when nothing has been written yet (<see cref="RecordCount"/> is 0) and
	/// <see cref="CsvWriterOptions.HasHeader"/> is on, then each record.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or a
	/// record is not valid (see <see cref="EndRecord"/>). That record is discarded; the records before it are written.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteRecords<T>(IEnumerable<T> records)
	{
		ArgumentNullException.ThrowIfNull(records);
		if (DynamicRecords<T>.Apply)
		{
			ThrowIfDisposed();
			WriteDynamicRecords(records);

			return;
		}

		var writer = RecordWriter<T>();
		if (NeedsHeader())
		{
			WriteHeaderFields(writer);
			EndRecord();
		}

		// One handler for the whole sequence, which discards the record that failed.
		try
		{
			// Arrays and lists without the two interface calls per record of IEnumerable<T>.
			if (records is T[] array)
			{
				foreach (var record in array)
				{
					if (AddRecord(writer, record))
					{
						FlushBuffer();
					}
				}
			}
			else if (records is List<T> list)
			{
				foreach (var record in list)
				{
					if (AddRecord(writer, record))
					{
						FlushBuffer();
					}
				}
			}
			else
			{
				foreach (var record in records)
				{
					if (AddRecord(writer, record))
					{
						FlushBuffer();
					}
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>
	/// Writes a record of a sequence and ends it, without I/O; the caller discards the record when this throws.
	/// Returns whether the buffer is mostly full, so the caller flushes it.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool AddRecord<T>(CsvRecordWriter<T> writer, T record)
	{
		writer.Write(this, record ?? throw NullRecord("records"));
		FinishRecord();

		return pos >= flushThreshold;
	}

	/// <summary>
	/// Writes <paramref name="records"/> as <see cref="WriteRecords{T}(IEnumerable{T})"/> does, flushing asynchronously
	/// whenever the buffer is mostly full, so it never blocks on I/O.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="cancellationToken">Cancels writing to the destination.</param>
	/// <returns>A task that completes when every record is in the buffer and the full buffers have been written out.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or a
	/// record is not valid (see <see cref="EndRecord"/>). That record is discarded; the records before it are written.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteRecordsAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		if (DynamicRecords<T>.Apply)
		{
			ThrowIfDisposed();

			return WriteDynamicRecordsAsync(records, cancellationToken);
		}

		return WriteRecordsAsync(RecordWriter<T>(), records, cancellationToken);
	}

	/// <summary>
	/// Writes an asynchronous sequence of records as <see cref="WriteRecords{T}(IEnumerable{T})"/> does, flushing
	/// asynchronously whenever the buffer is mostly full, so it never blocks on I/O.
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="cancellationToken">Cancels the enumeration of <paramref name="records"/> and writing to the destination.</param>
	/// <returns>A task that completes when every record is in the buffer and the full buffers have been written out.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, or a
	/// record is not valid (see <see cref="EndRecord"/>). That record is discarded; the records before it are written.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		if (DynamicRecords<T>.Apply)
		{
			ThrowIfDisposed();

			return WriteDynamicRecordsAsync(records, cancellationToken);
		}

		return WriteRecordsAsync(RecordWriter<T>(), records, cancellationToken);
	}

	private async ValueTask WriteRecordsAsync<T>(CsvRecordWriter<T> writer, IEnumerable<T> records, CancellationToken cancellationToken)
	{
		if (NeedsHeader())
		{
			WriteHeaderFields(writer);
			await EndRecordAsync(cancellationToken).ConfigureAwait(false);
		}

		// As in WriteRecords; it awaits only when the buffer is mostly full.
		try
		{
			if (records is T[] array)
			{
				foreach (var record in array)
				{
					if (AddRecord(writer, record))
					{
						await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
					}
				}
			}
			else
			{
				foreach (var record in records)
				{
					if (AddRecord(writer, record))
					{
						await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	private async ValueTask WriteRecordsAsync<T>(CsvRecordWriter<T> writer, IAsyncEnumerable<T> records, CancellationToken cancellationToken)
	{
		if (NeedsHeader())
		{
			WriteHeaderFields(writer);
			await EndRecordAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				if (AddRecord(writer, record))
				{
					await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	/// <summary>Writes the fields of one record: with the record writer used last, else as the type of the record says.</summary>
	private void WriteRecordFields<T>(T record)
	{
		ThrowIfDisposed();
		if (recordWriter is CsvRecordWriter<T> writer)
		{
			WriteFields(writer, record);
		}
		else
		{
			WriteRecordFieldsSlow(record);
		}
	}

	// Out of line: the first record of a type, and dynamic records.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void WriteRecordFieldsSlow<T>(T record)
	{
		if (DynamicRecords<T>.Apply)
		{
			WriteDynamicRecord(record!);
		}
		else
		{
			WriteFields(RecordWriter<T>(), record);
		}
	}

	private CsvRecordWriter<T> RecordWriter<T>()
	{
		ThrowIfDisposed();
		if (recordWriter is not CsvRecordWriter<T> writer)
		{
			recordWriter = writer = CsvRecordWriter.Create<T>();
		}

		return writer;
	}

	// Nothing written yet (comments aside): the records get a header, unless the options say the output has none.
	private bool NeedsHeader() => options.HasHeader && records == 0 && fieldCount == 0;

	private void WriteHeaderFields<T>(CsvRecordWriter<T> writer)
	{
		try
		{
			writer.WriteHeader(this);
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	private void WriteFields<T>(CsvRecordWriter<T> writer, T record)
	{
		// A record that fails halfway (a converter, a value that does not fit its fixed-width column) is not left half
		// written in front of the next one.
		try
		{
			writer.Write(this, record);
		}
		catch
		{
			DiscardRecord();
			throw;
		}
	}

	private static ArgumentException NullRecord(string name) => new("The sequence contains a null record.", name);
}
