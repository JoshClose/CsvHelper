#if NET
using System;
using System.IO;
using System.IO.Compression;

namespace CsvHelper.Excel;

/// <summary>
/// Deflates one chunk of a chunked entry (<see cref="DeflateStage"/>) on net8.0 and net10.0: a fresh
/// <see cref="GZipStream"/> fed the chunk's input in writes of <see cref="DeflateSizes.WriteSize"/> from its start, so that
/// the output depends on the input alone. A chunk that isn't the last is sync-flushed (<see cref="Stream.Flush"/>, which
/// ends it at a byte boundary with <c>00 00 FF FF</c>), then disposed, and the empty final block that disposing adds
/// (<c>03 00</c>) is checked and dropped, so that the next chunk's blocks follow it. The last chunk is disposed, its final
/// block kept: empty, it is <c>03 00</c>. Either way the GZip header is checked and dropped (<see cref="DeflaterSink"/>), and the trailer taken off for the
/// chunk's CRC and length mod 2^32, which is checked. Safe to use on one thread at a time; a stage thread keeps one.
/// </summary>
internal sealed class ChunkDeflater
{
	/// <summary>The final block an empty deflate stream ends with, after a sync flush: one fixed block of only its end.</summary>
	private static ReadOnlySpan<byte> EmptyFinalBlock => [0x03, 0x00];

	/// <summary>The level the chunks are deflated at.</summary>
	private readonly CompressionLevel level;

	/// <summary>Creates a deflater.</summary>
	/// <param name="level">
	/// The writer's level: <see cref="CompressionLevel.Fastest"/> is zlib's level 2 on net10.0 (level 1 on net8.0, which
	/// has no way to ask for 2), and the other levels are their own. Not <see cref="CompressionLevel.NoCompression"/>.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is <see cref="CompressionLevel.NoCompression"/>.</exception>
	public ChunkDeflater(CompressionLevel level)
	{
		if (level == CompressionLevel.NoCompression)
		{
			throw new ArgumentOutOfRangeException(nameof(level), level, "A stored entry isn't deflated.");
		}

		this.level = level;
	}

	/// <summary>Deflates a chunk's input into the chunk, whose output is empty.</summary>
	/// <param name="input">The chunk's input: <see cref="ZipWriter.ChunkSize"/> bytes for a chunk that isn't the last, 0 or more for the last.</param>
	/// <param name="last">Whether the chunk is the entry's last.</param>
	/// <param name="chunk">The chunk: its output, CRC and input length are set.</param>
	/// <exception cref="InvalidOperationException">The deflater's GZip header, final block or trailer is not what it should be.</exception>
	public void Deflate(ReadOnlySpan<byte> input, bool last, DeflatedChunk chunk)
	{
		if (input.IsEmpty)
		{
			if (!last)
			{
				throw new InvalidOperationException("Only the last chunk of an entry can be empty.");
			}

			// .NET's deflater writes nothing for no input: an empty last chunk is the empty final block, as zlib ends one.
			chunk.Output.Append(EmptyFinalBlock);
			chunk.Crc = 0;
			chunk.InputLength = 0;

			return;
		}

		var sink = new DeflaterSink(chunk.Output);
		var gzip = Create(sink);
		try
		{
			for (var at = 0; at < input.Length; at += DeflateSizes.WriteSize)
			{
				gzip.Write(input.Slice(at, Math.Min(DeflateSizes.WriteSize, input.Length - at)));
			}

			if (!last)
			{
				gzip.Flush();
			}
		}
		catch
		{
			sink.Discard();
			try
			{
				gzip.Dispose();
			}
			catch
			{
				// The deflater finishes into nothing, and its failure would hide the one that stopped the chunk.
			}

			throw;
		}

		gzip.Dispose();
		var (crc, size) = sink.TakeTrailer();
		if (size != (uint)input.Length)
		{
			throw new InvalidOperationException($"The deflater's GZip trailer gives a chunk's size mod 2^32 as {size}, not {(uint)input.Length}.");
		}

		if (!last)
		{
			Span<byte> end = stackalloc byte[2];
			if (chunk.Output.Length < end.Length)
			{
				throw new InvalidOperationException("The deflater's sync-flushed chunk ended without its final block.");
			}

			chunk.Output.RemoveLast(end);
			if (!end.SequenceEqual(EmptyFinalBlock))
			{
				throw new InvalidOperationException($"The deflater ended a sync-flushed chunk with 0x{end[0]:X2} 0x{end[1]:X2}, not the empty final block 03 00.");
			}
		}

		chunk.Crc = crc;
		chunk.InputLength = input.Length;
	}

	/// <summary>Makes a chunk's deflater, at the level, writing into the sink.</summary>
	/// <param name="sink">The chunk's output.</param>
	/// <returns>The deflater.</returns>
	private GZipStream Create(DeflaterSink sink)
	{
#if NET10_0_OR_GREATER
		if (level == CompressionLevel.Fastest)
		{
			return new GZipStream(sink, new ZLibCompressionOptions { CompressionLevel = 2, CompressionStrategy = ZLibCompressionStrategy.Default }, leaveOpen: true);
		}

#endif
		return new GZipStream(sink, level, leaveOpen: true);
	}
}
#endif
