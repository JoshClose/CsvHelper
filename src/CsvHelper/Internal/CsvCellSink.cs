using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// The output of a <see cref="CsvWriter"/> that is not CSV text, such as the cells of a spreadsheet. In place of writing
/// CSV, the writer hands the sink each field as CSV formats it, before quoting, with the type it was formatted from, and
/// tells it where each record ends, so that generated <see cref="CsvRecordWriter{T}"/>s, converters and dynamic records
/// write cells unchanged.
/// </summary>
/// <remarks>
/// <para>
/// Internal, for CsvHelper.Excel, which depends on an exact version of CsvHelper.
/// </para>
/// <para>
/// Fields come as text, not as typed values, because the writer gets them to the sink without adding code to the
/// methods that CSV record loops inline: any code added there, even a test that is never taken, changed how the JIT
/// compiled those loops (2-8% slower CSV writes). The text round-trips for the default formats: numbers in the invariant
/// culture are the shortest text that reads back as the same value (the text a spreadsheet stores), dates and times are
/// ISO 8601 ("O"), and booleans are <see cref="bool.TrueString"/> and <see cref="bool.FalseString"/>. A field written
/// with a format string, or with a culture, comes as that text; the format string itself does not reach the sink.
/// </para>
/// <para>
/// The writer does not say which record is a header: a sink takes its first record as the header when
/// <see cref="CsvWriterOptions.HasHeader"/> is set, which is where the record methods write one and where a caller of
/// <see cref="CsvWriter.WriteHeader(ReadOnlySpan{string})"/> puts one. Marking headers in <c>WriteHeader</c>, even with a
/// single store, changed how the JIT compiled CSV record loops that call it (up to 7% slower writes).
/// </para>
/// <para>
/// The writer keeps its own rules for records: it counts fields, checks field counts as
/// <see cref="CsvWriterOptions.ValidateFieldCount"/> says (discarding a record that fails), and does I/O only when a
/// record ends and the sink asks to be flushed (<see cref="EndRecord"/>), at <see cref="CsvWriter.Flush"/> and when it
/// is disposed, each in the sync or async form the caller used. The CSV options of the writer do not apply to the
/// fields: no quoting, escapes, padding or injection protection, no null text (a null value is an empty field), and no
/// true and false text. A writer with a sink writes no comments (<see cref="NotSupportedException"/>).
/// </para>
/// <para>
/// The parallel write methods (<see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/>,
/// <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> and their async forms) run on a
/// sink that says so (<see cref="SupportsParallel"/>), and throw <see cref="NotSupportedException"/> on one that doesn't,
/// the default. The calling thread writes the header and the records up to <see cref="ReadyForWorkers"/> through this
/// sink, as a serial write does; then workers format batches of items, each worker's writer with a sink of its own
/// (<see cref="CreateWorker"/>), each batch into a chunk (<see cref="BeginChunk"/>, <see cref="TakeChunk"/>); and the
/// calling thread hands each chunk to this sink in input order (<see cref="WriteChunk"/>), which may refuse one, so that
/// its items are written again through this sink, serially. Each member says which sink it is called on, the owner (the
/// writer's) or a worker's, and on which thread. For the async methods, the calling thread is the call's own flow: its
/// members run one at a time and in order, but not always on one thread.
/// </para>
/// <para>
/// So a sink that takes chunks gets every record of a parallel call once, in input order, either through its own fields
/// and record ends or in a chunk, and what it writes is a serial write's when a chunk appends what the owner would have
/// written for those records (for CsvHelper.Excel, the sheet's XML is the serial write's). Records are numbered as the
/// workers number them (<see cref="BeginChunk"/>), and the items of a refused chunk run twice: a <c>ParallelWrite</c>
/// callback once on the worker's writer and once on the owner's.
/// </para>
/// </remarks>
internal abstract class CsvCellSink
{
	/// <summary>Writes a field.</summary>
	/// <param name="utf8">
	/// The field as CSV formats it, before quoting: empty for a null value or an empty string. Valid UTF-8, except for the
	/// bytes of a UTF-8 field (<see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/>), which the writer does not validate.
	/// </param>
	/// <param name="type">
	/// The type the field was formatted from: <see cref="string"/> for text (strings, spans of chars and UTF-8, and a null
	/// string or URI), the type of a formattable value (a number, a date or time, <see cref="Guid"/>, ...) or of a nullable
	/// one that is null, <see cref="bool"/>, <see cref="Uri"/> for a URI, and the runtime type of a value of
	/// <see cref="CsvWriter.WriteField(object)"/> (<see cref="object"/> when it is null).
	/// </param>
	public abstract void WriteField(ReadOnlySpan<byte> utf8, Type type);

