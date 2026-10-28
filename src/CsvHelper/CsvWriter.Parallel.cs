using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
#if !NET9_0_OR_GREATER
using Lock = System.Object;
#endif

namespace CsvHelper;

// ParallelWriteRecords and ParallelWrite: the calling thread enumerates the items and hands them to workers in batches,
// through a ring of slots. A worker formats a batch with a writer of its own into a buffer of its own, and the calling
// thread writes the finished buffers to the destination in input order, so the output is that of a serial write. What
// spans records is settled on the calling thread before the workers start: the header, and the first record when it
// fixes the field count or the columns of dynamic records. Workers run while batches wait for them and leave when none
// do, so a slow source does not keep threads of the pool waiting.
public sealed partial class CsvWriter
{
	/// <summary>The output a batch aims for: the number of items in a batch follows from the output of the items so far.</summary>
	private const int ParallelBatchBytes = 128 * 1024;

	/// <summary>The number of items in the first batches, before the output of an item is known.</summary>
	private const int ParallelFirstBatchItems = 64;

	/// <summary>The largest number of items in a batch, which bounds the items that wait in the ring.</summary>
	private const int ParallelMaxBatchItems = 4096;

	/// <summary>Ring slots per worker: how many batches the workers may run ahead of the destination.</summary>
	private const int ParallelSlotsPerWorker = 4;

	// Set on the writers that the workers of a parallel write format batches with.
	private bool parallelWorker;

	/// <summary>
	/// Writes <paramref name="records"/> as <see cref="WriteRecords{T}(IEnumerable{T})"/> does, formatting them on several
	/// threads: the output is the same, in the same order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This thread enumerates <paramref name="records"/> and writes to the destination, while workers format batches of
	/// records, each with a writer of its own. The header, and the first record when it fixes the field count
	/// (<see cref="CsvWriterOptions.ValidateFieldCount"/>) or the columns of dynamic records, are written on this thread
	/// before the workers start, and so are records that fit in one batch.
	/// </para>
	/// <para>
	/// The record writer, the converters and <see cref="CsvWriterOptions.ShouldQuote"/> are called on several threads at
	/// once, so they must be thread-safe; generated record writers are. What this writer has buffered is written to the
	/// destination before the first batch, and each batch as soon as the batches before it are, without flushing the
	/// destination. An exception, from the enumeration or from a record, is thrown after the records before it are
	/// written. Do not use this writer otherwise until the call returns.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">
	/// The <see cref="CsvRecordAttribute"/> type, or a type of dynamic records (see <see cref="WriteRecords{T}(IEnumerable{T})"/>).
	/// </typeparam>
	/// <param name="records">The records, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, a record is
	/// not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/> callback got. An
	/// invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void ParallelWriteRecords<T>(IEnumerable<T> records, int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(records);
		var degree = ParallelDegree(maxDegreeOfParallelism);
		if (DynamicRecords<T>.Apply)
		{
			ParallelWriteDynamic(records, degree);

			return;
		}

