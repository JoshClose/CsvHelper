using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The deflate stages with threads (<see cref="DeflateStage"/>): on .NET 8 and later the chunked stage, whose joins inflate
/// back to their input at chunk sizes from 1 byte to 1 MB, whose bytes are the inline stage's for any thread count, cut
/// and level, which deflates an entry under one chunk on the writing thread without a thread, starts a thread only when
/// none is free, must wait at two chunks a thread, and deflates the last chunk on a stage thread once one has started; on
/// every build the one-deflater stage, whose bytes are the serial entry's (and the inline stage's on .NET Framework) on one
/// thread. For both: chunks in the order cut, Take blocking and TakeAsync awaiting off the stage's threads, a canceled
/// wait leaving its chunk, no wait for input, a stage thread's fault thrown by the next call (or ending a wait for it),
/// and disposal in every state, after which every buffer, segment and chunk has returned once and no stage thread is
/// alive.
/// </summary>
public class DeflateStageTests
{
	/// <summary>How long a test waits for a stage thread before it fails, rather than hanging the suite.</summary>
	private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

	/// <summary>How a test's input is appended.</summary>
	public enum Feed
	{
		/// <summary>Owned segments, handed over uncopied.</summary>
		Owned,

		/// <summary>Bytes, which the stage copies.</summary>
		Copied,

		/// <summary>Both, at random.</summary>
		Mixed,
	}

	/// <summary>The chunk sizes, synchronously and asynchronously.</summary>
	/// <returns>The chunk sizes and modes.</returns>
	public static TheoryData<int, bool> ChunkSizesAndModes()
	{
		var data = new TheoryData<int, bool>();
		foreach (var chunkSize in new[] { 1, 64, 4096, 1 << 20 })
		{
			data.Add(chunkSize, false);
			data.Add(chunkSize, true);
		}

		return data;
	}

	/// <summary>The deflated levels this build has.</summary>
	/// <returns>The levels.</returns>
	public static TheoryData<CompressionLevel> DeflatedLevels()
	{
		var data = new TheoryData<CompressionLevel> { CompressionLevel.Optimal, CompressionLevel.Fastest };
#if NET
		data.Add(CompressionLevel.SmallestSize);
#endif

		return data;
	}

	/// <summary>The stages with threads this build has: the chunked one on .NET 8 and later, and the one-deflater one.</summary>
	/// <returns>The stages' names.</returns>
	public static TheoryData<string> Kinds()
	{
		var data = new TheoryData<string>();
#if NET
		data.Add("chunked");
#endif
		data.Add("serial");

		return data;
	}

	/// <summary>Each stage, synchronously and asynchronously.</summary>
	/// <returns>The stages and modes.</returns>
	public static TheoryData<string, bool> KindsAndModes()
	{
		var data = new TheoryData<string, bool>();
		foreach (var kind in Kinds())
		{
			data.Add(kind, false);
			data.Add(kind, true);
		}

		return data;
	}

	/// <summary>Each stage, and each call that can throw a stage thread's fault.</summary>
	/// <returns>The stages and calls.</returns>
	public static TheoryData<string, string> FaultCalls()
	{
		var data = new TheoryData<string, string>();
		foreach (var kind in Kinds())
		{
			foreach (var call in new[] { "append segment", "append bytes", "complete", "try take", "take", "take async" })
			{
				data.Add(kind, call);
			}
		}

		return data;
	}

	/// <summary>Each stage, and each state it is disposed in.</summary>
	/// <returns>The stages and states.</returns>
	public static TheoryData<string, string> DisposeStates()
	{
		var data = new TheoryData<string, string>();
		foreach (var kind in Kinds())
		{
			foreach (var state in new[] { "new", "pending", "in flight", "complete", "some taken", "taken and not returned", "all taken", "faulted", "waiting in take", "waiting in take async" })
			{
				data.Add(kind, state);
			}
		}

		return data;
	}

	/// <summary>
	/// Every join inflates back to its input, for random appends of owned segments, copied bytes and both, from 1 byte to
	/// more than a chunk, at chunk sizes 1, 64, 4 KB and 1 MB (on .NET Framework through the one-deflater stage, whatever
	/// the chunk size): its CRC, combined from the chunks' in order, is the input's (System.IO.Hashing's and
	/// <see cref="Crc32.Append"/>'s), the chunks' lengths add up, and they come in order; on .NET 8 and later every chunk
	/// but the last takes a whole chunk size, and the last the rest (empty, at 1 and 64). Every buffer and segment returns,
	/// and no thread is left.
	/// </summary>
	/// <param name="chunkSize">The chunk size.</param>
	/// <param name="async">Whether to take chunks asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(ChunkSizesAndModes))]
	public async Task JoinsInflateBackToTheInput(int chunkSize, bool async)
	{
		var length = chunkSize switch
		{
			1 => 2_000,
			64 => 40_000,
			4096 => 300_000,
			_ => (3 << 20) + 300_000,
		};

		foreach (var feed in new[] { Feed.Owned, Feed.Copied, Feed.Mixed })
		{
			var input = ZipWriterTests.Bytes(length, (int)feed);
			var writer = NewWriter(CompressionLevel.Fastest, chunkSize);
			var stage = DeflateStages.Create(writer, 4);

			var joined = await RunAsync(stage, writer, input, Cut(length, chunkSize, (int)feed + 10, feed), async);
			stage.Dispose();

			Assert.Equal(input, Inflate(joined.Bytes));
			Assert.Equal(Reference(input), joined.Crc);
			Assert.Equal(Crc32.Append(0, input), joined.Crc);
			Assert.Equal(length, joined.Length);
#if NET
			Assert.Equal((length / chunkSize) + 1, joined.Lengths.Count);
			Assert.All(joined.Lengths.Take(joined.Lengths.Count - 1), l => Assert.Equal(chunkSize, l));
			Assert.Equal(length % chunkSize, joined.Lengths[^1]);
#endif
			AssertReleased(stage, writer);
		}
	}

#if NET
	/// <summary>
	/// An entry under one chunk (nothing, a byte, some, a byte short) starts no thread: nothing is cut until it completes,
	/// so neither Take waits, and then its one chunk, deflated on the writing thread, is there to take at once, the inline
	/// stage's bytes.
	/// </summary>
	[Fact]
	public void DeflatesAnEntryUnderOneChunkOnTheWritingThread()
	{
		const int chunkSize = 64 * 1024;
		foreach (var length in new[] { 0, 1, 1_000, chunkSize - 1 })
		{
			var input = ZipWriterTests.Bytes(length, 7);
			var writer = NewWriter(CompressionLevel.Fastest, chunkSize);
			var stage = new ChunkedDeflateStage(writer, 4);
			var deflatedOn = new ConcurrentBag<Thread>();
			stage.BeforeDeflate = _ => deflatedOn.Add(Thread.CurrentThread);
			var at = 0;
			foreach (var (n, owned) in Cut(length, chunkSize, 8, Feed.Mixed))
			{
				Append(stage, writer, input, at, n, owned);
				at += n;

				Assert.False(stage.TryTake(out _));
				Assert.Null(stage.Take());
			}

			stage.Complete();

			Assert.True(stage.TryTake(out var chunk));
			Assert.Equal(Inline(input, CompressionLevel.Fastest, chunkSize).Outputs.Single(), Bytes(chunk));
			Assert.Equal(Reference(input), chunk.Crc);
			Assert.Equal(length, chunk.InputLength);
			stage.Return(chunk);
			Assert.Null(stage.Take());
			Assert.Empty(stage.StartedThreads());
			Assert.Equal(Thread.CurrentThread, Assert.Single(deflatedOn));
			stage.Dispose();
			AssertReleased(stage, writer);
		}
	}

