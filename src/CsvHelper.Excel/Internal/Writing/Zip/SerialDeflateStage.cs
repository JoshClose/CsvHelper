using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace CsvHelper.Excel;

/// <summary>
/// The deflate stage of a chunked entry on the builds without chunked deflate, net47 and netstandard
/// (<see cref="DeflateStage"/>): one background thread deflates the whole entry through one <see cref="DeflateStream"/>
/// at the writer's level, fed each append in writes of <see cref="DeflateSizes.WriteSize"/> from its start, as the serial
/// entry feeds its writes, so that the bytes are the serial entry's and the tests' threadless stage's there. Each
/// append is a chunk: what the deflater gave for it (often nothing, as it fills its window), with its input's CRC
/// (<see cref="Crc32.Append"/>, on the thread) and length. Completing it disposes the deflater on the thread, whose last
/// output is the last chunk; with nothing appended, the last chunk is <c>03 00</c>, made on the writing thread, and no
/// thread starts. Compiled on every target, so that its tests run on each; the net8.0 and net10.0 builds use
/// <c>ChunkedDeflateStage</c>.
/// </summary>
/// <remarks>
/// A segment appended is handed to the thread uncopied, and returned once deflated; bytes appended as a span are copied
/// into a pooled segment of their own first. Its <see cref="DeflateStage.Threads"/> is 1, whatever it is asked, and
/// <see cref="DeflateStage.RaiseThreads"/> changes nothing.
/// </remarks>
internal sealed class SerialDeflateStage : ThreadedDeflateStage
{
	/// <summary>Whether anything was appended, which starts the thread.</summary>
	private bool appended;

	/// <summary>Whether the stage is complete.</summary>
	private bool completed;

	/// <summary>Creates the stage of a chunked entry, which starts its thread with the first byte appended.</summary>
	/// <param name="writer">The writer: its level (not <see cref="CompressionLevel.NoCompression"/>) and its pool.</param>
	/// <exception cref="ArgumentOutOfRangeException">The writer's level is <see cref="CompressionLevel.NoCompression"/>.</exception>
	public SerialDeflateStage(ZipWriter writer)
		: base(writer, 1, 1)
	{
	}

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
			var job = NewJob();
			job.Add(held, 0, segment.Length);
			appended = true;
			Hand(job);
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
		if (bytes.IsEmpty)
		{
			return;
		}

		var array = ArrayPool<byte>.Shared.Rent(bytes.Length);
		bytes.CopyTo(array);
		var segment = new OwnedSegment(array, bytes.Length);
		Writer.Hold(segment);
		Append(segment);
	}

	/// <inheritdoc/>
	public override void Complete()
	{
		CheckOpen();
		completed = true;
		var job = NewJob();
		job.Last = true;
		if (appended)
		{
			Hand(job);
		}
		else
		{
			DeflateHere(job);
		}
	}

	/// <inheritdoc/>
	protected override object CreateWorker() => new Worker(Writer.Level);

	/// <summary>
	/// Deflates an append, its one piece fed in writes of <see cref="DeflateSizes.WriteSize"/> from its start, with its CRC;
	/// or, for the last chunk, disposes the deflater (<c>03 00</c> when there is none: nothing was appended).
	/// </summary>
	/// <param name="worker">The thread's worker (<see cref="Worker"/>).</param>
	/// <param name="job">The append's job, or the last.</param>
	/// <param name="chunk">The chunk.</param>
	protected override void Deflate(object worker, DeflateJob job, DeflatedChunk chunk)
	{
		var state = (Worker)worker;
		state.Sink.Target = chunk;
		try
		{
			if (job.Last)
			{
				if (state.Deflater is null)
				{
					chunk.Output.Append([0x03, 0x00]);
				}
				else
				{
					var deflater = state.Deflater;
					state.Deflater = null;
					deflater.Dispose();
				}

				return;
			}

			var piece = job[0];
			var array = piece.Segment.Segment.Array!;
			state.Deflater ??= new DeflateStream(state.Sink, state.Level, leaveOpen: true);
			for (var at = 0; at < piece.Length; at += DeflateSizes.WriteSize)
			{
				state.Deflater.Write(array, piece.Offset + at, Math.Min(DeflateSizes.WriteSize, piece.Length - at));
			}

			chunk.Crc = Crc32.Append(0, piece.Span);
			chunk.InputLength = piece.Length;
		}
		finally
		{
			state.Sink.Target = null;
		}
	}

	/// <summary>Drops the deflater of an entry that didn't complete, disposed into nothing.</summary>
	/// <param name="worker">The worker (<see cref="Worker"/>).</param>
	protected override void EndWorker(object worker)
	{
		var state = (Worker)worker;
		if (state.Deflater is { } deflater)
		{
			state.Deflater = null;
			state.Sink.Target = null;
			try
			{
				deflater.Dispose();
			}
			catch
			{
				// The deflater finishes into nothing, and its failure would hide the one that stopped the stage.
			}
		}
	}

	/// <summary>Nothing: every append is a job as it comes.</summary>
	protected override void ReleaseInput()
	{
	}

	/// <summary>Throws if the thread has thrown, or the stage is complete or disposed.</summary>
	/// <exception cref="InvalidOperationException">The stage is complete or disposed.</exception>
	private void CheckOpen()
	{
		ThrowIfFaulted();
		if (completed || IsDisposed)
		{
			throw new InvalidOperationException("The deflate stage is complete: an entry's data ends once.");
		}
	}

	/// <summary>The thread's own state: the entry's deflater and where its output goes.</summary>
	/// <param name="level">The writer's level.</param>
	private sealed class Worker(CompressionLevel level)
	{
		/// <summary>The level the entry is deflated at.</summary>
		public CompressionLevel Level { get; } = level;

		/// <summary>The stream the deflater writes into: the chunk being deflated.</summary>
		public ChunkSink Sink { get; } = new();

		/// <summary>The deflater of the whole entry, made with the first append; <see langword="null"/> before, and once disposed.</summary>
		public DeflateStream? Deflater { get; set; }
	}

	/// <summary>The stream the entry's deflater writes into: the output of the chunk being deflated, or nothing.</summary>
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

#if NET || NETSTANDARD2_1_OR_GREATER
		/// <inheritdoc/>
		public override void Write(ReadOnlySpan<byte> buffer) => Target?.Output.Append(buffer);
#endif
	}
}
