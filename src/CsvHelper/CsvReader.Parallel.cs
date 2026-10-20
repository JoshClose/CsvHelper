using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
#if !NET
using Lock = System.Object;
#endif

namespace CsvHelper;

// ParallelEnumerate: workers take turns, under a lock, to read and scan the next batch of whole records from
// this reader, and then run the selector over that batch themselves while its bytes are still in their cache.
// A batch owns its bytes and its part of the index (handed over without copying) until it is processed; then
// they go back to free lists for the reader. Results wait in a deep ring of slots that the enumerating thread
// drains in input order, so a batch held up at the head does not stop the other workers. Everything that decides
// which records exist (empty lines, comments, malformed data, field counts) runs under the lock exactly as in
// Read, one batch after the other, so callbacks run one at a time in input order, and exceptions are thrown by
// the enumerator in input order.
public sealed partial class CsvReader
{
	/// <summary>Input bytes scanned for each batch.</summary>
	private const int ParallelBatchBytes = 256 * 1024;

	/// <summary>Ring slots per worker: how many batches the workers may run ahead of the enumerator.</summary>
	private const int SlotsPerWorker = 6;

	// Set on the readers that ParallelEnumerate passes to the selector.
	private bool parallelWorker;

	/// <summary>
	/// Maps the remaining records with <paramref name="selector"/> on several threads and returns the
	/// results in input order.
	/// </summary>
	/// <remarks>
	/// The selector gets a reader positioned on one record. Use its field accessors (<see cref="GetString(int)"/>,
	/// <see cref="GetSpan(int)"/>, <see cref="Parse{T}(int, Func{string, IFormatProvider, T})"/> and so on) and return a value; the reader and its
	/// spans are only valid during the call, and <see cref="Read"/> cannot be called on it. Calls run concurrently
	/// on different threads, each thread with its own reader. The input is read and checked by one thread at a
	/// time, in order, so <see cref="CsvReaderOptions.OnMalformedRecord"/> is never called concurrently. Exceptions
	/// from the reader or the selector are thrown by the enumerator after the results of the preceding records.
	/// This reader must not be used otherwise until the enumeration ends.
	/// </remarks>
	/// <typeparam name="T">The type of the results.</typeparam>
	/// <param name="selector">Maps the current record of the given reader to a result.</param>
	/// <param name="maxDegreeOfParallelism">The number of worker threads, or -1 for one per processor.</param>
	/// <returns>
	/// The results in input order. The workers start when the enumeration starts, and stop when it ends or is
	/// disposed. The sequence can be enumerated once.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">This reader is the one a selector got.</exception>
	public IEnumerable<T> ParallelEnumerate<T>(Func<CsvReader, T> selector, int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(selector);
		if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Must be positive or -1.");
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		var degree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

		return ParallelIterator(selector, degree);
	}

	private IEnumerable<T> ParallelIterator<T>(Func<CsvReader, T> selector, int degree)
	{
		if (!headerDone && !EnsureHeader()) yield break;
		var run = new ParallelRun<T>(this, selector, degree);
		var next = 0L;
		try
		{
			while (true)
			{
				var batch = run.Slot(next);
				batch.Done.Wait();
				if (batch.End) yield break;
				var results = batch.Results;
				for (var i = 0; i < batch.Completed; i++) yield return results[i];
				batch.SelectorError?.Throw();
				batch.ReadError?.Throw();
				run.Consumed(batch);
				next++;
			}
		}
		finally
		{
			run.Stop(next);
		}
	}

