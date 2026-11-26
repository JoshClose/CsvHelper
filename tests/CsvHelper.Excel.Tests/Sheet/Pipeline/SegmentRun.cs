using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// A <see cref="SegmentPipeline"/> over a generated part, with the stubs and checks the pipeline's tests share. The part's
/// stream is a real <see cref="LimitedReadStream"/> over a <see cref="GatedStream"/>, with a test package's archive for the
/// workbook's guard. The serial read before the pipeline is played by reading the part's first <see cref="WindowEnd"/>
/// bytes through it (counted), with the scanner's position at <see cref="Start"/>: the bytes between are carried.
/// </summary>
internal sealed class SegmentRun : IDisposable
{
	/// <summary>The part's name.</summary>
	public const string PartName = "xl/worksheets/sheet1.xml";

	/// <summary>The longest a test waits for the pipeline to stop, or for anything else, before it fails.</summary>
	public static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

	private readonly PackageArchive archive;
	private long nextSequence;
	private long nextOffset;

	/// <summary>Creates the run; <see cref="Begin"/> starts the pipeline.</summary>
	/// <param name="data">The part's bytes.</param>
	/// <param name="start">The scanner's position when the segments begin: the first carried byte.</param>
	/// <param name="windowEnd">How far the serial window has read: the end of the carried bytes.</param>
	/// <param name="scanThreads">The number of scan threads.</param>
	/// <param name="segmentSize">The cut target.</param>
	/// <param name="segmentCap">The most bytes a segment takes.</param>
	/// <param name="seed">The seed of the stubs' delays.</param>
	/// <param name="prefix">The prefix of the sheet's elements, or empty.</param>
	/// <param name="maxPartSize">The part's size limit.</param>
	/// <param name="maxUncompressedSize">The workbook's limit.</param>
	public SegmentRun(
		byte[] data, int start, int windowEnd, int scanThreads, int segmentSize, int segmentCap = SegmentSizes.MaxSegment, int seed = 1, string prefix = "",
		long maxPartSize = long.MaxValue, long maxUncompressedSize = 1L << 40)
	{
		Data = data;
		Start = start;
		WindowEnd = windowEnd;
		archive = SyncOrAsync.Result(PackageArchive.OpenAsync(TestPackage.Workbook("Data").ToStream(), false, Packages.Limits, false, CancellationToken.None));
		Inner = new GatedStream(data);
		Part = new LimitedReadStream(Inner, archive, PartName, maxPartSize, maxUncompressedSize);
		var window = new byte[windowEnd];
		var read = 0;
		while (read < windowEnd)
		{
			var count = Part.Read(window, read, windowEnd - read);
			Assert.True(count > 0);
			read += count;
		}

		var element = prefix.Length > 0 ? prefix + ":" : "";
		RowStart = Encoding.ASCII.GetBytes("<" + element + "row");
		Scanners = [.. Enumerable.Range(0, scanThreads).Select(i => new StubScanner((seed * 7919) + i) { EndMarker = Encoding.ASCII.GetBytes("</" + element + "sheetData>") })];
		Pool = new CheckingPool { InUse = Using };
		Pipeline = new SegmentPipeline(Part, [.. Scanners], RowStart, segmentSize, segmentCap, Pool);
		nextOffset = start;
	}

	/// <summary>The part's bytes.</summary>
	public byte[] Data { get; }

	/// <summary>The first carried byte's offset.</summary>
	public int Start { get; }

	/// <summary>The end of the carried bytes.</summary>
	public int WindowEnd { get; }

	/// <summary>The bytes a row's start tag begins with.</summary>
	public byte[] RowStart { get; }

	/// <summary>The part's inner stream.</summary>
	public GatedStream Inner { get; }

	/// <summary>The part's stream.</summary>
	public LimitedReadStream Part { get; }

	/// <summary>The stub scanners, one for each scan thread.</summary>
	public StubScanner[] Scanners { get; }

	/// <summary>The pool of the pipeline's buffers.</summary>
	public CheckingPool Pool { get; }