	/// <summary>
	/// An entry of whole chunks ends with an empty last chunk, <c>03 00</c> with CRC 0 and no input; the join inflates and
	/// is the inline stage's.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task EndsAnEntryOfWholeChunksWithTheEmptyFinalBlock()
	{
		var input = ZipWriterTests.Bytes(3 * 4096, 9);
		var writer = NewWriter(CompressionLevel.Fastest, 4096);
		var stage = new ChunkedDeflateStage(writer, 2);

		var joined = await RunAsync(stage, writer, input, Cut(input.Length, 4096, 10, Feed.Mixed), async: false);
		stage.Dispose();

		Assert.Equal(4, joined.Outputs.Count);
		Assert.Equal(new byte[] { 0x03, 0x00 }, joined.Outputs[^1]);
		Assert.Equal(0u, joined.Crcs[^1]);
		Assert.Equal(0, joined.Lengths[^1]);
		Assert.Equal(input, Inflate(joined.Bytes));
		Assert.Equal(Inline(input, CompressionLevel.Fastest, 4096).Outputs, joined.Outputs);
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// The bytes depend on the data alone: at 2 to 16 threads, for appends cut at random (owned, copied and both, taken
	/// synchronously and asynchronously), every chunk is the inline stage's for the input in one append, as are the chunks
	/// of the inline stage itself cut at random; and the CRC is the whole input's.
	/// </summary>
	/// <param name="threads">The threads.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(8)]
	[InlineData(16)]
	public async Task GivesTheInlineStagesBytesForAnyThreadsAndCuts(int threads)
	{
		const int chunkSize = 64 * 1024;
		var input = ZipWriterTests.Bytes(700_000, 11);
		var expected = Inline(input, CompressionLevel.Fastest, chunkSize);
		var seed = 20;
		foreach (var feed in new[] { Feed.Owned, Feed.Copied, Feed.Mixed })
		{
			foreach (var async in new[] { false, true })
			{
				var writer = NewWriter(CompressionLevel.Fastest, chunkSize);
				var stage = new ChunkedDeflateStage(writer, threads);

				var joined = await RunAsync(stage, writer, input, Cut(input.Length, chunkSize, seed++, feed), async);
				stage.Dispose();

				Assert.Equal(expected.Outputs, joined.Outputs);
				Assert.Equal(Reference(input), joined.Crc);
				AssertReleased(stage, writer);
			}
		}

		var inlineWriter = NewWriter(CompressionLevel.Fastest, chunkSize);
		var inline = new InlineDeflateStage(inlineWriter, 1);
		var cut = await RunAsync(inline, inlineWriter, input, Cut(input.Length, chunkSize, seed, Feed.Mixed), async: false);
		inline.Dispose();

		Assert.Equal(expected.Outputs, cut.Outputs);
		Assert.Equal(0, inlineWriter.RentedBuffers);
		Assert.Equal(0, inlineWriter.HeldSegments);
	}

	/// <summary>At every deflated level, the chunked stage's chunks are the inline stage's, and inflate back.</summary>
	/// <param name="level">The level.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(DeflatedLevels))]
	public async Task GivesTheInlineStagesBytesAtEachLevel(CompressionLevel level)
	{
		var input = ZipWriterTests.Bytes(100_000, 12);
		var writer = NewWriter(level, 4096);
		var stage = new ChunkedDeflateStage(writer, 3);

		var joined = await RunAsync(stage, writer, input, Cut(input.Length, 4096, 13, Feed.Mixed), async: true);
		stage.Dispose();

		Assert.Equal(Inline(input, level, 4096).Outputs, joined.Outputs);
		Assert.Equal(input, Inflate(joined.Bytes));
		AssertReleased(stage, writer);
	}

	/// <summary>In chunks of 1 MB, as the writer cuts them, at 2 and 16 threads and two cuts each, the chunks are the inline stage's.</summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task GivesTheInlineStagesBytesInChunksOfAMegabyte()
	{
		var input = ZipWriterTests.Bytes((5 << 20) / 2, 14);
		var expected = Inline(input, CompressionLevel.Fastest, DeflateSizes.ChunkSize);
		foreach (var threads in new[] { 2, 16 })
		{
			foreach (var seed in new[] { 30, 31 })
			{
				var writer = NewWriter(CompressionLevel.Fastest, DeflateSizes.ChunkSize);
				var stage = new ChunkedDeflateStage(writer, threads);

				var joined = await RunAsync(stage, writer, input, Cut(input.Length, DeflateSizes.ChunkSize, seed, Feed.Mixed), async: seed == 31);
				stage.Dispose();

				Assert.Equal(expected.Outputs, joined.Outputs);
				AssertReleased(stage, writer);
			}
		}
	}

	/// <summary>
	/// The writing thread must wait at two chunks a thread cut and not taken; raising the threads raises that, and starts
	/// threads at once for the chunks waiting, up to the new count; a count at or below the stage's changes nothing.
	/// </summary>
	[Fact]
	public void MustWaitAtTwoChunksAThread()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new ChunkedDeflateStage(writer, 2);
		using var gate = new Gate(_ => true);
		stage.BeforeDeflate = gate.Hook;
		var input = ZipWriterTests.Bytes(64 * 6, 15);

		for (var i = 0; i < 3; i++)
		{
			stage.Append(input.AsSpan(i * 64, 64));

			Assert.False(stage.MustWait);
		}

		stage.Append(input.AsSpan(3 * 64, 64));

		Assert.True(stage.MustWait);
		gate.WaitHeld(2);
		Assert.Equal(2, stage.StartedThreads().Length);

		stage.RaiseThreads(3);

		Assert.Equal(3, stage.Threads);
		Assert.False(stage.MustWait);
		gate.WaitHeld(1);
		Assert.Equal(3, stage.StartedThreads().Length);

		stage.RaiseThreads(2);
		stage.Append(input.AsSpan(4 * 64, 2 * 64));

		Assert.Equal(3, stage.Threads);
		Assert.True(stage.MustWait);
		Assert.Equal(3, stage.StartedThreads().Length);

		gate.Open();
		stage.Complete();
		var joined = TakeAll(stage, 7);

		Assert.Equal(input, Inflate(joined));
		Assert.False(stage.MustWait);
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// A thread starts for a chunk only when no thread started is free to take it: an idle thread takes the next chunk, and
	/// a second thread starts only while the first is busy.
	/// </summary>
	[Fact]
	public void StartsAThreadOnlyWhenNoneIsFree()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new ChunkedDeflateStage(writer, 4);
		using var gate = new Gate(sequence => sequence >= 2);
		stage.BeforeDeflate = gate.Hook;
		var input = ZipWriterTests.Bytes(64 * 4, 16);

		stage.Append(input.AsSpan(0, 64));
		stage.Return(stage.Take()!);
		Assert.True(SpinWait.SpinUntil(() => stage.IdleThreads == 1, Bound));
		stage.Append(input.AsSpan(64, 64));
		stage.Return(stage.Take()!);

		Assert.Single(stage.StartedThreads());

		Assert.True(SpinWait.SpinUntil(() => stage.IdleThreads == 1, Bound));
		stage.Append(input.AsSpan(128, 64));
		gate.WaitHeld(1);

		Assert.Single(stage.StartedThreads());

		stage.Append(input.AsSpan(192, 64));

		Assert.Equal(2, stage.StartedThreads().Length);

		gate.Open();
		for (var sequence = 2; sequence < 4; sequence++)
		{
			var chunk = stage.Take()!;

			Assert.Equal(sequence, chunk.Sequence);
			stage.Return(chunk);
		}

		stage.Complete();
		stage.Return(stage.Take()!);
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// Once a thread has started, the last chunk is deflated on a stage thread too, not on the writing thread, so that the
	/// writing thread writes the chunks before it meanwhile.
	/// </summary>
	[Fact]
	public void DeflatesTheLastChunkOnAStageThreadOnceOneHasStarted()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new ChunkedDeflateStage(writer, 2);
		var deflatedOn = new ConcurrentDictionary<long, Thread>();
		stage.BeforeDeflate = sequence => deflatedOn[sequence] = Thread.CurrentThread;
		var input = ZipWriterTests.Bytes((2 * 64) + 10, 17);

		stage.Append(input);
		stage.Complete();
		var joined = TakeAll(stage, 3);
		stage.Dispose();

		Assert.Equal(input, Inflate(joined));
		Assert.Equal(3, deflatedOn.Count);
		Assert.All(deflatedOn.Values, thread => Assert.Contains(thread, stage.StartedThreads()));
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// Chunks come in the order cut, whatever order they finish in: with the first held on its thread, TryTake takes
	/// nothing though later ones may be done, and Take blocks for real until it is, then gives each in turn.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task TakesChunksInTheOrderCut()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new ChunkedDeflateStage(writer, 4);
		using var gate = new Gate(sequence => sequence == 0);
		stage.BeforeDeflate = gate.Hook;
		var input = ZipWriterTests.Bytes(64 * 4, 18);

		stage.Append(input);
		gate.WaitHeld(1);

		Assert.False(stage.TryTake(out _));
		var take = Task.Run(() => stage.Take());
		Assert.False(await CompletesWithin(take, TimeSpan.FromMilliseconds(100)));

		gate.Open();
		var first = await take;

		Assert.Equal(0, first!.Sequence);
		stage.Return(first);
		for (var sequence = 1; sequence < 4; sequence++)
		{
			var chunk = stage.Take()!;

			Assert.Equal(sequence, chunk.Sequence);
			stage.Return(chunk);
		}

		Assert.Null(stage.Take());
		stage.Complete();
		stage.Return(stage.Take()!);
		stage.Dispose();
		AssertReleased(stage, writer);
	}
#endif

