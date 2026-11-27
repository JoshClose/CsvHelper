using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Chunked entries (<see cref="ZipEntryStream.TakesSegments"/>): parts opened chunked on a writer with threads, whose data
/// goes to a deflate stage, as copies of writes and as segments handed over uncopied, and whose chunks come back in order:
/// written after the data before them, the CRCs combined, the shape whole or streamed as any deflated entry's, Zip64
/// included; the same bytes for any thread count and any cut of the writes; on .NET 8 and later the chunks joined as each
/// was deflated on its own, and on the other builds the serial entry's bytes. The switch to chunks mid-entry
/// (<see cref="ZipEntryStream.UseChunks"/>). Through a test stage (<see cref="TestStage"/>): the waits, synchronous and
/// asynchronous, a canceled wait, a stage's fault, and a stage that miscounts. Streams that fail with chunks in flight.
/// After every path, every pooled buffer and segment has returned.
/// </summary>
public class ZipChunkedTests
{
	/// <summary>How a part's bytes reach its entry.</summary>
	internal enum Feed
	{
		/// <summary>Copies: <see cref="Stream.Write(byte[], int, int)"/> and <see cref="Stream.WriteAsync(byte[], int, int, CancellationToken)"/>.</summary>
		Copies,

		/// <summary>Segments handed over (<see cref="PackageWriter.AppendAsync"/>).</summary>
		Segments,

		/// <summary>Each piece one way or the other, at random.</summary>
		Mixed,
	}

	/// <summary>Threads, chunk sizes, streams that seek and don't, synchronously and asynchronously.</summary>
	/// <returns>The cases.</returns>
	public static TheoryData<int, int, bool, bool> Modes()
	{
		var data = new TheoryData<int, int, bool, bool>();
		foreach (var threads in new[] { 2, 4, 16 })
		{
			foreach (var chunkSize in new[] { 1, 64, 4096 })
			{
				foreach (var seekable in new[] { true, false })
				{
					data.Add(threads, chunkSize, seekable, false);
					data.Add(threads, chunkSize, seekable, true);
				}
			}
		}

		return data;
	}

	/// <summary>When an entry switches to chunks, synchronously and asynchronously.</summary>
	/// <returns>The cases.</returns>
	public static TheoryData<string, bool> Switches()
	{
		var data = new TheoryData<string, bool>();
		foreach (var when in new[] { "before any byte", "while the header waits", "after the header", "twice", "with nothing after" })
		{
			data.Add(when, false);
			data.Add(when, true);
		}

		return data;
	}

	/// <summary>Where a test stage fails, synchronously and asynchronously.</summary>
	/// <returns>The cases.</returns>
	public static TheoryData<string, bool> StagePoints()
	{
		var data = new TheoryData<string, bool>();
		foreach (var point in new[] { "append", "append segment", "try take", "take", "complete", "raise" })
		{
			data.Add(point, false);
			data.Add(point, true);
		}

		return data;
	}

	/// <summary>Where the stream fails under a chunked entry, with chunks in flight or not, synchronously and asynchronously.</summary>
	/// <returns>The cases.</returns>
	public static TheoryData<string, bool, bool> StreamPoints()
	{
		var data = new TheoryData<string, bool, bool>();
		foreach (var point in new[] { "local header", "held data", "chunk", "descriptor", "directory" })
		{
			foreach (var waits in new[] { false, true })
			{
				data.Add(point, waits, false);
				data.Add(point, waits, true);
			}
		}

		return data;
	}

	/// <summary>
	/// Chunked entries empty, tiny, of a whole number of chunks (whose last chunk is empty), and large, text and random,
	/// written in pieces of copies and segments: valid by their records, read back by <see cref="ZipArchive"/> and
	/// WindowsBase, each entry's CRC that of its bytes (the chunks' combined), whole while its data is at most one buffer
	/// and streamed past it, flagged with its level, its data written where its records say in writes of at most one
	/// buffer, and every write and flush of the stream synchronous or asynchronous as the calls were.
	/// </summary>
	/// <param name="threads">The writer's threads.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesChunkedEntriesThatReadBack(int threads, int chunkSize, bool seekable, bool async)
	{
		var parts = Parts(chunkSize);
		var stream = new ZipTestStream(seekable) { ForbidSync = async, ForbidAsync = !async };

		var package = await WriteAsync(parts, CompressionLevel.Fastest, stream, async, threads, chunkSize, piece: 10_000);

		ZipStructure.AssertValid(package);
		ZipStructure.AssertPackagingReads(package);
		ZipPackages.AssertReadsBack(package, parts);
		Assert.Equal(async ? (0, 1) : (1, 0), (stream.Flushes, stream.AsyncFlushes));
		var zip = ZipStructure.Read(package);
		foreach (var part in parts)
		{
			var entry = zip.Entries.Single(e => e.Name == part.Name);
			Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(part.Bytes), entry.Central.Crc);
			AssertWrittenInPlace(stream, entry);
		}

