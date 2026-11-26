using System.Text;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// Tests of <see cref="RowSplitter"/>, alone and as the pipeline's inflating thread cuts with it: at every row start and only
/// there, from the cut target on, with the serial window's unread bytes carried into the first segments, and no cut past
/// the cap.
/// </summary>
public sealed class RowSplitterTests
{
	private static readonly RowSplitter Rows = new(Encoding.ASCII.GetBytes("<row"));

	[Theory]
	[InlineData("<row>", 0)]
	[InlineData("<row/>", 0)]
	[InlineData("<row r=\"1\">", 0)]
	[InlineData("<row\tr=\"1\">", 0)]
	[InlineData("<row\rr=\"1\">", 0)]
	[InlineData("<row\nr=\"1\">", 0)]
	[InlineData("<rowX/><row>", 7)]
	[InlineData("<rows a=\"1\"/><row>", 13)]
	[InlineData("</row><row>", 6)]
	[InlineData("<c><v>1</v></c></row>\r\n<row r=\"2\">", 23)]
	[InlineData("<row-a/><row:b/><row.c/><row_d/><row1/><ROW/>", -1)]
	[InlineData("<ro w><r ow><rowrow>", -1)]
	[InlineData("&lt;row r=\"1\"&gt;", -1)]
	public void CutsAtRowStartsOnly(string text, int expected)
	{
		Assert.Equal(expected, Rows.Find(Encoding.ASCII.GetBytes(text), 0, out _));
	}

	[Fact]
	public void NeedsTheByteThatEndsTheName()
	{
		// "<row" at the end could be "<rows": it waits for the next byte, and the search goes on from it.
		var bytes = Encoding.ASCII.GetBytes("<c/><row");
		Assert.Equal(-1, Rows.Find(bytes, 0, out var resume));
		Assert.Equal(4, resume);

		Assert.Equal(-1, Rows.Find(Encoding.ASCII.GetBytes("<c/><rows"), resume, out resume));
		Assert.Equal(5, resume);
		Assert.Equal(4, Rows.Find(Encoding.ASCII.GetBytes("<c/><row "), 4, out resume));
		Assert.Equal(4, resume);

		// Part of a row start at the end: the search goes on from where it could begin.
		Assert.Equal(-1, Rows.Find(Encoding.ASCII.GetBytes("<c/></c><ro"), 0, out resume));
		Assert.Equal(7, resume);
		Assert.Equal(-1, Rows.Find(Encoding.ASCII.GetBytes("<c/>"), 0, out resume));
		Assert.Equal(0, resume);
		Assert.Equal(-1, Rows.Find([], 0, out resume));
		Assert.Equal(0, resume);
	}

	[Fact]
	public void FindsNothingBeforeWhereItStarts()
	{
		var bytes = Encoding.ASCII.GetBytes("<row><row/></row>");
		Assert.Equal(0, Rows.Find(bytes, 0, out _));
		Assert.Equal(5, Rows.Find(bytes, 1, out _));
		Assert.Equal(5, Rows.Find(bytes, 5, out _));
		Assert.Equal(-1, Rows.Find(bytes, 6, out var resume));
		Assert.Equal(bytes.Length - 4, resume);
		Assert.Equal(-1, Rows.Find(bytes, bytes.Length + 3, out resume));
		Assert.Equal(bytes.Length + 3, resume);
	}

	[Fact]
	public void CutsOnlyAtTheSheetsPrefix()
	{
		var prefixed = new RowSplitter(Encoding.ASCII.GetBytes("<x:row"));
		var bytes = Encoding.ASCII.GetBytes("<row r=\"1\"/><y:row/><x:rows/><x:row r=\"2\">");
		Assert.Equal(29, prefixed.Find(bytes, 0, out _));
		Assert.Equal(0, Rows.Find(bytes, 0, out _));
		Assert.Equal(-1, Rows.Find(bytes, 1, out _));
	}