	/// <summary>Ends the current record, which has at least one field.</summary>
	/// <returns>
	/// Whether the sink has buffered enough to write it out: the writer then calls <see cref="Flush"/> or
	/// <see cref="FlushAsync"/> before the next record, as the caller is sync or async.
	/// </returns>
	public abstract bool EndRecord();

	/// <summary>
	/// Drops the fields written to the current record since it began: the record failed (a field count that does not
	/// match, or an exception while its fields were written), and the writer discards it.
	/// </summary>
	public abstract void DiscardRecord();

	/// <summary>Writes out what the sink has buffered (whole records).</summary>
	public abstract void Flush();

	/// <summary>Asynchronously writes out what the sink has buffered (whole records).</summary>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>A task that completes when the buffered records are written out.</returns>
	public abstract ValueTask FlushAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Finishes the output: the writer is being disposed, after it ended any open record and flushed. Called once.
	/// </summary>
	public abstract void Complete();

	/// <summary>Asynchronously finishes the output. See <see cref="Complete"/>.</summary>
	/// <returns>A task that completes when the output is finished.</returns>
	public abstract ValueTask CompleteAsync();

	/// <summary>
	/// Whether the parallel write methods run on this sink: <see langword="false"/>, the default, and they throw
	/// <see cref="NotSupportedException"/>, as before. A sink that returns <see langword="true"/> takes the members below.
	/// Read on the owner, on the calling thread, as each parallel call checks its arguments.
	/// </summary>
	public virtual bool SupportsParallel => false;

	/// <summary>
	/// A parallel call begins: called on the owner, on the calling thread, once per call, after its arguments are checked
	/// and before it writes the header or enumerates the items. Every call that returns from it reaches
	/// <see cref="EndParallel"/>, however the call ends. Does nothing by default.
	/// </summary>
	/// <param name="degree">The most workers the call runs: its <c>maxDegreeOfParallelism</c>, -1 resolved to <see cref="Environment.ProcessorCount"/>.</param>
	public virtual void BeginParallel(int degree)
	{
	}

	/// <summary>
	/// Whether workers may take the next records: read on the owner, on the calling thread, before each item of the serial
	/// prefix (after the header, and after the records that fix the field count or the columns of dynamic records). While
	/// it is <see langword="false"/>, the item is written serially through the owner, as a serial write writes it; once it
	/// is <see langword="true"/>, the rest go to workers (but for a rest that fits in one batch, which is written serially
	/// too), and it isn't read again in the call. <see langword="true"/> by default.
	/// </summary>
	public virtual bool ReadyForWorkers => true;

	/// <summary>
	/// Workers are about to start: called on the owner, on the calling thread, at most once per call, after the serial
	/// prefix, when the first batch is handed to the workers, and so before the first <see cref="CreateWorker"/>. Not
	/// called when the items end in the prefix, or when the rest fits in one batch, which the calling thread writes
	/// serially instead. After it, the owner still writes serially the items of each chunk <see cref="WriteChunk"/>
	/// refuses, while workers run. Does nothing by default.
	/// </summary>
	public virtual void BeginWorkers()
	{
	}

	/// <summary>
	/// Makes the sink of a worker's writer: called on the owner, on a worker thread (of the thread pool), one call at a
	/// time, about once for each worker that runs at once (<c>degree</c> at most, and a few more when workers leave and
	/// others start before their writers are free), at any time between <see cref="BeginWorkers"/> and
	/// <see cref="EndParallel"/>, also while the calling thread writes through the owner, so it reads only what
	/// <see cref="BeginWorkers"/> made ready. The worker's sink is used by one thread at a time: it
	/// takes the fields and record ends of the batches its writer formats (<see cref="WriteField"/>,
	/// <see cref="EndRecord"/>, whose result is ignored, and <see cref="DiscardRecord"/> when an item fails), framed by
	/// <see cref="BeginChunk"/> and <see cref="TakeChunk"/>, and never <see cref="Flush"/>, <see cref="FlushAsync"/>,
	/// <see cref="Complete"/> or <see cref="CompleteAsync"/>. Each one made reaches <see cref="ReleaseWorker"/> once.
	/// </summary>
	/// <returns>The worker's sink.</returns>
	/// <exception cref="NotSupportedException">The sink doesn't support parallel writes: the default.</exception>
	public virtual CsvCellSink CreateWorker() => throw new NotSupportedException("This sink takes no parallel writes.");