		var entries = zip.Entries.ToDictionary(e => e.Name);
		Assert.Equal((2L, 0u), (entries["parts/empty.xml"].CompressedSize, entries["parts/empty.xml"].Central.Crc));
		Assert.All(zip.Entries, e => Assert.Equal(e.CompressedSize > ZipStructure.WholeData, e.Streamed));
		Assert.All(zip.Entries, e => Assert.Equal(0x0006, e.Central.Flags & 0x0006));
#if NET
		Assert.True(entries["parts/random.bin"].Streamed);
#else
		// One deflater takes the whole entry here, whatever the chunk size, so the random part's 12,000 bytes (at a chunk of
		// a byte) stay within a buffer, and its 120,000 (at the others) pass it.
		Assert.Equal(chunkSize > 1, entries["parts/random.bin"].Streamed);
#endif
	}

	/// <summary>
	/// A chunked package's bytes depend on its parts alone: the same for 2, 4 or 16 threads, for writes cut in pieces of
	/// 7,777 bytes, of a buffer or whole, as copies, segments or both, written synchronously or asynchronously. On the
	/// other builds the one deflater's bytes depend on the writes' cuts, as a serial entry's do, and are a serial entry's:
	/// the same for any threads, copies or segments, and those of the writer without threads, cut for cut.
	/// </summary>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(4096)]
	[InlineData(DeflateSizes.ChunkSize)]
	public async Task WritesTheSameBytesForAnyThreadsAndCuts(int chunkSize)
	{
		List<ZipPart> parts = [new("a.xml", ZipWriterTests.Bytes(2_600_000, 7)), new("b.bin", ZipPackages.RandomBytes(300_000, 8)), new("c.xml", ZipWriterTests.Bytes(5_000, 9))];
		var packages = new List<(int Piece, string How, byte[] Bytes)>();

		foreach (var piece in new[] { 7_777, 65_536, int.MaxValue })
		{
			foreach (var threads in new[] { 2, 4, 16 })
			{
				foreach (var feed in new[] { Feed.Copies, Feed.Segments, Feed.Mixed })
				{
					packages.Add((piece, $"{threads} threads, {feed}", await WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(), false, threads, chunkSize, piece, feed)));
				}
			}

			packages.Add((piece, "4 threads, asynchronously", await WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(false), true, 4, chunkSize, piece)));
		}

#if NET
		var first = packages[0].Bytes;
		foreach (var (piece, how, bytes) in packages)
		{
			Assert.True(first.AsSpan().SequenceEqual(bytes), $"In pieces of {piece}, {how}: the package differs.");
		}
#else
		foreach (var group in packages.GroupBy(p => p.Piece))
		{
			var serial = await ZipPackages.WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(), false, group.Key);
			foreach (var (piece, how, bytes) in group)
			{
				Assert.True(serial.AsSpan().SequenceEqual(bytes), $"In pieces of {piece}, {how}: the package differs from the serial one.");
			}
		}
#endif
	}

#if NET
	/// <summary>
	/// A chunked entry's data is its input cut at every chunk's size, by byte count, each chunk deflated on its own by
	/// <see cref="ChunkDeflater"/> (sync-flushed but the last, which is final, and empty when the input is a whole number
	/// of chunks), joined in order: at every level, at the chunk sizes of the tests and at 1 MB. It inflates back to the
	/// input.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <param name="length">The input's length.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(CompressionLevel.Fastest, 64, 100_000)]
	[InlineData(CompressionLevel.Fastest, 4096, 1_000_003)]
	[InlineData(CompressionLevel.Fastest, 4096, 4096 * 50)]
	[InlineData(CompressionLevel.Optimal, 4096, 1_000_003)]
	[InlineData(CompressionLevel.SmallestSize, 4096, 1_000_003)]
	[InlineData(CompressionLevel.Fastest, DeflateSizes.ChunkSize, 2_500_000)]
	[InlineData(CompressionLevel.Fastest, DeflateSizes.ChunkSize, 900_000)]
	public async Task JoinsChunksDeflatedOnTheirOwn(CompressionLevel level, int chunkSize, int length)
	{
		var input = ZipWriterTests.Bytes(length, 11);

		var package = await WriteAsync([new ZipPart("a.xml", input)], level, new ZipTestStream(), false, 4, chunkSize, piece: 30_000);

		var entry = ZipStructure.Read(package).Entries[0];
		var data = package.AsSpan((int)entry.Local.DataStart, (int)entry.CompressedSize).ToArray();
		Assert.True(Joined(input, level, chunkSize).AsSpan().SequenceEqual(data), "The entry's data isn't its chunks deflated on their own, joined.");
		Assert.True(input.AsSpan().SequenceEqual(Inflate(data)));
		Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(input), entry.Central.Crc);
	}
