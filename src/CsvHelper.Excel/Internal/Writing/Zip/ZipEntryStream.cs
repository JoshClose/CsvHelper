using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// An entry's stream, write-only: what is written is deflated (or stored, at <see cref="CompressionLevel.NoCompression"/>)
/// into the entry's pooled buffers, which reach the package's stream whole as each fills and is followed by more data. A
/// stored entry is held so only until its data passes one buffer: it then writes its local header and all it holds, and
/// from then on each write goes straight to the package's stream, as one write of the caller's bytes, uncopied
/// (<see cref="WritesThrough"/>). Closing it writes the rest, patches a patched entry's local header
/// (<see cref="Patched"/>), writes the descriptor of a streamed entry, and adds the entry to the central directory
/// (<see cref="ZipWriter"/>). Synchronous calls write the package's stream synchronously, asynchronous ones
/// asynchronously. <see cref="Flush"/> does nothing: flushing the deflater would change the bytes. Once the writer is
/// faulted, writing and closing do nothing.
/// </summary>
/// <remarks>
/// An entry opened chunked on a writer with threads (<see cref="TakesSegments"/>) is deflated off the writing thread: its
/// data goes to a deflate stage (<see cref="DeflateStage"/>), as segments handed over uncopied (<see cref="AppendAsync"/>)
/// or as copies of writes, and after each append the entry writes the chunks the stage has deflated, in order, waiting for
/// the next only while the stage holds as many as it may. A chunk goes after the data before it: into the buffers while
/// the local header waits, so that an entry of at most one buffer of data is still written whole; as the data passes one
/// buffer, the local header (streamed) and the buffers; from then on each chunk's buffers straight to the package's
/// stream. The chunks' CRCs combine into the entry's in order (<see cref="Crc32.Combine"/>). A synchronous call's wait
/// blocks, and an asynchronous call's awaits, so that each writes the package's stream in its own mode.
/// </remarks>
internal sealed class ZipEntryStream : Stream
#if !NET && !NETSTANDARD2_1_OR_GREATER
	, IAsyncDisposable
