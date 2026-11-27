using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// A deflate stage without threads (<see cref="DeflateStage"/>): each chunk is deflated on the writing thread as it is
/// cut, or as the stage completes. The tests' oracle: the library's stages give this one's bytes, for every input, cut
/// and thread count.
/// </summary>
/// <remarks>
/// On net8.0 and net10.0 it cuts the data at every <see cref="ZipWriter.ChunkSize"/> bytes and deflates each chunk
/// through a <c>ChunkDeflater</c>. On the other builds one <see cref="DeflateStream"/> takes the whole entry, each
/// append fed in writes of <see cref="DeflateSizes.WriteSize"/> from its start, as the serial entry feeds its writes, and
/// each append's output is a chunk, with the CRC of its input (slice-by-8); completing it disposes the deflater, whose
/// last output is the last chunk (<c>03 00</c> when nothing was appended).
/// </remarks>
internal sealed class InlineDeflateStage : DeflateStage
{
	/// <summary>The writer, whose pool the chunks' buffers come from.</summary>
	private readonly ZipWriter writer;

	/// <summary>The chunks deflated and not yet taken, in order.</summary>
	private readonly Queue<DeflatedChunk> ready = new();

#if NET
	/// <summary>The deflater of every chunk.</summary>
	private readonly ChunkDeflater deflater;

	/// <summary>The input a chunk takes.</summary>
	private readonly int chunkSize;

	/// <summary>The chunk being gathered: a pooled array of <see cref="chunkSize"/> bytes or more, made with its first byte.</summary>
	private byte[]? gathered;

	/// <summary>The bytes gathered.</summary>
	private int gatheredLength;
#else
	/// <summary>Where the deflater's output goes: the chunk of the current append.</summary>
	private readonly ChunkSink sink = new();

	/// <summary>The deflater of the whole entry, made with the first byte appended; <see langword="null"/> before, and once disposed.</summary>
	private DeflateStream? deflater;

	/// <summary>Whether anything was appended.</summary>
	private bool started;
#endif

	/// <summary>The chunks cut so far.</summary>
	private long cut;

	/// <summary>The most threads, which this stage only reports.</summary>
	private int threads;

	/// <summary>Whether the stage is complete.</summary>
	private bool completed;

	/// <summary>Whether the stage has been disposed.</summary>
	private bool disposed;

	/// <summary>Creates a stage for an entry of a writer.</summary>
	/// <param name="writer">The writer: its level (not <see cref="CompressionLevel.NoCompression"/>), its chunk size and its pool.</param>
	/// <param name="threads">The most threads, 1 or more, which this stage doesn't start.</param>
	public InlineDeflateStage(ZipWriter writer, int threads)
	{
		this.writer = writer;
		this.threads = Math.Max(threads, 1);
#if NET
		deflater = new ChunkDeflater(writer.Level);
		chunkSize = writer.ChunkSize;
#endif
	}

	/// <inheritdoc/>
	public override int Threads => threads;

	/// <summary>Never: every chunk is deflated as it is cut.</summary>
	public override bool MustWait => false;

	/// <inheritdoc/>
	public override void RaiseThreads(int threads) => this.threads = Math.Max(this.threads, threads);

	/// <inheritdoc/>
	public override void Append(OwnedSegment segment)
	{
		try
		{
			Append(segment.Span);
		}
		finally
		{
			writer.Return(segment);
		}
	}

	/// <inheritdoc/>
	public override void Append(ReadOnlySpan<byte> bytes)
	{
		CheckOpen();
#if NET
		while (!bytes.IsEmpty)
		{
			gathered ??= ArrayPool<byte>.Shared.Rent(chunkSize);
			var n = Math.Min(chunkSize - gatheredLength, bytes.Length);
			bytes.Slice(0, n).CopyTo(gathered.AsSpan(gatheredLength));
			gatheredLength += n;
			bytes = bytes.Slice(n);
			if (gatheredLength == chunkSize)
			{
				Cut(last: false);
			}
		}
#else
		if (bytes.IsEmpty)
		{
			return;
		}

		var chunk = NewChunk();
		sink.Target = chunk;
		try
		{
			deflater ??= new DeflateStream(sink, writer.Level, leaveOpen: true);
			started = true;
			var array = bytes.ToArray();
			for (var at = 0; at < array.Length; at += DeflateSizes.WriteSize)
			{
				deflater.Write(array, at, Math.Min(DeflateSizes.WriteSize, array.Length - at));
			}
		}
		catch
		{
			chunk.Clear();
			sink.Target = null;

			throw;
		}

		sink.Target = null;
		chunk.Crc = Crc32.Append(0, bytes);
		chunk.InputLength = bytes.Length;
		ready.Enqueue(chunk);
#endif
	}

	/// <inheritdoc/>
	public override void Complete()
	{
		CheckOpen();
		completed = true;
#if NET
		Cut(last: true);
#else
		var chunk = NewChunk();
		if (!started)
		{
			chunk.Output.Append([0x03, 0x00]);
		}
		else
		{
			sink.Target = chunk;
			try
			{
				deflater!.Dispose();
				deflater = null;
			}
			catch
			{
				chunk.Clear();

				throw;
			}
			finally
			{
				sink.Target = null;
			}
		}

		ready.Enqueue(chunk);
#endif
	}

	/// <inheritdoc/>
	public override bool TryTake(out DeflatedChunk chunk)
	{
		if (ready.Count > 0)
		{
			chunk = ready.Dequeue();

			return true;
		}

		chunk = null!;

		return false;
	}

	/// <inheritdoc/>
	public override DeflatedChunk? Take() => TryTake(out var chunk) ? chunk : null;

	/// <inheritdoc/>
	public override ValueTask<DeflatedChunk?> TakeAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return new(Take());
	}

	/// <inheritdoc/>
	public override void Return(DeflatedChunk chunk) => chunk.Clear();

	/// <inheritdoc/>
	public override void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		while (ready.Count > 0)
		{
			ready.Dequeue().Clear();
		}

#if NET
		if (gathered is not null)
		{
			ArrayPool<byte>.Shared.Return(gathered);
			gathered = null;
		}
#else
		if (deflater is not null)
		{
			sink.Target = null;
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
#endif
	}

	/// <summary>Throws if the stage is complete or disposed.</summary>
	/// <exception cref="InvalidOperationException">The stage is complete or disposed.</exception>
	private void CheckOpen()
	{
		if (completed || disposed)
		{
			throw new InvalidOperationException("The deflate stage is complete: an entry's data ends once.");
		}
	}

	/// <summary>A new chunk, next in order.</summary>
	/// <returns>The chunk.</returns>
	private DeflatedChunk NewChunk() => new(writer) { Sequence = cut++ };

#if NET
	/// <summary>Deflates the chunk gathered, which is full, or the last.</summary>
	/// <param name="last">Whether it is the last.</param>
	private void Cut(bool last)
	{
		var chunk = NewChunk();
		try
		{
			deflater.Deflate(gathered is null ? default : gathered.AsSpan(0, gatheredLength), last, chunk);
		}
		catch
		{
			chunk.Clear();

			throw;
		}

		gatheredLength = 0;
		ready.Enqueue(chunk);
	}
#else
	/// <summary>The stream the entry's deflater writes into: the current append's chunk, or nothing.</summary>
	private sealed class ChunkSink : Stream
	{
		/// <summary>The chunk the output goes to; <see langword="null"/> drops it.</summary>
		public DeflatedChunk? Target { get; set; }

		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => true;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush()
		{
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => Target?.Output.Append(new ReadOnlySpan<byte>(buffer, offset, count));
	}
#endif
}
