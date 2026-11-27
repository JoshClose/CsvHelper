using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Writes a zip one entry at a time, forward only but for one patch per large stored entry on a stream that seeks, so
/// that a stream that can't seek works too (<see cref="ZipRecords"/>), however an entry's data is cut into writes. An
/// entry's data goes into pooled buffers of <see cref="BufferSize"/> bytes and reaches the stream in whole buffers, but
/// for its last. An entry closed with at most one buffer of data and sizes within 32 bits is written whole: its local
/// header holds its CRC and sizes. A deflated entry past one buffer, and on a stream that can't seek a stored one too, is
/// streamed, its local header written as its data passes one buffer: bit 3, version 4.5, CRC and sizes 0 with a Zip64
/// extra of zeros, and a signed descriptor with 8-byte sizes after the data. A stored entry past one buffer on a stream
/// that seeks is patched: its local header, written as its data passes one buffer, has no bit 3 and no extra field, and
/// CRC and sizes 0, which closing it overwrites with the real ones, so that no descriptor follows, as readers that check a
/// stored entry's sizes before its data (POI from a stream) need. Its sizes at or past <see cref="ZipLimits.Max32"/> don't
/// fit the header, so closing it then patches the header to the streamed form (bit 3, version 4.5, CRC and sizes 0, no
/// extra field), and the descriptor follows. A stored entry past one buffer writes all its buffers hold, the last too,
/// and from then on each of its writes reaches the stream as one write of the caller's bytes, uncopied. The central
/// directory, kept in pooled buffers until the end, holds every entry's real sizes and offset; when any of an entry's
/// three reaches <see cref="ZipLimits.Max32"/>, all three are all ones and in its Zip64 extra. The end records hold the
/// directory's, with Zip64 fields only where they overflow (<see cref="ZipLimits"/>).
/// </summary>
/// <remarks>
/// <para>
/// A package's bytes depend only on its parts, their order and level, the runtime's deflate output, and whether the
/// stream seeks: the same for the same, written synchronously or asynchronously.
/// </para>
/// <para>
/// A synchronous call writes the stream only with <c>Write</c> and <see cref="Stream.Flush"/>, an asynchronous one only
/// with <c>WriteAsync</c> and <see cref="Stream.FlushAsync()"/>, on every target: the deflater writes into memory, and
/// only the buffers, a large stored entry's writes, headers, patches, descriptors and directory reach the stream. A patch
/// seeks the stream (<see cref="Stream.Seek"/>) to the header and back, in either mode. The buffers, headers, patches,
/// descriptors and directory are written from arrays (<see cref="Stream.Write(byte[], int, int)"/>), and a large stored
/// entry's writes from spans and memory where the target's streams take them, else from arrays.
/// </para>
/// <para>
/// Offsets count from the stream's position when the writer is made, on a stream that seeks, and from 0 on one that
/// can't, as <see cref="ZipArchive"/> counts them. Whether the stream seeks is taken then too.
/// </para>
/// <para>
/// Once a write, seek, flush or dispose of the stream throws, the writer is faulted: its pooled buffers are returned,
/// and from then on opening, writing and closing entries and disposing it write nothing and throw nothing, so that a
/// dispose doesn't replace the stream's exception. The package is left without its directory.
/// </para>
/// </remarks>
internal sealed class ZipWriter
{
	/// <summary>
	/// The size of the pooled buffers, and of the stream's writes of an entry's buffered data, but for its last: the most
	/// data a whole entry has.
	/// </summary>
	public const int BufferSize = 65536;

	/// <summary>The stream the package is written to.</summary>
	private readonly Stream stream;

	/// <summary>Whether disposing the writer leaves <see cref="stream"/> open.</summary>
	private readonly bool leaveOpen;

	/// <summary>The offset of the package's first byte: the stream's position on a stream that seeks, else 0.</summary>
	private readonly long start;

	/// <summary>The central directory's headers, written when the package ends.</summary>
	private readonly ZipBuffers directory;

	/// <summary>The bytes of the header, descriptor or end records being written; grown for a long name.</summary>
	private byte[] scratch = new byte[256];

	/// <summary>The bytes written to the stream.</summary>
	private long written;

	/// <summary>The entries closed.</summary>
	private long entries;

	/// <summary>The entry open, or <see langword="null"/>.</summary>
	private ZipEntryStream? open;