#endif
{
	/// <summary>An empty deflate stream: one final fixed block of only its end, as zlib finishes empty input.</summary>
	private static ReadOnlySpan<byte> EmptyDeflate => [0x03, 0x00];

	/// <summary>The package's writer.</summary>
	private readonly ZipWriter writer;

	/// <summary>The entry's data, deflated or stored, not yet written to the package's stream.</summary>
	private readonly ZipBuffers data;

	/// <summary>Whether the entry is stored rather than deflated.</summary>
	private readonly bool stored;

	/// <summary>The deflater's output, into <see cref="data"/>, for a deflated entry.</summary>
	private readonly DeflaterSink? sink;

	/// <summary>The deflater, made with the first bytes written; <see langword="null"/> before, and once finished.</summary>
	private Stream? deflater;

	/// <summary>The bytes of data written to the package's stream: a write straight through counts as it starts.</summary>
	private long dataWritten;

	/// <summary>Whether the local header has been written.</summary>
	private bool headerWritten;

	/// <summary>Whether the entry has been closed.</summary>
	private bool closed;

	/// <summary>
	/// Whether the entry deflates in chunks on its deflate stage (<see cref="TakesSegments"/>): opened chunked and deflated,
	/// with more than one thread as it opened, or since <see cref="UseChunks"/>.
	/// </summary>
	private bool chunking;

	/// <summary>The most threads the stage deflates on, once the entry chunks: 2 or more.</summary>
	private int threads;

	/// <summary>The stage of a chunked entry, made with its first byte; <see langword="null"/> before, and once released.</summary>
	private DeflateStage? stage;

	/// <summary>The stage's chunks taken, which is the next one's place in order (<see cref="DeflatedChunk.Sequence"/>).</summary>
	private long chunksTaken;

	/// <summary>
	/// The bytes written whose deflate is in the data written or held: the input of the chunks written, and of a switched
	/// entry's data before its switch (<see cref="UseChunks"/>). At close, all the bytes written.
	/// </summary>
	private long chunkedInput;

	/// <summary>Opens an entry.</summary>
	/// <param name="writer">The package's writer.</param>
	/// <param name="name">The entry's name, ASCII.</param>
	/// <param name="chunked">Whether the entry is deflated off the writing thread when the writer has threads (<see cref="ZipWriter.Open(string, bool)"/>).</param>
	/// <param name="atLeast">The threads a chunked entry takes at least, besides the writer's (<see cref="ZipWriter.Open(string, bool, int)"/>), or 0.</param>
	public ZipEntryStream(ZipWriter writer, string name, bool chunked = false, int atLeast = 0)
	{
		this.writer = writer;
		Name = name;
		Chunked = chunked;
		data = new ZipBuffers(writer);
		stored = writer.Level == CompressionLevel.NoCompression;
		if (!stored)
		{
			sink = new DeflaterSink(data);
			var count = Math.Max(Math.Max(writer.Threads, writer.ParallelThreads), atLeast);
			if (chunked && count > 1)
			{
				chunking = true;
				threads = count;
			}
		}
	}

	/// <summary>The entry's name.</summary>
	public string Name { get; }

	/// <summary>
	/// Whether the entry was opened chunked (<see cref="ZipWriter.Open(string, bool)"/>): deflated off the writing thread
	/// when the writer has threads, or once a parallel write's sink asks (<see cref="UseChunks"/>).
	/// </summary>
	public bool Chunked { get; }

	/// <summary>
	/// Whether <see cref="AppendAsync"/> hands a segment on uncopied, to the entry's deflate stage: so the sink detaches its
	/// buffer at each flush (<see cref="XmlOutput.Detach"/>) rather than writing from it. <see langword="true"/> for an entry
	/// opened chunked and deflated whose writer had more than one thread as it opened (<c>Math.Max(</c><see cref="ZipWriter.Threads"/><c>, </c><see cref="ZipWriter.ParallelThreads"/><c>)</c>),
	/// or since <see cref="UseChunks"/> switched it; <see langword="false"/> for an entry that deflates on the writing thread
	/// or is stored, which copy what they take as <see cref="Write(byte[], int, int)"/> does.
	/// </summary>
	public bool TakesSegments => chunking;

	/// <summary>
	/// Makes the entry's stage in place of <see cref="DeflateStages.Create"/>, for tests that give it one of their own: set
	/// before the entry's first byte. Given the writer and the threads, it returns a new stage.
	/// </summary>
	internal Func<ZipWriter, int, DeflateStage>? StageFactory { get; set; }

	/// <summary>The compression method: 0, stored, or 8, deflated.</summary>
	public ushort Method => stored ? (ushort)0 : (ushort)8;

	/// <summary>
	/// The general purpose flags: bits 1-2 of the deflate level (<c>0x0006</c> at <see cref="CompressionLevel.Fastest"/>,
	/// <c>0x0002</c> at <c>SmallestSize</c>), and bit 3 for a streamed entry. Set with the local header, and bit 3 added as
	/// a patched entry whose sizes don't fit its header closes.
	/// </summary>
	public ushort Flags { get; private set; }

	/// <summary>
	/// The version needed to extract: 45 for a streamed entry or one past the 32-bit offsets, else 20. Set with the local
	/// header, and to 45 as a patched entry whose sizes don't fit its header closes.
	/// </summary>
	public ushort VersionNeeded { get; private set; }

	/// <summary>
	/// Whether the local header is patched after the data, rather than streamed: a stored entry's, past one buffer, on a
	/// stream that seeks (<see cref="ZipWriter.Seekable"/>). Its header is written without bit 3 or an extra field, CRC and
	/// sizes 0, and closing the entry writes its real CRC and sizes into it, or, when a size reaches
	/// <see cref="ZipLimits.Max32"/>, bit 3 and version 4.5, for the descriptor after the data. Set with the local header.
	/// </summary>
	public bool Patched { get; private set; }

	/// <summary>The offset of the local header. Set with it.</summary>
	public long Offset { get; private set; }

	/// <summary>The CRC-32 of the bytes written, complete once the entry is closed.</summary>
	public uint Crc { get; private set; }

	/// <summary>The data's size, deflated or stored, once the entry is closed (or its header is written whole).</summary>
	public long CompressedSize { get; private set; }

	/// <summary>The bytes written.</summary>
	public long UncompressedSize { get; private set; }

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <summary>Whether the entry can be written: until it is closed.</summary>
	public override bool CanWrite => !closed;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <summary>The bytes written. It can't be set.</summary>
	public override long Position
	{
		get => UncompressedSize;
		set => throw new NotSupportedException();
	}

	/// <summary>
	/// Whether writes go straight to the package's stream, uncopied: a stored entry's, once its data has passed one buffer
	/// (its local header written while it is open, patched or streamed), which holds nothing from then on.
	/// </summary>
	private bool WritesThrough => stored && headerWritten;

	/// <summary>
	/// Does nothing: the data reaches the package's stream in whole buffers, or as it is written once a stored entry's data
	/// has passed one buffer, and the deflater isn't flushed.
	/// </summary>
	public override void Flush()
	{
	}

	/// <summary>
	/// Does nothing: the data reaches the package's stream in whole buffers, or as it is written once a stored entry's data
	/// has passed one buffer, and the deflater isn't flushed.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>A completed task.</returns>
	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <summary>Writes bytes to the entry, writing the package's stream synchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	public override void Write(byte[] buffer, int offset, int count)
	{
#if NET || NETSTANDARD2_1_OR_GREATER
		Write(new ReadOnlySpan<byte>(buffer, offset, count));
#else
		var bytes = new ArraySegment<byte>(buffer, offset, count);
		CheckOpen();
		if (writer.Faulted)
		{
			return;
		}

		if (chunking)
		{
			WriteChunked(new ReadOnlySpan<byte>(buffer, offset, count));

			return;
		}

		for (var at = 0; at < bytes.Count; at += ZipWriter.BufferSize)
		{
			if (WritesThrough)
			{
				WriteThrough(bytes.Array!, bytes.Offset + at, bytes.Count - at);

				return;
			}

			Take(bytes.Array!, bytes.Offset + at, Math.Min(ZipWriter.BufferSize, bytes.Count - at));
			SyncOrAsync.Wait(WriteFullBuffersAsync(false, CancellationToken.None));
		}
#endif
	}

	/// <summary>Writes bytes to the entry, writing the package's stream asynchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken; while a chunked entry waits for a chunk's deflate, after they are taken, the wait ends and the entry goes on.</param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		WriteMemoryAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

#if NET || NETSTANDARD2_1_OR_GREATER
	/// <summary>Writes bytes to the entry, writing the package's stream synchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		CheckOpen();
		if (writer.Faulted)
		{
			return;
		}

		if (chunking)
		{
			WriteChunked(buffer);

			return;
		}

		while (!buffer.IsEmpty)
		{
			if (WritesThrough)
			{
				WriteThrough(buffer);

				return;
			}

			var slice = buffer.Slice(0, Math.Min(ZipWriter.BufferSize, buffer.Length));
			Take(slice);
			SyncOrAsync.Wait(WriteFullBuffersAsync(false, CancellationToken.None));
			buffer = buffer.Slice(slice.Length);
		}
	}

	/// <summary>Writes bytes to the entry, writing the package's stream asynchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken; while a chunked entry waits for a chunk's deflate, after they are taken, the wait ends and the entry goes on.</param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => WriteMemoryAsync(buffer, cancellationToken);

	/// <summary>Closes the entry, writing the package's stream asynchronously. Calling it again does nothing.</summary>
	/// <returns>A task that completes when the entry is closed.</returns>
	public override async ValueTask DisposeAsync()