	/// <summary>
	/// A batch begins: called on a worker's sink, on its worker thread, before the batch's first item, and followed by
	/// <see cref="TakeChunk"/> once its items are written, unless it throws: the batch then has no chunk, and its exception
	/// is thrown once the batches before it are written. Does nothing by default.
	/// </summary>
	/// <param name="firstRecord">
	/// The record number of the batch's first item as the parallel write numbers them, as if every item wrote one record:
	/// exact for <c>ParallelWriteRecords</c>.
	/// </param>
	public virtual void BeginChunk(long firstRecord)
	{
	}

	/// <summary>
	/// The batch's output: called on a worker's sink, on its worker thread, after the batch's last item, or after an item
	/// that threw (with the records that ended before it, its own discarded). The chunk is the owner's to take
	/// (<see cref="WriteChunk"/>) or drop (<see cref="DiscardChunk"/>), once.
	/// </summary>
	/// <param name="length">The chunk's size in bytes, by which the parallel write sizes its batches (it aims for 128 KB a batch): 0 or more.</param>
	/// <returns>The chunk, never <see langword="null"/>.</returns>
	/// <exception cref="NotSupportedException">The sink doesn't support parallel writes: the default.</exception>
	public virtual object TakeChunk(out int length) => throw new NotSupportedException("This sink takes no parallel writes.");

	/// <summary>
	/// Appends a worker's chunk: called on the owner, on the calling thread, once per chunk, in input order, after the
	/// owner's own records before it and its <see cref="Flush"/> when it asked for one, and before the worker's exception, if
	/// the batch has one, is thrown. The sink takes the chunk whatever it returns or throws; when it throws, the call ends
	/// with that exception, without counting the batch's records, and the chunks after it are dropped
	/// (<see cref="DiscardChunk"/>).
	/// </summary>
	/// <param name="chunk">A chunk from <see cref="TakeChunk"/> of one of the owner's workers.</param>
	/// <returns>
	/// <see langword="true"/> when the chunk is appended, and the batch's records are counted; <see langword="false"/> when
	/// nothing of it was appended: the parallel write then writes the batch's items again, every one, serially through the
	/// owner's writer (a <c>ParallelWrite</c> callback runs again for each), and drops the worker's exception if it had one.
	/// </returns>
	/// <exception cref="NotSupportedException">The sink doesn't support parallel writes: the default.</exception>
	public virtual bool WriteChunk(object chunk) => throw new NotSupportedException("This sink takes no parallel writes.");

	/// <summary>Appends a worker's chunk as <see cref="WriteChunk"/> does, writing asynchronously: for the async parallel methods.</summary>
	/// <param name="chunk">A chunk from <see cref="TakeChunk"/> of one of the owner's workers.</param>
	/// <param name="cancellationToken">The call's token: cancels writing.</param>
	/// <returns>A task whose result is <see cref="WriteChunk"/>'s.</returns>
	/// <exception cref="NotSupportedException">The sink doesn't support parallel writes: the default.</exception>
	public virtual ValueTask<bool> WriteChunkAsync(object chunk, CancellationToken cancellationToken) => throw new NotSupportedException("This sink takes no parallel writes.");

	/// <summary>
	/// Drops a chunk that won't be appended: the call ended (an exception, a cancellation) before its turn. Called on the
	/// owner, on any thread, before <see cref="EndParallel"/>. Each chunk from <see cref="TakeChunk"/> reaches exactly one of
	/// <see cref="WriteChunk"/>, <see cref="WriteChunkAsync"/> and this. It must not throw: the call is stopping its
	/// workers. Does nothing by default.
	/// </summary>
	/// <param name="chunk">The chunk.</param>
	public virtual void DiscardChunk(object chunk)
	{
	}

	/// <summary>
	/// A worker's sink is done: called on the owner, on any thread, once for each <see cref="CreateWorker"/>, and maybe
	/// after the call has returned (and after <see cref="EndParallel"/>), so it touches only the worker's own state. It must
	/// not throw: on a worker thread, an exception would end the process. Does nothing by default.
	/// </summary>
	/// <param name="worker">The worker's sink, from <see cref="CreateWorker"/>.</param>
	public virtual void ReleaseWorker(CsvCellSink worker)
	{
	}

	/// <summary>
	/// A parallel call ends: called on the owner, on the calling thread, once per <see cref="BeginParallel"/>, in the call's
	/// <c>finally</c>, after every batch was appended, written again or dropped. Worker sinks may still be released after
	/// it. Does nothing by default.
	/// </summary>
	public virtual void EndParallel()
	{
	}
}