	/// <summary>The pipeline.</summary>
	public SegmentPipeline Pipeline { get; }

	/// <summary>The workbook's total so far.</summary>
	public long Charged => archive.Guard.Total;

	/// <summary>The archive, for a test that disposes the workbook.</summary>
	public PackageArchive Archive => archive;

	/// <summary>The segments taken so far, in order: their offsets and lengths.</summary>
	public List<(long Offset, int Length, SegmentResult Result, bool Final)> Taken { get; } = [];

	/// <summary>Runs a test's body on a thread of its own, and fails when it doesn't end in time: a stuck wait fails the test rather than hanging the run.</summary>
	/// <param name="body">The body.</param>
	public static void Within(Action body) => Within(Bound, body);

	/// <summary>Runs a test's body on a thread of its own, and fails when it doesn't end in time.</summary>
	/// <param name="limit">The time it has.</param>
	/// <param name="body">The body.</param>
	public static void Within(TimeSpan limit, Action body)
	{
		ExceptionDispatchInfo? failure = null;
		var thread = new Thread(() =>
		{
			try
			{
				body();
			}
			catch (Exception e)
			{
				failure = ExceptionDispatchInfo.Capture(e);
			}
		})
		{
			IsBackground = true,
			Name = "SegmentRun test",
		};
		thread.Start();
		if (!thread.Join(limit))
		{
			Assert.Fail($"The test didn't end within {limit}: a wait is stuck.");
		}

		failure?.Throw();
	}

	/// <summary>Reads a stream to its end in reads of a window's room, as the serial window reads.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="chunk">The most bytes a read asks for.</param>
	/// <returns>The bytes read.</returns>
	public static byte[] ReadAll(Stream stream, int chunk = XmlWindow.DefaultSize)
	{
		var output = new MemoryStream();
		var buffer = new byte[chunk];
		int count;
		while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			output.Write(buffer, 0, count);
		}