#else
	/// <summary>Closes the entry, writing the package's stream asynchronously. Calling it again does nothing.</summary>
	/// <returns>A task that completes when the entry is closed.</returns>
	public async ValueTask DisposeAsync()
#endif
	{
		try
		{
			await CloseAsync(true).ConfigureAwait(false);
		}
		finally
		{
			Dispose();
		}
	}

	/// <summary>
	/// Takes a segment of the entry's bytes, after those written before it, writing the package's stream synchronously or
	/// asynchronously as <see cref="Write(byte[], int, int)"/> and <see cref="WriteAsync(byte[], int, int, CancellationToken)"/>
	/// do. The segment is the entry's from the call on, whatever happens: handed to its deflate stage uncopied when the
	/// entry is chunked (<see cref="TakesSegments"/>), else written or deflated as a write is and returned at once
	/// (<see cref="ZipWriter.Return(OwnedSegment)"/>); returned as well when the call throws before the bytes are taken,
	/// the entry is closed or the writer faulted. The empty segment writes nothing.
	/// </summary>
	/// <param name="segment">The bytes.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">
	/// Cancels an asynchronous write. An entry that isn't chunked checks it before any of the write, and the bytes then
	/// aren't taken. A chunked entry takes the bytes first, since its caller has handed them over, and then throws, or ends
	/// a wait for a chunk's deflate, leaving the entry as it was, with the bytes.
	/// </param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	internal async ValueTask AppendAsync(OwnedSegment segment, bool async, CancellationToken cancellationToken)
	{
		writer.Hold(segment);
		if (!chunking)
		{
			try
			{
				if (segment.IsEmpty)
				{
					CheckOpen();

					return;
				}

				if (async)
				{
					await WriteMemoryAsync(segment.Memory, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					Write(segment.Array!, 0, segment.Length);
				}
			}
			finally
			{
				writer.Return(segment);
			}

			return;
		}

		var held = true;
		try
		{
			CheckOpen();
			if (segment.IsEmpty || writer.Faulted)
			{
				return;
			}

			var current = Stage();
			held = false;
			try
			{
				current.Append(segment);
			}
			catch
			{
				writer.Fault();

				throw;
			}

			UncompressedSize += segment.Length;
		}
		finally
		{
			if (held)
			{
				writer.Return(segment);
			}
		}

		if (async)
		{
			cancellationToken.ThrowIfCancellationRequested();
		}

		await WriteChunksAsync(async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Chunks the entry from here on, as a parallel write on its sheet begins (<see cref="ZipWriter.ParallelThreads"/>): a
	/// deflated entry opened chunked deflates on <c>Math.Max(</c><see cref="ZipWriter.Threads"/><c>, </c><see cref="ZipWriter.ParallelThreads"/><c>)</c>
	/// threads when that is above 1. On net8.0 and net10.0 the bytes written before end as one chunk (a sync flush) and the
	/// rest is cut in chunks; on the other builds an entry that has deflated nothing yet goes to one deflate thread, and one
	/// that has stays on the writing thread. A stored entry, an entry not opened chunked, and one already chunked (but for
	/// its thread count) don't change. It writes nothing to the package's stream: the bytes so far stay in the buffers until
	/// the next write or the close. A closed entry, or one of a faulted writer, doesn't change either.
	/// </summary>
	/// <exception cref="InvalidOperationException">The deflater's sync flush, GZip trailer or final block is not what it should be (which faults the writer).</exception>
	internal void UseChunks()
	{
		if (closed || stored || !Chunked || writer.Faulted)
		{
			return;
		}

		var count = Math.Max(writer.Threads, writer.ParallelThreads);
		if (chunking)
		{
			if (count > threads)
			{
				threads = count;
				try
				{
					stage?.RaiseThreads(count);
				}
				catch
				{
					writer.Fault();

					throw;
				}
			}

			return;
		}

		if (count <= 1)
		{
			return;
		}

		if (deflater is not null)
		{
#if NET
			EndSerialChunk();
#else
			// Here Flush doesn't sync-flush, so the bytes so far can't end at a byte boundary: the entry stays on the writing thread.
			return;
#endif
		}

		chunking = true;
		threads = count;
	}

	/// <summary>
	/// Closes the entry: finishes the deflater (a chunked entry's stage, its chunks all written), writes the local header if
	/// it waits and the rest of the data, patches a patched entry's local header (<see cref="PatchHeaderAsync"/>), writes a
	/// streamed entry's descriptor, and adds the entry to the central directory. The pooled buffers return, whatever
	/// happens. Calling it again does nothing, and so does closing an entry of a faulted writer.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	internal async ValueTask CloseAsync(bool async)
	{
		if (closed)
		{
			return;
		}

		closed = true;
		try
		{
			if (writer.Faulted)
			{
				return;
			}

			if (chunking)
			{
				await FinishChunksAsync(async).ConfigureAwait(false);
			}
			else
			{
				Finish();
			}

			CompressedSize = dataWritten + data.Length;
			if (!headerWritten)
			{
				var limits = writer.Limits;
				var whole = data.Length <= ZipWriter.BufferSize && CompressedSize < limits.Max32 && UncompressedSize < limits.Max32;
				await WriteHeaderAsync(!whole, async, CancellationToken.None).ConfigureAwait(false);
			}

			for (var i = 0; i < data.Count; i++)
			{
				await writer.WriteAsync(data[i], data.LengthOf(i), async, CancellationToken.None).ConfigureAwait(false);
			}

			data.Clear();
			if (Patched)
			{
				await PatchHeaderAsync(async).ConfigureAwait(false);
			}

			if ((Flags & ZipRecords.Streamed) != 0)
			{
				await writer.WriteDescriptorAsync(this, async).ConfigureAwait(false);
			}

			writer.AddEntry(this);
		}
		finally
		{
			Release();
			writer.Closed(this);
		}
	}

	/// <summary>
	/// Returns the entry's buffers to the pool and drops its deflater, as it closes or as the writer faults: a chunked
	/// entry's stage first, which stops and joins its threads and then returns every segment and chunk it holds. Calling it
	/// again does nothing.
	/// </summary>
	internal void Release()
	{
		if (stage is { } current)
		{
			stage = null;
			try
			{
				current.Dispose();
			}
			catch
			{
				// The stage has stopped as far as it can, and its failure would hide the one that released the entry.
			}
		}

		data.Clear();
		if (deflater is not null)
		{
			sink!.Discard();
			try
			{
				deflater.Dispose();
			}
			catch
			{
				// The deflater finishes into nothing, and its failure would hide the one that released the entry.
			}

			deflater = null;
		}
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				SyncOrAsync.Wait(CloseAsync(false));
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	/// <summary>Throws if the entry has been closed.</summary>
	/// <exception cref="ObjectDisposedException">The entry has been closed.</exception>
	private void CheckOpen() => ObjectDisposedException.ThrowIf(closed, this);

	/// <summary>
	/// Writes bytes to the entry, writing the package's stream asynchronously: a buffer's worth at a time into the
	/// buffers, and once a stored entry is streamed, the rest straight through in one write; a chunked entry's as one
	/// copy to its stage.
	/// </summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken; while a chunked entry waits for a chunk's deflate, after they are taken, the wait ends and the entry goes on.</param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	private async ValueTask WriteMemoryAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		CheckOpen();
		if (writer.Faulted)
		{
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (chunking)
		{
			if (!buffer.IsEmpty)
			{
				AppendCopy(buffer.Span);
				await WriteChunksAsync(true, cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		while (!buffer.IsEmpty)
		{
			if (WritesThrough)
			{
				CountThrough(buffer.Span);
				await writer.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

				return;
			}

			var slice = buffer.Slice(0, Math.Min(ZipWriter.BufferSize, buffer.Length));
#if NET || NETSTANDARD2_1_OR_GREATER
			Take(slice.Span);
#else
			MemoryMarshal.TryGetArray(slice, out var segment);
			Take(segment.Array!, segment.Offset, segment.Count);
#endif
			await WriteFullBuffersAsync(true, cancellationToken).ConfigureAwait(false);
			buffer = buffer.Slice(slice.Length);
		}
	}

#if NET || NETSTANDARD2_1_OR_GREATER
	/// <summary>Takes bytes into the entry's buffers, deflated or stored, in memory.</summary>
	/// <param name="input">The bytes.</param>
	private void Take(ReadOnlySpan<byte> input)
	{
		try
		{
			if (stored)
			{
				Crc = Crc32.Append(Crc, input);
				data.Append(input);
			}
			else
			{
#if !NET
				Crc = Crc32.Append(Crc, input);
#endif
				deflater ??= CreateDeflater();
				deflater.Write(input);
			}
		}
		catch
		{
			writer.Fault();

			throw;
		}

		UncompressedSize += input.Length;
	}
#else
	/// <summary>Takes bytes into the entry's buffers, deflated or stored, in memory.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	private void Take(byte[] buffer, int offset, int count)
	{
		var input = new ReadOnlySpan<byte>(buffer, offset, count);
		try
		{
			Crc = Crc32.Append(Crc, input);
			if (stored)
			{
				data.Append(input);
			}
			else
			{
				deflater ??= CreateDeflater();
				deflater.Write(buffer, offset, count);
			}
		}
		catch
		{
			writer.Fault();

			throw;
		}

		UncompressedSize += count;
	}
#endif

#if NET || NETSTANDARD2_1_OR_GREATER
	/// <summary>
	/// Writes bytes straight to the package's stream, synchronously, uncopied, once a stored entry is streamed
	/// (<see cref="WritesThrough"/>): counted first (<see cref="CountThrough"/>), then written in one write.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	private void WriteThrough(ReadOnlySpan<byte> bytes)
	{
		CountThrough(bytes);
		writer.Write(bytes);
	}
#else
	/// <summary>
	/// Writes bytes straight to the package's stream, synchronously, uncopied, once a stored entry is streamed
	/// (<see cref="WritesThrough"/>): counted first (<see cref="CountThrough"/>), then written in one write.
	/// </summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	private void WriteThrough(byte[] buffer, int offset, int count)
	{
		CountThrough(new ReadOnlySpan<byte>(buffer, offset, count));
		writer.Write(buffer, offset, count);
	}
#endif

	/// <summary>
	/// Counts bytes about to go straight to the package's stream: their CRC, the bytes written and the data written. A
	/// failed write faults the writer, so the counts are never used after it.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	private void CountThrough(ReadOnlySpan<byte> bytes)
	{
		Crc = Crc32.Append(Crc, bytes);
		UncompressedSize += bytes.Length;
		dataWritten += bytes.Length;
	}

	/// <summary>Makes the deflater, at the writer's level, writing into <see cref="sink"/>.</summary>
	/// <returns>The deflater.</returns>
	private Stream CreateDeflater()
	{
#if NET
		return new GZipStream(sink!, writer.Level, leaveOpen: true);
#else
		return new DeflateStream(sink!, writer.Level, leaveOpen: true);
#endif
	}

	/// <summary>
	/// Writes the buffers that are full and followed by more data, after the local header the first time, so that the
	/// entry is streamed (or patched): it has more than one buffer of data. A stored entry writes the rest it holds
	/// too, and from then on writes through (<see cref="WriteBuffersAsync"/>).
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private ValueTask WriteFullBuffersAsync(bool async, CancellationToken cancellationToken)
	{
		if (data.Count < 2 || writer.Faulted)
		{
			return default;
		}

		return WriteBuffersAsync(data.Count - 1, async, cancellationToken);
	}

	/// <summary>
	/// Writes the first buffers, which are full, after the local header the first time: streamed, or patched for a stored
	/// entry on a stream that seeks (<see cref="Patched"/>). A stored entry, past one buffer from here on, writes its last
	/// buffer too, full or not, and holds nothing more: its later writes go straight to the package's stream
	/// (<see cref="WritesThrough"/>).
	/// </summary>
	/// <param name="full">How many: all but the last.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask WriteBuffersAsync(int full, bool async, CancellationToken cancellationToken)
	{
		if (!headerWritten)
		{
			Patched = stored && writer.Seekable;
			await WriteHeaderAsync(!Patched, async, cancellationToken).ConfigureAwait(false);
		}

		for (var i = 0; i < full; i++)
		{
			await writer.WriteAsync(data[i], ZipWriter.BufferSize, async, cancellationToken).ConfigureAwait(false);
			dataWritten += ZipWriter.BufferSize;
		}

		if (stored)
		{
			var rest = data.LengthOf(full);
			await writer.WriteAsync(data[full], rest, async, cancellationToken).ConfigureAwait(false);
			dataWritten += rest;
			data.Clear();
		}
		else
		{
			data.RemoveFirst(full);
		}
	}

	/// <summary>
	/// Writes the local header, at the package's position, which becomes the entry's offset: version 4.5 for a streamed
	/// entry, or at an offset at or past <see cref="ZipLimits.Max32"/>, else 2.0.
	/// </summary>
	/// <param name="streamed">Whether the entry is streamed, rather than whole or patched.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the header is written.</returns>
	private ValueTask WriteHeaderAsync(bool streamed, bool async, CancellationToken cancellationToken)
	{
		Offset = writer.Position;
		Flags = (ushort)(LevelFlags(writer.Level) | (streamed ? ZipRecords.Streamed : 0));
		VersionNeeded = streamed || Offset >= writer.Limits.Max32 ? ZipRecords.Version45 : ZipRecords.Version20;
		headerWritten = true;

		return writer.WriteLocalHeaderAsync(this, async, cancellationToken);
	}

	/// <summary>
	/// Patches the local header of a patched entry as it closes, its data written: its real CRC and sizes when both sizes
	/// are below <see cref="ZipLimits.Max32"/>; otherwise they don't fit the header, which takes the streamed form (bit 3,
	/// version 4.5, its CRC and sizes left 0, still with no extra field), and the descriptor follows the data. Either way
	/// the stream is back at the package's end.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the header is patched.</returns>
	private ValueTask PatchHeaderAsync(bool async)
	{
		var limits = writer.Limits;
		if (CompressedSize >= limits.Max32 || UncompressedSize >= limits.Max32)
		{
			Flags = (ushort)(Flags | ZipRecords.Streamed);
			VersionNeeded = ZipRecords.Version45;
		}

		return writer.PatchLocalHeaderAsync(this, async);
	}

	/// <summary>
	/// Finishes the data: disposes the deflater, whose last output goes into the buffers, and on .NET 8 and later takes its
	/// GZip trailer off for the CRC; an entry deflated from nothing gets an empty deflate stream. A failure faults the
	/// writer, as the package can't be completed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The deflater's GZip header or trailer is not what it should be.</exception>
	private void Finish()
	{
		try
		{
			if (deflater is not null)
			{
				deflater.Dispose();
				deflater = null;
#if NET
				var (crc, size) = sink!.TakeTrailer();
				if (size != (uint)UncompressedSize)
				{
					throw new InvalidOperationException($"The deflater's GZip trailer gives the size mod 2^32 as {size}, not {(uint)UncompressedSize}.");
				}

				Crc = crc;
#endif
			}
			else if (!stored)
			{
				data.Append(EmptyDeflate);
			}
		}
		catch
		{
			writer.Fault();

			throw;
		}
	}

	/// <summary>
	/// Writes bytes to a chunked entry, writing the package's stream synchronously: a copy goes to the stage, and the chunks
	/// it has deflated are written (<see cref="WriteChunksAsync"/>), waiting for them with a wait that blocks.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	private void WriteChunked(ReadOnlySpan<byte> bytes)
	{
		if (bytes.IsEmpty)
		{
			return;
		}

		AppendCopy(bytes);
		SyncOrAsync.Wait(WriteChunksAsync(false, CancellationToken.None));
	}

	/// <summary>
	/// The chunked entry's stage, made with its first byte (<see cref="DeflateStages.Create"/>, or <see cref="StageFactory"/>)
	/// on the threads the entry chunks with. A failure faults the writer.
	/// </summary>
	/// <returns>The stage.</returns>
	private DeflateStage Stage()
	{
		if (stage is null)
		{
			try
			{
				stage = StageFactory is { } factory ? factory(writer, threads) : DeflateStages.Create(writer, threads);
			}
			catch
			{
				writer.Fault();

				throw;
			}
		}

		return stage;
	}

	/// <summary>Appends a copy of bytes to a chunked entry's stage, and counts them. A failure faults the writer.</summary>
	/// <param name="bytes">The bytes: 1 or more.</param>
	private void AppendCopy(ReadOnlySpan<byte> bytes)
	{
		var current = Stage();
		try
		{
			current.Append(bytes);
		}
		catch
		{
			writer.Fault();

			throw;
		}

		UncompressedSize += bytes.Length;
	}

	/// <summary>
	/// Writes the chunks a chunked entry's stage has deflated, in order (<see cref="WriteChunkAsync"/>): every one it has
	/// ready, and while it holds as many as it may (<see cref="DeflateStage.MustWait"/>), the next, waiting for it: with a
	/// wait that blocks for a synchronous call, and asynchronously for an asynchronous one. It never waits for data. A
	/// canceled wait throws and leaves the chunk to the stage and the entry as it was; any other failure faults the writer.
	/// </summary>
	/// <param name="async">Whether to wait and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous wait or write.</param>
	/// <returns>A task that completes when the chunks are written.</returns>
	private async ValueTask WriteChunksAsync(bool async, CancellationToken cancellationToken)
	{
		while (stage is { } current)
		{
			DeflatedChunk? chunk;
			try
			{
				if (current.TryTake(out var ready))
				{
					chunk = ready;
				}
				else if (!current.MustWait)
				{
					return;
				}
				else if (async)
				{
					chunk = await current.TakeAsync(cancellationToken).ConfigureAwait(false);
				}
				else
				{
					chunk = current.Take();
				}
			}
			catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
			{
				writer.Fault();

				throw;
			}

			if (chunk is null)
			{
				return;
			}

			await WriteChunkAsync(chunk, async, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Writes a chunk of a chunked entry after the data before it, and returns it to the stage: while the local header
	/// waits, into the buffers, so that an entry of at most one buffer of data is still written whole; as the data passes
	/// one buffer, the local header (streamed) and the buffers; from then on, the buffers the entry holds (a switched
	/// entry's last bytes before its switch) and the chunk's own, straight to the package's stream. Its CRC combines into
	/// the entry's (<see cref="Crc32.Combine"/>). A chunk out of order, or of more input than the entry was given, faults
	/// the writer, as a failed write does.
	/// </summary>
	/// <param name="chunk">The chunk, taken from the stage.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the chunk is written.</returns>
	/// <exception cref="InvalidOperationException">The chunk isn't the next one in order, or has more input than the entry was given.</exception>
	private async ValueTask WriteChunkAsync(DeflatedChunk chunk, bool async, CancellationToken cancellationToken)
	{
		try
		{
			if (chunk.Sequence != chunksTaken || chunk.InputLength < 0 || chunk.InputLength > UncompressedSize - chunkedInput)
			{
				writer.Fault();

				throw new InvalidOperationException(
					$"The deflate stage gave chunk {chunk.Sequence} of {chunk.InputLength} bytes of input, where the entry takes chunk {chunksTaken} of at most {UncompressedSize - chunkedInput}.");
			}

			chunksTaken++;
			chunkedInput += chunk.InputLength;
			Crc = Crc32.Combine(Crc, chunk.Crc, chunk.InputLength);
			var output = chunk.Output;
			var straight = headerWritten;
			if (!straight)
			{
				for (var i = 0; i < output.Count; i++)
				{
					data.Append(new ReadOnlySpan<byte>(output[i], 0, output.LengthOf(i)));
				}

				if (data.Count < 2)
				{
					return;
				}

				await WriteHeaderAsync(true, async, cancellationToken).ConfigureAwait(false);
			}

			for (var i = 0; i < data.Count; i++)
			{
				var length = data.LengthOf(i);
				await writer.WriteAsync(data[i], length, async, cancellationToken).ConfigureAwait(false);
				dataWritten += length;
			}

			data.Clear();
			if (straight)
			{
				for (var i = 0; i < output.Count; i++)
				{
					var length = output.LengthOf(i);
					await writer.WriteAsync(output[i], length, async, cancellationToken).ConfigureAwait(false);
					dataWritten += length;
				}
			}
		}
		finally
		{
			if (stage is { } current)
			{
				current.Return(chunk);
			}
			else
			{
				// The writer faulted and released the entry, whose stage is gone: the chunk's buffers go back to the pool.
				chunk.Clear();
			}
		}
	}

	/// <summary>
	/// Finishes a chunked entry's data: ends the stage's input, whose rest is its last chunk, deflated final, and writes
	/// every chunk left, in order, waiting for each as the call's mode does. Without a stage, as nothing was written since
	/// the entry chunks, the data ends with an empty final block, as an entry deflated from nothing does. Then the chunks'
	/// input must be every byte written. A failure faults the writer.
	/// </summary>
	/// <param name="async">Whether to wait and write asynchronously.</param>
	/// <returns>A task that completes when the data is finished.</returns>
	/// <exception cref="InvalidOperationException">The chunks' input isn't the bytes written.</exception>
	private async ValueTask FinishChunksAsync(bool async)
	{
		if (stage is not { } current)
		{
			data.Append(EmptyDeflate);
		}
		else
		{
			try
			{
				current.Complete();
			}
			catch
			{
				writer.Fault();

				throw;
			}

			while (true)
			{
				DeflatedChunk? chunk;
				try
				{
					chunk = async ? await current.TakeAsync(CancellationToken.None).ConfigureAwait(false) : current.Take();
				}
				catch
				{
					writer.Fault();

					throw;
				}

				if (chunk is null)
				{
					break;
				}

				await WriteChunkAsync(chunk, async, CancellationToken.None).ConfigureAwait(false);
			}
		}

		if (chunkedInput != UncompressedSize)
		{
			writer.Fault();

			throw new InvalidOperationException($"The deflate stage's chunks took {chunkedInput} bytes of input, not the {UncompressedSize} written.");
		}
	}

#if NET
	/// <summary>
	/// Ends what the entry's deflater has taken as a chunk, as the entry switches to chunks (<see cref="UseChunks"/>): a sync
	/// flush, so that the data so far ends at a byte boundary, then the deflater disposed, its GZip trailer taken off for
	/// the CRC so far (its size checked), and the empty final block that disposing added checked and dropped, so that the
	/// first chunk's blocks follow. The bytes stay in the buffers. A failure faults the writer.
	/// </summary>
	/// <exception cref="InvalidOperationException">The deflater's GZip trailer or final block is not what it should be.</exception>
	private void EndSerialChunk()
	{
		try
		{
			deflater!.Flush();
			deflater.Dispose();
			deflater = null;
			var (crc, size) = sink!.TakeTrailer();
			if (size != (uint)UncompressedSize)
			{
				throw new InvalidOperationException($"The deflater's GZip trailer gives the size mod 2^32 as {size}, not {(uint)UncompressedSize}.");
			}

			Span<byte> end = stackalloc byte[2];
			if (data.Length < end.Length)
			{
				throw new InvalidOperationException("The deflater's sync-flushed data ended without its final block.");
			}

			data.RemoveLast(end);
			if (!end.SequenceEqual(EmptyDeflate))
			{
				throw new InvalidOperationException($"The deflater ended its sync-flushed data with 0x{end[0]:X2} 0x{end[1]:X2}, not the empty final block 03 00.");
			}

			Crc = crc;
			chunkedInput = UncompressedSize;
		}
		catch
		{
			writer.Fault();

			throw;
		}
	}
#endif

	/// <summary>The flags' bits 1-2 for a level: <c>0x0006</c> at <see cref="CompressionLevel.Fastest"/>, <c>0x0002</c> at <c>SmallestSize</c>, else 0.</summary>
	/// <param name="level">The level.</param>
	/// <returns>The bits.</returns>
	private static ushort LevelFlags(CompressionLevel level)
	{
		return level switch
		{
			CompressionLevel.Fastest => 0x0006,
#if NET
			CompressionLevel.SmallestSize => 0x0002,
#endif
			_ => 0,
		};
	}
}
