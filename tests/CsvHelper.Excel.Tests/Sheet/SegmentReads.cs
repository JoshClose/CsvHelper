using System.Text;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Reads sheets through segments on the test's thread, as the record source does under <c>ParallelEnumerate</c>, with no
/// pipeline: serially until segments can begin (<see cref="SheetScanner.CanStartSegments"/>, past the part's first bytes),
/// then the rest of the part cut at row starts (<see cref="Cut"/>), every segment scanned by a worker, the segments joined by
/// the stitcher, and at a fallback the sheet's scanner reading on from the segment's start over the part's bytes.
/// </summary>
internal static class SegmentReads
{
	/// <summary>How far past the workbook's total a segment mode's budget goes: far past any test's sheet.</summary>
	public const long Allowance = 1L << 40;

	/// <summary>A workbook whose package lends a read its guard, so that a scanner has a budget (<see cref="LimitedReadStream"/>).</summary>
	private static readonly TestPackage BudgetPackage = TestPackage.Workbook("Data");

	/// <summary>Opens a package whose guard a read can charge (<see cref="Budget"/>).</summary>
	/// <returns>The package, which the caller disposes.</returns>
	public static PackageArchive OpenBudget() => BudgetPackage.OpenAsync(false).GetAwaiter().GetResult();

	/// <summary>Makes the part's stream that counts its reads, and a scanner's charges, toward <paramref name="archive"/>'s guard.</summary>
	/// <param name="archive">The package whose guard counts.</param>
	/// <param name="allowance">How far past the guard's total now the limit is.</param>
	/// <returns>A function from the part's stream to the counting one.</returns>
	public static Func<Stream, LimitedReadStream> Budget(PackageArchive archive, long allowance = Allowance)
	{
		var limit = archive.Guard.Total + allowance;

		return stream => new LimitedReadStream(stream, archive, Sheets.PartName, long.MaxValue, limit);
	}

	/// <summary>
	/// Reads a sheet's records in a segment mode (<see cref="SheetMode.Segments"/>): whole reads into the mode's window, with
	/// a budget far past the sheet, segments cut at the mode's target, two workers taking them in turn, and fills that take
	/// the mode's records, of which a block is appended whole.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it: a segment mode.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <param name="reading">The styles, date system, error policy and formulas, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>The records, and how the segments went.</returns>
	public static SegmentRead Read(string xml, SheetMode mode, bool skipEmptyRows, SharedStrings? strings, string sheetName, long maxSize, CellReading? reading)
	{
		using var archive = OpenBudget();
		using var sheet = Sheets.Open(xml, mode.Scan, skipEmptyRows, strings, sheetName, maxSize, reading, Budget(archive));
		var run = new SegmentRun(sheet, Encoding.UTF8.GetBytes(xml), _ => mode.SegmentTarget);

		return run.ReadAll(mode.RecordsPerFill);
	}

	/// <summary>
	/// Cuts a part's bytes from <paramref name="from"/> into segments, as the pipeline's splitter does: each ends at the first
	/// row start (<paramref name="rowStart"/>, then XML white space, <c>&gt;</c> or <c>/</c>) at or after its target, a
	/// number of bytes from its start; the last holds the rest of the part.
	/// </summary>
	/// <param name="part">The part's bytes.</param>
	/// <param name="from">Where the first segment starts.</param>
	/// <param name="rowStart">The rows' start: <c>&lt;</c>, <c>sheetData</c>'s prefix and <c>row</c>.</param>
	/// <param name="target">The target of each segment, by its index: 1 or more.</param>
	/// <returns>Each segment's start and end.</returns>
	public static List<(int Start, int End)> Cut(byte[] part, int from, byte[] rowStart, Func<int, int> target)
	{
		var segments = new List<(int Start, int End)>();
		var start = from;
		while (true)
		{
			var cut = NextRowStart(part, start + Math.Max(target(segments.Count), 1), rowStart);
			if (cut < 0)
			{
				segments.Add((start, part.Length));

				return segments;
			}

			segments.Add((start, cut));
			start = cut;
		}
	}