	/// <summary>
	/// The one-deflater stage's bytes are the serial entry's: one DeflateStream over the whole entry at the writer's level,
	/// fed each append in 64 KB writes from its start; its chunks are one per append, each with its append's CRC and length,
	/// then the last (the deflater disposed); on .NET Framework they are the inline stage's, chunk by chunk. One thread
	/// deflates them all.
	/// </summary>
	/// <param name="level">The level.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(DeflatedLevels))]
	public async Task TheSerialStageGivesTheSerialEntrysBytes(CompressionLevel level)
	{
		var input = ZipWriterTests.Bytes(400_000, 19);
		foreach (var async in new[] { false, true })
		{
			var cuts = Cut(input.Length, 64 * 1024, async ? 41 : 40, Feed.Mixed);
			var writer = NewWriter(level, 64 * 1024);
			var stage = new SerialDeflateStage(writer);

			var joined = await RunAsync(stage, writer, input, cuts, async);
			var threads = stage.StartedThreads();
			stage.Dispose();

			Assert.Equal(SerialReference(input, cuts, level), joined.Bytes);
			Assert.Equal(cuts.Count + 1, joined.Outputs.Count);
			var at = 0;
			for (var i = 0; i < cuts.Count; i++)
			{
				Assert.Equal(cuts[i].Length, joined.Lengths[i]);
				Assert.Equal(Reference(input.AsSpan(at, cuts[i].Length)), joined.Crcs[i]);
				at += cuts[i].Length;
			}

			Assert.Equal(0, joined.Lengths[cuts.Count]);
			Assert.Equal(0u, joined.Crcs[cuts.Count]);
			Assert.Equal(Reference(input), joined.Crc);
			Assert.Single(threads);
#if !NET
			var inlineWriter = NewWriter(level, 64 * 1024);
			var inline = new InlineDeflateStage(inlineWriter, 1);
			var expected = await RunAsync(inline, inlineWriter, input, cuts, async);
			inline.Dispose();

			Assert.Equal(expected.Outputs, joined.Outputs);
#endif
			AssertReleased(stage, writer);
		}
	}

