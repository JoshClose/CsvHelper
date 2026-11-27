#if NET
using System;
using System.Buffers;
using System.Threading;

namespace CsvHelper.Excel;

/// <summary>
/// The deflate stage of a chunked entry on net8.0 and net10.0 (<see cref="DeflateStage"/>): the entry's data is cut at every
/// <see cref="ZipWriter.ChunkSize"/> bytes of it, by byte count, and each chunk is deflated on its own by a stage thread
/// (<see cref="ChunkDeflater"/>), so that the bytes are those of the tests' threadless stage: they depend on the data alone,
/// not on how its appends were cut, the thread count or timing.
/// </summary>
/// <remarks>
/// <para>
/// The data isn't copied on the writing thread: a segment appended (<see cref="Append(OwnedSegment)"/>) is held by
/// reference, and a chunk's pieces point into the segments it takes bytes from; a segment across a cut belongs to both
/// chunks, and the last to release it returns it (<see cref="HeldSegment"/>). Bytes appended as a span are copied into
/// pooled segments of the stage's own, <see cref="DeflateSizes.WriteSize"/> bytes each, filled one after the other.
/// </para>
/// <para>
/// Each thread gathers its chunk's pieces into a pooled buffer of its own (a chunk of one piece is deflated where it is),
/// releases them, and deflates the buffer with a <see cref="ChunkDeflater"/> of its own. The first chunk cut starts the
/// first thread, and so an entry that never fills a chunk starts none: its one chunk is deflated on the writing thread in
/// <see cref="Complete"/>. Once a thread has started, the last chunk goes to the threads too, so that the writing thread
/// writes the chunks before it as they finish, unless it is empty (<c>03 00</c>, made on the writing thread).
/// </para>
/// </remarks>
internal sealed class ChunkedDeflateStage : ThreadedDeflateStage
{
	/// <summary>The size of the segments the stage copies bytes appended as a span into.</summary>
	private const int CopySize = DeflateSizes.WriteSize;

	/// <summary>The input a chunk takes: the writer's <see cref="ZipWriter.ChunkSize"/> as the stage was made.</summary>
	private readonly int chunkSize;

	/// <summary>The chunk being filled, not yet cut; <see langword="null"/> before its first byte.</summary>
	private DeflateJob? pending;

	/// <summary>The stage's own segment that bytes appended as a span are copied into, while it has room; <see langword="null"/> when there is none.</summary>
	private HeldSegment? copy;

	/// <summary>The bytes of <see cref="copy"/> filled.</summary>
	private int copied;

	/// <summary>Whether the stage is complete.</summary>
	private bool completed;

	/// <summary>The gather buffers rented and not yet returned (<see cref="GatherBuffers"/>).</summary>
	private int gatherBuffers;

	/// <summary>Creates the stage of a chunked entry, which starts no thread until a chunk is cut.</summary>
	/// <param name="writer">The writer: its level (not <see cref="System.IO.Compression.CompressionLevel.NoCompression"/>), its chunk size and its pool.</param>
	/// <param name="threads">The most threads to deflate on: 1 or more (less is 1).</param>
	/// <exception cref="ArgumentOutOfRangeException">The writer's level is <see cref="System.IO.Compression.CompressionLevel.NoCompression"/>.</exception>
	public ChunkedDeflateStage(ZipWriter writer, int threads)
		: base(writer, threads, int.MaxValue)
	{
		chunkSize = writer.ChunkSize;
	}

	/// <summary>The gather buffers the stage's workers hold, on every thread, for tests that check that they all return.</summary>
	internal int GatherBuffers => Volatile.Read(ref gatherBuffers);

	/// <inheritdoc/>
	public override void Append(OwnedSegment segment)
	{
		if (segment.IsEmpty)
		{
			CheckOpen();

			return;
		}

		HeldSegment? held = null;
		try
		{
			CheckOpen();
			held = new HeldSegment(segment);
			Add(held, 0, segment.Length);
		}
		finally
		{
			if (held is null)
			{
				Writer.Return(segment);
			}
			else
			{
				held.Release(Writer);
			}
		}
	}

	/// <inheritdoc/>
	public override void Append(ReadOnlySpan<byte> bytes)
	{
		CheckOpen();
		while (!bytes.IsEmpty)
		{
			if (copy is null || copied == copy.Segment.Length)
			{
				CloseCopy();
				var array = ArrayPool<byte>.Shared.Rent(CopySize);
				var segment = new OwnedSegment(array, array.Length);
				Writer.Hold(segment);
				copy = new HeldSegment(segment);
			}

			// The copy before the pieces: a chunk they fill is a thread's at once.
			var n = Math.Min(copy.Segment.Length - copied, bytes.Length);
			bytes.Slice(0, n).CopyTo(copy.Segment.Array!.AsSpan(copied));
			var at = copied;
			copied += n;
			bytes = bytes.Slice(n);
			Add(copy, at, n);
		}
	}