	/// <summary>The first row start at or after <paramref name="from"/>: the bytes of <paramref name="rowStart"/>, then XML white space, <c>&gt;</c> or <c>/</c>.</summary>
	/// <param name="part">The part's bytes.</param>
	/// <param name="from">Where to look from.</param>
	/// <param name="rowStart">The rows' start.</param>
	/// <returns>Its offset; -1 when there is none.</returns>
	public static int NextRowStart(byte[] part, int from, byte[] rowStart)
	{
		for (var at = from; at + rowStart.Length < part.Length; at++)
		{
			if (part.AsSpan(at).StartsWith(rowStart) && part[at + rowStart.Length] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/')
			{
				return at;
			}
		}

		return -1;
	}
}

/// <summary>What a read through segments gave: the records, and how its segments went.</summary>
/// <param name="records">The records.</param>
/// <param name="run">The read.</param>
internal sealed class SegmentRead(List<CapturedRecord> records, SegmentRun run)
{
	/// <summary>The records.</summary>
	public List<CapturedRecord> Records => records;

	/// <summary>The read: its segments, results and fallback.</summary>
	public SegmentRun Run => run;
}

/// <summary>One segment of a read: its bytes' place in the part, its scan, and what joining it did.</summary>
/// <param name="start">The offset in the part of its first byte.</param>
/// <param name="end">The offset in the part after its last byte.</param>
/// <param name="block">Its records.</param>
/// <param name="result">Its scan.</param>
/// <param name="uncounted">Its bytes that the window's stream didn't count before the segments began.</param>
internal sealed class ReadSegment(int start, int end, SegmentBlock block, SegmentResult result, long uncounted)
{
	/// <summary>The offset in the part of its first byte.</summary>
	public int Start => start;

	/// <summary>The offset in the part after its last byte.</summary>
	public int End => end;

	/// <summary>Its records.</summary>
	public SegmentBlock Block => block;

	/// <summary>Its scan.</summary>
	public SegmentResult Result => result;

	/// <summary>Its bytes that no stream counted.</summary>
	public long Uncounted => uncounted;

	/// <summary>What each call that joined it, or fell back at it, did.</summary>
	public List<Stitch> Outcomes { get; } = [];

	/// <inheritdoc/>
	public override string ToString() =>
		$"[{start}, {end}): {result.End}" + (result.Mark == SegmentMark.None ? "" : $" ({result.Mark})") + $", rows {result.FirstRow} to {result.LastRow}, " +
		$"{block.RecordCount} records, extras {result.Extras}" + (Outcomes.Count == 0 ? "" : $", {string.Join(" ", Outcomes)}");
}

/// <summary>
/// A read through segments, as the record source runs it, on the test's thread: fills serially until segments can begin,
/// then every segment scanned at once by workers taken in turn (as the pipeline's threads scan ahead of the stitcher), its
/// buffer longer than its bytes and filled past them with markup that a scan reading too far would trip on; then fills that
/// join segments, and at a fallback the sheet's scanner reading on over the part's bytes from the segment's start, through a
/// stream that charges what no stream counted before.
/// </summary>
internal sealed class SegmentRun
{
	// What a segment's buffer holds past its bytes: markup that the scan must not see.
	private static readonly byte[] Past = Encoding.UTF8.GetBytes("<!--<?</sheetData></row><row r=\"1\">");

	private readonly SheetScanner sheet;
	private readonly byte[] part;
	private readonly Func<int, int> target;
	private readonly int workers;
	private readonly long blockCap;
	private readonly long minimum;
	private readonly long? extrasLimit;
	private readonly List<ReadSegment> segments = [];
	private SegmentStitcher? stitcher;
	private int next;
	private Phase phase;
	// The part's bytes the window's stream counted before the segments began.
	private long countedEnd;

