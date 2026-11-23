using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// An entry's stream, write-only: what is written is deflated (or stored, at <see cref="CompressionLevel.NoCompression"/>)
/// into the entry's pooled buffers, which reach the package's stream whole as each fills and is followed by more data;
/// closing it writes the rest, the descriptor of a streamed entry, and adds the entry to the central directory
/// (<see cref="ZipWriter"/>). Synchronous calls write the package's stream synchronously, asynchronous ones
/// asynchronously. <see cref="Flush"/> does nothing: flushing the deflater would change the bytes. Once the writer is
/// faulted, writing and closing do nothing.
/// </summary>
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

	/// <summary>The bytes of data written to the package's stream.</summary>
	private long dataWritten;

	/// <summary>Whether the local header has been written.</summary>
	private bool headerWritten;

	/// <summary>Whether the entry has been closed.</summary>
	private bool closed;

	/// <summary>Opens an entry.</summary>
	/// <param name="writer">The package's writer.</param>
	/// <param name="name">The entry's name, ASCII.</param>
	public ZipEntryStream(ZipWriter writer, string name)
	{
		this.writer = writer;
		Name = name;
		data = new ZipBuffers(writer);
		stored = writer.Level == CompressionLevel.NoCompression;
		if (!stored)
		{
			sink = new DeflaterSink(data);
		}
	}

	/// <summary>The entry's name.</summary>
	public string Name { get; }

	/// <summary>The compression method: 0, stored, or 8, deflated.</summary>
	public ushort Method => stored ? (ushort)0 : (ushort)8;

	/// <summary>
	/// The general purpose flags: bits 1-2 of the deflate level (<c>0x0006</c> at <see cref="CompressionLevel.Fastest"/>,
	/// <c>0x0002</c> at <c>SmallestSize</c>), and bit 3 for a streamed entry. Set with the local header.
	/// </summary>
	public ushort Flags { get; private set; }

	/// <summary>The version needed to extract: 45 for a streamed entry or one past the 32-bit offsets, else 20. Set with the local header.</summary>
	public ushort VersionNeeded { get; private set; }

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

	/// <summary>Does nothing: the data reaches the package's stream in whole buffers, and the deflater isn't flushed.</summary>
	public override void Flush()
	{
	}

	/// <summary>Does nothing: the data reaches the package's stream in whole buffers, and the deflater isn't flushed.</summary>
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

		for (var at = 0; at < bytes.Count; at += ZipWriter.BufferSize)
		{
			Take(bytes.Array!, bytes.Offset + at, Math.Min(ZipWriter.BufferSize, bytes.Count - at));
			SyncOrAsync.Wait(WriteFullBuffersAsync(false, CancellationToken.None));
		}
#endif
	}

	/// <summary>Writes bytes to the entry, writing the package's stream asynchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken.</param>
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

		while (!buffer.IsEmpty)
		{
			var slice = buffer.Slice(0, Math.Min(ZipWriter.BufferSize, buffer.Length));
			Take(slice);
			SyncOrAsync.Wait(WriteFullBuffersAsync(false, CancellationToken.None));
			buffer = buffer.Slice(slice.Length);
		}
	}

	/// <summary>Writes bytes to the entry, writing the package's stream asynchronously.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken.</param>
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
	/// Closes the entry: finishes the deflater, writes the local header if it waits, the rest of the data and a streamed
	/// entry's descriptor, and adds the entry to the central directory. The pooled buffers return, whatever happens.
	/// Calling it again does nothing, and so does closing an entry of a faulted writer.
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

			Finish();
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
	/// Returns the entry's buffers to the pool and drops its deflater, as it closes or as the writer faults. Calling it
	/// again does nothing.
	/// </summary>
	internal void Release()
	{
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

	/// <summary>Writes bytes to the entry, writing the package's stream asynchronously, a buffer's worth at a time.</summary>
	/// <param name="buffer">The bytes.</param>
	/// <param name="cancellationToken">Cancels the write: before any of it, the bytes aren't taken.</param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	private async ValueTask WriteMemoryAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		CheckOpen();
		if (writer.Faulted)
		{
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();
		while (!buffer.IsEmpty)
		{
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
	/// entry is streamed: it has more than one buffer of data.
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

	/// <summary>Writes the first buffers, which are full, after the streamed local header the first time.</summary>
	/// <param name="full">How many.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private async ValueTask WriteBuffersAsync(int full, bool async, CancellationToken cancellationToken)
	{
		if (!headerWritten)
		{
			await WriteHeaderAsync(true, async, cancellationToken).ConfigureAwait(false);
		}

		for (var i = 0; i < full; i++)
		{
			await writer.WriteAsync(data[i], ZipWriter.BufferSize, async, cancellationToken).ConfigureAwait(false);
			dataWritten += ZipWriter.BufferSize;
		}

		data.RemoveFirst(full);
	}

	/// <summary>Writes the local header, at the package's position, which becomes the entry's offset.</summary>
	/// <param name="streamed">Whether the entry is streamed, rather than whole.</param>
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