	/// <summary>
	/// Fills <paramref name="batch"/> with the next records (at least one). Returns false at the end of the
	/// input. An exception from the reader is stored in the batch, after the records before it.
	/// </summary>
	private bool NextBatch<T>(ParallelBatch<T> batch, ParallelRun<T> run)
	{
		batch.FirstRow = rowNumber + 1;
		batch.FirstRecord = recordNumber + 1;
		if (batch.Records.Length == 0) batch.Records = run.TakeRecords();
		var n = 0;
		try
		{
			while (true)
			{
				var records = batch.Records;
				while (TryNextRecord())
				{
					rowNumber++;
					if (2 * n + 2 > records.Length) records = batch.Records = Grow(records);
					records[2 * n] = fieldBase;
					records[2 * n + 1] = fieldCount;
					if (trackLines)
					{
						if (batch.Lines.Length <= n)
						{
							Array.Resize(ref batch.Lines, Math.Max(n * 2, 512));
						}

						batch.Lines[n] = lineNumber;
					}

					n++;
				}

				if (n > 0 || finished) break;
				ScanBatch();
			}
		}
		catch (Exception ex)
		{
			batch.ReadError = ExceptionDispatchInfo.Capture(ex);
		}

		if (n == 0 && batch.ReadError is null) return false;
		batch.Count = n;
		if (batch.Results.Length < n) batch.Results = new T[Math.Max(n, batch.Results.Length * 2)];
		// The input before the batch's buffer, for ByteOffset on the worker (detaching moves it on).
		batch.Discarded = discarded;
		DetachBatchInput(batch, run);

		return true;

		static int[] Grow(int[] records)
		{
			var bigger = new int[Math.Max(records.Length * 2, 1024)];
			records.CopyTo(bigger, 0);

			return bigger;
		}
	}

	/// <summary>Reads until the buffer is full (or the input ends) and scans it, until it holds a whole record.</summary>
	private void ScanBatch()
	{
		while (true)
		{
			// Checked each time: encoding detection can turn in-memory input into a transcoding source.
			var memory = sourceKind == SourceKind.Memory;
			if (!memory)
			{
				if (buffer.Length < ParallelBatchBytes) GrowBuffer(ParallelBatchBytes);
				while (!eof && buffer.Length - dataEnd >= 1024) ReadMore();
			}

			if (!initialized && !TryInitialize()) continue;
			// In-memory input is one buffer: scan it a batch at a time.
			var end = memory ? (int)Math.Min(dataEnd, (long)scanPos + ParallelBatchBytes) : dataEnd;
			while (end - scanPos >= BlockSize) ScanAvailable(end);
			if (eof && dataEnd - scanPos < BlockSize)
			{
				if (!finished) ScanFinalBlock();

				return;
			}

			if (rowCount > rowIndex) return;
			// Not even one record fits: make room for it.
			if (!memory)
			{
				// The scanned bytes of the pending record have no record terminator: they are all part of it.
				if (maxRecordSize > 0 && scanPos - (ends[rowBase] & int.MaxValue) > maxRecordSize)
				{
					ThrowRecordTooLarge();
				}

				GrowBuffer(buffer.Length * 2);
			}
		}
	}

	private void GrowBuffer(int size)
	{
		var bigger = ArrayPool<byte>.Shared.Rent(size);
		Buffer.BlockCopy(buffer, 0, bigger, 0, dataEnd);
		if (bufferRented) ArrayPool<byte>.Shared.Return(buffer);
		buffer = bigger;
		bufferRented = true;
	}

	/// <summary>
	/// Gives the buffer and the index of the batch's records to <paramref name="batch"/>, and continues with free
	/// buffers from <paramref name="run"/>, which receive the pending (incomplete) record.
	/// </summary>
	private void DetachBatchInput<T>(ParallelBatch<T> batch, ParallelRun<T> run)
	{
		var pending = rowBase;
		var pendingCount = endsCount - pending;
		var ends = run.TakeIndex(this.ends.Length);
		batch.Ends = this.ends;
		if (sourceKind == SourceKind.Memory)
		{
			// Every batch reads the same input array in place: only the index moves.
			Array.Copy(this.ends, pending, ends, 0, pendingCount);
			batch.Buffer = buffer;
			batch.OwnsBuffer = false;
		}
		else
		{
			// At the end of the input the last record can end one past the data (no final line break).
			var keepFrom = Math.Min(this.ends[pending] & int.MaxValue, dataEnd);
			var keep = dataEnd - keepFrom;
			var buffer = run.TakeBuffer(this.buffer.Length);
			Buffer.BlockCopy(this.buffer, keepFrom, buffer, 0, keep);
			for (var i = 0; i < pendingCount; i++) ends[i] = this.ends[pending + i] - keepFrom;
			batch.Buffer = this.buffer;
			batch.OwnsBuffer = bufferRented;
			this.buffer = buffer;
			bufferRented = true;
			dataEnd = keep;
			scanPos -= keepFrom;
			dataStart = Math.Max(0, dataStart - keepFrom);
			for (var i = badIndex; i < badCount; i++) badPos[i] -= keepFrom;
			if (nextBadPos != int.MaxValue) nextBadPos -= keepFrom;
			discarded += keepFrom;
		}

		this.ends = ends;
		endsCount = pendingCount;
		rowBase = 0;
		rowCount = 0;
		rowIndex = 0;
	}