	/// <summary>Whether the writer has been disposed.</summary>
	private bool disposed;

	/// <summary>The pooled buffers held (<see cref="RentedBuffers"/>), counted on every thread that rents or returns one.</summary>
	private int rentedBuffers;

	/// <summary>The owned segments the entries hold (<see cref="HeldSegments"/>), counted on every thread.</summary>
	private int heldSegments;

	/// <summary>Starts a package whose entries deflate on the writing thread.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the writer leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where its fields overflow into Zip64's.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	public ZipWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits)
		: this(stream, leaveOpen, level, limits, 1)
	{
	}

	/// <summary>Starts a package.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the writer leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where its fields overflow into Zip64's.</param>
	/// <param name="threads">
	/// The threads that deflate a chunked entry beside the writing thread (<see cref="Threads"/>): 1 or more, as
	/// <c>ExcelWriterOptions.CompressionThreads</c> resolves (-1 is <see cref="Environment.ProcessorCount"/>).
	/// </param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threads"/> is 0 or less.</exception>
	public ZipWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits, int threads)
	{
		if (!stream.CanWrite)
		{
			throw new ArgumentException("The stream can't be written.", nameof(stream));
		}

		if (threads < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(threads), threads, "A zip writer has 1 thread or more.");
		}

		this.stream = stream;
		this.leaveOpen = leaveOpen;
		Level = level;
		Limits = limits;
		Threads = threads;
		Seekable = stream.CanSeek;
		start = Seekable ? stream.Position : 0;
		directory = new ZipBuffers(this);
	}

	/// <summary>The compression of every entry.</summary>
	public CompressionLevel Level { get; }

	/// <summary>Where the package's fields overflow into Zip64's.</summary>
	public ZipLimits Limits { get; }

	/// <summary>
	/// Whether the stream seeks, as the writer was made: a stored entry past one buffer is then patched, its CRC and sizes
	/// written into its local header after its data (<see cref="PatchLocalHeaderAsync"/>), rather than streamed.
	/// </summary>
	public bool Seekable { get; }

	/// <summary>Whether a write, seek, flush or dispose of the stream has thrown, so that the writer writes nothing more.</summary>
	public bool Faulted { get; private set; }

	/// <summary>The offset of the next byte written: the package's start and the bytes written so far.</summary>
	public long Position => start + written;

	/// <summary>
	/// The threads that deflate a chunked entry (<see cref="Open(string, bool)"/>) beside the writing thread: 1, the
	/// default, chunks nothing, and every entry deflates on the writing thread as before; more chunks every entry opened
	/// chunked, on net8.0 and net10.0 in chunks of <see cref="ChunkSize"/> on up to that many threads, and on the other
	/// builds through one deflater on one thread (<see cref="DeflateStage"/>).
	/// </summary>
	public int Threads { get; }

	/// <summary>
	/// The input a chunk of a chunked entry takes: <see cref="DeflateSizes.ChunkSize"/>, and less in tests (1 byte, 64
	/// bytes), set before the first entry opens.
	/// </summary>
	internal int ChunkSize { get; set; } = DeflateSizes.ChunkSize;

	/// <summary>
	/// The threads a chunked entry takes at least while a parallel write runs on a sheet: its degree, which the sheet's
	/// sink sets as the write begins and sets back to 0 as it ends. An entry opened chunked meanwhile deflates on
	/// <c>Math.Max(</c><see cref="Threads"/><c>, ParallelThreads)</c> threads, and an entry already open switches when its
	/// sink asks (<see cref="ZipEntryStream.UseChunks"/>).
	/// </summary>
	internal int ParallelThreads { get; set; }

	/// <summary>The pooled buffers held, on every thread, for tests that check that they all return.</summary>
	internal int RentedBuffers => Volatile.Read(ref rentedBuffers);

	/// <summary>
	/// The owned segments the entries took (<see cref="Hold"/>), and the copies a chunked entry's stage made of bytes
	/// written to it, that haven't returned yet, for tests that check that they all return.
	/// </summary>
	internal int HeldSegments => Volatile.Read(ref heldSegments);

	/// <summary>
	/// Called with each deflate stage a chunked entry makes (<see cref="DeflateStages.Create"/>), for tests that hold or
	/// fail its chunks (<see cref="ThreadedDeflateStage.BeforeDeflate"/>) or check that its threads end
	/// (<see cref="ThreadedDeflateStage.StartedThreads"/>). <see langword="null"/> by default.
	/// </summary>
	internal Action<DeflateStage>? StageCreated { get; set; }

	/// <summary>
	/// Opens an entry, which writes nothing: its local header waits until its data passes one buffer, or it closes. Once
	/// the writer is faulted, the entry writes nothing.
	/// </summary>
	/// <param name="name">The entry's name, ASCII.</param>
	/// <returns>The entry's stream.</returns>
	/// <exception cref="InvalidOperationException">Another entry is open, or the name isn't ASCII or is longer than 65,535 characters.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public ZipEntryStream Open(string name) => Open(name, false);

	/// <summary>
	/// Opens an entry, which writes nothing: its local header waits until its data passes one buffer, or it closes. Once
	/// the writer is faulted, the entry writes nothing.
	/// </summary>
	/// <param name="name">The entry's name, ASCII.</param>
	/// <param name="chunked">
	/// Whether the entry's data is deflated off the writing thread when the writer has threads (<see cref="Threads"/>, or
	/// <see cref="ParallelThreads"/> while a parallel write runs): a part streamed in many writes, a sheet or the shared
	/// strings. Parts written whole stay on the writing thread.
	/// </param>
	/// <returns>The entry's stream.</returns>
	/// <exception cref="InvalidOperationException">Another entry is open, or the name isn't ASCII or is longer than 65,535 characters.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public ZipEntryStream Open(string name, bool chunked)
	{
		if (Faulted)
		{
			return new ZipEntryStream(this, name, chunked);
		}

		ObjectDisposedException.ThrowIf(disposed, this);
		if (open is not null)
		{
			throw new InvalidOperationException($"The zip entry \"{open.Name}\" is still open: an entry is written after the one before it is closed.");
		}

		if (name.Length > 0xFFFF)
		{
			throw new InvalidOperationException("A zip entry's name is at most 65,535 characters.");
		}

		foreach (var c in name)
		{
			if (c > 0x7F)
			{
				throw new InvalidOperationException($"The zip entry name \"{name}\" isn't ASCII: part names are written without the UTF-8 flag.");
			}
		}

		open = new ZipEntryStream(this, name, chunked);

		return open;
	}

	/// <summary>
	/// Finishes the package: closes an entry left open, writes the central directory and the end records, flushes the
	/// stream and disposes it unless it was left open. A faulted writer writes nothing and throws nothing, and disposes
	/// the stream unless it was left open. Calling it again does nothing.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the package is finished.</returns>
	public async ValueTask DisposeAsync(bool async)
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		var failed = Faulted;
		try
		{
			if (!Faulted && open is { } entry)
			{
				await entry.CloseAsync(async).ConfigureAwait(false);
			}

			if (!Faulted)
			{
				var directoryOffset = Position;
				for (var i = 0; i < directory.Count; i++)
				{
					await WriteAsync(directory[i], directory.LengthOf(i), async, CancellationToken.None).ConfigureAwait(false);
				}

				var directorySize = Position - directoryOffset;
				directory.Clear();
				var length = ZipRecords.End(Scratch(ZipRecords.EndLength), entries, directorySize, directoryOffset, Position, Limits);
				await WriteAsync(scratch, length, async, CancellationToken.None).ConfigureAwait(false);
				await FlushAsync(async).ConfigureAwait(false);
			}
		}
		catch
		{
			failed = true;

			throw;
		}
		finally
		{
			directory.Clear();
			if (!leaveOpen)
			{
				await CloseStreamAsync(async, failed).ConfigureAwait(false);
			}
		}
	}

	/// <summary>Writes an entry's local header, at the package's position, which is the entry's offset.</summary>
	/// <param name="entry">The entry, whose shape is known.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the header is written.</returns>
	internal ValueTask WriteLocalHeaderAsync(ZipEntryStream entry, bool async, CancellationToken cancellationToken)
	{
		var length = ZipRecords.Local(Scratch(ZipRecords.LocalLength + entry.Name.Length + ZipRecords.LocalExtraLength), entry);

		return WriteAsync(scratch, length, async, cancellationToken);
	}

	/// <summary>
	/// Patches a patched entry's local header as it closes: its real CRC and sizes
	/// (<see cref="ZipRecords.LocalSizes"/>), or, once it has fallen back to the streamed form, its version needed and
	/// flags (<see cref="ZipRecords.LocalVersion"/>).
	/// </summary>
	/// <param name="entry">The entry, closed, its CRC, sizes, version and flags set.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the header is patched and the stream is back at the package's end.</returns>
	internal ValueTask PatchLocalHeaderAsync(ZipEntryStream entry, bool async)
	{
		if ((entry.Flags & ZipRecords.Streamed) != 0)
		{
			var length = ZipRecords.LocalVersion(Scratch(ZipRecords.LocalVersionLength), entry);

			return PatchAsync(entry.Offset + ZipRecords.LocalVersionAt, length, async);
		}

		var sizes = ZipRecords.LocalSizes(Scratch(ZipRecords.LocalCrcLength), entry);

		return PatchAsync(entry.Offset + ZipRecords.LocalCrcAt, sizes, async);
	}

	/// <summary>Writes a streamed entry's descriptor.</summary>
	/// <param name="entry">The entry, closed.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the descriptor is written.</returns>
	internal ValueTask WriteDescriptorAsync(ZipEntryStream entry, bool async)
	{
		var length = ZipRecords.Descriptor(Scratch(ZipRecords.DescriptorLength), entry);

		return WriteAsync(scratch, length, async, CancellationToken.None);
	}

	/// <summary>Adds a closed entry's central header to the directory, and lets the next entry open.</summary>
	/// <param name="entry">The entry.</param>
	internal void AddEntry(ZipEntryStream entry)
	{
		var length = ZipRecords.Central(Scratch(ZipRecords.CentralLength + entry.Name.Length + ZipRecords.CentralExtraLength), entry, Limits);
		directory.Append(scratch.AsSpan(0, length));
		entries++;
	}

	/// <summary>Lets the next entry open, once an entry is closed or released.</summary>
	/// <param name="entry">The entry.</param>
	internal void Closed(ZipEntryStream entry)
	{
		if (open == entry)
		{
			open = null;
		}
	}

	/// <summary>Writes bytes to the stream, faulting the writer if the stream throws.</summary>
	/// <param name="buffer">The bytes, from its start.</param>
	/// <param name="count">How many.</param>
	/// <param name="async">Whether to write with <c>WriteAsync</c> rather than <see cref="Stream.Write(byte[], int, int)"/>.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the bytes are written.</returns>
	internal async ValueTask WriteAsync(byte[] buffer, int count, bool async, CancellationToken cancellationToken)
	{
		try
		{
			if (async)
			{
				await stream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, count), cancellationToken).ConfigureAwait(false);
			}
			else
			{
				stream.Write(buffer, 0, count);
			}
		}
		catch
		{
			Fault();

			throw;
		}

		written += count;
	}

