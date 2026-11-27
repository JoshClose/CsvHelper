#if NET
using System.IO.Compression;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// One chunk's deflate on .NET 8 and later (<see cref="ChunkDeflater"/>), at each level: a chunk that isn't the last is a
/// raw DeflateStream's output after a sync flush (fed the same 64 KB writes), so it ends at a byte boundary
/// (<c>00 00 FF FF</c>) without a final block, and the next chunk's blocks continue it; the last chunk is the whole
/// stream's, final; an empty last chunk is the empty final block, and only the last can be empty; each chunk gives its
/// input's CRC and length and returns its buffers; zlib's level 2 at <see cref="CompressionLevel.Fastest"/> on .NET 10 (1 on
/// .NET 8), whose GZip header (its XFL byte) is taken; and a deflater reused gives what a fresh one does.
/// </summary>
/// <remarks>
/// The trailer's size check can't fail through a real <see cref="GZipStream"/> (its size is the input's), so it isn't
/// planted here; that the trailer and the final block's <c>03 00</c> are taken off is what the byte equality with the raw
/// DeflateStream shows.
/// </remarks>
public class ChunkDeflaterTests
{
	/// <summary>The input lengths: 1, a 64 KB write either side, several writes, and a whole 1 MB chunk.</summary>
	private static readonly int[] Lengths = [1, 100, 65_535, 65_536, 65_537, 200_000, 1 << 20];

	/// <summary>The deflated levels.</summary>
	public static TheoryData<CompressionLevel> Levels() => new() { CompressionLevel.Optimal, CompressionLevel.Fastest, CompressionLevel.SmallestSize };

	/// <summary>
	/// A chunk that isn't the last is the raw DeflateStream's output after its sync flush, so it ends at a byte boundary with
	/// the flush's empty stored block, and the empty final block after it ends the stream at its input.
	/// </summary>
	/// <param name="level">The level.</param>
	[Theory]
	[MemberData(nameof(Levels))]
	public void EndsAChunkThatIsntTheLastAtAByteBoundary(CompressionLevel level)
	{
		var deflater = new ChunkDeflater(level);
		foreach (var length in Lengths)
		{
			var input = ZipWriterTests.Bytes(length, length);

			var chunk = Deflate(deflater, input, last: false);

			Assert.Equal(Reference(input, level, last: false), chunk);
			Assert.Equal(new byte[] { 0x00, 0x00, 0xFF, 0xFF }, chunk[^4..]);
			Assert.Equal(input, DeflateStageTests.Inflate([.. chunk, 0x03, 0x00]));
		}
	}

	/// <summary>The last chunk is the raw DeflateStream's whole output, final: it inflates to its input alone.</summary>
	/// <param name="level">The level.</param>
	[Theory]
	[MemberData(nameof(Levels))]
	public void FinishesTheLastChunk(CompressionLevel level)
	{
		var deflater = new ChunkDeflater(level);
		foreach (var length in Lengths)
		{
			var input = ZipWriterTests.Bytes(length, length + 1);

			var chunk = Deflate(deflater, input, last: true);

			Assert.Equal(Reference(input, level, last: true), chunk);
			Assert.Equal(input, DeflateStageTests.Inflate(chunk));
		}
	}

	/// <summary>
	/// Chunks joined are one deflate stream: each starts where the one before ended, at a byte boundary, with no reference
	/// back into it, and the last ends the stream. Random bytes (stored blocks) and text among them.
	/// </summary>
	/// <param name="level">The level.</param>
	[Theory]
	[MemberData(nameof(Levels))]
	public void ContinuesAChunkWithTheNext(CompressionLevel level)
	{
		var deflater = new ChunkDeflater(level);
		byte[][] inputs = [ZipWriterTests.Bytes(300_000, 1), ZipPackages.RandomBytes(150_000, 2), ZipWriterTests.Bytes(1, 3), ZipWriterTests.Bytes(70_000, 4)];

		for (var count = 1; count <= inputs.Length; count++)
		{
			var joined = new List<byte>();
			for (var i = 0; i < count; i++)
			{
				joined.AddRange(Deflate(deflater, inputs[i], last: i == count - 1));
			}

			Assert.Equal(inputs.Take(count).SelectMany(input => input).ToArray(), DeflateStageTests.Inflate([.. joined]));
		}
	}