	/// <summary>A reader for a worker thread: it serves the records of the batches given to it.</summary>
	private CsvReader CreateParallelWorker() => new(options, rentIndex: false, sharedPool: pool)
	{
		parallelWorker = true,
		sourceKind = SourceKind.Memory,
		sepByte = sepByte,
		separatorKnown = true,
		initialized = true,
		eof = true,
		finished = true,
		headerDone = true,
		headers = headers,
		headerKeys = headerKeys,
		headerIndex = headerIndex,
		headerIndexLoose = headerIndexLoose,
	};

	/// <summary>Runs the selector for every record of the batch, on this worker reader.</summary>
	private void ProcessBatch<T>(ParallelBatch<T> batch, Func<CsvReader, T> selector)
	{
		buffer = batch.Buffer;
		ends = batch.Ends;
		discarded = batch.Discarded;
		var records = batch.Records;
		var results = batch.Results;
		var count = batch.Count;
		var i = 0;
		try
		{
			for (; i < count; i++)
			{
				fieldBase = records[2 * i];
				fieldCount = records[2 * i + 1];
				rowNumber = batch.FirstRow + i;
				recordNumber = batch.FirstRecord + i;
				if (trackLines)
				{
					lineNumber = batch.Lines[i];
				}

				scratchPos = 0;
				results[i] = selector(this);
			}
		}
		catch (Exception ex)
		{
			batch.SelectorError = ExceptionDispatchInfo.Capture(ex);
		}

		batch.Completed = i;
		buffer = [];
		ends = [];
		fieldCount = 0;
	}

	private void ThrowIfParallelWorker()
	{
		if (parallelWorker)
			throw new InvalidOperationException("The reader passed to a ParallelEnumerate selector is positioned by the enumeration; it cannot be advanced.");
	}

	/// <summary>
	/// A slot of the ring: one batch of records and its results. Its input (bytes, index and record positions) is
	/// only held while the batch waits to be processed and while it is processed.
	/// </summary>
	/// <typeparam name="T">The type of the selector's results.</typeparam>
	private sealed class ParallelBatch<T>
	{
		/// <summary>
		/// The bytes of the batch's records, at the positions in <see cref="Ends"/>; empty once the batch is processed.
		/// </summary>
		public byte[] Buffer = [];

		/// <summary>
		/// Whether <see cref="Buffer"/> is one of the reader's own buffers (which goes back to the run's free list),
		/// not the caller's input array.
		/// </summary>
		public bool OwnsBuffer;

		/// <summary>
		/// The reader's field index for the batch's records (see <c>ends</c> in the reader); empty once the batch is
		/// processed.
		/// </summary>
		public int[] Ends = [];

		/// <summary>
		/// The field base and the field count of each record, two entries per record; empty once the batch is
		/// processed.
		/// </summary>
		public int[] Records = [];

		/// <summary>The number of records in the batch.</summary>
		public int Count;

		/// <summary>
		/// The number of records the selector has processed: <see cref="Count"/>, or fewer when it threw
		/// (<see cref="SelectorError"/>).
		/// </summary>
		public int Completed;

		/// <summary>The row number (<see cref="CsvReader.RowNumber"/>) of the batch's first record.</summary>
		public long FirstRow;

		/// <summary>The record number of the batch's first record.</summary>
		public long FirstRecord;

		/// <summary>The bytes of input before <see cref="Buffer"/> (the reader's <c>discarded</c>), for <see cref="CsvReader.ByteOffset"/>.</summary>
		public long Discarded;

