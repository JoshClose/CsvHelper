using System.Diagnostics;
using System.Text;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// Tests of <see cref="SegmentPipeline"/> with stub scanners (<see cref="StubScanner"/>), a gated stream under the part's
/// <see cref="LimitedReadStream"/> (<see cref="GatedStream"/>) and a checking pool (<see cref="CheckingPool"/>): segments in
/// the part's order whatever order their scans end in, the ring's bound, a stop that joins every thread at every stage
/// before any buffer goes back, every inflated byte charged once, and an inflate error thrown where the serial read throws
/// it. Every wait is bounded (<see cref="SegmentRun.Within(Action)"/>), so a deadlock fails a test rather than hanging the run.
/// </summary>
public sealed class SegmentPipelineTests
{
	/// <summary>Where the pipeline is when it is stopped.</summary>
	public enum Stage
	{
		/// <summary>Before <see cref="SegmentPipeline.Start"/>.</summary>
		NotStarted,

		/// <summary>Right after it: the threads are anywhere.</summary>
		JustStarted,

		/// <summary>The inflating thread is in a read the test holds.</summary>
		InARead,

		/// <summary>The ring is full: the inflating thread waits for a slot.</summary>
		WaitingForASlot,

		/// <summary>Every scan thread is in a scan the test holds.</summary>
		InAScan,

		/// <summary>The source holds a segment it hasn't released.</summary>
		HoldingASegment,

		/// <summary>Every segment was stitched, the final one too.</summary>
		AfterTheLast,

		/// <summary>The splitter found no row start before the cap.</summary>
		AfterAnUnsplitSegment,

		/// <summary>A read of the part failed.</summary>
		AfterAReadFailed,
	}

	/// <summary>How the source is done with the pipeline.</summary>
	public enum Ending
	{
		/// <summary>The rows end in a segment: the rest is charged unread.</summary>
		RowsEnded,

		/// <summary>A marked segment: the serial read goes on from it to the part's end.</summary>
		FallbackReadToEnd,

		/// <summary>A marked segment: the serial read stops part way (an error), and the stream is disposed.</summary>
		FallbackPartlyRead,

		/// <summary>The enumeration stops early: the serial read goes on from the first segment not released.</summary>
		EarlyStopReadToEnd,

		/// <summary>The enumeration stops early, and the reader is disposed part way.</summary>
		EarlyStopPartlyRead,

		/// <summary>The pipeline is disposed with no fallback and no charge.</summary>
		DisposedOnly,
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(1, 300)]
	[InlineData(2, 1)]
	[InlineData(2, 4096)]
	[InlineData(3, 77)]
	[InlineData(8, 1)]
	[InlineData(8, 512)]
	public void DeliversSegmentsInThePartsOrder(int scanThreads, int segmentSize)
	{
		SegmentRun.Within(() =>
		{
			for (var seed = 0; seed < 4; seed++)
			{
				var (data, start) = Sheet(seed, 600);
				using var run = new SegmentRun(data, start, start + 500, scanThreads, segmentSize, seed: seed);
				foreach (var scanner in run.Scanners)
				{
					scanner.Delays = true;
				}

				run.Inner.Chunk = 2000;
				run.Inner.Chunks = new Random(seed);
				run.Begin();

				// SegmentRun.Next checks each segment: the next in order, the part's bytes after the one before, its own block.
				var ended = run.StitchToEnd();
				Assert.NotNull(ended);
				Assert.Equal(SegmentEnd.Ended, ended.Result.End);
				run.Stitch(ended);
				Assert.Equal(data.Length, run.Taken.Sum(t => t.Length) + start);

				// Each segment was scanned once, by one scanner or another.
				var scanned = run.Scanners.SelectMany(s => s.Calls).Select(c => c.Offset).ToList();
				Assert.Equal(scanned.Count, scanned.Distinct().Count());
				Assert.Equal(run.Taken.Select(t => t.Offset).OrderBy(o => o), scanned.OrderBy(o => o));

				run.StopWithin();
				run.Pipeline.ChargeUnread();
				run.Seal();
				run.AssertQuiet();
				run.Finish();
			}
		});
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	public void HoldsAtMostTwoSegmentsPerScanThread(int scanThreads)
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(11, 3000);
			using var run = new SegmentRun(data, start, start, scanThreads, 256);
			Assert.Equal(2 * scanThreads, run.Pipeline.RingSize);
			run.Begin();

			// With every slot full, the inflating thread waits: it has read the segments, and no more than one read past them.
			var held = new List<SegmentSlot>();
			for (var i = 0; i < run.Pipeline.RingSize; i++)
			{
				held.Add(run.Next()!);
			}

			WaitUntilIdle(run.Inner);
			var end = held.Last().Offset + held.Last().Length;
			var delivered = run.Inner.Delivered;
			Assert.InRange(delivered, end, end + 256 + (64 * 1024));
			Assert.True(delivered < data.Length / 2);

			// The slots held are untouched while the inflating thread waits.
			for (var i = 0; i < held.Count; i++)
			{
				Assert.Equal(i, held[i].Sequence);
				Assert.True(data.AsSpan((int)held[i].Offset, held[i].Length).SequenceEqual(held[i].Buffer.AsSpan(0, held[i].Length)));
			}

			// One more without a release would wait for ever: it throws, and changes nothing.
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.Next());