#endif

	/// <summary>
	/// Chunked entries past lowered limits: the first's uncompressed size, the second's sizes and offset, and the third's
	/// offset, each with all three fields in its central Zip64 extra, and the entries past the 16-bit limit, after a Zip64
	/// end record; valid under those limits, read back by <see cref="ZipArchive"/> and WindowsBase.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(true, false)]
	[InlineData(true, true)]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public async Task WritesChunkedEntriesPastTheLimits(bool seekable, bool async)
	{
		var limits = new ZipLimits(100_000, 3);
		List<ZipPart> parts = [new("a.xml", ZipWriterTests.Bytes(300_000, 1)), new("b.bin", ZipPackages.RandomBytes(150_000, 2)), new("c.xml", ZipWriterTests.Bytes(2_000, 3))];

		var package = await WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(seekable), async, 4, 4096, piece: 20_000, limits: limits);

		ZipStructure.AssertValid(package, limits.Max32, limits.Max16);
		ZipPackages.AssertReadsBack(package, parts);
		ZipStructure.AssertPackagingReads(package);
		var zip = ZipStructure.Read(package);
		Assert.All(zip.Entries, e => Assert.Equal(28, e.Central.Extra.Length));
		Assert.Equal((true, true), (zip.Entries[0].Streamed, zip.Entries[1].Streamed));
		Assert.Equal(4ul, zip.End64!.Entries);
	}

	/// <summary>
	/// An entry opened chunked on a writer without threads switches to chunks as a parallel write begins (its threads
	/// raised, <see cref="ZipWriter.ParallelThreads"/>): before any byte, while its local header waits, after it, twice,
	/// and with nothing written after. It reads back with its CRC, valid, in the call's mode. On .NET 8 and later its data
	/// is the bytes deflated before the switch, sync-flushed, then the rest in chunks; on the other builds only an entry
	/// that has deflated nothing switches, and its bytes are the serial entry's either way.
	/// </summary>
	/// <param name="when">When it switches.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Switches))]
	public async Task SwitchesToChunksMidEntry(string when, bool async)
	{
		const int piece = 10_000;
		const int chunkSize = 4096;
		var input = ZipWriterTests.Bytes(600_000, 5);
		var at = when switch
		{
			"before any byte" => 0,
			"while the header waits" => 20_000,
			"with nothing after" => input.Length,
			_ => 250_000,
		};
		var stream = new ZipTestStream(false) { ForbidSync = async, ForbidAsync = !async };
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 1);
		package.Zip.ChunkSize = chunkSize;

		var entry = await Packages.Run(async, a => package.OpenPartAsync("a.xml", chunked: true, a, CancellationToken.None));
		Assert.False(PackageWriter.TakesSegments(entry));
		await WritePiecesAsync(entry, input, 0, at, piece, async, Feed.Copies);
		Assert.Equal(when == "after the header" || when == "twice" || when == "with nothing after", stream.Writes.Count > 0);
		package.ParallelThreads = 3;
		PackageWriter.UseChunks(entry);
		if (when == "twice")
		{
			package.ParallelThreads = 8;
			PackageWriter.UseChunks(entry);
		}

#if NET
		Assert.True(PackageWriter.TakesSegments(entry));
#else
		Assert.Equal(at == 0, PackageWriter.TakesSegments(entry));
#endif
		await WritePiecesAsync(entry, input, at, input.Length - at, piece, async, Feed.Mixed);
		package.ParallelThreads = 0;
		await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		await WriteContentTypesAsync(package, async);
		await ZipPackages.RunAsync(async, package.DisposeAsync);

		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		ZipStructure.AssertPackagingReads(written);
		ZipPackages.AssertReadsBack(written, [new ZipPart("a.xml", input)]);
		var zip = ZipStructure.Read(written);
		Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(input), zip.Entries[0].Central.Crc);
#if NET
		var data = written.AsSpan((int)zip.Entries[0].Local.DataStart, (int)zip.Entries[0].CompressedSize).ToArray();
		Assert.True(Switched(input, at, piece, CompressionLevel.Fastest, chunkSize).AsSpan().SequenceEqual(data), "The entry's data isn't its serial bytes, sync-flushed, then its chunks.");
#else
		var serial = await ZipPackages.WriteAsync([new ZipPart("a.xml", input)], CompressionLevel.Fastest, new ZipTestStream(false), false, piece);
		Assert.True(serial.AsSpan().SequenceEqual(written), "The switched entry's bytes aren't the serial entry's.");