		/// <summary>With line numbers tracked, the line where each record starts (<see cref="CsvReader.LineNumber"/>).</summary>
		public long[] Lines = [];

		/// <summary>The selector's result for each record; the first <see cref="Completed"/> entries are set.</summary>
		public T[] Results = [];

		/// <summary>
		/// The exception that the reader threw while reading the batch, after the records before it; the enumerator
		/// rethrows it after their results.
		/// </summary>
		public ExceptionDispatchInfo? ReadError;

		/// <summary>
		/// The exception that the selector threw for record <see cref="Completed"/> (or that creating the worker
		/// reader threw); the enumerator rethrows it after the results before it.
		/// </summary>
		public ExceptionDispatchInfo? SelectorError;

		/// <summary>Whether the slot holds no batch because the input ended.</summary>
		public bool End;

		/// <summary>Set when the batch is processed (or <see cref="End"/> is set); the enumerator waits for it.</summary>
		public readonly ManualResetEventSlim Done = new(false);
	}

	/// <summary>The workers of one enumeration, the ring of batches they fill and the input buffers they share.</summary>
	/// <typeparam name="T">The type of the selector's results.</typeparam>
	private sealed class ParallelRun<T>
	{
		private readonly CsvReader owner;
		private readonly Func<CsvReader, T> selector;
		private readonly int degree;
		private readonly ParallelBatch<T>[] ring;
		private readonly SemaphoreSlim freeSlots;
		private readonly Lock readLock = new();
		// Guarded by readLock:
		private long produced;
		private bool ended;
		private int started;
		private volatile bool stopping;
		// Input buffers of processed batches, for the next batches to read into. Kept here rather than in the
		// shared array pool: workers would return them to their own thread's cache, out of reach of the reader.
		private readonly Stack<byte[]> freeBuffers = new();
		private readonly Stack<int[]> freeIndexes = new();
		private readonly Stack<int[]> freeRecords = new();
		private readonly Lock freeLock = new();

		/// <summary>Creates the ring and starts the first worker; the others start as batches appear.</summary>
		/// <param name="owner">The reader whose records are enumerated; the workers read from it in turn, under a lock.</param>
		/// <param name="selector">The function that maps each record.</param>
		/// <param name="degree">The largest number of workers.</param>
		public ParallelRun(CsvReader owner, Func<CsvReader, T> selector, int degree)
		{
			this.owner = owner;
			this.selector = selector;
			this.degree = degree;
			// Results wait in the ring until the enumerator takes them in order. A deep ring lets the other workers
			// carry on while the batch at its head is held up (by a GC pause, or a thread that lost its processor);
			// a slot costs little, as its input buffers go back to the free lists once it is processed.
			ring = new ParallelBatch<T>[SlotsPerWorker * degree];
			for (var i = 0; i < ring.Length; i++) ring[i] = new ParallelBatch<T>();
			freeSlots = new SemaphoreSlim(ring.Length);
			started = 1;
#if NET
			ThreadPool.UnsafeQueueUserWorkItem(static r => r.Work(), this, preferLocal: false);
#else
			ThreadPool.UnsafeQueueUserWorkItem(static r => ((ParallelRun<T>)r!).Work(), this);
#endif
		}

		/// <summary>The slot of the ring that holds the batch with the given sequence number.</summary>
		/// <param name="sequence">The 0-based number of the batch, in input order.</param>
		/// <returns>The slot, which batches <paramref name="sequence"/> plus each multiple of the ring's length reuse.</returns>
		public ParallelBatch<T> Slot(long sequence) => ring[(int)(sequence % ring.Length)];

		/// <summary>The enumerator is done with the batch: its slot can take the next one.</summary>
		/// <param name="batch">The batch whose results the enumerator has returned.</param>
		public void Consumed(ParallelBatch<T> batch)
		{
#if NET
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) Array.Clear(batch.Results, 0, batch.Count);
#else
			Array.Clear(batch.Results, 0, batch.Count);
#endif
			batch.Count = batch.Completed = 0;
			batch.ReadError = batch.SelectorError = null;
			batch.Done.Reset();
			freeSlots.Release();
		}