	/// <summary>Creates the read.</summary>
	/// <param name="sheet">The sheet's scanner, at the part's start, with a budget for segments to begin.</param>
	/// <param name="part">The part's bytes.</param>
	/// <param name="target">The cut target of each segment, by its index.</param>
	/// <param name="workers">The number of workers, which take the segments in turn.</param>
	/// <param name="blockCap">Each block's limit.</param>
	/// <param name="minimum">How far into the part the serial read goes before segments can begin.</param>
	/// <param name="extrasLimit">The workers' limit of extras; <see langword="null"/> for what the budget has left when they begin.</param>
	public SegmentRun(SheetScanner sheet, byte[] part, Func<int, int> target, int workers = 2, long blockCap = SegmentSizes.MaxBlock, long minimum = 64, long? extrasLimit = null)
	{
		this.sheet = sheet;
		this.part = part;
		this.target = target;
		this.workers = workers;
		this.blockCap = blockCap;
		this.minimum = minimum;
		this.extrasLimit = extrasLimit;
	}

	/// <summary>Where the read is.</summary>
	private enum Phase
	{
		/// <summary>Serial fills, until segments can begin.</summary>
		Serial,

		/// <summary>Joining segments.</summary>
		Segments,

		/// <summary>After a fallback: serial fills from a segment's start.</summary>
		FellBack,

		/// <summary>The rows ended in a segment.</summary>
		Ended,
	}

	/// <summary>Whether segments began.</summary>
	public bool Started => phase != Phase.Serial;

	/// <summary>The offset in the part where segments began; -1 before.</summary>
	public long StartOffset { get; private set; } = -1;

	/// <summary>The segments, once they began.</summary>
	public IReadOnlyList<ReadSegment> Segments => segments;

	/// <summary>The number of segments joined.</summary>
	public int Stitched { get; private set; }

	/// <summary>The segment the read fell back at, or <see langword="null"/>.</summary>
	public ReadSegment? FellBackAt { get; private set; }

	/// <summary>The part's bytes the stitcher charged, and those the fallback's stream charged: with the window's stream's, the XML's bytes charged.</summary>
	public long BytesCharged { get; private set; }

	/// <summary>The cells read by the tokenizer's path: the sheet's scanner's, and the joined segments'.</summary>
	public long FallbackCells => sheet.FallbackCells + segments.Take(Stitched).Sum(s => s.Result.FallbackCells);

	/// <summary>
	/// Fills until the sheet has no more records, checking each fill (<see cref="Captured.EndFill"/>). An error is thrown as
	/// the fill threw it.
	/// </summary>
	/// <param name="recordsPerFill">The records a fill takes.</param>
	/// <returns>The records, and the read.</returns>
	public SegmentRead ReadAll(int recordsPerFill)
	{
		var captured = new Captured(recordsPerFill);
		ReadInto(captured);

		return new SegmentRead(captured.Records, this);
	}

	/// <summary>
	/// Fills <paramref name="captured"/> until the sheet has no more records, checking each fill (<see cref="Captured.EndFill"/>).
	/// An error is thrown as the fill threw it, with the records before it captured.
	/// </summary>
	/// <param name="captured">Takes the records.</param>
	public void ReadInto(Captured captured)
	{
		var fill = new CapturingTarget(captured);
		while (true)
		{
			captured.StartFill();
			var more = Fill(fill);
			captured.EndFill(more);
			if (!more)
			{
				return;
			}
		}
	}

	/// <summary>Writes the next records, as the record source's fill does.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="fill">Takes the records.</param>
	/// <returns>Whether more records are to come.</returns>
	public bool Fill<TTarget>(TTarget fill)
		where TTarget : struct, IBlockTarget
	{
		switch (phase)
		{
			case Phase.Serial:
			{
				var more = sheet.Fill(fill);
				if (more && sheet.CanStartSegments && sheet.Position >= minimum)
				{
					Start();
				}

				return more;
			}
			case Phase.FellBack:
				return sheet.Fill(fill);
			case Phase.Ended:
				Assert.Fail("A fill after the rows ended.");

				return false;
		}

		while (true)
		{
			var segment = segments[next];
			var outcome = stitcher!.Stitch(segment.Result, segment.Uncounted, segment.Block, fill);
			segment.Outcomes.Add(outcome);
			switch (outcome)
			{
				case Stitch.Appended:
					Stitched++;
					BytesCharged += segment.Uncounted;
					next++;
					Assert.True(next < segments.Count, "The part's last segment was joined, and the rows didn't end.");
					if (fill.IsFull)
					{
						return true;
					}

					break;
				case Stitch.Full:
					return true;
				case Stitch.Ended:
					Stitched++;
					BytesCharged += segment.Uncounted;
					phase = Phase.Ended;

					return false;
				default:
					FallBack(segment);

					return sheet.Fill(fill);
			}
		}
	}