	[Fact]
	public void FindsWhatANaiveSearchFinds()
	{
		var random = new Random(8);
		var alphabet = Encoding.ASCII.GetBytes("<<<rrooww   >>//xX:\t\n");
		var rowStart = Encoding.ASCII.GetBytes("<row");
		for (var i = 0; i < 20_000; i++)
		{
			var bytes = new byte[random.Next(0, 40)];
			for (var j = 0; j < bytes.Length; j++)
			{
				bytes[j] = random.Next(3) == 0 ? rowStart[random.Next(rowStart.Length)] : alphabet[random.Next(alphabet.Length)];
			}

			var from = random.Next(0, bytes.Length + 2);
			var expected = PipelineSheets.RowStarts(bytes, rowStart).Where(s => s >= from).DefaultIfEmpty(-1).First();
			Assert.Equal(expected, Rows.Find(bytes, from, out var resume));

			// A search resumed with more bytes finds what a search of them all finds.
			if (expected < 0)
			{
				var longer = bytes.Concat(Encoding.ASCII.GetBytes(" <row>")).ToArray();
				var whole = PipelineSheets.RowStarts(longer, rowStart).Where(s => s >= from).First();
				Assert.Equal(whole, Rows.Find(longer, resume, out _));
			}
		}
	}

	[Fact]
	public void ChecksItsArguments()
	{
		Assert.Throws<ArgumentNullException>(() => new RowSplitter(null!));
		Assert.Throws<ArgumentException>(() => new RowSplitter([]));
		Assert.Throws<ArgumentException>(() => new RowSplitter(Encoding.ASCII.GetBytes("<")));
		Assert.Throws<ArgumentException>(() => new RowSplitter(Encoding.ASCII.GetBytes("row")));
		Assert.Throws<ArgumentOutOfRangeException>(() => Rows.Find([], -1, out _));
	}

	[Theory]
	[InlineData(1, "")]
	[InlineData(2, "")]
	[InlineData(7, "x")]
	[InlineData(64, "")]
	[InlineData(300, "x")]
	[InlineData(4096, "")]
	public void CutsEachSegmentAtTheFirstRowStartPastItsTarget(int segmentSize, string prefix)
	{
		SegmentRun.Within(() =>
		{
			for (var seed = 0; seed < 6; seed++)
			{
				var random = new Random((segmentSize * 31) + seed);
				var data = PipelineSheets.Part(random, 300, prefix);
				var rowStart = Encoding.ASCII.GetBytes(prefix.Length > 0 ? "<x:row" : "<row");
				var starts = PipelineSheets.RowStarts(data, rowStart);
				var start = starts[random.Next(starts.Count / 2)];
				using var run = new SegmentRun(data, start, Math.Min(data.Length, start + random.Next(0, 3000)), random.Next(1, 4), segmentSize, seed: seed, prefix: prefix);
				run.Inner.Chunk = 1 + random.Next(0, 3) switch
				{
					0 => 0,
					1 => random.Next(1, 64),
					_ => random.Next(1, 20_000),
				};
				run.Inner.Chunks = new Random(seed);
				run.Begin();
				run.StitchAll();

				// Every byte from the scanner's position, once, in order; then every cut by the rule.
				Assert.Equal(data.Length, run.Taken.Sum(t => t.Length) + start);
				Assert.True(run.Taken.Last().Final);
				run.AssertCuts(segmentSize);
				run.StopWithin();
				run.Finish();
			}
		});
	}

	[Fact]
	public void CutsOneRowASegmentAtOneByteTargets()
	{
		SegmentRun.Within(() =>
		{
			var data = PipelineSheets.Part(new Random(3), 200);
			var rowStart = Encoding.ASCII.GetBytes("<row");
			var starts = PipelineSheets.RowStarts(data, rowStart);
			using var run = new SegmentRun(data, starts[0], starts[0], 2, 1);
			run.Begin();
			run.StitchAll();

			// Each segment is one row, and what follows it up to the next: white space, or the sheet's end.
			Assert.Equal(starts.Count, run.Taken.Count);
			Assert.Equal(starts.Select(s => (long)s), run.Taken.Select(t => t.Offset));
			run.StopWithin();
			run.Finish();
		});
	}