			// A release frees a slot: the inflating thread fills it with the next segment.
			run.Stitch(held[0]);
			var next = run.Next();
			Assert.NotNull(next);
			Assert.Equal(end, next.Offset);
			foreach (var slot in held.Skip(1))
			{
				run.Stitch(slot);
			}

			run.Stitch(next);
			Assert.NotNull(run.StitchToEnd());
			run.StopWithin();
			run.Finish();
		});
	}

	[Theory]
	[InlineData(Stage.NotStarted)]
	[InlineData(Stage.JustStarted)]
	[InlineData(Stage.InARead)]
	[InlineData(Stage.WaitingForASlot)]
	[InlineData(Stage.InAScan)]
	[InlineData(Stage.HoldingASegment)]
	[InlineData(Stage.AfterTheLast)]
	[InlineData(Stage.AfterAnUnsplitSegment)]
	[InlineData(Stage.AfterAReadFailed)]
	public void StopsAndJoinsAtEveryStage(Stage stage)
	{
		SegmentRun.Within(() =>
		{
			for (var seed = 0; seed < 4; seed++)
			{
				StopAt(stage, seed);
			}
		});
	}

	[Fact]
	public void WakesANextThatWaits()
	{
		SegmentRun.Within(() =>
		{
			// The inflating thread is held in a read, so Next waits; a stop wakes it before the read returns.
			var (data, start) = Sheet(12, 200);
			using var run = new SegmentRun(data, start, start, 2, 4096);
			run.Inner.Hold();
			run.Begin();
			Assert.True(run.Inner.WaitForHeldRead(SegmentRun.Bound));
			var next = Task.Factory.StartNew(run.Pipeline.Next, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			Assert.False(next.Wait(100));
			var stop = Task.Factory.StartNew(run.Pipeline.Stop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			Assert.True(next.Wait(SegmentRun.Bound));
			Assert.Null(next.Result);
			Assert.False(stop.Wait(100));
			run.Inner.Open();
			Assert.True(stop.Wait(SegmentRun.Bound));
			Assert.Null(run.Pipeline.Next());
			run.Seal();
			run.AssertQuiet();
			run.Finish();
		});
	}

	[Fact]
	public void MarksAScanThatThrows()
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(13, 400);
			using var run = new SegmentRun(data, start, start, 2, 512);
			var thrown = new InvalidOperationException("The scanner failed.");
			var marked = -1L;
			foreach (var scanner in run.Scanners)
			{
				scanner.Script = (call, block) =>
				{
					if (Interlocked.CompareExchange(ref marked, call.Offset, -1) == -1)
					{
						throw thrown;
					}

					return new SegmentResult { End = SegmentEnd.Done, FirstRow = -1 };
				};
			}

			// The scan that threw marks its segment with the exception, and the others go on.
			run.Begin();
			var count = 0;
			while (run.Next() is { } slot)
			{
				if (slot.Offset == Volatile.Read(ref marked))
				{
					Assert.Equal(SegmentEnd.Marked, slot.Result.End);
					Assert.Equal(SegmentMark.Error, slot.Result.Mark);
					Assert.Same(thrown, slot.Result.Error);
				}
				else
				{
					Assert.Equal(SegmentEnd.Done, slot.Result.End);
				}

				run.Stitch(slot);
				count++;
			}

			Assert.True(count > 3);
			run.StopWithin();
			run.Finish();
		});
	}

	[Fact]
	public void CatchesBuffersReturnedTwiceOrInUse()
	{
		SegmentRun.Within(() =>
		{
			// The checking pool itself: what the pipeline's tests count on it to catch.
			var pool = new CheckingPool();
			var once = pool.Rent(10);
			Assert.True(once.Length >= 10);
			Assert.All(once, b => Assert.Equal(0xCC, b));
			pool.Return(once);
			Assert.All(once, b => Assert.Equal(0xDD, b));
			Assert.Empty(pool.Failures);
			pool.Return(once);
			Assert.Equal("A buffer was returned twice.", Assert.Single(pool.Failures));

			pool = new CheckingPool();
			pool.Return(new byte[16]);
			Assert.Equal("A buffer of 16 bytes that the pool didn't rent was returned.", Assert.Single(pool.Failures));

			// A return while a held read writes into the buffer.
			pool = new CheckingPool();
			var stream = new GatedStream(new byte[100]);
			pool.InUse = stream.Reading;
			var reading = pool.Rent(50);
			stream.Hold();
			var read = Task.Factory.StartNew(() => stream.Read(reading, 0, 50), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			Assert.True(stream.WaitForHeldRead(SegmentRun.Bound));
			pool.Return(reading);
			stream.Open();
			Assert.True(read.Wait(SegmentRun.Bound));
			Assert.Equal("A buffer was returned while a read of the part's stream used it.", Assert.Single(pool.Failures));

			// A buffer still rented.
			pool = new CheckingPool();
			pool.Rent(5);
			Assert.Equal(1, pool.Outstanding);
			Assert.ThrowsAny<Exception>(pool.AssertClean);
		});
	}

	[Theory]
	[InlineData(Ending.RowsEnded)]
	[InlineData(Ending.FallbackReadToEnd)]
	[InlineData(Ending.FallbackPartlyRead)]
	[InlineData(Ending.EarlyStopReadToEnd)]
	[InlineData(Ending.EarlyStopPartlyRead)]
	[InlineData(Ending.DisposedOnly)]
	public void ChargesEveryInflatedByteOnce(Ending ending)
	{
		SegmentRun.Within(() =>
		{
			for (var seed = 0; seed < 10; seed++)
			{
				var random = new Random((seed * 17) + (int)ending);
				var (data, start) = Sheet(100 + seed, 500);
				var windowEnd = Math.Min(data.Length, start + random.Next(0, 4000));
				int[] sizes = [1, 64, 700, 4096];
				using var run = new SegmentRun(data, start, windowEnd, random.Next(1, 4), sizes[random.Next(sizes.Length)], seed: seed);
				run.Inner.Chunk = random.Next(1, 5000);
				foreach (var scanner in run.Scanners)
				{
					scanner.Delays = true;
				}

				// Segments past a random byte end the rows, or are marked.
				var past = start + random.Next(0, data.Length - start);
				foreach (var scanner in run.Scanners)
				{
					var endMarker = scanner.EndMarker;
					scanner.Script = (call, _) => call.Offset + call.Bytes.Length <= past
						? StubScanner.Default(call, endMarker)
						: ending == Ending.RowsEnded
							? new SegmentResult { End = SegmentEnd.Ended, FirstRow = -1 }
							: new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Error, FirstRow = -1 };
				}

				run.Begin();
				SegmentStream? fallback = null;
				switch (ending)
				{
					case Ending.RowsEnded:
						var ended = run.StitchToEnd()!;
						run.Stitch(ended);
						run.StopWithin();
						run.Pipeline.ChargeUnread();
						run.Pipeline.ChargeUnread();
						break;
					case Ending.FallbackReadToEnd:
					case Ending.FallbackPartlyRead:
						var marked = run.StitchToEnd()!;
						run.StopWithin();
						fallback = run.Pipeline.FallbackFrom(marked);
						Assert.Equal(marked.Offset, fallback.Offset);
						break;
					case Ending.EarlyStopReadToEnd:
					case Ending.EarlyStopPartlyRead:
						run.StitchToEnd(random.Next(0, 6));
						if (random.Next(2) == 0)
						{
							run.Next();
						}

						run.StopWithin();
						fallback = run.Pipeline.FallbackAfterDelivered();
						break;
					case Ending.DisposedOnly:
						run.StitchToEnd(random.Next(0, 6));
						if (random.Next(2) == 0)
						{
							run.Next();
						}

						break;
				}

				if (fallback is not null)
				{
					var offset = (int)fallback.Offset;
					var expected = data.AsSpan(offset).ToArray();
					var length = ending is Ending.FallbackPartlyRead or Ending.EarlyStopPartlyRead ? random.Next(0, expected.Length) : expected.Length;
					var bytes = ReadSome(fallback, length, random);
					Assert.Equal(expected.Take(bytes.Length), bytes);
					Assert.True(bytes.Length >= length);
					fallback.Dispose();
				}

				// The serial window's bytes, the stitcher's, the fallback's and the unread ones: each inflated byte counted once.
				run.Finish();
			}
		});
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(700)]
	[InlineData(9000)]
	public void RethrowsAnInflateErrorAtItsByte(int afterWindow)
	{
		SegmentRun.Within(() =>
		{
			for (var seed = 0; seed < 6; seed++)
			{
				var random = new Random(seed);
				var (data, start) = Sheet(200 + seed, 500);
				var windowEnd = start + 300;
				var failAt = windowEnd + afterWindow;
				using var run = new SegmentRun(data, start, windowEnd, 2, new[] { 1, 256, 4096 }[seed % 3], seed: seed);
				run.Inner.FailAt = failAt;
				run.Inner.Chunk = random.Next(1, 3000);

				// The fallback is from the final segment, which ends at the failed read, or from an earlier one that a scan marks.
				var markFrom = random.Next(2) == 0 ? long.MaxValue : start + random.Next(0, failAt - start);
				foreach (var scanner in run.Scanners)
				{
					var endMarker = scanner.EndMarker;
					scanner.Script = (call, _) => call.Offset >= markFrom
						? new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Error, FirstRow = -1 }
						: StubScanner.Default(call, endMarker);
				}

				run.Begin();
				var from = run.StitchToEnd()!;
				if (from.Final)
				{
					Assert.Equal(failAt, from.Offset + from.Length);
					Assert.Null(run.Pipeline.Next());
				}

				// From there, the part's bytes up to the failed read, then its exception, again at each read.
				run.StopWithin();
				using var fallback = run.Pipeline.FallbackFrom(from);
				var bytes = ReadUntilError(fallback, out var error);
				Assert.Equal(data.AsSpan((int)from.Offset, failAt - (int)from.Offset).ToArray(), bytes);
				Assert.Same(run.Inner.Failure, error);
				Assert.Same(run.Inner.Failure, Record.Exception(() => fallback.Read(new byte[10], 0, 10)));

				// The serial read of the part, in a workbook of its own, throws the same exception after the same bytes.
				var serialStream = new GatedStream(data) { FailAt = failAt };
				using var serialArchive = SyncOrAsync.Result(PackageArchive.OpenAsync(TestPackage.Workbook("Data").ToStream(), false, Packages.Limits, false, CancellationToken.None));
				using var serial = new LimitedReadStream(serialStream, serialArchive, SegmentRun.PartName, long.MaxValue, 1L << 40);
				var serialBytes = ReadUntilError(serial, out var serialError);
				Assert.Equal(failAt, serialBytes.Length);
				Assert.Same(serialStream.Failure.GetType(), serialError!.GetType());
				Assert.Equal(serialStream.Failure.Message, serialError.Message);

				fallback.Dispose();
				run.Seal();
				run.AssertQuiet();
				run.Finish();
			}
		});
	}

	[Fact]
	public void RethrowsAPartPastItsSizeLimit()
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(21, 400);
			var limit = data.Length / 2;
			using var run = new SegmentRun(data, start, start + 100, 2, 512, maxPartSize: limit);
			run.Begin();
			var last = run.StitchToEnd()!;
			Assert.True(last.Final);
			Assert.True(last.Offset + last.Length <= limit);
			run.StopWithin();
			using var fallback = run.Pipeline.FallbackFrom(last);
			var bytes = ReadUntilError(fallback, out var error);
			Assert.Equal(data.AsSpan((int)last.Offset, bytes.Length).ToArray(), bytes);
			var e = Assert.IsType<InvalidDataException>(error);
			Assert.Equal($"{SegmentRun.PartName} is larger than ExcelReaderOptions.MaxPartSize ({limit} bytes uncompressed).", e.Message);
			fallback.Dispose();
			run.Pipeline.Dispose();
			run.Pool.AssertClean();
		});
	}

	[Fact]
	public void RethrowsWhenTheWorkbookIsDisposed()
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(22, 400);
			using var run = new SegmentRun(data, start, start + 100, 2, 512);
			run.Inner.Hold();
			run.Begin();
			Assert.True(run.Inner.WaitForHeldRead(SegmentRun.Bound));
			run.Archive.Dispose();
			run.Inner.Open();
			var last = run.StitchToEnd()!;
			Assert.True(last.Final);
			Assert.Equal(run.Inner.Delivered, last.Offset + last.Length);
			run.StopWithin();
			using var fallback = run.Pipeline.FallbackFrom(last);
			var bytes = ReadUntilError(fallback, out var error);
			Assert.Equal(data.AsSpan((int)last.Offset, last.Length).ToArray(), bytes);
			Assert.IsType<ObjectDisposedException>(error);
			fallback.Dispose();
			run.Finish();
		});
	}

	[Fact]
	public void HalvesTheCutTargetWhileBlocksAreLarge()
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(31, 14_000);
			using var run = new SegmentRun(data, start, start, 1, 64 * 1024);
			var large = 1;
			var endMarker = run.Scanners[0].EndMarker;
			run.Scanners[0].Script = (call, block) =>
			{
				if (Volatile.Read(ref large) == 1)
				{
					block.GetFieldSpan(SegmentSizes.LargeBlock);
					block.CommitField(SegmentSizes.LargeBlock, SheetCellKind.Text);
					block.EndRecord(0);
				}

				return StubScanner.Default(call, endMarker);
			};

			run.Begin();
			var lengths = new List<int>();
			while (run.Next() is { } slot)
			{
				lengths.Add(slot.Length);
				if (lengths.Count == 9)
				{
					Volatile.Write(ref large, 0);
				}

				run.Stitch(slot);
			}

			// A segment's target is set by the scans of the segments a ring before it at least: with one scan thread, segment n
			// starts after segment n - 2's scan. So the target halves to its minimum by segment 3, and doubles back by 13.
			Assert.True(lengths[0] >= 64 * 1024);
			Assert.All(lengths.Skip(3).Take(6), l => Assert.InRange(l, SegmentSizes.MinimumCut, SegmentSizes.MinimumCut + 1000));
			var after = lengths.Skip(14).Take(lengths.Count - 15).ToList();
			Assert.True(after.Count >= 2);
			Assert.All(after, l => Assert.True(l >= 64 * 1024));
			run.StopWithin();
			run.Finish();
		});
	}

	[Fact]
	public void SurvivesStartsAndStopsAtRandom()
	{
		SegmentRun.Within(TimeSpan.FromMinutes(5), () =>
		{
			var sheets = Enumerable.Range(0, 6).Select(i => Sheet(300 + i, 150 + (i * 150))).ToArray();
			var watch = Stopwatch.StartNew();
			// At least 20 iterations whatever the machine's load, and up to 400 while 6 s last: Within's 5 minutes catch a hang.
			for (var iterations = 0; iterations < 20 || (iterations < 400 && watch.Elapsed < TimeSpan.FromSeconds(6)); iterations++)
			{
				var random = new Random(iterations);
				var (data, start) = sheets[random.Next(sheets.Length)];
				int[] sizes = [1, 37, 512, 4096];
				var windowEnd = Math.Min(data.Length, start + random.Next(0, 2000));
				using var run = new SegmentRun(data, start, windowEnd, random.Next(1, 5), sizes[random.Next(sizes.Length)], segmentCap: random.Next(2) == 0 ? 2048 : SegmentSizes.MaxSegment, seed: iterations);
				run.Inner.Chunk = random.Next(1, 4000);
				run.Inner.Chunks = new Random(iterations);
				foreach (var scanner in run.Scanners)
				{
					scanner.Delays = random.Next(2) == 0;
				}

				if (random.Next(3) == 0)
				{
					run.Inner.FailAt = random.Next(windowEnd, data.Length + 1);
				}

				var held = random.Next(4) == 0;
				if (held)
				{
					run.Inner.Hold();
				}

				run.Begin();
				Thread? opener = null;
				if (held)
				{
					// A thread of its own, not the pool's, so that other tests' work items can't hold the opening up for seconds.
					var delay = random.Next(0, 3);
					opener = new Thread(() =>
					{
						Thread.Sleep(delay);
						run.Inner.Open();
					})
					{
						IsBackground = true,
					};
					opener.Start();
				}

				SegmentSlot? kept = null;
				var take = random.Next(0, 12);
				for (var i = 0; i < take; i++)
				{
					var slot = run.Next();
					if (slot is null)
					{
						break;
					}

					if (slot.Result.End != SegmentEnd.Done || random.Next(6) == 0)
					{
						kept = slot;
						break;
					}

					run.Stitch(slot);
				}

				SegmentStream? fallback = null;
				switch (random.Next(4))
				{
					case 0:
						run.StopWithin();
						fallback = kept is not null && random.Next(2) == 0 ? run.Pipeline.FallbackFrom(kept) : run.Pipeline.FallbackAfterDelivered();
						break;
					case 1:
						run.StopWithin();
						run.Pipeline.ChargeUnread();
						break;
					case 2:
						run.StopWithin();
						break;
					default:
						run.Pipeline.Dispose();
						break;
				}

				run.Seal();
				run.AssertQuiet();
				run.Inner.Sealed = false;
				if (fallback is not null)
				{
					var bytes = ReadUntilError(fallback, out var error, random.Next(2) == 0 ? random.Next(0, data.Length) : int.MaxValue, random.Next(1, 70_000));
					Assert.Equal(data.AsSpan((int)fallback.Offset, bytes.Length).ToArray(), bytes);
					if (error is not null)
					{
						Assert.Same(run.Inner.Failure, error);
						Assert.Equal(run.Inner.FailAt, fallback.Offset + bytes.Length);
					}

					fallback.Dispose();
				}

				opener?.Join();
				run.Finish();
			}
		});
	}

	[Fact]
	public void ChecksItsArgumentsAndOrder()
	{
		SegmentRun.Within(() =>
		{
			var (data, start) = Sheet(40, 100);
			using var run = new SegmentRun(data, start, start, 1, 64);
			var rowStart = Encoding.ASCII.GetBytes("<row");
			ISegmentScanner[] scanners = [new StubScanner(0)];
			Assert.Throws<ArgumentNullException>(() => new SegmentPipeline(null!, scanners, rowStart, 1, 1, run.Pool));
			Assert.Throws<ArgumentNullException>(() => new SegmentPipeline(run.Part, null!, rowStart, 1, 1, run.Pool));
			Assert.Throws<ArgumentNullException>(() => new SegmentPipeline(run.Part, scanners, null!, 1, 1, run.Pool));
			Assert.Throws<ArgumentNullException>(() => new SegmentPipeline(run.Part, scanners, rowStart, 1, 1, null!));
			Assert.Throws<ArgumentException>(() => new SegmentPipeline(run.Part, [], rowStart, 1, 1, run.Pool));
			Assert.Throws<ArgumentException>(() => new SegmentPipeline(run.Part, [null!], rowStart, 1, 1, run.Pool));
			Assert.Throws<ArgumentException>(() => new SegmentPipeline(run.Part, scanners, Encoding.ASCII.GetBytes("row"), 1, 1, run.Pool));
			Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentPipeline(run.Part, scanners, rowStart, 0, 1, run.Pool));
			Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentPipeline(run.Part, scanners, rowStart, 1, 0, run.Pool));

			// Not started: nothing to take, nothing to fall back from until stopped.
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.Next());
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.FallbackAfterDelivered());
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.ChargeUnread());
			Assert.Throws<ArgumentOutOfRangeException>(() => run.Pipeline.Start([], -1));
			run.Begin();
			Assert.Throws<InvalidOperationException>(run.Begin);

			// Released in order, and only what Next gave.
			var first = run.Next()!;
			var second = run.Next()!;
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.Release(second));
			Assert.Throws<ArgumentNullException>(() => run.Pipeline.Release(null!));
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.FallbackFrom(first));
			run.Stitch(first);
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.Release(first));

			// After the stop: a fallback from the oldest held, once.
			run.StopWithin();
			run.StopWithin();
			Assert.Null(run.Pipeline.Next());
			Assert.Throws<InvalidOperationException>(() => run.Pipeline.FallbackFrom(first));
			using (var fallback = run.Pipeline.FallbackFrom(second))
			{
				Assert.Throws<InvalidOperationException>(() => run.Pipeline.FallbackFrom(second));
				Assert.Throws<InvalidOperationException>(() => run.Pipeline.FallbackAfterDelivered());
				Assert.Throws<InvalidOperationException>(() => run.Pipeline.Release(second));
				Assert.Equal(data.AsSpan((int)second.Offset).ToArray(), SegmentRun.ReadAll(fallback));
			}

			run.Pipeline.ChargeUnread();
			Assert.Throws<InvalidOperationException>(run.Begin);
			run.Finish();
			run.Pipeline.Dispose();
			Assert.Throws<ObjectDisposedException>(run.Begin);

			// Stopped before it started: it never starts.
			using var stopped = new SegmentRun(data, start, start, 1, 64);
			stopped.Pipeline.Stop();
			Assert.Throws<InvalidOperationException>(stopped.Begin);
			stopped.Finish();
		});
	}

	/// <summary>Stops the pipeline at a stage, and checks that every thread was joined, the fallback's bytes, the pool and the charges.</summary>
	/// <param name="stage">Where the pipeline is when it is stopped.</param>
	/// <param name="seed">The seed of the part and the stubs.</param>
	private static void StopAt(Stage stage, int seed)
	{
		var (data, start) = stage == Stage.AfterAnUnsplitSegment ? Sheet(seed, 300, longRow: 150, longLength: 3000) : Sheet(seed, 300);
		using var run = new SegmentRun(data, start, start + 200, 2, 128, segmentCap: 1024, seed: seed);
		var failAt = data.Length / 2;
		if (stage == Stage.AfterAReadFailed)
		{
			run.Inner.FailAt = failAt;
		}

		SegmentStream? fallback = null;
		switch (stage)
		{
			case Stage.NotStarted:
				run.StopWithin();
				Assert.Equal(0, run.Pool.Rents);
				break;
			case Stage.JustStarted:
				run.Begin();
				run.StopWithin();
				fallback = run.Pipeline.FallbackAfterDelivered();
				break;
			case Stage.InARead:
				run.Inner.Hold();
				run.Begin();
				Assert.True(run.Inner.WaitForHeldRead(SegmentRun.Bound));
				StopWhileHeld(run, run.Inner.Open);
				fallback = run.Pipeline.FallbackAfterDelivered();
				break;
			case Stage.WaitingForASlot:
				run.Begin();
				WaitUntilIdle(run.Inner);
				Assert.True(run.Inner.Delivered < data.Length);
				run.StopWithin();
				fallback = run.Pipeline.FallbackAfterDelivered();
				break;
			case Stage.InAScan:
				var gate = new ManualResetEventSlim(false);
				foreach (var scanner in run.Scanners)
				{
					scanner.Gate = gate;
				}

				run.Begin();
				Assert.All(run.Scanners, s => Assert.True(s.Entered.Wait(SegmentRun.Bound)));
				StopWhileHeld(run, gate.Set);
				fallback = run.Pipeline.FallbackAfterDelivered();
				break;
			case Stage.HoldingASegment:
				run.Begin();
				run.Stitch(run.Next()!);
				var held = run.Next()!;
				run.StopWithin();
				fallback = run.Pipeline.FallbackFrom(held);
				break;
			case Stage.AfterTheLast:
				run.Begin();
				run.StitchAll();
				run.StopWithin();
				Assert.Null(run.Pipeline.FallbackAfterDelivered());
				break;
			case Stage.AfterAnUnsplitSegment:
				run.Begin();
				var unsplit = run.StitchToEnd()!;
				Assert.Equal(SegmentMark.Unsplit, unsplit.Result.Mark);
				run.StopWithin();
				fallback = run.Pipeline.FallbackFrom(unsplit);
				break;
			case Stage.AfterAReadFailed:
				run.Begin();
				var last = run.StitchToEnd()!;
				Assert.True(last.Final);
				run.StopWithin();
				fallback = run.Pipeline.FallbackFrom(last);
				break;
		}

		// No thread reads or scans after the stop.
		run.Seal();
		run.AssertQuiet();
		run.Inner.Sealed = false;
		if (fallback is not null)
		{
			// The serial read goes on with the part's bytes from the stream's offset, to the end or to the failed read.
			var end = stage == Stage.AfterAReadFailed ? failAt : data.Length;
			var bytes = ReadUntilError(fallback, out var error);
			Assert.Equal(data.AsSpan((int)fallback.Offset, end - (int)fallback.Offset).ToArray(), bytes);
			if (stage == Stage.AfterAReadFailed)
			{
				Assert.Same(run.Inner.Failure, error);
			}
			else
			{
				Assert.Null(error);
			}

			fallback.Dispose();
		}

		run.Finish();
	}

	/// <summary>Stops the pipeline while a read or scan is held: the stop waits for it, and returns once it is let go.</summary>
	/// <param name="run">The run.</param>
	/// <param name="open">Lets the held read or scan go on.</param>
	private static void StopWhileHeld(SegmentRun run, Action open)
	{
		var stop = Task.Factory.StartNew(run.Pipeline.Stop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		var early = stop.Wait(150);
		open();
		Assert.True(stop.Wait(SegmentRun.Bound), "Stop didn't return once the held read or scan was let go.");
		Assert.False(early, "Stop returned while a thread was held in a read or a scan: it didn't join it.");
	}

	/// <summary>Waits until the stream has had reads, and then none for a while: the inflating thread waits for a slot.</summary>
	/// <param name="stream">The stream.</param>
	private static void WaitUntilIdle(GatedStream stream)
	{
		var watch = Stopwatch.StartNew();
		var reads = -1;
		var quietSince = watch.Elapsed;
		while (watch.Elapsed < SegmentRun.Bound)
		{
			var now = stream.Reads;
			if (now != reads)
			{
				reads = now;
				quietSince = watch.Elapsed;
			}
			else if (reads > 0 && watch.Elapsed - quietSince > TimeSpan.FromMilliseconds(250))
			{
				return;
			}

			Thread.Sleep(10);
		}

		Assert.Fail("The stream was never idle.");
	}

	/// <summary>A generated part, and the offset of its fourth row's start: where the serial read starts the segments.</summary>
	/// <param name="seed">The seed.</param>
	/// <param name="rows">The number of rows.</param>
	/// <param name="longRow">The row given a long cell, or 0.</param>
	/// <param name="longLength">Its length.</param>
	/// <returns>The part and the offset.</returns>
	private static (byte[] Data, int Start) Sheet(int seed, int rows, int longRow = 0, int longLength = 0)
	{
		var data = PipelineSheets.Part(new Random(seed), rows, longRow: longRow, longLength: longLength);

		return (data, PipelineSheets.RowStarts(data, Encoding.ASCII.GetBytes("<row"))[3]);
	}

	/// <summary>Reads a stream to its end or its first exception, as the serial window would.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="error">The exception, or <see langword="null"/>.</param>
	/// <param name="limit">The most bytes to read; the read stops once it has them.</param>
	/// <param name="chunk">The most bytes a read asks for.</param>
	/// <returns>The bytes read before the end or the exception.</returns>
	private static byte[] ReadUntilError(Stream stream, out Exception? error, int limit = int.MaxValue, int chunk = XmlWindow.DefaultSize)
	{
		var output = new MemoryStream();
		var buffer = new byte[chunk];
		error = null;
		try
		{
			int count;
			while (output.Length < limit && (count = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write(buffer, 0, count);
			}
		}
		catch (Exception e)
		{
			error = e;
		}

		return output.ToArray();
	}

	/// <summary>Reads at least some bytes of a stream, in random chunks, or to its end.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="length">The bytes wanted.</param>
	/// <param name="random">The randomness of the chunks.</param>
	/// <returns>The bytes read.</returns>
	private static byte[] ReadSome(Stream stream, int length, Random random)
	{
		var output = new MemoryStream();
		var buffer = new byte[70_000];
		while (output.Length < length)
		{
			var count = stream.Read(buffer, 0, random.Next(1, buffer.Length));
			if (count == 0)
			{
				break;
			}

			output.Write(buffer, 0, count);
		}

		return output.ToArray();
	}
}