	/// <summary>
	/// An empty last chunk is the empty final block <c>03 00</c>, with CRC 0 and no input (.NET's deflater writes nothing for
	/// nothing); a chunk that isn't the last can't be empty, and the refusal leaves the chunk empty.
	/// </summary>
	[Fact]
	public void EndsAnEmptyLastChunkWithTheEmptyFinalBlock()
	{
		var writer = NewWriter();
		var deflater = new ChunkDeflater(CompressionLevel.Fastest);
		var chunk = new DeflatedChunk(writer);

		deflater.Deflate([], last: true, chunk);

		Assert.Equal(new byte[] { 0x03, 0x00 }, DeflateStageTests.Bytes(chunk));
		Assert.Equal(0u, chunk.Crc);
		Assert.Equal(0, chunk.InputLength);
		Assert.Empty(DeflateStageTests.Inflate([0x03, 0x00]));
		chunk.Clear();

		Assert.Throws<InvalidOperationException>(() => deflater.Deflate([], last: false, chunk));
		Assert.Equal(0, chunk.Output.Length);
		Assert.Equal(0, writer.RentedBuffers);
	}

	/// <summary>Each chunk, last or not, gives System.IO.Hashing's CRC of its input and its length, from its trailer.</summary>
	[Fact]
	public void GivesTheInputsCrcAndLength()
	{
		var deflater = new ChunkDeflater(CompressionLevel.Fastest);
		foreach (var length in Lengths)
		{
			foreach (var last in new[] { false, true })
			{
				var input = ZipPackages.RandomBytes(length, length);
				var writer = NewWriter();
				var chunk = new DeflatedChunk(writer);

				deflater.Deflate(input, last, chunk);

				Assert.Equal((length, last, System.IO.Hashing.Crc32.HashToUInt32(input)), (length, last, chunk.Crc));
				Assert.Equal(length, chunk.InputLength);
				chunk.Clear();
				Assert.Equal(0, writer.RentedBuffers);
			}
		}
	}

	/// <summary>
	/// At <see cref="CompressionLevel.Fastest"/> the chunks are zlib's level 2 on .NET 10 and level 1 on .NET 8 (decision
	/// 33): the outputs above equal a raw DeflateStream's at that level, and on .NET 10 they differ from level 1's.
	/// </summary>
	[Fact]
	public void DeflatesFastestAtTheBuildsLevel()
	{
		var input = ZipWriterTests.Bytes(400_000, 5);

		var chunk = Deflate(new ChunkDeflater(CompressionLevel.Fastest), input, last: true);

		Assert.Equal(Reference(input, CompressionLevel.Fastest, last: true), chunk);
		var levelOne = Reference(input, last: true, output => new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true));
#if NET10_0_OR_GREATER
		Assert.NotEqual(levelOne, chunk);
#else
		Assert.Equal(levelOne, chunk);
#endif
	}

	/// <summary>
	/// The GZip header at each zlib level the chunks use is taken: its XFL byte differs by level (zlib's 4 below level 2, 2 at
	/// 9, 0 between, so level 2's is 0 where level 1's is 4), and the check reads only the first four bytes.
	/// </summary>
	[Fact]
	public void TakesTheGZipHeaderAtEachLevel()
	{
		var headers = new List<(string Level, byte[] Header)>
		{
			("1", Header(output => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))),
			("6", Header(output => new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))),
			("9", Header(output => new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))),
#if NET10_0_OR_GREATER
			("2", Header(output => new GZipStream(output, new ZLibCompressionOptions { CompressionLevel = 2, CompressionStrategy = ZLibCompressionStrategy.Default }, leaveOpen: true))),