#endif
	}

	/// <summary>
	/// Only a deflated entry opened chunked on a writer with threads chunks (or with a parallel write's threads as it
	/// opens); not one without threads, one not opened chunked, or a stored one, nor do the last two switch.
	/// </summary>
	[Fact]
	public void ChunksOnlyDeflatedEntriesOpenedChunkedWithThreads()
	{
		Assert.False(TakesSegments(1, CompressionLevel.Fastest, chunked: true, parallel: 0, switched: false));
		Assert.True(TakesSegments(2, CompressionLevel.Fastest, chunked: true, parallel: 0, switched: false));
		Assert.True(TakesSegments(2, CompressionLevel.Optimal, chunked: true, parallel: 0, switched: false));
		Assert.False(TakesSegments(2, CompressionLevel.Fastest, chunked: false, parallel: 0, switched: false));
		Assert.False(TakesSegments(2, CompressionLevel.NoCompression, chunked: true, parallel: 0, switched: false));
		Assert.True(TakesSegments(1, CompressionLevel.Fastest, chunked: true, parallel: 3, switched: false));
		Assert.False(TakesSegments(1, CompressionLevel.NoCompression, chunked: true, parallel: 3, switched: false));
		Assert.False(TakesSegments(1, CompressionLevel.Fastest, chunked: true, parallel: 1, switched: true));
		Assert.True(TakesSegments(1, CompressionLevel.Fastest, chunked: true, parallel: 2, switched: true));
		Assert.False(TakesSegments(4, CompressionLevel.Fastest, chunked: false, parallel: 4, switched: true));
		Assert.False(TakesSegments(4, CompressionLevel.NoCompression, chunked: true, parallel: 4, switched: true));
	}

	/// <summary>
	/// A canceled write to a chunked entry throws before it takes any bytes, as any write does; a segment handed over with
	/// a canceled token is taken all the same (its caller has let it go) and then the call throws. Either way the writer
	/// isn't faulted, the entry goes on, and the package holds the segment's bytes and not the canceled write's.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task TakesASegmentButNotAWriteWhenCanceled()
	{
		var stream = new ZipTestStream(false);
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 4);
		package.Zip.ChunkSize = 64;
		var bytes = ZipWriterTests.Bytes(1_000, 3);
		var entry = await package.OpenPartAsync("a.xml", chunked: true, true, CancellationToken.None);
		using (var canceled = new CancellationTokenSource())
		{
			canceled.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.WriteAsync(bytes, 0, 300, canceled.Token));
			Assert.Equal(0, entry.Position);
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AppendAsync(entry, bytes, 300, 300, true, canceled.Token));
			Assert.Equal(300, entry.Position);
		}

		await AppendAsync(entry, bytes, 600, 400, true);
		await PackageWriter.CloseAsync(entry, true);
		Assert.False(package.Zip.Faulted);
		await WriteContentTypesAsync(package, true);
		await package.DisposeAsync(true);

		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		ZipPackages.AssertReadsBack(written, [new ZipPart("a.xml", bytes.AsSpan(300).ToArray())]);
	}

	/// <summary>
	/// A segment is the entry's whatever happens: handed to an entry that is closed, it is returned and the call throws
	/// <see cref="ObjectDisposedException"/>; to an entry of a faulted writer, returned and nothing written; the empty
	/// segment writes nothing. Chunked or not, every segment held returns.
	/// </summary>
	/// <param name="chunked">Whether the entries chunk.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReturnsEverySegmentItIsGiven(bool chunked)
	{
		var bytes = ZipPackages.RandomBytes(200_000, 4);
		var stream = new ZipTestStream(false) { FailAt = 3 };
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, chunked ? 4 : 1);
		package.Zip.ChunkSize = 4096;

		var first = await package.OpenPartAsync("a.xml", chunked, true, CancellationToken.None);
		await AppendAsync(first, [], 0, 0, true);
		await AppendAsync(first, bytes, 0, 1_000, true);
		await PackageWriter.CloseAsync(first, true);
		await Assert.ThrowsAsync<ObjectDisposedException>(() => AppendAsync(first, bytes, 0, 1_000, true));
		Assert.Throws<ObjectDisposedException>(() => SyncOrAsync.Wait(PackageWriter.AppendAsync(first, Segment(bytes, 0, 10), false, CancellationToken.None)));
		Assert.Equal(0, package.Zip.HeldSegments);

		// The second entry passes a buffer, and its header's write, the stream's third, fails: in the append that writes it,
		// which is a later one when one deflater on a thread of its own takes the bytes (the other builds), so they come in
		// pieces, and the entry is closed if none has failed yet.
		var second = await package.OpenPartAsync("b.bin", chunked, true, CancellationToken.None);
		var caught = await Record(async () =>
		{
			for (var at = 0; at < bytes.Length && !package.Zip.Faulted; at += 20_000)
			{
				await AppendAsync(second, bytes, at, Math.Min(20_000, bytes.Length - at), true);
			}

			if (!package.Zip.Faulted)
			{
				await PackageWriter.CloseAsync(second, true);
			}
		});
		Assert.Same(stream.Failure, caught);
		Assert.True(package.Zip.Faulted);
		await AppendAsync(second, bytes, 0, 70_000, true);
		SyncOrAsync.Wait(PackageWriter.AppendAsync(second, Segment(bytes, 0, 70_000), false, CancellationToken.None));
		await PackageWriter.CloseAsync(second, true);
		await package.DisposeAsync(true);

		Assert.Equal(3, stream.Attempts);
		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
	}

	/// <summary>
	/// A stage that makes the entry wait for each chunk: a synchronous write waits with the stage's blocking take only,
	/// and writes the stream only synchronously; an asynchronous one awaits the stage's asynchronous take only, and writes
	/// the stream only asynchronously. The package reads back, and the stage was disposed once.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WaitsForChunksInTheCallsMode(bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(150_000, 1)), new("b.bin", ZipPackages.RandomBytes(100_000, 2)) };
		var stages = new List<TestStage>();
		var stream = new ZipTestStream(false) { ForbidSync = async, ForbidAsync = !async };

		var package = await WriteAsync(parts, CompressionLevel.Fastest, stream, async, 4, 4096, piece: 20_000, stages: (w, t) => Add(stages, new TestStage(w, t) { Mode = TestStageMode.Waits }));

		ZipStructure.AssertValid(package);
		ZipPackages.AssertReadsBack(package, parts);
		Assert.Equal(2, stages.Count);
		Assert.All(stages, s => Assert.Equal((1, 4), (s.Disposals, s.AskedThreads)));
		Assert.All(stages, s => Assert.True(s.Waits >= 5, $"{s.Waits} waits"));
		Assert.All(stages, s => Assert.Equal(0, async ? s.SyncTakes : s.AsyncTakes));
		Assert.All(stages, s => Assert.Equal(s.Waits, async ? s.AsyncTakes : s.SyncTakes));
	}

	/// <summary>
	/// A wait for a chunk canceled: the write throws, its bytes taken; the writer isn't faulted, the chunk is left to the
	/// stage, and the entry goes on: a later write and the close take every chunk, in order, and the package holds every
	/// byte written.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task LeavesTheEntryAsItWasWhenAWaitIsCanceled()
	{
		var bytes = ZipWriterTests.Bytes(60_000, 6);
		var stream = new ZipTestStream(false);
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 4);
		package.Zip.ChunkSize = 4096;
		TestStage? stage = null;
		using var canceled = new CancellationTokenSource();
		var entry = (ZipEntryStream)await package.OpenPartAsync("a.xml", chunked: true, true, CancellationToken.None);
		entry.StageFactory = (w, t) => stage = new TestStage(w, t) { Mode = TestStageMode.Held, WaitAt = 1, Waiting = canceled.Cancel };

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.WriteAsync(bytes, 0, 20_000, canceled.Token));

		Assert.False(package.Zip.Faulted);
		Assert.Equal(20_000, entry.Position);
		Assert.Equal(1, stage!.Waits);
		stage.Open();
		await entry.WriteAsync(bytes, 20_000, 20_000, CancellationToken.None);
		await AppendAsync(entry, bytes, 40_000, 20_000, true);
		await PackageWriter.CloseAsync(entry, true);
		await WriteContentTypesAsync(package, true);
		await package.DisposeAsync(true);

		Assert.Equal((0, 0, 1), (package.Zip.RentedBuffers, package.Zip.HeldSegments, stage.Disposals));
		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		ZipPackages.AssertReadsBack(written, [new ZipPart("a.xml", bytes)]);
	}

	/// <summary>
	/// A stage that fails on the writing thread, as a stage rethrows its threads' exceptions: as it takes a copy or a
	/// segment, as a chunk is tried or waited for, as the data ends, or as its threads are raised. The stage's exception
	/// reaches the caller; the writer is faulted, the stage disposed once; from then on nothing more is written or thrown,
	/// and every buffer and segment returns.
	/// </summary>
	/// <param name="point">Where the stage fails.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(StagePoints))]
	public async Task RethrowsTheStagesFault(string point, bool async)
	{
		var bytes = ZipWriterTests.Bytes(100_000, 7);
		var stream = new ZipTestStream(false);
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 4);
		package.Zip.ChunkSize = 4096;
		TestStage? stage = null;
		var entry = (ZipEntryStream)await Packages.Run(async, a => package.OpenPartAsync("a.xml", chunked: true, a, CancellationToken.None));
		entry.StageFactory = (w, t) => stage = new TestStage(w, t) { Mode = point == "take" ? TestStageMode.Waits : TestStageMode.Ready, FailAt = point, FailAfter = point == "complete" ? 1 : 2 };

		var caught = await Record(async () =>
		{
			await ZipPackages.WriteAsync(entry, bytes, 0, 10_000, async);
			await AppendAsync(entry, bytes, 10_000, 10_000, async);
			await ZipPackages.WriteAsync(entry, bytes, 20_000, 10_000, async);
			await AppendAsync(entry, bytes, 30_000, 10_000, async);
			package.ParallelThreads = 6;
			PackageWriter.UseChunks(entry);
			package.ParallelThreads = 8;
			PackageWriter.UseChunks(entry);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		});

		Assert.Same(stage!.Failure, caught);
		Assert.True(package.Zip.Faulted);
		Assert.Equal(1, stage.Disposals);
		var attempts = stream.Attempts;
		await AssertWritesNothingMoreAsync(package, stream, point == "complete" ? null : entry, async);
		Assert.Equal(attempts, stream.Attempts);
		Assert.Equal(1, stage.Disposals);
	}

	/// <summary>
	/// A stage whose chunks are out of order, or whose chunks report more input than they took: the entry refuses the chunk
	/// with <see cref="InvalidOperationException"/>, faulting the writer, so that a wrong entry is never completed; every
	/// buffer and segment returns.
	/// </summary>
	/// <param name="outOfOrder">Whether the chunks are out of order, rather than miscounted.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(true, false)]
	[InlineData(true, true)]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public async Task RefusesChunksOutOfOrderOrMiscounted(bool outOfOrder, bool async)
	{
		var bytes = ZipWriterTests.Bytes(20_000, 8);
		var stream = new ZipTestStream(false);
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 4);
		package.Zip.ChunkSize = 4096;
		TestStage? stage = null;
		var entry = (ZipEntryStream)await Packages.Run(async, a => package.OpenPartAsync("a.xml", chunked: true, a, CancellationToken.None));
		entry.StageFactory = (w, t) => stage = new TestStage(w, t) { OutOfOrder = outOfOrder, Overcounts = !outOfOrder };

		var caught = await Record(async () =>
		{
			await ZipPackages.WriteAsync(entry, bytes, 0, bytes.Length, async);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		});

		Assert.IsType<InvalidOperationException>(caught);
		Assert.NotSame(stage!.Failure, caught);
		Assert.True(package.Zip.Faulted);
		await AssertWritesNothingMoreAsync(package, stream, null, async);
		Assert.Equal(1, stage.Disposals);
	}

	/// <summary>
	/// A stage whose dispose throws, after it has released what it holds: the entry closes all the same, and the package
	/// is complete, since the stage's failure there would hide nothing worth more than the package.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task ClosesWhenTheStagesDisposeThrows()
	{
		var bytes = ZipWriterTests.Bytes(50_000, 9);
		var stream = new ZipTestStream(false);
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest, 4);
		package.Zip.ChunkSize = 4096;
		var entry = (ZipEntryStream)await package.OpenPartAsync("a.xml", chunked: true, false, CancellationToken.None);
		entry.StageFactory = (w, t) => new TestStage(w, t) { FailAt = "dispose" };

		entry.Write(bytes, 0, bytes.Length);
		SyncOrAsync.Wait(PackageWriter.CloseAsync(entry, false));
		await WriteContentTypesAsync(package, false);
		SyncOrAsync.Wait(package.DisposeAsync(false));

		Assert.False(package.Zip.Faulted);
		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
		ZipPackages.AssertReadsBack(stream.ToArray(), [new ZipPart("a.xml", bytes)]);
	}

	/// <summary>
	/// A stream that fails under a chunked entry: at its local header, at the data it held as its header waited, at a
	/// chunk written straight through, at its descriptor, and at the directory after it; with the stage's chunks ready as
	/// they are cut, or waited for, so that chunks are cut and not written when it fails. The stream's exception reaches
	/// the caller; from then on nothing more is written or thrown, the stage is disposed, and every buffer and segment
	/// returns.
	/// </summary>
	/// <param name="point">Where the stream fails.</param>
	/// <param name="waits">Whether the stage makes the entry wait for its chunks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(StreamPoints))]
	public async Task FaultsWithChunksInFlight(string point, bool waits, bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(400_000, 2)) };
		var plain = new ZipTestStream(false);
		await WriteAsync(parts, CompressionLevel.Fastest, plain, true, 4, 4096, piece: 20_000);
		var sizes = plain.Writes.Select(w => w.Size).ToList();

		// Writes from 1: a.xml's header (30 + 5 + 20 bytes), the data it held as its header waited, its chunks, its
		// descriptor (24), the content types whole (header and data), the directory and the end records.
		var failAt = point switch
		{
			"local header" => 1,
			"held data" => 2,
			"chunk" => (2 + sizes.IndexOf(24)) / 2,
			"descriptor" => sizes.IndexOf(24) + 1,
			_ => sizes.Count - 1,
		};
		Assert.Equal(55, sizes[0]);
		Assert.True(sizes.IndexOf(24) > 4, "The entry has too few writes for the test's points.");
		var stages = new List<TestStage>();
		var stream = new ZipTestStream(false) { FailAt = failAt };
		Stream? open = null;
		PackageWriter? package = null;

		var caught = await Record(() => WriteAsync(parts, CompressionLevel.Fastest, stream, async, 4, 4096, piece: 20_000,
			stages: (w, t) => Add(stages, new TestStage(w, t) { Mode = waits ? TestStageMode.Waits : TestStageMode.Ready }), opened: e => open = e, made: p => package = p));

		Assert.Same(stream.Failure, caught);
		Assert.Equal(failAt - 1, stream.Writes.Count);
		Assert.All(stages, s => Assert.Equal(1, s.Disposals));
		await AssertWritesNothingMoreAsync(package!, stream, open, async);
		Assert.Equal(failAt, stream.Attempts);
	}

	/// <summary>
	/// The writer's own stage, not a test stage: when the stream fails with chunks in flight, the entry, released as the
	/// writer faults, disposes its stage, which joins every thread it started, and every pooled buffer and segment returns.
	/// </summary>
	/// <param name="async">Whether the package is written asynchronously.</param>
	/// <returns>A task that completes when the check is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EndsTheStageThreadsWhenTheStreamFails(bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(3_000_000, 5)) };
		var stages = new List<DeflateStage>();
		var stream = new ZipTestStream(false) { FailAt = 4 };
		PackageWriter? package = null;

		var caught = await Record(() => WriteAsync(parts, CompressionLevel.Fastest, stream, async, 4, 64 * 1024, piece: 20_000,
			made: p =>
			{
				package = p;
				p.Zip.StageCreated = s =>
				{
					lock (stages)
					{
						stages.Add(s);
					}
				};
			}));

		Assert.Same(stream.Failure, caught);
		var threads = stages.Cast<ThreadedDeflateStage>().SelectMany(s => s.StartedThreads()).ToList();
		Assert.NotEmpty(threads);
		Assert.All(threads, t => Assert.False(t.IsAlive, $"The stage's thread {t.Name} is alive after the writer faulted."));
		Assert.Equal((0, 0), (package!.Zip.RentedBuffers, package.Zip.HeldSegments));
	}

	/// <summary>
	/// Writes a package of chunked parts: each opened chunked, written in pieces as the feed says, and closed, then
	/// <c>[Content_Types].xml</c> whole, and the package disposed, synchronously or asynchronously. Every pooled buffer and
	/// segment has returned at the end.
	/// </summary>
	/// <param name="parts">The parts.</param>
	/// <param name="level">The compression.</param>
	/// <param name="stream">The stream written to, left open.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="threads">The writer's threads.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <param name="piece">The most bytes of a write to an entry.</param>
	/// <param name="feed">How the pieces reach the entries.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for the default ones.</param>
	/// <param name="stages">Makes each entry's stage, or <see langword="null"/> for the writer's own.</param>
	/// <param name="opened">Told of each entry opened, and of <see langword="null"/> before it is closed.</param>
	/// <param name="made">Told of the package, once it is made.</param>
	/// <returns>The bytes the stream holds.</returns>
	internal static async Task<byte[]> WriteAsync(IEnumerable<ZipPart> parts, CompressionLevel level, ZipTestStream stream, bool async, int threads, int chunkSize,
		int piece = 65_536, Feed feed = Feed.Mixed, ZipLimits? limits = null, Func<ZipWriter, int, DeflateStage>? stages = null, Action<Stream?>? opened = null, Action<PackageWriter>? made = null)
	{
		var package = new PackageWriter(stream, leaveOpen: true, level, limits ?? ZipLimits.Default, threads);
		package.Zip.ChunkSize = chunkSize;
		made?.Invoke(package);
		foreach (var part in parts)
		{
			var entry = await Packages.Run(async, a => package.OpenPartAsync(part.Name, chunked: true, a, CancellationToken.None));
			if (stages is not null)
			{
				((ZipEntryStream)entry).StageFactory = stages;
			}

			opened?.Invoke(entry);
			await WritePiecesAsync(entry, part.Bytes, 0, part.Bytes.Length, piece, async, feed);
			opened?.Invoke(null);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		}

		await WriteContentTypesAsync(package, async);
		await ZipPackages.RunAsync(async, package.DisposeAsync);
		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));

		return stream.ToArray();
	}

	/// <summary>Hands bytes to an entry as a segment of a pooled array, synchronously or not (<see cref="PackageWriter.AppendAsync"/>).</summary>
	/// <param name="entry">The entry's stream.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many: 0 hands over the empty segment.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the entry has taken them.</returns>
	internal static async Task AppendAsync(Stream entry, byte[] bytes, int offset, int count, bool async, CancellationToken cancellationToken = default)
	{
		var segment = Segment(bytes, offset, count);
		if (async)
		{
			await PackageWriter.AppendAsync(entry, segment, true, cancellationToken);
		}
		else
		{
			SyncOrAsync.Wait(PackageWriter.AppendAsync(entry, segment, false, CancellationToken.None));
		}
	}

	/// <summary>A copy of bytes in a pooled array, as a segment; the empty segment for none.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <returns>The segment.</returns>
	internal static OwnedSegment Segment(byte[] bytes, int offset, int count)
	{
		if (count == 0)
		{
			return default;
		}

		var array = ArrayPool<byte>.Shared.Rent(count);
		Buffer.BlockCopy(bytes, offset, array, 0, count);

		return new OwnedSegment(array, count);
	}

	/// <summary>Writes bytes to an entry in pieces, each a copy or a segment as the feed says (at random for <see cref="Feed.Mixed"/>, seeded).</summary>
	/// <param name="entry">The entry's stream.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <param name="piece">The most bytes of a piece.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="feed">How the pieces reach the entry.</param>
	/// <returns>A task that completes when they are written.</returns>
	internal static async Task WritePiecesAsync(Stream entry, byte[] bytes, int offset, int count, int piece, bool async, Feed feed)
	{
		var random = new Random(count ^ piece);
		for (var at = offset; at < offset + count; at += piece)
		{
			var length = Math.Min(piece, offset + count - at);
			if (feed == Feed.Segments || (feed == Feed.Mixed && random.Next(2) == 0))
			{
				await AppendAsync(entry, bytes, at, length, async);
			}
			else
			{
				await ZipPackages.WriteAsync(entry, bytes, at, length, async);
			}
		}
	}

	/// <summary>Writes the content types whole, in an entry not opened chunked, so that WindowsBase opens the package.</summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	private static async Task WriteContentTypesAsync(PackageWriter package, bool async)
	{
		var types = await Packages.Run(async, a => package.OpenPartAsync("[Content_Types].xml", a, CancellationToken.None));
		await ZipPackages.WriteAsync(types, ZipPackages.ContentTypes, 0, ZipPackages.ContentTypes.Length, async);
		await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(types, a));
	}

	/// <summary>
	/// The package's calls after a failure: another entry opened chunked (while the first is still open, too), written as
	/// copies and segments, the entry left open written, segments too, and closed, and the package disposed twice: nothing
	/// more reaches the stream, nothing throws, and every buffer and segment has returned.
	/// </summary>
	/// <param name="package">The package, faulted.</param>
	/// <param name="stream">Its stream.</param>
	/// <param name="open">The entry left open, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the checks are done.</returns>
	private static async Task AssertWritesNothingMoreAsync(PackageWriter package, ZipTestStream stream, Stream? open, bool async)
	{
		Assert.True(package.Zip.Faulted);
		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
		var attempts = stream.Attempts;
		var bytes = ZipPackages.RandomBytes(200_000, 2);
		var after = await Packages.Run(async, a => package.OpenPartAsync("after.xml", chunked: true, a, CancellationToken.None));
		await ZipPackages.WriteAsync(after, bytes, 0, bytes.Length, async);
		await AppendAsync(after, bytes, 0, 100_000, async);
		if (open is not null)
		{
			await ZipPackages.WriteAsync(open, bytes, 0, bytes.Length, async);
			await AppendAsync(open, bytes, 0, 100_000, async);
			PackageWriter.UseChunks(open);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(open, a));
		}

		await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(after, a));
		await ZipPackages.RunAsync(async, package.DisposeAsync);
		await ZipPackages.RunAsync(async, package.DisposeAsync);
		Assert.Equal(attempts, stream.Attempts);
		Assert.Equal((0, 0), (package.Zip.RentedBuffers, package.Zip.HeldSegments));
	}

	/// <summary>
	/// Checks that an entry's data reached the stream where its records say, in writes of at most one buffer: the writes
	/// that start inside it add up to it.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="entry">The entry's records.</param>
	private static void AssertWrittenInPlace(ZipTestStream stream, ZipEntryRecords entry)
	{
		var start = entry.Local.DataStart;
		var end = start + entry.CompressedSize;
		var sizes = stream.Writes.Select((w, i) => (w.Size, Start: stream.Starts[i])).Where(w => w.Start >= start && w.Start < end).Select(w => w.Size).ToList();
		Assert.Equal(entry.CompressedSize, sizes.Sum(s => (long)s));
		Assert.All(sizes, s => Assert.InRange(s, 1, ZipWriter.BufferSize));
	}

	/// <summary>Whether an entry takes segments, opened on a writer with threads, a parallel write's threads, and switched or not.</summary>
	/// <param name="threads">The writer's threads.</param>
	/// <param name="level">The compression.</param>
	/// <param name="chunked">Whether the entry is opened chunked.</param>
	/// <param name="parallel">The parallel write's threads: set before the entry opens, or before it switches.</param>
	/// <param name="switched">Whether the entry switches (<see cref="PackageWriter.UseChunks"/>) after it opens, rather than opening with them.</param>
	/// <returns>Whether it takes segments.</returns>
	private static bool TakesSegments(int threads, CompressionLevel level, bool chunked, int parallel, bool switched)
	{
		var package = new PackageWriter(new ZipTestStream(), leaveOpen: false, level, threads);
		if (!switched)
		{
			package.ParallelThreads = parallel;
		}

		var entry = SyncOrAsync.Result(package.OpenPartAsync("a.xml", chunked, false, CancellationToken.None));
		if (switched)
		{
			package.ParallelThreads = parallel;
			PackageWriter.UseChunks(entry);
		}

		var takes = PackageWriter.TakesSegments(entry);
		SyncOrAsync.Wait(PackageWriter.CloseAsync(entry, false));
		SyncOrAsync.Wait(package.DisposeAsync(false));

		return takes;
	}

	/// <summary>
	/// The parts of the chunked tests at a chunk size: empty, tiny, a whole number of chunks, and text and random bytes of
	/// many chunks, past one buffer of data, sized so that a chunk of a byte stays quick.
	/// </summary>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <returns>The parts.</returns>
	private static List<ZipPart> Parts(int chunkSize) =>
	[
		new("parts/empty.xml", []),
		new("parts/tiny.xml", Encoding.ASCII.GetBytes("<a>tiny</a>")),
		new("parts/exact.xml", ZipWriterTests.Bytes(chunkSize * 5, 3)),
		new("parts/text.xml", ZipWriterTests.Bytes(chunkSize switch { 1 => 3_000, 64 => 150_000, _ => 1_500_000 }, 1)),
		new("parts/random.bin", ZipPackages.RandomBytes(chunkSize == 1 ? 12_000 : 120_000, 2)),
	];

	/// <summary>Adds a stage to a list, for the tests that look at each entry's.</summary>
	/// <param name="stages">The list.</param>
	/// <param name="stage">The stage.</param>
	/// <returns><paramref name="stage"/>.</returns>
	private static DeflateStage Add(List<TestStage> stages, TestStage stage)
	{
		stages.Add(stage);

		return stage;
	}

	/// <summary>Inflates raw deflate data.</summary>
	/// <param name="data">The data.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Inflate(byte[] data)
	{
		using var inflater = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
		using var copy = new MemoryStream();
		inflater.CopyTo(copy);

		return copy.ToArray();
	}