		ParallelWriteWith(RecordWriter<T>(), records, degree);
	}

	/// <summary>
	/// Writes records with <paramref name="recordWriter"/>, as <see cref="WriteRecords{T}(IEnumerable{T}, CsvRecordWriter{T})"/>
	/// does, formatting them on several threads: the output is the same, in the same order. See
	/// <see cref="ParallelWriteRecords{T}(IEnumerable{T}, int)"/>.
	/// </summary>
	/// <remarks>
	/// The workers share <paramref name="recordWriter"/>, so it must be thread-safe: one that keeps no state between calls
	/// of <see cref="CsvRecordWriter{T}.Write"/> is.
	/// </remarks>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="recordWriter">Writes the header and the fields of each record.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="recordWriter"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/>
	/// callback got. An invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void ParallelWriteRecords<T>(IEnumerable<T> records, CsvRecordWriter<T> recordWriter, int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(recordWriter);
		var degree = ParallelDegree(maxDegreeOfParallelism);
		ParallelWriteWith(recordWriter, records, degree);
	}

	/// <summary>
	/// Writes <paramref name="records"/> as <see cref="ParallelWriteRecords{T}(IEnumerable{T}, int)"/> does, writing to the
	/// destination asynchronously.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="CsvRecordAttribute"/> type, or a type of dynamic records (see <see cref="WriteRecords{T}(IEnumerable{T})"/>).
	/// </typeparam>
	/// <param name="records">The records, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">Cancels waiting for the workers and writing to the destination.</param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, a record is
	/// not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/> callback got. An
	/// invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteRecordsAsync<T>(IEnumerable<T> records, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteRecordsCoreAsync(records, null, DynamicRecords<T>.Apply ? null : RecordWriter<T>(), degree, cancellationToken);
	}

	/// <summary>
	/// Writes an asynchronous sequence of records as <see cref="ParallelWriteRecords{T}(IEnumerable{T}, int)"/> does,
	/// enumerating it and writing to the destination asynchronously.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="CsvRecordAttribute"/> type, or a type of dynamic records (see <see cref="WriteRecords{T}(IEnumerable{T})"/>).
	/// </typeparam>
	/// <param name="records">The records, enumerated once, by this call.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">
	/// Cancels the enumeration of <paramref name="records"/>, waiting for the workers and writing to the destination.
	/// </param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type or its records cannot be written, a record is
	/// not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/> callback got. An
	/// invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="FormatException">A member's format is not valid for its type.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteRecordsAsync<T>(IAsyncEnumerable<T> records, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteRecordsCoreAsync(null, records, DynamicRecords<T>.Apply ? null : RecordWriter<T>(), degree, cancellationToken);
	}

	/// <summary>
	/// Writes records with <paramref name="recordWriter"/> as
	/// <see cref="ParallelWriteRecords{T}(IEnumerable{T}, CsvRecordWriter{T}, int)"/> does, writing to the destination
	/// asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="recordWriter">Writes the header and the fields of each record; the workers share it.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">Cancels waiting for the workers and writing to the destination.</param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="recordWriter"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/>
	/// callback got. An invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteRecordsAsync<T>(IEnumerable<T> records, CsvRecordWriter<T> recordWriter, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(recordWriter);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteRecordsCoreAsync(records, null, recordWriter, degree, cancellationToken);
	}

	/// <summary>
	/// Writes an asynchronous sequence of records with <paramref name="recordWriter"/> as
	/// <see cref="ParallelWriteRecords{T}(IEnumerable{T}, CsvRecordWriter{T}, int)"/> does, enumerating it and writing to
	/// the destination asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once, by this call.</param>
	/// <param name="recordWriter">Writes the header and the fields of each record; the workers share it.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">
	/// Cancels the enumeration of <paramref name="records"/>, waiting for the workers and writing to the destination.
	/// </param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="recordWriter"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="records"/> contains <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), or this writer is the one a <see cref="ParallelWrite{T}"/>
	/// callback got. An invalid record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteRecordsAsync<T>(IAsyncEnumerable<T> records, CsvRecordWriter<T> recordWriter, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(recordWriter);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteRecordsCoreAsync(null, records, recordWriter, degree, cancellationToken);
	}

	/// <summary>
	/// Writes the records of each item with <paramref name="write"/>, called on several threads, and writes them to the
	/// destination in input order: the output of a <see langword="foreach"/> loop that calls <paramref name="write"/> with
	/// this writer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This thread enumerates <paramref name="items"/> and writes to the destination, while workers call
	/// <paramref name="write"/> for batches of items. The callback gets the worker's writer (or this writer, for the items
	/// written on this thread) and writes whole records of the item on it: fields and <see cref="EndRecord"/>,
	/// <see cref="WriteRecord{T}(T)"/> and the like, as many records as the item needs, or none. A record that it leaves
	/// unfinished throws <see cref="InvalidOperationException"/>. Do not flush or dispose that writer, or start a parallel
	/// write with it.
	/// </para>
	/// <para>
	/// Calls run concurrently, so anything shared that the callback changes must be thread-safe, and so must the converters
	/// and <see cref="CsvWriterOptions.ShouldQuote"/>. No header is written: write it first, with
	/// <see cref="WriteHeader(ReadOnlySpan{string})"/> or <see cref="WriteHeader{T}"/>. When records must have as many
	/// fields as the first (<see cref="CsvWriterOptions.ValidateFieldCount"/>) and none has been written, the items up to
	/// the first record are written on this thread before the workers start, and so are items that fit in one batch.
	/// </para>
	/// <para>
	/// The callback's writer numbers records as if every item wrote one: at the start of an item, its
	/// <see cref="RecordCount"/> (the record index that <see cref="CsvWriterOptions.ShouldQuote"/> gets, and the record
	/// number in exception messages) is the <see cref="RecordCount"/> of this writer before the call plus the index of the
	/// item. After the call, <see cref="RecordCount"/> counts every record written. An exception, from the enumeration or
	/// from the callback, is thrown after the records of the items before it are written. Do not use this writer otherwise
	/// until the call returns.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The items, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="write">Writes the records of an item to the writer it gets.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="write"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), the callback left a record unfinished, or this writer is the one
	/// a callback got. The record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void ParallelWrite<T>(IEnumerable<T> items, Action<CsvWriter, T> write, int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(write);
		var degree = ParallelDegree(maxDegreeOfParallelism);
		var source = new ParallelSource<T>(items, null, default);
		try
		{
			ParallelWriteItems(source, CallbackItemWriter(write), degree, records, 0);
		}
		finally
		{
			source.Dispose();
		}
	}

	/// <summary>
	/// Writes the records of each item with <paramref name="write"/> as
	/// <see cref="ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> does, writing to the destination
	/// asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The items, enumerated once, on this thread; the workers read an array or a <see cref="List{T}"/> in place.</param>
	/// <param name="write">Writes the records of an item to the writer it gets; called on several threads at once.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">Cancels waiting for the workers and writing to the destination.</param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="write"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), the callback left a record unfinished, or this writer is the one
	/// a callback got. The record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteAsync<T>(IEnumerable<T> items, Action<CsvWriter, T> write, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(write);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteCoreAsync(items, null, write, degree, cancellationToken);
	}

	/// <summary>
	/// Writes the records of each item of an asynchronous sequence with <paramref name="write"/> as
	/// <see cref="ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> does, enumerating it and writing to the
	/// destination asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The items, enumerated once, by this call.</param>
	/// <param name="write">Writes the records of an item to the writer it gets; called on several threads at once.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <param name="cancellationToken">
	/// Cancels the enumeration of <paramref name="items"/>, waiting for the workers and writing to the destination.
	/// </param>
	/// <returns>A task that completes when every record has been written to the destination.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="write"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="InvalidOperationException">
	/// A record is not valid (see <see cref="EndRecord"/>), the callback left a record unfinished, or this writer is the one
	/// a callback got. The record is discarded; the records before it are written, and none after it.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask ParallelWriteAsync<T>(IAsyncEnumerable<T> items, Action<CsvWriter, T> write, int maxDegreeOfParallelism = -1, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(write);
		var degree = ParallelDegree(maxDegreeOfParallelism);

		return ParallelWriteCoreAsync(null, items, write, degree, cancellationToken);
	}

	/// <summary>Checks the arguments shared by the parallel methods and returns the number of workers.</summary>
	private int ParallelDegree(int maxDegreeOfParallelism)
	{
		if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Must be positive or -1.");
		}

		ThrowIfDisposed();
		if (parallelWorker)
		{
			throw new InvalidOperationException("The writer passed to a ParallelWrite callback writes the records of one item; it cannot start a parallel write.");
		}

		return maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;
	}

	private void ParallelWriteWith<T>(CsvRecordWriter<T> recordWriter, IEnumerable<T> records, int degree)
	{
		if (NeedsHeader())
		{
			WriteHeaderFields(recordWriter);
			EndRecord();
		}

		var source = new ParallelSource<T>(records, null, default);
		try
		{
			ParallelWriteItems(source, RecordItemWriter(recordWriter), degree, this.records, 0);
		}
		finally
		{
			source.Dispose();
		}
	}

	private void ParallelWriteDynamic<T>(IEnumerable<T> records, int degree)
	{
		var source = new ParallelSource<T>(records, null, default);
		try
		{
			if (!source.MoveNext())
			{
				return;
			}

			// The columns are those of the first record, which is written here, before the workers start.
			var first = source.Current ?? throw NullRecord(nameof(records));
			var names = DynamicNames(first);
			if (NeedsHeader())
			{
				WriteHeader(names);
			}

			var write = DynamicItemWriter<T>(names, (first as CsvDynamicRecord)?.Schema);
			var recordBase = this.records;
			WriteSerialItem(write, first);
			ParallelWriteItems(source, write, degree, recordBase, 1);
		}
		finally
		{
			source.Dispose();
		}
	}

	/// <summary>
	/// Writes the rest of <paramref name="items"/>: the items that settle what spans records here, then the others on the
	/// workers.
	/// </summary>
	/// <param name="items">The items, positioned before the next one to write.</param>
	/// <param name="write">Writes the records of an item.</param>
	/// <param name="degree">The largest number of workers.</param>
	/// <param name="recordBase">The record count that item 0 starts at on the workers.</param>
	/// <param name="itemsBefore">The number of items already written.</param>
	private void ParallelWriteItems<T>(ParallelSource<T> items, Action<CsvWriter, T> write, int degree, long recordBase, long itemsBefore)
	{
		// The workers must not decide the field count or finish an open record: what does is written here.
		while (fieldCount > 0 || (validate && expectedFields < 0))
		{
			if (!items.MoveNext())
			{
				return;
			}

			WriteSerialItem(write, items.Current);
			itemsBefore++;
		}

		new ParallelWriteRun<T>(this, write, degree, recordBase, itemsBefore).Run(items);
	}

	private async ValueTask ParallelWriteRecordsCoreAsync<T>(IEnumerable<T>? items, IAsyncEnumerable<T>? asyncItems, CsvRecordWriter<T>? recordWriter, int degree, CancellationToken cancellationToken)
	{
		var source = new ParallelSource<T>(items, asyncItems, cancellationToken);
		try
		{
			if (recordWriter is null)
			{
				await ParallelWriteDynamicAsync(source, degree, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				if (NeedsHeader())
				{
					WriteHeaderFields(recordWriter);
					await EndRecordAsync(cancellationToken).ConfigureAwait(false);
				}

				await ParallelWriteItemsAsync(source, RecordItemWriter(recordWriter), degree, records, 0, cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			await source.DisposeAsync().ConfigureAwait(false);
		}
	}

	private async ValueTask ParallelWriteDynamicAsync<T>(ParallelSource<T> items, int degree, CancellationToken cancellationToken)
	{
		if (!await items.MoveNextAsync().ConfigureAwait(false))
		{
			return;
		}

		var first = items.Current ?? throw NullRecord("records");
		var names = DynamicNames(first);
		if (NeedsHeader())
		{
			foreach (var name in names)
			{
				WriteField(name.AsSpan());
			}

			await EndRecordAsync(cancellationToken).ConfigureAwait(false);
		}

		var write = DynamicItemWriter<T>(names, (first as CsvDynamicRecord)?.Schema);
		var recordBase = records;
		await WriteSerialItemAsync(write, first, cancellationToken).ConfigureAwait(false);
		await ParallelWriteItemsAsync(items, write, degree, recordBase, 1, cancellationToken).ConfigureAwait(false);
	}

	private async ValueTask ParallelWriteCoreAsync<T>(IEnumerable<T>? items, IAsyncEnumerable<T>? asyncItems, Action<CsvWriter, T> write, int degree, CancellationToken cancellationToken)
	{
		var source = new ParallelSource<T>(items, asyncItems, cancellationToken);
		try
		{
			await ParallelWriteItemsAsync(source, CallbackItemWriter(write), degree, records, 0, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await source.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Writes the rest of <paramref name="items"/> as <see cref="ParallelWriteItems{T}"/> does, asynchronously.</summary>
	/// <param name="items">The items, positioned before the next one to write.</param>
	/// <param name="write">Writes the records of an item.</param>
	/// <param name="degree">The largest number of workers.</param>
	/// <param name="recordBase">The record count that item 0 starts at on the workers.</param>
	/// <param name="itemsBefore">The number of items already written.</param>
	/// <param name="cancellationToken">Cancels waiting for the workers and writing to the destination.</param>
	private async ValueTask ParallelWriteItemsAsync<T>(ParallelSource<T> items, Action<CsvWriter, T> write, int degree, long recordBase, long itemsBefore, CancellationToken cancellationToken)
	{
		while (fieldCount > 0 || (validate && expectedFields < 0))
		{
			if (!await items.MoveNextAsync().ConfigureAwait(false))
			{
				return;
			}

			await WriteSerialItemAsync(write, items.Current, cancellationToken).ConfigureAwait(false);
			itemsBefore++;
		}

		await new ParallelWriteRun<T>(this, write, degree, recordBase, itemsBefore).RunAsync(items, cancellationToken).ConfigureAwait(false);
	}

	private static Action<CsvWriter, T> RecordItemWriter<T>(CsvRecordWriter<T> recordWriter) =>
		(writer, record) => writer.AddRecord(recordWriter, record);

	private static Action<CsvWriter, T> DynamicItemWriter<T>(string[] names, CsvDynamicSchema? schema) =>
		(writer, record) =>
		{
			writer.WriteDynamicFields(record ?? throw NullRecord("records"), names, schema);
			writer.FinishRecord();
		};

	private static Action<CsvWriter, T> CallbackItemWriter<T>(Action<CsvWriter, T> write) =>
		(writer, item) =>
		{
			write(writer, item);
			if (writer.fieldCount > 0)
			{
				// The next item may be in another batch: its fields cannot continue this record.
				throw new InvalidOperationException(
					"A ParallelWrite callback must end the records it starts (EndRecord). The unfinished record is discarded.");
			}
		};

	/// <summary>Writes the records of an item with this writer, on this thread, as a serial write does.</summary>
	private void WriteSerialItem<T>(Action<CsvWriter, T> write, T item)
	{
		try
		{
			write(this, item);
		}
		catch
		{
			DiscardRecord();
			throw;
		}

		if (pos >= flushThreshold)
		{
			FlushBuffer();
		}
	}

	/// <summary>Writes the records of an item with this writer, on this thread, flushing asynchronously.</summary>
	private ValueTask WriteSerialItemAsync<T>(Action<CsvWriter, T> write, T item, CancellationToken cancellationToken)
	{
		try
		{
			write(this, item);
		}
		catch
		{
			DiscardRecord();
			throw;
		}

		return pos >= flushThreshold ? FlushBufferAsync(cancellationToken) : default;
	}

	/// <summary>A writer for a worker thread: it formats batches into buffers that it hands over, and never flushes.</summary>
	private CsvWriter CreateParallelWorker() => new(options, leaveOpen: true)
	{
		parallelWorker = true,
		flushThreshold = int.MaxValue,
		expectedFields = expectedFields,
		recordWriter = recordWriter,
	};

	/// <summary>Formats the items of <paramref name="batch"/> on this worker writer, into a buffer that it hands to the batch.</summary>
	private void FormatBatch<T>(ParallelWriteBatch<T> batch, ParallelWriteRun<T> run)
	{
		if (buffer.Length == 0)
		{
			buffer = run.TakeBuffer();
		}

		pos = 0;
		recordStart = 0;
		fieldCount = 0;
		var write = run.Write;
		var count = batch.Count;
		// Records are numbered as if every item wrote one, which is exact for records.
		var first = run.RecordBase + batch.FirstItem;
		var finished = 0L;
		var i = 0;
		try
		{
			for (; i < count; i++)
			{
				records = first + i;
				write(this, batch.Item(i));
				finished += records - (first + i);
			}
		}
		catch (Exception ex)
		{
			// The records the item finished stay, as in a serial write; its unfinished one does not.
			finished += records - (first + i);
			DiscardRecord();
			batch.Error = ExceptionDispatchInfo.Capture(ex);
		}

		batch.Completed = i;
		batch.Records = finished;
		batch.Buffer = buffer;
		batch.Length = pos;
		buffer = [];
	}

	/// <summary>
	/// The items of a parallel write. An array or a <see cref="List{T}"/> is handed to the workers in ranges, which they
	/// read in place; other sequences are enumerated by the calling thread into the batches' own arrays.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	private sealed class ParallelSource<T>
	{
		private readonly T[]? array;
		private readonly List<T>? list;
		private readonly int length;
		private readonly IEnumerator<T>? items;
		private readonly IAsyncEnumerator<T>? asyncItems;
		private int next;   // an array or a list: the index of the next item
		private T current = default!;

		/// <summary>Starts enumerating <paramref name="items"/>, or else <paramref name="asyncItems"/>.</summary>
		/// <param name="items">The items, or <see langword="null"/> for <paramref name="asyncItems"/>.</param>
		/// <param name="asyncItems">The items when <paramref name="items"/> is <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the enumeration of <paramref name="asyncItems"/>.</param>
		public ParallelSource(IEnumerable<T>? items, IAsyncEnumerable<T>? asyncItems, CancellationToken cancellationToken)
		{
			if (items is T[] array)
			{
				this.array = array;
				length = array.Length;
			}
			else if (items is List<T> list)
			{
				this.list = list;
				length = list.Count;
			}
			else if (items is not null)
			{
				this.items = items.GetEnumerator();
			}
			else
			{
				this.asyncItems = asyncItems!.GetAsyncEnumerator(cancellationToken);
			}
		}

		/// <summary>Whether the items are an array or a list, which batches take in ranges (<see cref="Take"/>).</summary>
		public bool Indexed => array is not null || list is not null;

		/// <summary>The current item.</summary>
		public T Current => Indexed ? current : items is not null ? items.Current : asyncItems!.Current;

		/// <summary>Moves to the next item of a sequence that is not asynchronous.</summary>
		/// <returns>Whether there is one.</returns>
		public bool MoveNext()
		{
			if (!Indexed)
			{
				return items!.MoveNext();
			}

			if (next >= length)
			{
				return false;
			}

			current = array is not null ? array[next] : list![next];
			next++;

			return true;
		}

		/// <summary>Moves to the next item.</summary>
		/// <returns>Whether there is one.</returns>
		public ValueTask<bool> MoveNextAsync() => asyncItems is not null ? asyncItems.MoveNextAsync() : new ValueTask<bool>(MoveNext());

		/// <summary>Gives <paramref name="batch"/> the next items of an array or a list, up to <paramref name="size"/>, as a range.</summary>
		/// <param name="batch">The batch.</param>
		/// <param name="size">The largest number of items to take.</param>
		/// <returns>Whether the items ended.</returns>
		public bool Take(ParallelWriteBatch<T> batch, int size)
		{
			var count = Math.Min(size, length - next);
			batch.Items = array ?? [];
			batch.List = list;
			batch.Copied = false;
			batch.Offset = next;
			batch.Count = count;
			next += count;

			return next >= length;
		}

		/// <summary>Disposes the enumerator of a sequence that is not asynchronous.</summary>
		public void Dispose()
		{
			items?.Dispose();
		}

		/// <summary>Disposes the enumerator.</summary>
		/// <returns>A task that completes when the enumerator is disposed.</returns>
		public ValueTask DisposeAsync()
		{
			if (asyncItems is not null)
			{
				return asyncItems.DisposeAsync();
			}

			Dispose();

			return default;
		}
	}

	/// <summary>An item that a batch has copied from a sequence.</summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	private struct ParallelItem<T>
	{
		/// <summary>The item.</summary>
		public T Value;
	}

	/// <summary>
	/// A slot of the ring: a batch of items and, once a worker has formatted them, their output. The calling thread fills
	/// it, a worker formats it, and the calling thread writes its output and fills it again.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	private sealed class ParallelWriteBatch<T>
	{
		/// <summary>The array of the source when it is one, which holds the batch's items at <see cref="Offset"/>.</summary>
		public T[] Items = [];

		/// <summary>The list of the source when it is one, which holds the batch's items at <see cref="Offset"/>.</summary>
		public List<T>? List;

		/// <summary>The index of the batch's first item in <see cref="Items"/> or <see cref="List"/>.</summary>
		public int Offset;

		/// <summary>
		/// The batch's own copy of its items, from index 0, when <see cref="Copied"/> is set. A struct array, so that storing
		/// an item does not check its type, which reads the object: a cache miss per item on the calling thread.
		/// </summary>
		public ParallelItem<T>[] Copies = [];

		/// <summary>Whether the items are in <see cref="Copies"/>: the source is neither an array nor a list.</summary>
		public bool Copied;

		/// <summary>The number of items in the batch.</summary>
		public int Count;

		/// <summary>The index of the batch's first item in the whole sequence.</summary>
		public long FirstItem;

		/// <summary>The output of the batch, its first <see cref="Length"/> bytes; empty until the batch is formatted.</summary>
		public byte[] Buffer = [];

		/// <summary>The number of bytes of output in <see cref="Buffer"/>.</summary>
		public int Length;

		/// <summary>The number of records in the output.</summary>
		public long Records;

		/// <summary>
		/// The number of items formatted: <see cref="Count"/>, or fewer when one threw (<see cref="Error"/>).
		/// </summary>
		public int Completed;

		/// <summary>
		/// The exception that item <see cref="Completed"/> threw (or that creating the worker's writer threw); the calling
		/// thread rethrows it after writing the output before it.
		/// </summary>
		public ExceptionDispatchInfo? Error;

		/// <summary>Released when the batch is formatted; the calling thread waits for it before writing the output.</summary>
		public readonly SemaphoreSlim Done = new(0, 1);

		/// <summary>Gets an item of the batch.</summary>
		/// <param name="index">The index of the item in the batch.</param>
		/// <returns>The item.</returns>
		public T Item(int index) => Copied ? Copies[index].Value : List is null ? Items[Offset + index] : List[Offset + index];
	}

	/// <summary>
	/// One parallel write: the ring of batches, the workers that format them, and the buffers and writers they reuse.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	private sealed class ParallelWriteRun<T>
	{
		private readonly CsvWriter owner;
		private readonly int degree;
		private readonly ParallelWriteBatch<T>?[] ring;
		private readonly int bufferLength;
		// Output buffers and worker writers for reuse. Kept here rather than in the shared array pool: workers would
		// return buffers to their own thread's cache, out of reach of the others.
		private readonly Stack<byte[]> freeBuffers = new();
		private readonly Stack<CsvWriter> idleWriters = new();
		private readonly Lock freeLock = new();
		private bool closed;   // guarded by freeLock: the run has ended, so buffers and writers go back to the pool
		// Shared with the workers:
		private int pending;   // published batches that no worker has claimed
		private long claimed;  // the number of batches claimed
		private int active;    // the number of workers that run (or are queued)
		private volatile bool stopping;
		// Used by the calling thread only:
		private long produced;
		private long written;
		private long nextItem;
		private long outputBytes;
		private long outputItems;
		private ExceptionDispatchInfo? sourceError;

		/// <summary>Creates the run; workers start as batches are published.</summary>
		/// <param name="owner">The writer that writes the output to its destination.</param>
		/// <param name="write">Writes the records of an item, on a worker's writer.</param>
		/// <param name="degree">The largest number of workers.</param>
		/// <param name="recordBase">The record count that item 0 starts at on the workers.</param>
		/// <param name="firstItem">The index of the next item in the whole sequence.</param>
		public ParallelWriteRun(CsvWriter owner, Action<CsvWriter, T> write, int degree, long recordBase, long firstItem)
		{
			this.owner = owner;
			this.degree = degree;
			Write = write;
			RecordBase = recordBase;
			nextItem = firstItem;
			ring = new ParallelWriteBatch<T>?[ParallelSlotsPerWorker * degree];
			bufferLength = Math.Max(owner.options.BufferSize, 2 * ParallelBatchBytes);
		}

		/// <summary>Writes the records of an item, on a worker's writer.</summary>
		public Action<CsvWriter, T> Write { get; }

		/// <summary>The record count that item 0 starts at on the workers; item <c>n</c> starts at this plus <c>n</c>.</summary>
		public long RecordBase { get; }

		/// <summary>Writes the rest of <paramref name="items"/>.</summary>
		/// <param name="items">The items, positioned before the next one to write.</param>
		public void Run(ParallelSource<T> items)
		{
			var first = SlotToFill(0);
			if (Fill(first, items, ParallelFirstBatchItems))
			{
				// It all fits in one batch, which is not worth handing over.
				for (var i = 0; i < first.Count; i++)
				{
					owner.WriteSerialItem(Write, first.Item(i));
				}

				sourceError?.Throw();

				return;
			}

			try
			{
				Publish(first);
				var ended = false;
				while (true)
				{
					while (!ended && produced - written < ring.Length)
					{
						var batch = SlotToFill(produced);
						ended = Fill(batch, items, BatchSize());
						if (batch.Count > 0)
						{
							Publish(batch);
						}

						// Batches finished in the meantime go out now, not once the ring is full.
						while (written < produced && Slot(written).Done.Wait(0))
						{
							var done = Slot(written);
							written++;
							WriteOut(done);
						}
					}

					if (written == produced)
					{
						break;
					}

					var head = Slot(written);
					head.Done.Wait();
					written++;
					WriteOut(head);
				}
			}
			finally
			{
				Stop();
			}

			sourceError?.Throw();
		}

		/// <summary>Writes the rest of <paramref name="items"/>, enumerating them and writing the output asynchronously.</summary>
		/// <param name="items">The items, positioned before the next one to write.</param>
		/// <param name="cancellationToken">Cancels waiting for the workers and writing to the destination.</param>
		/// <returns>A task that completes when the output of every item has been written.</returns>
		public async ValueTask RunAsync(ParallelSource<T> items, CancellationToken cancellationToken)
		{
			var first = SlotToFill(0);
			if (await FillAsync(first, items, ParallelFirstBatchItems).ConfigureAwait(false))
			{
				for (var i = 0; i < first.Count; i++)
				{
					await owner.WriteSerialItemAsync(Write, first.Item(i), cancellationToken).ConfigureAwait(false);
				}

				sourceError?.Throw();

				return;
			}

			try
			{
				Publish(first);
				var ended = false;
				while (true)
				{
					while (!ended && produced - written < ring.Length)
					{
						var batch = SlotToFill(produced);
						ended = await FillAsync(batch, items, BatchSize()).ConfigureAwait(false);
						if (batch.Count > 0)
						{
							Publish(batch);
						}

						while (written < produced && Slot(written).Done.Wait(0))
						{
							var done = Slot(written);
							written++;
							await WriteOutAsync(done, cancellationToken).ConfigureAwait(false);
						}
					}

					if (written == produced)
					{
						break;
					}

					var head = Slot(written);
					await head.Done.WaitAsync(cancellationToken).ConfigureAwait(false);
					written++;
					await WriteOutAsync(head, cancellationToken).ConfigureAwait(false);
				}
			}
			finally
			{
				await StopAsync().ConfigureAwait(false);
			}

			sourceError?.Throw();
		}

		/// <summary>A free output buffer for a worker.</summary>
		/// <returns>A buffer that the calling thread has written out, or one rented from the shared pool.</returns>
		public byte[] TakeBuffer()
		{
			lock (freeLock)
			{
				if (freeBuffers.TryPop(out var buffer))
				{
					return buffer;
				}
			}

			return ArrayPool<byte>.Shared.Rent(bufferLength);
		}

		/// <summary>The slot of the batch with the given sequence number, created on first use (by the calling thread).</summary>
		private ParallelWriteBatch<T> SlotToFill(long sequence) => ring[(int)(sequence % ring.Length)] ??= new ParallelWriteBatch<T>();

		/// <summary>The slot of a batch that has been published.</summary>
		private ParallelWriteBatch<T> Slot(long sequence) => ring[(int)(sequence % ring.Length)]!;

		/// <summary>
		/// Fills <paramref name="batch"/> with up to <paramref name="size"/> items. Returns whether the items ended; an
		/// exception from the enumeration ends them too, and is thrown once the items before it are written.
		/// </summary>
		private bool Fill(ParallelWriteBatch<T> batch, ParallelSource<T> items, int size)
		{
			if (items.Indexed)
			{
				return items.Take(batch, size);
			}

			var array = Reserve(batch, size);
			var n = 0;
			var more = true;
			try
			{
				while (n < size && (more = items.MoveNext()))
				{
					array[n++].Value = items.Current;
				}
			}
			catch (Exception ex)
			{
				sourceError = ExceptionDispatchInfo.Capture(ex);
				more = false;
			}

			batch.Count = n;

			return !more;
		}

		/// <summary>Fills <paramref name="batch"/> as <see cref="Fill"/> does, enumerating asynchronously.</summary>
		private async ValueTask<bool> FillAsync(ParallelWriteBatch<T> batch, ParallelSource<T> items, int size)
		{
			if (items.Indexed)
			{
				return items.Take(batch, size);
			}

			var array = Reserve(batch, size);
			var n = 0;
			var more = true;
			try
			{
				while (n < size && (more = await items.MoveNextAsync().ConfigureAwait(false)))
				{
					array[n++].Value = items.Current;
				}
			}
			catch (Exception ex)
			{
				sourceError = ExceptionDispatchInfo.Capture(ex);
				more = false;
			}

			batch.Count = n;

			return !more;
		}

		/// <summary>The batch's own array for at least <paramref name="size"/> items.</summary>
		private static ParallelItem<T>[] Reserve(ParallelWriteBatch<T> batch, int size)
		{
			if (batch.Copies.Length < size)
			{
				batch.Copies = new ParallelItem<T>[Math.Min(Math.Max(size, batch.Copies.Length * 2), ParallelMaxBatchItems)];
			}

			batch.Copied = true;

			return batch.Copies;
		}

		/// <summary>The number of items for the next batch: as many as make about <see cref="ParallelBatchBytes"/> of output.</summary>
		private int BatchSize()
		{
			if (outputBytes == 0)
			{
				return outputItems == 0 ? ParallelFirstBatchItems : ParallelMaxBatchItems;
			}

			return (int)Math.Min(Math.Max(ParallelBatchBytes * outputItems / outputBytes, 1), ParallelMaxBatchItems);
		}

		/// <summary>Hands <paramref name="batch"/> to the workers, starting one if fewer than <see cref="degree"/> run.</summary>
		private void Publish(ParallelWriteBatch<T> batch)
		{
			batch.FirstItem = nextItem;
			nextItem += batch.Count;
			produced++;
			// The increment publishes the batch: the worker that claims it sees what was written to it.
			Interlocked.Increment(ref pending);
			if (TryJoin())
			{
#if NET
				ThreadPool.UnsafeQueueUserWorkItem(static r => r.Work(), this, preferLocal: false);
#else
				ThreadPool.UnsafeQueueUserWorkItem(static r => ((ParallelWriteRun<T>)r!).Work(), this);
#endif
			}
		}

		/// <summary>Counts one more worker, unless <see cref="degree"/> already run.</summary>
		private bool TryJoin()
		{
			while (true)
			{
				var count = Volatile.Read(ref active);
				if (count >= degree)
				{
					return false;
				}

				if (Interlocked.CompareExchange(ref active, count + 1, count) == count)
				{
					return true;
				}
			}
		}

		/// <summary>Takes the next published batch that no worker has claimed, if there is one.</summary>
		private bool TryClaim(out ParallelWriteBatch<T> batch)
		{
			while (true)
			{
				var count = Volatile.Read(ref pending);
				if (count == 0)
				{
					batch = null!;

					return false;
				}

				if (Interlocked.CompareExchange(ref pending, count - 1, count) == count)
				{
					break;
				}
			}

			// Batches are published and claimed in order, so the one with this number is published.
			batch = Slot(Interlocked.Increment(ref claimed) - 1);

			return true;
		}

		/// <summary>A worker: formats batches while there are any, then leaves.</summary>
		private void Work()
		{
			var writer = TakeWriter(out var failure);
			while (true)
			{
				if (TryClaim(out var batch))
				{
					if (writer is null)
					{
						batch.Error = failure;
					}
					else if (!stopping)
					{
						try
						{
							writer.FormatBatch(batch, this);
						}
						catch (Exception ex)
						{
							// Renting a buffer failed: the calling thread must not wait for this batch forever.
							batch.Error = ExceptionDispatchInfo.Capture(ex);
						}
					}

					batch.Done.Release();
					continue;
				}

				// Nothing waits: leave, unless a batch came in meanwhile and no other worker can take it. A batch published
				// before the decrement saw this worker running, so it is this worker's to take.
				Interlocked.Decrement(ref active);
				if (Volatile.Read(ref pending) == 0 || !TryJoin())
				{
					break;
				}
			}

			if (writer is not null)
			{
				ReturnWriter(writer);
			}
		}

		/// <summary>Writes the output of a formatted batch to the destination, then throws its exception, if it has one.</summary>
		private void WriteOut(ParallelWriteBatch<T> batch)
		{
			var error = batch.Error;
			try
			{
				if (batch.Length > 0)
				{
					// What the writer buffered (the header, the first record) goes before the first batch.
					owner.FlushBuffer();
					owner.WriteToDestination(batch.Buffer, batch.Length);
				}

				owner.records += batch.Records;
			}
			finally
			{
				Recycle(batch);
			}

			error?.Throw();
		}

		/// <summary>Writes the output of a formatted batch as <see cref="WriteOut"/> does, asynchronously.</summary>
		private async ValueTask WriteOutAsync(ParallelWriteBatch<T> batch, CancellationToken cancellationToken)
		{
			var error = batch.Error;
			try
			{
				if (batch.Length > 0)
				{
					await owner.FlushBufferAsync(cancellationToken).ConfigureAwait(false);
					await owner.WriteToDestinationAsync(batch.Buffer, batch.Length, cancellationToken).ConfigureAwait(false);
				}

				owner.records += batch.Records;
			}
			finally
			{
				Recycle(batch);
			}

			error?.Throw();
		}

		/// <summary>Counts the output of a batch for the size of the next ones and frees its slot.</summary>
		private void Recycle(ParallelWriteBatch<T> batch)
		{
			outputBytes += batch.Length;
			outputItems += batch.Completed;
			if (batch.Buffer.Length > 0)
			{
				ReturnBuffer(batch.Buffer);
			}

			batch.Buffer = [];
			batch.Length = 0;
			batch.Records = 0;
			batch.Completed = 0;
			batch.Error = null;
		}

		/// <summary>
		/// Ends the run: the workers skip the batches that are left, and the calling thread waits for those still being
		/// formatted, which use their buffers.
		/// </summary>
		private void Stop()
		{
			stopping = true;
			for (; written < produced; written++)
			{
				var batch = Slot(written);
				batch.Done.Wait();
				Recycle(batch);
			}

			Close();
		}

		/// <summary>Ends the run as <see cref="Stop"/> does, waiting asynchronously.</summary>
		private async ValueTask StopAsync()
		{
			stopping = true;
			for (; written < produced; written++)
			{
				var batch = Slot(written);
				await batch.Done.WaitAsync().ConfigureAwait(false);
				Recycle(batch);
			}

			Close();
		}

		/// <summary>Returns the free buffers and the idle writers to the pool; workers that leave later return their own.</summary>
		private void Close()
		{
			lock (freeLock)
			{
				closed = true;
				while (freeBuffers.TryPop(out var buffer))
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}

				while (idleWriters.TryPop(out var writer))
				{
					writer.Release();
				}
			}
		}

		private void ReturnBuffer(byte[] buffer)
		{
			lock (freeLock)
			{
				if (!closed)
				{
					freeBuffers.Push(buffer);

					return;
				}
			}

			ArrayPool<byte>.Shared.Return(buffer);
		}

		/// <summary>An idle worker writer, or a new one; <see langword="null"/> with the exception when creating it threw.</summary>
		private CsvWriter? TakeWriter(out ExceptionDispatchInfo? failure)
		{
			failure = null;
			lock (freeLock)
			{
				if (idleWriters.TryPop(out var idle))
				{
					return idle;
				}
			}

			try
			{
				return owner.CreateParallelWorker();
			}
			catch (Exception ex)
			{
				failure = ExceptionDispatchInfo.Capture(ex);

				return null;
			}
		}

		private void ReturnWriter(CsvWriter writer)
		{
			lock (freeLock)
			{
				if (!closed)
				{
					idleWriters.Push(writer);

					return;
				}
			}

			writer.Release();
		}
	}
}