	/// <summary>
	/// The one-deflater stage with nothing appended (empty appends among it) ends with the empty final block, made on the
	/// writing thread without starting a thread; and so do the other builds' stages from <see cref="DeflateStages.Create"/>.
	/// </summary>
	[Fact]
	public void TheSerialStageEndsAnEmptyEntryWithTheEmptyFinalBlock()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new SerialDeflateStage(writer);

		stage.Append(ReadOnlySpan<byte>.Empty);
		stage.Append(default(OwnedSegment));

		Assert.False(stage.TryTake(out _));
		Assert.Null(stage.Take());

		stage.Complete();

		Assert.True(stage.TryTake(out var chunk));
		Assert.Equal(new byte[] { 0x03, 0x00 }, Bytes(chunk));
		Assert.Equal(0u, chunk.Crc);
		Assert.Equal(0, chunk.InputLength);
		Assert.Equal(0, chunk.Sequence);
		stage.Return(chunk);
		Assert.Null(stage.Take());
		Assert.Empty(stage.StartedThreads());
		stage.Dispose();
		AssertReleased(stage, writer);
#if !NET
		Assert.IsType<SerialDeflateStage>(DeflateStages.Create(writer, 8));
#endif
	}

	/// <summary>
	/// The one-deflater stage deflates on one thread, whatever it is asked: its threads are 1, raised or not, and the
	/// writing thread must wait at two appends not taken.
	/// </summary>
	[Fact]
	public void TheSerialStageRunsOnOneThread()
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = new SerialDeflateStage(writer);
		using var gate = new Gate(_ => true);
		stage.BeforeDeflate = gate.Hook;
		var input = ZipWriterTests.Bytes(300, 20);

		stage.RaiseThreads(8);

		Assert.Equal(1, stage.Threads);

		stage.Append(input.AsSpan(0, 100));
		gate.WaitHeld(1);

		Assert.False(stage.MustWait);

		stage.Append(input.AsSpan(100, 100));

		Assert.True(stage.MustWait);

		stage.Append(input.AsSpan(200, 100));

		Assert.Single(stage.StartedThreads());

		gate.Open();
		var joined = TakeAll(stage, 3);
		stage.Complete();
		joined = [.. joined, .. TakeAll(stage, 1, first: 3)];

		Assert.Equal(input, Inflate(joined));
		Assert.Single(stage.StartedThreads());
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// Neither Take nor TakeAsync waits for input: with no chunk cut and not taken, both give null at once, as TryTake gives
	/// nothing; for the chunked stage, so too with bytes short of a chunk appended.
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Kinds))]
	public async Task DoesntWaitForInput(string kind)
	{
		var writer = NewWriter(CompressionLevel.Fastest, 1 << 20);
		var stage = NewStage(kind, writer, 2);

		Assert.False(stage.TryTake(out _));
		Assert.Null(stage.Take());
		Assert.Null(await stage.TakeAsync(CancellationToken.None));

		if (kind == "chunked")
		{
			var input = ZipWriterTests.Bytes(2_000, 21);
			stage.Append(input.AsSpan(0, 1_000));
			stage.Append(Segment(writer, input, 1_000, 1_000));

			Assert.False(stage.TryTake(out _));
			Assert.Null(stage.Take());
			Assert.Null(await stage.TakeAsync(CancellationToken.None));
		}

		stage.Complete();
		while (await stage.TakeAsync(CancellationToken.None) is { } chunk)
		{
			stage.Return(chunk);
		}

		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// A canceled TakeAsync throws and leaves the chunk it waited for to the next call; a token canceled before the call
	/// throws too.
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Kinds))]
	public async Task ACanceledTakeLeavesTheChunk(string kind)
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		using var gate = new Gate(sequence => sequence == 0);
		SetHook(stage, gate.Hook);
		var input = ZipWriterTests.Bytes(64, 22);

		stage.Append(input);
		gate.WaitHeld(1);
		using var source = new CancellationTokenSource();
		var take = stage.TakeAsync(source.Token).AsTask();

		Assert.False(take.IsCompleted);

		source.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => take);
		Assert.False(stage.TryTake(out _));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stage.TakeAsync(source.Token));

		gate.Open();
		var chunk = await stage.TakeAsync(CancellationToken.None);

		Assert.Equal(0, chunk!.Sequence);
		var joined = Bytes(chunk);
		stage.Return(chunk);
		stage.Complete();
		while (await stage.TakeAsync(CancellationToken.None) is { } next)
		{
			joined = [.. joined, .. Bytes(next)];
			stage.Return(next);
		}

		Assert.Equal(input, Inflate(joined));
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// TakeAsync's continuation runs off the stage's threads, though a stage thread completes its wait: a stage thread never
	/// runs the writing thread's code, which writes to the package's stream.
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Kinds))]
	public async Task ContinuesTakeAsyncOffTheStageThreads(string kind)
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		using var gate = new Gate(sequence => sequence == 0);
		SetHook(stage, gate.Hook);

		stage.Append(ZipWriterTests.Bytes(64, 23));
		gate.WaitHeld(1);
		var take = TakeOnAsync(stage);

		Assert.False(take.IsCompleted);

		gate.Open();
		var (chunk, thread) = await take;

		Assert.DoesNotContain(thread, StartedThreads(stage));
		stage.Return(chunk!);
		stage.Complete();
		stage.Return(stage.Take()!);
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// A stage thread's exception is held and thrown, the same object, by the writing thread's next call, whichever it is
	/// (TryTake too, though the chunk before it may be ready), and by every call after; the segment of an append that throws
	/// is returned; disposing after it returns everything and leaves no thread.
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <param name="call">The call that throws it.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(FaultCalls))]
	public async Task RethrowsAStageThreadsFault(string kind, string call)
	{
		var planted = new InvalidTimeZoneException("Planted on a stage thread.");
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		SetHook(stage, sequence =>
		{
			if (sequence == 1)
			{
				throw planted;
			}
		});

		var input = ZipWriterTests.Bytes(64 * 4, 24);

		stage.Append(input.AsSpan(0, 64));
		stage.Append(input.AsSpan(64, 64));

		Assert.True(SpinWait.SpinUntil(() => Faulted(stage), Bound));
		var thrown = call switch
		{
			"append segment" => Record.Exception(() => stage.Append(Segment(writer, input, 128, 64))),
			"append bytes" => Record.Exception(() => stage.Append(input.AsSpan(128, 64))),
			"complete" => Record.Exception(stage.Complete),
			"try take" => Record.Exception(() => stage.TryTake(out _)),
			"take" => Record.Exception(() => stage.Take()),
			_ => await Record.ExceptionAsync(async () => await stage.TakeAsync(CancellationToken.None)),
		};

		Assert.Same(planted, thrown);
		Assert.Same(planted, Record.Exception(() => stage.TryTake(out _)));
		Assert.Same(planted, Record.Exception(() => stage.Append(Segment(writer, input, 192, 64))));

		stage.Dispose();
		stage.Dispose();

		AssertReleased(stage, writer);
	}

	/// <summary>
	/// A stage thread's fault wakes the writing thread already waiting in Take or TakeAsync for the chunk that failed, which
	/// then throws it.
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <param name="async">Whether the writing thread waits with TakeAsync rather than Take.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(KindsAndModes))]
	public async Task WakesAWaitingTakeWithAFault(string kind, bool async)
	{
		var planted = new InvalidTimeZoneException("Planted on a stage thread.");
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		using var gate = new Gate(sequence => sequence == 0);
		SetHook(stage, sequence =>
		{
			gate.Hook(sequence);

			throw planted;
		});

		stage.Append(ZipWriterTests.Bytes(64, 29));
		gate.WaitHeld(1);
		var take = async ? stage.TakeAsync(CancellationToken.None).AsTask() : Task.Run(() => stage.Take());

		Assert.False(await CompletesWithin(take, TimeSpan.FromMilliseconds(100)));

		gate.Open();

		Assert.True(await CompletesWithin(take, Bound));
		Assert.Same(planted, await Record.ExceptionAsync(() => take));
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// A failure in a chunk deflated on the writing thread (an entry's only chunk) is thrown by Complete itself, not held;
	/// everything returns, and no thread started.
	/// </summary>
	/// <param name="kind">The stage.</param>
	[Theory]
	[MemberData(nameof(Kinds))]
	public void ThrowsAFailureOfTheWritingThreadsChunkAtOnce(string kind)
	{
		var planted = new InvalidTimeZoneException("Planted on the writing thread.");
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		SetHook(stage, _ => throw planted);
		if (kind == "chunked")
		{
			var input = ZipWriterTests.Bytes(20, 25);
			stage.Append(input.AsSpan(0, 10));
			stage.Append(Segment(writer, input, 10, 10));
		}

		Assert.Same(planted, Record.Exception(stage.Complete));
		Assert.False(Faulted(stage));
		Assert.Empty(StartedThreads(stage));
		Assert.False(stage.TryTake(out _));

		stage.Dispose();

		AssertReleased(stage, writer);
	}

	/// <summary>Once complete, a stage refuses more input, and returns a segment it is given at once.</summary>
	/// <param name="kind">The stage.</param>
	[Theory]
	[MemberData(nameof(Kinds))]
	public void RefusesInputOnceComplete(string kind)
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 2);
		var input = ZipWriterTests.Bytes(100, 26);
		stage.Append(input);
		stage.Complete();

		Assert.Throws<InvalidOperationException>(() => stage.Append(Segment(writer, input, 0, 10)));
		Assert.Throws<InvalidOperationException>(() => stage.Append(input.AsSpan(0, 10)));
		Assert.Throws<InvalidOperationException>(stage.Complete);
		Assert.Equal(input, Inflate(TakeAll(stage, 2)));
		Assert.Equal(0, writer.HeldSegments);
		stage.Dispose();
		AssertReleased(stage, writer);
	}

	/// <summary>
	/// Disposing in every state: before any input; with input not yet cut; with chunks in flight (it waits for them,
	/// joining the threads before it returns a buffer, which the segments still held show); after completing; with some
	/// chunks taken; with one taken and not yet returned (the caller's to return after); after every chunk; after a fault;
	/// and from another thread while the writing thread waits in Take or TakeAsync, which then give null. Every buffer,
	/// segment and chunk returns once, no thread is left, disposing again does nothing, and the calls after it refuse or
	/// give nothing (or throw the fault).
	/// </summary>
	/// <param name="kind">The stage.</param>
	/// <param name="state">The state it is disposed in.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(DisposeStates))]
	public async Task DisposesInEveryState(string kind, string state)
	{
		var writer = NewWriter(CompressionLevel.Fastest, 64);
		var stage = NewStage(kind, writer, 3);
		var input = ZipWriterTests.Bytes((64 * 6) + 10, 27);
		using var gate = new Gate(_ => state is "in flight" or "waiting in take" or "waiting in take async");
		SetHook(stage, gate.Hook);
		DeflatedChunk? kept = null;
		Exception? planted = null;
		switch (state)
		{
			case "pending":
				stage.Append(input.AsSpan(0, 10));
				stage.Append(Segment(writer, input, 10, 20));
				break;
			case "in flight":
				stage.Append(input.AsSpan(0, 2 * 64));
				stage.Append(Segment(writer, input, 2 * 64, input.Length - (2 * 64)));
				gate.WaitHeld(1);
				var dispose = Task.Run(stage.Dispose);

				Assert.False(await CompletesWithin(dispose, TimeSpan.FromMilliseconds(100)));
				Assert.NotEqual(0, writer.HeldSegments);

				gate.Open();
				await dispose;
				break;
			case "complete":
				FeedAll(stage, writer, input);
				stage.Complete();
				break;
			case "some taken":
				FeedAll(stage, writer, input);
				stage.Complete();
				stage.Return(stage.Take()!);
				break;
			case "taken and not returned":
				FeedAll(stage, writer, input);
				stage.Complete();
				kept = stage.Take();
				break;
			case "all taken":
				FeedAll(stage, writer, input);
				stage.Complete();
				while (stage.Take() is { } chunk)
				{
					stage.Return(chunk);
				}

				break;
			case "faulted":
				planted = new InvalidTimeZoneException("Planted on a stage thread.");
				SetHook(stage, sequence =>
				{
					if (sequence == 1)
					{
						throw planted;
					}
				});

				// Two chunks, the second of which fails: no append comes after it to see the fault.
				stage.Append(input.AsSpan(0, 64));
				stage.Append(Segment(writer, input, 64, 64));

				Assert.True(SpinWait.SpinUntil(() => Faulted(stage), Bound));
				break;
			case "waiting in take":
			case "waiting in take async":
				stage.Append(input.AsSpan(0, 64));
				gate.WaitHeld(1);
				var take = state == "waiting in take" ? Task.Run(() => stage.Take()) : stage.TakeAsync(CancellationToken.None).AsTask();

				Assert.False(await CompletesWithin(take, TimeSpan.FromMilliseconds(100)));

				var disposing = Task.Run(stage.Dispose);

				Assert.True(await CompletesWithin(take, Bound));
				Assert.Null(await take);

				gate.Open();
				await disposing;
				break;
		}

		gate.Open();
		stage.Dispose();
		stage.Dispose();
		if (kept is not null)
		{
			stage.Return(kept);
		}

		var segment = Segment(writer, input, 0, 10);
		if (planted is null)
		{
			Assert.Throws<InvalidOperationException>(() => stage.Append(segment));
			Assert.Throws<InvalidOperationException>(() => stage.Append(input.AsSpan(0, 10)));
			Assert.Throws<InvalidOperationException>(stage.Complete);
			Assert.False(stage.TryTake(out _));
			Assert.Null(stage.Take());
			Assert.Null(await stage.TakeAsync(CancellationToken.None));
		}
		else
		{
			Assert.Same(planted, Record.Exception(() => stage.Append(segment)));
			Assert.Same(planted, Record.Exception(stage.Complete));
			Assert.Same(planted, Record.Exception(() => stage.Take()));
		}

		AssertReleased(stage, writer);
	}

	/// <summary>Inflates a raw deflate stream.</summary>
	/// <param name="deflated">The stream's bytes.</param>
	/// <returns>Its data.</returns>
	internal static byte[] Inflate(byte[] deflated)
	{
		using var inflater = new DeflateStream(new MemoryStream(deflated), CompressionMode.Decompress);
		using var output = new MemoryStream();
		inflater.CopyTo(output);

		return output.ToArray();
	}

	/// <summary>A chunk's output bytes.</summary>
	/// <param name="chunk">The chunk.</param>
	/// <returns>The bytes, joined.</returns>
	internal static byte[] Bytes(DeflatedChunk chunk)
	{
		var bytes = new byte[chunk.Output.Length];
		var at = 0;
		for (var i = 0; i < chunk.Output.Count; i++)
		{
			chunk.Output[i].AsSpan(0, chunk.Output.LengthOf(i)).CopyTo(bytes.AsSpan(at));
			at += chunk.Output.LengthOf(i);
		}

		return bytes;
	}

	/// <summary>A writer for a stage: its level, its chunk size and its pool, over a stream nothing is written to.</summary>
	/// <param name="level">The level.</param>
	/// <param name="chunkSize">The chunk size.</param>
	/// <returns>The writer.</returns>
	private static ZipWriter NewWriter(CompressionLevel level, int chunkSize)
	{
		return new ZipWriter(new MemoryStream(), false, level, ZipLimits.Default) { ChunkSize = chunkSize };
	}

	/// <summary>A stage with threads.</summary>
	/// <param name="kind">Which: <c>chunked</c> or <c>serial</c>.</param>
	/// <param name="writer">The writer.</param>
	/// <param name="threads">The most threads, for the chunked stage.</param>
	/// <returns>The stage.</returns>
	private static DeflateStage NewStage(string kind, ZipWriter writer, int threads)
	{
		return kind switch
		{
#if NET
			"chunked" => new ChunkedDeflateStage(writer, threads),
#endif
			"serial" => new SerialDeflateStage(writer),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such stage."),
		};
	}

	/// <summary>Sets a stage's hook, called on the thread that deflates a chunk before it does.</summary>
	/// <param name="stage">The stage.</param>
	/// <param name="hook">The hook.</param>
	private static void SetHook(DeflateStage stage, Action<long> hook) => ((ThreadedDeflateStage)stage).BeforeDeflate = hook;

	/// <summary>Whether a stage thread has thrown.</summary>
	/// <param name="stage">The stage.</param>
	/// <returns>Whether it has.</returns>
	private static bool Faulted(DeflateStage stage) => ((ThreadedDeflateStage)stage).Faulted;

	/// <summary>The threads a stage started.</summary>
	/// <param name="stage">The stage.</param>
	/// <returns>The threads.</returns>
	private static Thread[] StartedThreads(DeflateStage stage) => ((ThreadedDeflateStage)stage).StartedThreads();

	/// <summary>
	/// Checks that a disposed stage has returned everything: the writer's buffers and segments (a segment returned twice
	/// would take the count below 0), the chunked stage's gather buffers, and that none of its threads is alive.
	/// </summary>
	/// <param name="stage">The stage, disposed.</param>
	/// <param name="writer">Its writer.</param>
	private static void AssertReleased(DeflateStage stage, ZipWriter writer)
	{
		Assert.Equal(0, writer.RentedBuffers);
		Assert.Equal(0, writer.HeldSegments);
#if NET
		if (stage is ChunkedDeflateStage chunked)
		{
			Assert.Equal(0, chunked.GatherBuffers);
		}
#endif
		Assert.All(StartedThreads(stage), thread => Assert.False(thread.IsAlive));
	}

	/// <summary>
	/// An input cut into appends at random: each one's length (1 byte to 16, to half a chunk, to two chunks, or to an eighth
	/// of the input, up to 70,000, as a sheet's flushes) and whether it is an owned segment or copied bytes.
	/// </summary>
	/// <param name="total">The input's length.</param>
	/// <param name="chunkSize">The chunk size.</param>
	/// <param name="seed">The seed.</param>
	/// <param name="feed">How the appends are made.</param>
	/// <returns>The appends.</returns>
	private static List<(int Length, bool Owned)> Cut(int total, int chunkSize, int seed, Feed feed)
	{
		var random = new Random(seed);
		var cuts = new List<(int Length, bool Owned)>();
		for (var at = 0; at < total;)
		{
			var most = random.Next(4) switch
			{
				0 => 16,
				1 => Math.Max(1, chunkSize / 2),
				2 => chunkSize * 2,
				_ => Math.Max(1, Math.Min(70_000, total / 8)),
			};
			var length = Math.Min(total - at, 1 + random.Next(most));
			var owned = feed switch
			{
				Feed.Owned => true,
				Feed.Copied => false,
				_ => random.Next(2) == 0,
			};
			cuts.Add((length, owned));
			at += length;
		}

		return cuts;
	}

	/// <summary>Appends bytes of the input: as an owned segment (a pooled copy, counted by the writer) or as bytes.</summary>
	/// <param name="stage">The stage.</param>
	/// <param name="writer">Its writer.</param>
	/// <param name="input">The input.</param>
	/// <param name="offset">The first byte.</param>
	/// <param name="length">How many: 1 or more.</param>
	/// <param name="owned">Whether to append an owned segment.</param>
	private static void Append(DeflateStage stage, ZipWriter writer, byte[] input, int offset, int length, bool owned)
	{
		if (owned)
		{
			stage.Append(Segment(writer, input, offset, length));
		}
		else
		{
			stage.Append(input.AsSpan(offset, length));
		}
	}

	/// <summary>An owned segment holding bytes of the input, counted by the writer as an entry counts one it takes.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="input">The input.</param>
	/// <param name="offset">The first byte.</param>
	/// <param name="length">How many: 1 or more.</param>
	/// <returns>The segment.</returns>
	private static OwnedSegment Segment(ZipWriter writer, byte[] input, int offset, int length)
	{
		var array = ArrayPool<byte>.Shared.Rent(length);
		input.AsSpan(offset, length).CopyTo(array);
		var segment = new OwnedSegment(array, length);
		writer.Hold(segment);

		return segment;
	}

	/// <summary>Appends the input in a few appends, owned and copied, taking nothing.</summary>
	/// <param name="stage">The stage.</param>
	/// <param name="writer">Its writer.</param>
	/// <param name="input">The input.</param>
	private static void FeedAll(DeflateStage stage, ZipWriter writer, byte[] input)
	{
		var at = 0;
		foreach (var (length, owned) in Cut(input.Length, 64, 28, Feed.Mixed))
		{
			Append(stage, writer, input, at, length, owned);
			at += length;
		}
	}

	/// <summary>
	/// Runs an input through a stage as an entry does: each append, then every chunk ready taken, and while the stage must
	/// wait the next one taken (with Take, or TakeAsync); then the stage completed and every chunk left taken.
	/// </summary>
	/// <param name="stage">The stage.</param>
	/// <param name="writer">Its writer.</param>
	/// <param name="input">The input.</param>
	/// <param name="cuts">The appends.</param>
	/// <param name="async">Whether to wait with TakeAsync rather than Take.</param>
	/// <returns>A task whose result is the chunks taken.</returns>
	private static async Task<Joined> RunAsync(DeflateStage stage, ZipWriter writer, byte[] input, List<(int Length, bool Owned)> cuts, bool async)
	{
		var joined = new Joined(stage);
		var at = 0;
		foreach (var (length, owned) in cuts)
		{
			Append(stage, writer, input, at, length, owned);
			at += length;
			await joined.TakeReadyAsync(async);
		}

		stage.Complete();
		await joined.TakeRestAsync(async);

		return joined;
	}

	/// <summary>The inline stage's chunks for an input in one append: the oracle of the chunked stage's.</summary>
	/// <param name="input">The input.</param>
	/// <param name="level">The level.</param>
	/// <param name="chunkSize">The chunk size.</param>
	/// <returns>The chunks.</returns>
	private static Joined Inline(byte[] input, CompressionLevel level, int chunkSize)
	{
		var writer = NewWriter(level, chunkSize);
		var stage = new InlineDeflateStage(writer, 1);
		List<(int Length, bool Owned)> cuts = input.Length == 0 ? [] : [(input.Length, false)];

		var joined = RunAsync(stage, writer, input, cuts, async: false).GetAwaiter().GetResult();
		stage.Dispose();

		Assert.Equal(0, writer.RentedBuffers);

		return joined;
	}

	/// <summary>The serial entry's deflate: one DeflateStream at the level, fed each append in 64 KB writes from its start.</summary>
	/// <param name="input">The input.</param>
	/// <param name="cuts">The appends.</param>
	/// <param name="level">The level.</param>
	/// <returns>The deflate stream.</returns>
	private static byte[] SerialReference(byte[] input, List<(int Length, bool Owned)> cuts, CompressionLevel level)
	{
		using var output = new MemoryStream();
		using (var deflate = new DeflateStream(output, level, leaveOpen: true))
		{
			var at = 0;
			foreach (var (length, _) in cuts)
			{
				for (var i = 0; i < length; i += DeflateSizes.WriteSize)
				{
					deflate.Write(input, at + i, Math.Min(DeflateSizes.WriteSize, length - i));
				}

				at += length;
			}
		}

		return output.ToArray();
	}

	/// <summary>Takes chunks in order with Take, checking their sequence numbers, and joins their bytes.</summary>
	/// <param name="stage">The stage.</param>
	/// <param name="count">How many.</param>
	/// <param name="first">The first one's sequence number.</param>
	/// <returns>Their bytes, joined.</returns>
	private static byte[] TakeAll(DeflateStage stage, int count, int first = 0)
	{
		var joined = new List<byte>();
		for (var i = 0; i < count; i++)
		{
			var chunk = stage.Take();

			Assert.NotNull(chunk);
			Assert.Equal(first + i, chunk.Sequence);
			joined.AddRange(Bytes(chunk));
			stage.Return(chunk);
		}

		return [.. joined];
	}

	/// <summary>Whether a task completes within a time, waiting for it no longer.</summary>
	/// <param name="task">The task.</param>
	/// <param name="time">The time.</param>
	/// <returns>A task whose result is whether it completed.</returns>
	private static async Task<bool> CompletesWithin(Task task, TimeSpan time) => await Task.WhenAny(task, Task.Delay(time)) == task;

	/// <summary>Takes a chunk with TakeAsync, and the thread its continuation ran on.</summary>
	/// <param name="stage">The stage.</param>
	/// <returns>A task whose result is the chunk and the thread.</returns>
	private static async Task<(DeflatedChunk? Chunk, Thread Thread)> TakeOnAsync(DeflateStage stage)
	{
		var chunk = await stage.TakeAsync(CancellationToken.None).ConfigureAwait(false);

		return (chunk, Thread.CurrentThread);
	}

	/// <summary>System.IO.Hashing's CRC-32.</summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC.</returns>
	private static uint Reference(ReadOnlySpan<byte> data) => System.IO.Hashing.Crc32.HashToUInt32(data);

	/// <summary>The chunks a run took from a stage, in order: their bytes, CRCs and lengths, and the CRC combined.</summary>
	/// <param name="stage">The stage, which each chunk is returned to.</param>
	private sealed class Joined(DeflateStage stage)
	{
		/// <summary>The chunks' bytes, joined.</summary>
		private readonly MemoryStream joined = new();

		/// <summary>Each chunk's bytes.</summary>
		public List<byte[]> Outputs { get; } = [];

		/// <summary>Each chunk's CRC.</summary>
		public List<uint> Crcs { get; } = [];

		/// <summary>Each chunk's input length.</summary>
		public List<long> Lengths { get; } = [];

		/// <summary>The chunks' CRCs combined in order.</summary>
		public uint Crc { get; private set; }

		/// <summary>The chunks' input lengths added up.</summary>
		public long Length { get; private set; }

		/// <summary>The chunks' bytes, joined.</summary>
		public byte[] Bytes => joined.ToArray();

		/// <summary>Takes every chunk ready, then while the stage must wait, the next one.</summary>
		/// <param name="async">Whether to wait with TakeAsync rather than Take.</param>
		/// <returns>A task that completes when they are taken.</returns>
		public async Task TakeReadyAsync(bool async)
		{
			while (stage.TryTake(out var chunk))
			{
				Add(chunk);
			}

			while (stage.MustWait)
			{
				var chunk = await TakeAsync(async);

				Assert.NotNull(chunk);
				Add(chunk);
			}
		}

		/// <summary>Takes every chunk left, once the stage is complete.</summary>
		/// <param name="async">Whether to wait with TakeAsync rather than Take.</param>
		/// <returns>A task that completes when they are taken.</returns>
		public async Task TakeRestAsync(bool async)
		{
			while (await TakeAsync(async) is { } chunk)
			{
				Add(chunk);
			}
		}

		/// <summary>Takes the next chunk, waiting.</summary>
		/// <param name="async">Whether to wait with TakeAsync rather than Take.</param>
		/// <returns>A task whose result is the chunk, or <see langword="null"/>.</returns>
		private async Task<DeflatedChunk?> TakeAsync(bool async) => async ? await stage.TakeAsync(CancellationToken.None) : stage.Take();

		/// <summary>Adds a chunk, next in order, and returns it to the stage.</summary>
		/// <param name="chunk">The chunk.</param>
		private void Add(DeflatedChunk chunk)
		{
			Assert.Equal(Outputs.Count, chunk.Sequence);
			var bytes = DeflateStageTests.Bytes(chunk);
			joined.Write(bytes, 0, bytes.Length);
			Outputs.Add(bytes);
			Crcs.Add(chunk.Crc);
			Lengths.Add(chunk.InputLength);
			Crc = Crc32.Combine(Crc, chunk.Crc, chunk.InputLength);
			Length += chunk.InputLength;
			stage.Return(chunk);
		}
	}

	/// <summary>A stage's hook that holds the chunks it picks on their threads until it opens, counting those it holds.</summary>
	/// <param name="holds">Which chunks it holds, by sequence number.</param>
	private sealed class Gate(Func<long, bool> holds) : IDisposable
	{
		/// <summary>Open once the test lets the chunks go.</summary>
		private readonly ManualResetEventSlim open = new(false);

		/// <summary>Released once for each chunk held.</summary>
		private readonly SemaphoreSlim held = new(0);

		/// <summary>The hook: holds a chunk it picks until the gate opens (failing after <see cref="Bound"/>).</summary>
		/// <param name="sequence">The chunk's sequence number.</param>
		/// <exception cref="TimeoutException">The test didn't open the gate.</exception>
		public void Hook(long sequence)
		{
			if (holds(sequence))
			{
				held.Release();
				if (!open.Wait(Bound))
				{
					throw new TimeoutException("The test didn't open the gate.");
				}
			}
		}

		/// <summary>Waits until more chunks are held.</summary>
		/// <param name="count">How many more.</param>
		public void WaitHeld(int count)
		{
			for (var i = 0; i < count; i++)
			{
				Assert.True(held.Wait(Bound), "A stage thread didn't reach the gate.");
			}
		}

		/// <summary>Lets every chunk go, held and to come.</summary>
		public void Open() => open.Set();

		/// <summary>Opens the gate, so that no thread is left held when a test fails.</summary>
		public void Dispose() => open.Set();
	}
}
