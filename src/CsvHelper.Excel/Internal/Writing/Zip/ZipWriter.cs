using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Writes a zip one entry at a time, forward only, so that a stream that can't seek works too, and the same bytes go to
/// any stream (<see cref="ZipRecords"/>). An entry's data goes into pooled buffers of <see cref="BufferSize"/> bytes and
/// reaches the stream in whole buffers, but for its last. An entry closed with at most one buffer of data and sizes
/// within 32 bits is written whole: its local header holds its CRC and sizes. Any other entry is streamed, its local
/// header written as its data passes one buffer: bit 3, version 4.5, CRC and sizes 0 with a Zip64 extra of zeros, and a
/// signed descriptor with 8-byte sizes after the data. The central directory, kept in pooled buffers until the end,
/// holds every entry's real sizes and offset, with Zip64 fields only where they overflow (<see cref="ZipLimits"/>), and
/// so do the end records.
/// </summary>
/// <remarks>
/// <para>
/// A synchronous call writes the stream only with <see cref="Stream.Write(byte[], int, int)"/> and
/// <see cref="Stream.Flush"/>, an asynchronous one only with <c>WriteAsync</c> and <see cref="Stream.FlushAsync()"/>, on
/// every target: the deflater writes into memory, and only the buffers, headers, descriptors and directory reach the
/// stream.
/// </para>
/// <para>
/// Offsets count from the stream's position when the writer is made, on a stream that seeks, and from 0 on one that
/// can't, as <see cref="ZipArchive"/> counts them.
/// </para>
/// <para>
/// Once a write, flush or dispose of the stream throws, the writer is faulted: its pooled buffers are returned, and from
/// then on opening, writing and closing entries and disposing it write nothing and throw nothing, so that a dispose
/// doesn't replace the stream's exception. The package is left without its directory.
/// </para>
/// </remarks>
internal sealed class ZipWriter
{
	/// <summary>The size of the pooled buffers, and of the stream's writes of an entry's data.</summary>
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

	/// <summary>Starts a package.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the writer leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where its fields overflow into Zip64's.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	public ZipWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits)
	{
		if (!stream.CanWrite)
		{
			throw new ArgumentException("The stream can't be written.", nameof(stream));
		}

		this.stream = stream;
		this.leaveOpen = leaveOpen;
		Level = level;
		Limits = limits;
		start = stream.CanSeek ? stream.Position : 0;
		directory = new ZipBuffers(this);
	}

	/// <summary>The compression of every entry.</summary>
	public CompressionLevel Level { get; }

	/// <summary>Where the package's fields overflow into Zip64's.</summary>
	public ZipLimits Limits { get; }

	/// <summary>Whether a write, flush or dispose of the stream has thrown, so that the writer writes nothing more.</summary>
	public bool Faulted { get; private set; }

	/// <summary>The offset of the next byte written: the package's start and the bytes written so far.</summary>
	public long Position => start + written;

	/// <summary>The pooled buffers held, for tests that check that they all return.</summary>
	internal int RentedBuffers { get; private set; }

	/// <summary>
	/// Opens an entry, which writes nothing: its local header waits until its data passes one buffer, or it closes. Once
	/// the writer is faulted, the entry writes nothing.
	/// </summary>
	/// <param name="name">The entry's name, ASCII.</param>
	/// <returns>The entry's stream.</returns>
	/// <exception cref="InvalidOperationException">Another entry is open, or the name isn't ASCII or is longer than 65,535 characters.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public ZipEntryStream Open(string name)
	{
		if (Faulted)
		{
			return new ZipEntryStream(this, name);
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

		open = new ZipEntryStream(this, name);

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
		RentedBuffers++;

		return ArrayPool<byte>.Shared.Rent(BufferSize);
	}

	/// <summary>Returns a buffer to the pool.</summary>
	/// <param name="buffer">A buffer from <see cref="Rent"/>.</param>
	internal void Return(byte[] buffer)
	{
		RentedBuffers--;
		ArrayPool<byte>.Shared.Return(buffer);
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