		return output.ToArray();
	}

	/// <summary>Starts the pipeline with the carried bytes.</summary>
	public void Begin() => Pipeline.Start(Data.AsSpan(Start, WindowEnd - Start), Start);

	/// <summary>Takes the next segment, and checks it: in order, after the one before, the part's bytes, its own block, and its uncounted bytes.</summary>
	/// <returns>The segment, or <see langword="null"/> after the last.</returns>
	public SegmentSlot? Next()
	{
		var slot = Pipeline.Next();
		if (slot is null)
		{
			return null;
		}

		Assert.Equal(nextSequence, slot.Sequence);
		Assert.Equal(nextOffset, slot.Offset);
		Assert.True(slot.Offset + slot.Length <= Data.Length);
		Assert.True(Data.AsSpan((int)slot.Offset, slot.Length).SequenceEqual(slot.Buffer.AsSpan(0, slot.Length)), $"Segment {slot.Sequence} doesn't hold the part's bytes.");
		var carried = Math.Max(0, Math.Min(slot.Offset + slot.Length, WindowEnd) - Math.Max(slot.Offset, Start));
		Assert.Equal(slot.Length - carried, slot.Uncounted);
		if (slot.Result.Mark == SegmentMark.Unsplit)
		{
			Assert.Equal(SegmentEnd.Marked, slot.Result.End);
			Assert.DoesNotContain(Scanners.SelectMany(s => s.Calls), c => c.Offset == slot.Offset);
		}
		else if (slot.Result.Error is not TimeoutException)
		{
			// The block is the segment's own scan: its one record names the segment's offset.
			Assert.True(slot.Block.RecordCount >= 1, $"Segment {slot.Sequence}'s block is empty.");
			var field = Encoding.ASCII.GetString(slot.Block.Bytes.Slice(0, slot.Block.Ends[0]).ToArray());
			Assert.Equal(slot.Offset.ToString(CultureInfo.InvariantCulture), field);
		}

		nextSequence++;
		nextOffset += slot.Length;
		Taken.Add((slot.Offset, slot.Length, slot.Result, slot.Final));

		return slot;
	}

	/// <summary>Stitches a segment as the stitcher does: charges its uncounted bytes, and releases it.</summary>
	/// <param name="slot">The segment.</param>
	public void Stitch(SegmentSlot slot)
	{
		Part.Charge(slot.Uncounted);
		Pipeline.Release(slot);
	}

	/// <summary>Takes and stitches segments until one ends the rows or is marked, which it returns unreleased, or until there are no more.</summary>
	/// <param name="limit">The most segments to stitch.</param>
	/// <returns>The segment that ended the rows or was marked; <see langword="null"/> when there are no more, or the limit was reached.</returns>
	public SegmentSlot? StitchToEnd(int limit = int.MaxValue)
	{
		for (var i = 0; i < limit; i++)
		{
			var slot = Next();
			if (slot is null || slot.Result.End != SegmentEnd.Done)
			{
				return slot;
			}

			Stitch(slot);
		}

		return null;
	}

	/// <summary>Takes and stitches every segment, whatever its result, until there are no more.</summary>
	/// <returns>The number of segments.</returns>
	public int StitchAll()
	{
		var count = 0;
		while (Next() is { } slot)
		{
			Stitch(slot);
			count++;
		}

		return count;
	}

	/// <summary>Stops the pipeline on another thread, and fails when it doesn't return in time (opening every gate first, so the threads can end).</summary>
	public void StopWithin()
	{
		var stop = Task.Factory.StartNew(Pipeline.Stop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		if (!stop.Wait(Bound))
		{
			Unblock();
			Assert.Fail("Stop didn't return: a thread is stuck.");
		}
	}

	/// <summary>Opens every gate: the stream's and the scanners'.</summary>
	public void Unblock()
	{
		Inner.Open();
		foreach (var scanner in Scanners)
		{
			scanner.Gate?.Set();
		}
	}

	/// <summary>After the stop: from here on, a read of the part or a scan fails the test.</summary>
	public void Seal()
	{
		Inner.Sealed = true;
		foreach (var scanner in Scanners)
		{
			scanner.Sealed = true;
		}
	}

	/// <summary>Fails if the part was read or a segment scanned after <see cref="Seal"/>: a thread outlived the stop.</summary>
	public void AssertQuiet()
	{
		Thread.Sleep(20);
		Assert.Equal(0, Inner.ReadsAfterSealed);
		Assert.All(Scanners, s => Assert.Equal(0, s.CallsAfterSealed));
		Assert.False(Inner.HeldTooLong);
	}

	/// <summary>Disposes the pipeline, then checks the pool, and that each byte inflated was charged once (the serial window's with them).</summary>
	public void Finish()
	{
		Pipeline.Dispose();
		Pool.AssertClean();
		Assert.Equal(Inner.Delivered, archive.Guard.Total);
	}

	/// <summary>Checks each cut against the splitter's rule, done naively: the first row start at or after the target, below the cap.</summary>
	/// <param name="target">The cut target, which the stubs' small blocks keep where it starts.</param>
	public void AssertCuts(int target)
	{
		var starts = new HashSet<long>(PipelineSheets.RowStarts(Data, RowStart).Select(s => (long)s));
		for (var i = 0; i + 1 < Taken.Count; i++)
		{
			var (offset, length, _, _) = Taken[i];
			Assert.Contains(offset + length, starts);
			Assert.True(length >= target, $"Segment {i} is cut before its target: {length} bytes.");
			Assert.DoesNotContain(starts, s => s >= offset + target && s < offset + length);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Unblock();
		Pipeline.Dispose();
		Part.Dispose();
		archive.Dispose();
	}

	/// <summary>Says what uses a buffer the pool is given back: a read of the part, or a scan.</summary>
	/// <param name="buffer">The buffer.</param>
	/// <returns>What uses it, or <see langword="null"/>.</returns>
	private string? Using(byte[] buffer)
	{
		var user = Inner.Reading(buffer);
		foreach (var scanner in Scanners)
		{
			user ??= scanner.Using(buffer);
		}

		return user;
	}
}

/// <summary>Generated parts of sheets for the pipeline's tests.</summary>
internal static class PipelineSheets
{
	/// <summary>
	/// A sheet's part: rows with and without <c>r</c>, empty and with cells, separated by nothing or white space, and among them
	/// names a splitter must not cut at (<c>rowX</c>, <c>rows</c>, other prefixes, and <c>&lt;row</c> escaped in text).
	/// </summary>
	/// <param name="random">The randomness.</param>
	/// <param name="rows">The number of rows.</param>
	/// <param name="prefix">The prefix of the elements, or empty.</param>
	/// <param name="longRow">The row given a cell of <paramref name="longLength"/> bytes, or 0 for none.</param>
	/// <param name="longLength">The length of that cell's value.</param>
	/// <returns>The part's bytes.</returns>
	public static byte[] Part(Random random, int rows, string prefix = "", int longRow = 0, int longLength = 0)
	{
		var p = prefix.Length > 0 ? prefix + ":" : "";
		var namespaceName = prefix.Length > 0 ? "xmlns:" + prefix : "xmlns";
		var text = new StringBuilder();
		text.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n<{p}worksheet {namespaceName}=\"{TestPackage.MainNamespace}\"><{p}dimension ref=\"A1\"/><{p}sheetData>");
		string[] separators = ["", "", "\r\n", " ", "\t", "\n  "];
		for (var row = 1; row <= rows; row++)
		{
			text.Append(separators[random.Next(separators.Length)]);
			var start = random.Next(5) switch
			{
				0 => $"<{p}row>",
				1 => $"<{p}row\tr=\"{row}\">",
				2 => $"<{p}row\r\n r=\"{row}\" spans=\"1:3\">",
				_ => $"<{p}row r=\"{row}\">",
			};
			if (row != longRow && random.Next(12) == 0)
			{
				text.Append(random.Next(2) == 0 ? $"<{p}row/>" : $"<{p}row r=\"{row}\"/>");

				continue;
			}

			text.Append(start);
			var cells = random.Next(4);
			for (var cell = 0; cell < cells; cell++)
			{
				switch (random.Next(8))
				{
					case 0:
						text.Append($"<{p}c t=\"inlineStr\"><{p}is><{p}t>&lt;{p}row r=\"9\"&gt;</{p}t></{p}is></{p}c>");
						break;
					case 1:
						text.Append($"<{p}rowX/><{p}rows a=\"1\"/><y:row xmlns:y=\"urn:y\"/>{(p.Length > 0 ? "<row/>" : "<x:row xmlns:x=\"urn:x\"/>")}");
						break;
					default:
						text.Append($"<{p}c r=\"{(char)('A' + cell)}{row}\"><{p}v>{random.Next(1_000_000)}</{p}v></{p}c>");
						break;
				}
			}

			if (row == longRow)
			{
				text.Append($"<{p}c t=\"inlineStr\"><{p}is><{p}t>{new string('L', longLength)}</{p}t></{p}is></{p}c>");
			}

			text.Append($"</{p}row>");
		}

		text.Append($"</{p}sheetData><{p}pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></{p}worksheet>");

		return Encoding.UTF8.GetBytes(text.ToString());
	}

	/// <summary>The offsets of the row starts of a part, found naively: the row start's bytes, then white space, <c>&gt;</c> or <c>/</c>.</summary>
	/// <param name="data">The part's bytes.</param>
	/// <param name="rowStart">The bytes a row's start tag begins with.</param>
	/// <returns>The offsets, in order.</returns>
	public static List<int> RowStarts(byte[] data, byte[] rowStart)
	{
		var starts = new List<int>();
		for (var i = 0; i + rowStart.Length < data.Length; i++)
		{
			if (data.AsSpan(i, rowStart.Length).SequenceEqual(rowStart) && data[i + rowStart.Length] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/')
			{
				starts.Add(i);
			}
		}

		return starts;
	}
}