		/// <summary>A free buffer of at least <paramref name="length"/> bytes for the reader (called by the reading worker).</summary>
		/// <param name="length">The smallest length the buffer may have.</param>
		/// <returns>A buffer of a processed batch, or one rented from the shared pool.</returns>
		public byte[] TakeBuffer(int length) => Take(freeBuffers, length);

		/// <summary>A free index of at least <paramref name="length"/> entries for the reader.</summary>
		/// <param name="length">The smallest length the index may have.</param>
		/// <returns>The index of a processed batch, or one rented from the shared pool.</returns>
		public int[] TakeIndex(int length) => Take(freeIndexes, length);

		private TItem[] Take<TItem>(Stack<TItem[]> free, int length)
		{
			lock (freeLock)
			{
				while (free.TryPop(out var array))
				{
					if (array.Length >= length) return array;
					ArrayPool<TItem>.Shared.Return(array);   // too small since the reader's buffer grew
				}
			}

			return ArrayPool<TItem>.Shared.Rent(length);
		}

		/// <summary>A free list of record positions for the next batch, or an empty array.</summary>
		/// <returns>The record positions of a processed batch, or an empty array that the reader grows.</returns>
		public int[] TakeRecords()
		{
			lock (freeLock) return freeRecords.TryPop(out var records) ? records : [];
		}

		/// <summary>Takes back the input buffers and record positions of a processed batch.</summary>
		private void ReleaseInput(ParallelBatch<T> batch)
		{
			lock (freeLock)
			{
				if (batch.OwnsBuffer) freeBuffers.Push(batch.Buffer);
				if (batch.Ends.Length > 0) freeIndexes.Push(batch.Ends);
				if (batch.Records.Length > 0) freeRecords.Push(batch.Records);
			}

			batch.Buffer = [];
			batch.Ends = [];
			batch.Records = [];
			batch.OwnsBuffer = false;
		}

		/// <summary>
		/// Ends the enumeration: stops the workers, waits for the batches still being processed (which use their
		/// buffers) and returns the free buffers to the shared pool.
		/// </summary>
		/// <param name="next">The sequence number of the first batch the enumerator has not consumed.</param>
		public void Stop(long next)
		{
			stopping = true;
			freeSlots.Release(degree);   // wakes workers waiting for a slot, so they see the stop and leave
			long last;
			lock (readLock) last = produced;   // waits for a batch being read; none starts after this
			for (var s = next; s < last; s++) Slot(s).Done.Wait();
			lock (freeLock)
			{
				while (freeBuffers.TryPop(out var buffer)) ArrayPool<byte>.Shared.Return(buffer);
				while (freeIndexes.TryPop(out var index)) ArrayPool<int>.Shared.Return(index);
			}
		}

		private void Work()
		{
			CsvReader? reader = null;
			ExceptionDispatchInfo? failure = null;
			try
			{
				reader = owner.CreateParallelWorker();
			}
			catch (Exception ex)
			{
				failure = ExceptionDispatchInfo.Capture(ex);
			}

			while (true)
			{
				freeSlots.Wait();
				ParallelBatch<T> batch;
				lock (readLock)
				{
					if (ended || stopping)
					{
						freeSlots.Release();   // pass it on: other workers may be waiting to see this

						return;
					}

					batch = Slot(produced);
					if (!owner.NextBatch(batch, this))
					{
						// The input ended: this slot tells the enumerator.
						ended = true;
						batch.End = true;
						batch.Done.Set();

						return;
					}

					produced++;
					if (batch.ReadError is not null) ended = true;
					// Workers start as batches appear, so short inputs do not wake every thread.
					if (started < degree && !ended)
					{
						started++;
#if NET
						ThreadPool.UnsafeQueueUserWorkItem(static r => r.Work(), this, preferLocal: false);
#else
						ThreadPool.UnsafeQueueUserWorkItem(static r => ((ParallelRun<T>)r!).Work(), this);
#endif
					}
				}

				if (reader is null) batch.SelectorError = failure;
				else if (!stopping) reader.ProcessBatch(batch, selector);
				ReleaseInput(batch);
				batch.Done.Set();
			}
		}
	}
}