#if NET
	/// <summary>
	/// The data of a chunked entry, made without the entry: the input cut at every chunk's size, each chunk deflated by a
	/// <see cref="ChunkDeflater"/> of its own, the last final (empty when the input is a whole number of chunks), joined.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="level">The compression.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <returns>The data.</returns>
	private static byte[] Joined(byte[] input, CompressionLevel level, int chunkSize)
	{
		var writer = new ZipWriter(Stream.Null, false, level, ZipLimits.Default);
		var deflater = new ChunkDeflater(level);
		using var joined = new MemoryStream();
		var full = input.Length / chunkSize;
		for (var i = 0; i <= full; i++)
		{
			var start = i * chunkSize;
			var chunk = new DeflatedChunk(writer);
			deflater.Deflate(input.AsSpan(start, i < full ? chunkSize : input.Length - start), i == full, chunk);
			for (var b = 0; b < chunk.Output.Count; b++)
			{
				joined.Write(chunk.Output[b], 0, chunk.Output.LengthOf(b));
			}

			chunk.Clear();
		}

		Assert.Equal(0, writer.RentedBuffers);

		return joined.ToArray();
	}

	/// <summary>
	/// The data of an entry that switched to chunks, made without the entry: the bytes before the switch deflated as the
	/// serial entry deflates them (a <see cref="GZipStream"/> at the level, a write a piece), sync-flushed, its header,
	/// empty final block and trailer dropped; then the rest in chunks (<see cref="Joined"/>).
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="at">Where the entry switched.</param>
	/// <param name="piece">The pieces the bytes before were written in: a buffer or less.</param>
	/// <param name="level">The compression.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <returns>The data.</returns>
	private static byte[] Switched(byte[] input, int at, int piece, CompressionLevel level, int chunkSize)
	{
		using var data = new MemoryStream();
		if (at > 0)
		{
			using var gzip = new MemoryStream();
			using (var deflater = new GZipStream(gzip, level, leaveOpen: true))
			{
				for (var i = 0; i < at; i += piece)
				{
					deflater.Write(input, i, Math.Min(piece, at - i));
				}

				deflater.Flush();
			}

			var bytes = gzip.ToArray();
			Assert.Equal([0x03, 0x00], bytes.AsSpan(bytes.Length - 10, 2).ToArray());
			data.Write(bytes, 10, bytes.Length - 20);
		}

		var rest = Joined(input.AsSpan(at).ToArray(), level, chunkSize);
		data.Write(rest, 0, rest.Length);

		return data.ToArray();
	}
#endif

	/// <summary>Runs code, returning the exception it throws.</summary>
	/// <param name="run">The code.</param>
	/// <returns>The exception, or <see langword="null"/>.</returns>
	private static async Task<Exception?> Record(Func<Task> run)
	{
		try
		{
			await run();

			return null;
		}
		catch (Exception e)
		{
			return e;
		}
	}
}