#endif
		};

		foreach (var (level, header) in headers)
		{
			DeflaterSink.CheckHeader(header, 0);
			var expected = level switch
			{
				"1" => 4,
				"9" => 2,
				_ => 0,
			};

			// The level rides along, so that a failure names it.
			Assert.Equal((level, (byte)expected), (level, header[8]));
		}
	}

	/// <summary>A deflater used for chunk after chunk gives what a fresh one gives for each.</summary>
	[Fact]
	public void GivesTheSameBytesWhenReused()
	{
		var reused = new ChunkDeflater(CompressionLevel.Fastest);
		for (var i = 0; i < 12; i++)
		{
			var input = i % 3 == 0 ? ZipPackages.RandomBytes(10_000 * (i + 1), i) : ZipWriterTests.Bytes(50_000 * i, i);
			var last = i % 2 == 1;

			Assert.Equal(Deflate(new ChunkDeflater(CompressionLevel.Fastest), input, last), Deflate(reused, input, last));
		}
	}

	/// <summary>A stored entry has no deflater: <see cref="CompressionLevel.NoCompression"/> is refused.</summary>
	[Fact]
	public void RefusesNoCompression()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkDeflater(CompressionLevel.NoCompression));
	}

	/// <summary>
	/// A raw DeflateStream's output, fed as the chunk deflater feeds its own (64 KB writes from the start), at the level
	/// the chunk deflater uses: zlib's level 2 at <see cref="CompressionLevel.Fastest"/> on .NET 10.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="level">The writer's level.</param>
	/// <param name="last">Whether to take the whole stream (disposed), or the output after a sync flush.</param>
	/// <returns>The output.</returns>
	internal static byte[] Reference(byte[] input, CompressionLevel level, bool last)
	{
#if NET10_0_OR_GREATER
		if (level == CompressionLevel.Fastest)
		{
			return Reference(input, last, output => new DeflateStream(output, new ZLibCompressionOptions { CompressionLevel = 2, CompressionStrategy = ZLibCompressionStrategy.Default }, leaveOpen: true));
		}
#endif

		return Reference(input, last, output => new DeflateStream(output, level, leaveOpen: true));
	}

	/// <summary>Deflates a chunk's input, checks its CRC and length, and returns its output, its buffers back in the pool.</summary>
	/// <param name="deflater">The deflater.</param>
	/// <param name="input">The input.</param>
	/// <param name="last">Whether it is the last chunk.</param>
	/// <returns>The chunk's output.</returns>
	private static byte[] Deflate(ChunkDeflater deflater, byte[] input, bool last)
	{
		var writer = NewWriter();
		var chunk = new DeflatedChunk(writer);

		deflater.Deflate(input, last, chunk);

		Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(input), chunk.Crc);
		Assert.Equal(input.Length, chunk.InputLength);
		var bytes = DeflateStageTests.Bytes(chunk);
		chunk.Clear();
		Assert.Equal(0, writer.RentedBuffers);

		return bytes;
	}

	/// <summary>A raw deflater's output, fed in 64 KB writes from the start.</summary>
	/// <param name="input">The input.</param>
	/// <param name="last">Whether to take the whole stream (disposed), or the output after a sync flush.</param>
	/// <param name="create">Makes the deflater over the output.</param>
	/// <returns>The output.</returns>
	private static byte[] Reference(byte[] input, bool last, Func<Stream, DeflateStream> create)
	{
		using var output = new MemoryStream();
		var deflate = create(output);
		for (var at = 0; at < input.Length; at += DeflateSizes.WriteSize)
		{
			deflate.Write(input, at, Math.Min(DeflateSizes.WriteSize, input.Length - at));
		}

		if (!last)
		{
			deflate.Flush();
			var flushed = output.ToArray();
			deflate.Dispose();

			return flushed;
		}

		deflate.Dispose();

		return output.ToArray();
	}

	/// <summary>The first ten bytes a GZip stream writes: its header.</summary>
	/// <param name="create">Makes the stream over the output.</param>
	/// <returns>The header.</returns>
	private static byte[] Header(Func<Stream, GZipStream> create)
	{
		using var output = new MemoryStream();
		using (var gzip = create(output))
		{
			gzip.Write(ZipWriterTests.Bytes(1_000, 6));
		}

		return output.ToArray()[..10];
	}

	/// <summary>A writer for its pool alone.</summary>
	/// <returns>The writer.</returns>
	private static ZipWriter NewWriter() => new(new MemoryStream(), false, CompressionLevel.Fastest, ZipLimits.Default);
}
#endif