#if NET || NETSTANDARD2_1_OR_GREATER
	/// <summary>
	/// Writes bytes to the stream synchronously, in one write, faulting the writer if the stream throws: a stored entry's
	/// data past one buffer, written through.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	internal void Write(ReadOnlySpan<byte> bytes)
	{
		try
		{
			stream.Write(bytes);
		}
		catch
		{
			Fault();

			throw;
		}

		written += bytes.Length;
	}
#else
	/// <summary>
	/// Writes bytes to the stream synchronously, in one write, faulting the writer if the stream throws: a stored entry's
	/// data past one buffer, written through. The target's streams take no spans, so the caller's array is written.
	/// </summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	internal void Write(byte[] buffer, int offset, int count)
	{
		try
		{
			stream.Write(buffer, offset, count);
		}
		catch
		{
			Fault();

			throw;
		}

		written += count;
	}
#endif

	/// <summary>
	/// Writes bytes to the stream asynchronously, in one write, faulting the writer if the stream throws (a canceled
	/// write too): a stored entry's data past one buffer, written through. Where the target's streams take no memory,
	/// the caller's array is written (<c>WriteAsync(byte[], int, int, CancellationToken)</c>).
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="cancellationToken">Cancels the write.</param>
	/// <returns>A task that completes when the bytes are written.</returns>
	internal async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
	{
		try
		{
			await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			Fault();

			throw;
		}

		written += bytes.Length;
	}

	/// <summary>
	/// Overwrites bytes already written, on a stream that seeks: seeks to them, writes the scratch buffer's first bytes
	/// over them (with <c>WriteAsync</c> or <see cref="Stream.Write(byte[], int, int)"/>, as any write), and seeks back
	/// to the package's end, where the next write goes. It writes nothing new, so <see cref="Position"/> stays. A seek
	/// or write that throws faults the writer.
	/// </summary>
	/// <param name="position">The stream's position of the first byte overwritten.</param>
	/// <param name="count">How many bytes.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the bytes are written and the stream is back at the end.</returns>
	private async ValueTask PatchAsync(long position, int count, bool async)
	{
		try
		{
			stream.Seek(position, SeekOrigin.Begin);
			if (async)
			{
				await stream.WriteAsync(new ReadOnlyMemory<byte>(scratch, 0, count), CancellationToken.None).ConfigureAwait(false);
			}
			else
			{
				stream.Write(scratch, 0, count);
			}

			stream.Seek(Position, SeekOrigin.Begin);
		}
		catch
		{
			Fault();

			throw;
		}
	}

	/// <summary>
	/// Faults the writer: nothing more is written, and the open entry's buffers and the directory's return to the pool.
	/// </summary>
	internal void Fault()
	{
		if (Faulted)
		{
			return;
		}

		Faulted = true;
		open?.Release();
		directory.Clear();
	}

	/// <summary>Rents a buffer of <see cref="BufferSize"/> bytes.</summary>
	/// <returns>The buffer.</returns>
	internal byte[] Rent()
	{
		Interlocked.Increment(ref rentedBuffers);

		return ArrayPool<byte>.Shared.Rent(BufferSize);
	}

	/// <summary>Returns a buffer to the pool, on any thread.</summary>
	/// <param name="buffer">A buffer from <see cref="Rent"/>.</param>
	internal void Return(byte[] buffer)
	{
		Interlocked.Decrement(ref rentedBuffers);
		ArrayPool<byte>.Shared.Return(buffer);
	}

	/// <summary>Counts a segment an entry takes (<see cref="ZipEntryStream.AppendAsync"/>), which <see cref="Return(OwnedSegment)"/> returns.</summary>
	/// <param name="segment">The segment; the empty one isn't counted.</param>
	internal void Hold(OwnedSegment segment)
	{
		if (!segment.IsEmpty)
		{
			Interlocked.Increment(ref heldSegments);
		}
	}

	/// <summary>
	/// Returns a segment an entry took to the pool, on any thread: once its bytes are deflated or written, or as the entry
	/// is released.
	/// </summary>
	/// <param name="segment">A segment counted by <see cref="Hold"/>; the empty one does nothing.</param>
	internal void Return(OwnedSegment segment)
	{
		if (!segment.IsEmpty)
		{
			Interlocked.Decrement(ref heldSegments);
			segment.Return();
		}
	}

	/// <summary>The scratch buffer, grown to hold a record.</summary>
	/// <param name="length">The record's most bytes.</param>
	/// <returns>The buffer.</returns>
	private Span<byte> Scratch(int length)
	{
		if (scratch.Length < length)
		{
			scratch = new byte[length];
		}

		return scratch;
	}

	/// <summary>Flushes the stream, faulting the writer if it throws.</summary>
	/// <param name="async">Whether to flush with <see cref="Stream.FlushAsync()"/> rather than <see cref="Stream.Flush"/>.</param>
	/// <returns>A task that completes when the stream is flushed.</returns>
	private async ValueTask FlushAsync(bool async)
	{
		try
		{
			if (async)
			{
				await stream.FlushAsync().ConfigureAwait(false);
			}
			else
			{
				stream.Flush();
			}
		}
		catch
		{
			Fault();

			throw;
		}
	}

	/// <summary>
	/// Disposes the stream: asynchronously where the target's streams can (else synchronously), or synchronously. Its
	/// exception is the package's unless something failed before, which is the one that says why.
	/// </summary>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <param name="failed">Whether something failed before, so that the stream's exception is dropped.</param>
	/// <returns>A task that completes when the stream is disposed.</returns>
	private async ValueTask CloseStreamAsync(bool async, bool failed)
	{
		try
		{
			if (async)
			{
				await stream.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				stream.Dispose();
			}
		}
		catch
		{
			Faulted = true;
			if (!failed)
			{
				throw;
			}
		}
	}
}