	[Fact]
	public void CarriesTheWindowsUnreadBytesIntoTheFirstSegments()
	{
		SegmentRun.Within(() =>
		{
			var data = PipelineSheets.Part(new Random(4), 400);
			var starts = PipelineSheets.RowStarts(data, Encoding.ASCII.GetBytes("<row"));
			var start = starts[10] - 2;
			var windowEnd = start + 5000;
			using var run = new SegmentRun(data, start, windowEnd, 3, 64);
			run.Begin();
			run.StitchAll();

			// The carried bytes are the first segments' (SegmentRun.Next checks each one's uncounted bytes against them), and
			// they were counted once, by the serial window: the stitcher charged only the rest.
			Assert.Equal(start, run.Taken[0].Offset);
			Assert.True(run.Taken.TakeWhile(t => t.Offset + t.Length <= windowEnd).Count() > 10);
			run.StopWithin();
			run.Finish();
			Assert.Equal(data.Length, run.Charged);
		});
	}

	[Fact]
	public void CarriesBytesPastTheCap()
	{
		SegmentRun.Within(() =>
		{
			// A serial window that grew past the cap: its unread bytes are kept whole, and cut below the cap.
			var data = PipelineSheets.Part(new Random(5), 300);
			var starts = PipelineSheets.RowStarts(data, Encoding.ASCII.GetBytes("<row"));
			using var run = new SegmentRun(data, starts[0], starts[0] + 6000, 2, 100, segmentCap: 1000);
			run.Begin();
			run.StitchAll();

			Assert.All(run.Taken.Take(run.Taken.Count - 1), t => Assert.True(t.Length < 1000));
			Assert.Equal(data.Length, run.Taken.Sum(t => t.Length) + starts[0]);
			run.AssertCuts(100);
			run.StopWithin();
			run.Finish();
		});
	}

	[Theory]
	[InlineData(64, 1024)]
	[InlineData(1, 300)]
	[InlineData(900, 4096)]
	public void EndsTheCuttingAtARowPastTheCap(int segmentSize, int segmentCap)
	{
		SegmentRun.Within(() =>
		{
			var data = PipelineSheets.Part(new Random(6), 200, longRow: 120, longLength: 5000);
			var starts = PipelineSheets.RowStarts(data, Encoding.ASCII.GetBytes("<row"));
			using var run = new SegmentRun(data, starts[0], starts[0], 2, segmentSize, segmentCap: segmentCap);
			run.Begin();
			var unsplit = run.StitchToEnd();

			// The segment of the long row is published whole up to the cap, never scanned, and nothing comes after it: the
			// inflating thread read no further.
			Assert.NotNull(unsplit);
			Assert.Equal(SegmentMark.Unsplit, unsplit.Result.Mark);
			Assert.Equal(segmentCap, unsplit.Length);
			Assert.False(unsplit.Final);
			Assert.True(unsplit.Offset <= starts[119] && starts[119] < unsplit.Offset + segmentCap);
			Assert.Null(run.Pipeline.Next());
			Assert.True(run.Inner.Delivered <= unsplit.Offset + segmentCap);

			// The serial read goes on from its start, through the rest of the part.
			run.StopWithin();
			var delivered = run.Inner.Delivered;
			using (var fallback = run.Pipeline.FallbackFrom(unsplit))
			{
				Assert.Equal(unsplit.Offset, fallback.Offset);
				Assert.Equal(data.AsSpan((int)unsplit.Offset).ToArray(), SegmentRun.ReadAll(fallback));
			}

			Assert.True(run.Inner.Delivered > delivered);
			run.Finish();
		});
	}
}