	/// <inheritdoc/>
	public override void Complete()
	{
		CheckOpen();
		completed = true;
		CloseCopy();
		var job = pending ?? NewJob();
		pending = null;
		job.Last = true;
		if (job.Length == 0 || !HasThreads)
		{
			DeflateHere(job);
		}
		else
		{
			Hand(job);
		}
	}

	/// <inheritdoc/>
	protected override object CreateWorker() => new Worker(new ChunkDeflater(Writer.Level));

	/// <summary>
	/// Deflates a chunk: one piece where it is, more gathered into the worker's buffer first (rented with the first
	/// such chunk, as large as it), and released before the deflate.
	/// </summary>
	/// <param name="worker">The thread's worker (<see cref="Worker"/>).</param>
	/// <param name="job">The chunk's job.</param>
	/// <param name="chunk">The chunk.</param>
	protected override void Deflate(object worker, DeflateJob job, DeflatedChunk chunk)
	{
		var state = (Worker)worker;
		if (job.Count <= 1)
		{
			state.Deflater.Deflate(job.Count == 0 ? default : job[0].Span, job.Last, chunk);

			return;
		}

		var gather = state.Gather;
		if (gather is null || gather.Length < job.Length)
		{
			if (gather is not null)
			{
				state.Gather = null;
				ReturnGather(gather);
			}

			gather = ArrayPool<byte>.Shared.Rent(job.Length);
			Interlocked.Increment(ref gatherBuffers);
			state.Gather = gather;
		}

		var at = 0;
		for (var i = 0; i < job.Count; i++)
		{
			var piece = job[i].Span;
			piece.CopyTo(gather.AsSpan(at));
			at += piece.Length;
		}

		job.Release(Writer);
		state.Deflater.Deflate(gather.AsSpan(0, at), job.Last, chunk);
	}

	/// <summary>Returns the worker's gather buffer, if it has one.</summary>
	/// <param name="worker">The worker (<see cref="Worker"/>).</param>
	protected override void EndWorker(object worker)
	{
		var state = (Worker)worker;
		if (state.Gather is { } gather)
		{
			state.Gather = null;
			ReturnGather(gather);
		}
	}

	/// <inheritdoc/>
	protected override void ReleaseInput()
	{
		pending?.Release(Writer);
		pending = null;
		CloseCopy();
	}

	/// <summary>Throws if a thread has thrown, or the stage is complete or disposed.</summary>
	/// <exception cref="InvalidOperationException">The stage is complete or disposed.</exception>
	private void CheckOpen()
	{
		ThrowIfFaulted();
		if (completed || IsDisposed)
		{
			throw new InvalidOperationException("The deflate stage is complete: an entry's data ends once.");
		}
	}

	/// <summary>Adds bytes of a held segment to the chunks, after the data before: each chunk that fills is cut and handed over.</summary>
	/// <param name="segment">The segment, which the writing thread holds meanwhile.</param>
	/// <param name="offset">The first byte, in the segment's array.</param>
	/// <param name="length">How many bytes: 1 or more.</param>
	private void Add(HeldSegment segment, int offset, int length)
	{
		while (length > 0)
		{
			pending ??= NewJob();
			var n = Math.Min(chunkSize - pending.Length, length);
			pending.Add(segment, offset, n);
			offset += n;
			length -= n;
			if (pending.Length == chunkSize)
			{
				var job = pending;
				pending = null;
				Hand(job);
			}
		}
	}

	/// <summary>Lets the copy segment go: no more bytes go into it, and the chunks that point into it return it.</summary>
	private void CloseCopy()
	{
		copy?.Release(Writer);
		copy = null;
		copied = 0;
	}

	/// <summary>Returns a gather buffer to the pool.</summary>
	/// <param name="gather">The buffer.</param>
	private void ReturnGather(byte[] gather)
	{
		Interlocked.Decrement(ref gatherBuffers);
		ArrayPool<byte>.Shared.Return(gather);
	}

	/// <summary>A stage thread's own state: its deflater and its gather buffer.</summary>
	/// <param name="deflater">The thread's deflater.</param>
	private sealed class Worker(ChunkDeflater deflater)
	{
		/// <summary>The thread's deflater.</summary>
		public ChunkDeflater Deflater { get; } = deflater;

		/// <summary>The buffer the thread gathers a chunk's pieces into, from <see cref="ArrayPool{T}.Shared"/>; <see langword="null"/> until a chunk has more than one.</summary>
		public byte[]? Gather { get; set; }
	}
}
#endif