	/// <summary>Begins the segments where the sheet's scanner is: cuts the rest of the part, and scans every segment.</summary>
	private void Start()
	{
		var position = (int)sheet.Position;
		var unread = sheet.Unread;
		Assert.True(part.AsSpan(position, unread.Length).SequenceEqual(unread), "The window's unread bytes are the part's from the scanner's position.");
		var counted = position + unread.Length;
		var budget = sheet.Budget;
		var limit = extrasLimit ?? budget?.Remaining ?? long.MaxValue;
		var scanners = Enumerable.Range(0, workers).Select(_ => sheet.CreateSegmentScanner(limit)).ToArray();
		var cuts = SegmentReads.Cut(part, position, sheet.RowStart!, target);
		for (var i = 0; i < cuts.Count; i++)
		{
			var (start, end) = cuts[i];
			var length = end - start;
			var buffer = new byte[length + Past.Length];
			part.AsSpan(start, length).CopyTo(buffer);
			Past.CopyTo(buffer, length);
			var block = new SegmentBlock(blockCap);
			var result = scanners[i % scanners.Length].Scan(buffer, length, start, i == cuts.Count - 1, block);
			Assert.True(buffer.AsSpan(0, length).SequenceEqual(part.AsSpan(start, length)), "A scan wrote its segment's bytes.");
			segments.Add(new ReadSegment(start, end, block, result, Math.Max(0, end - Math.Max(start, counted))));
		}

		stitcher = new SegmentStitcher(sheet.LastRow, sheet.KeepsMissingRows, sheet.Layout, budget);
		StartOffset = position;
		phase = Phase.Segments;
		countedEnd = counted;
	}

	/// <summary>Has the sheet's scanner read on from a segment's start, over the part's bytes, charging those no stream counted.</summary>
	/// <param name="segment">The segment.</param>
	private void FallBack(ReadSegment segment)
	{
		var stream = new ChargingStream(part, segment.Start, countedEnd, sheet.Budget, n => BytesCharged += n);
		sheet.ResumeAt(stream, segment.Start, stitcher!.LastRow, stitcher.NextMissingRow);
		FellBackAt = segment;
		phase = Phase.FellBack;
	}

	/// <summary>
	/// The part's bytes from an offset on, for a fallback, as the pipeline's stream gives them: the bytes at or past where the
	/// window's stream had read to are charged to the budget as they are read, as a part's stream charges them.
	/// </summary>
	/// <param name="part">The part's bytes.</param>
	/// <param name="from">Where the stream starts.</param>
	/// <param name="counted">Where the bytes no stream has counted start.</param>
	/// <param name="budget">The budget, or <see langword="null"/> to charge nothing.</param>
	/// <param name="charged">Told of each charge.</param>
	private sealed class ChargingStream(byte[] part, int from, long counted, LimitedReadStream? budget, Action<long> charged) : Stream
	{
		private int position = from;

		/// <inheritdoc/>
		public override bool CanRead => true;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => false;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int at, int count)
		{
			var read = Math.Min(count, part.Length - position);
			part.AsSpan(position, read).CopyTo(buffer.AsSpan(at));
			var start = position;
			position += read;
			var uncharged = position - Math.Max(start, counted);
			if (uncharged > 0)
			{
				charged(uncharged);
				budget?.Charge(uncharged);
			}

			return read;
		}

		/// <inheritdoc/>
		public override void Flush()
		{
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
