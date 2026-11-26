using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The stitcher (<see cref="SegmentStitcher"/>): each of its rules, over blocks and scan results the tests make; the
/// charges toward the workbook's limit and their order; a segment that would pass the limit falling back with nothing
/// charged; the rows missing before a segment across a full target; and the state a fallback resumes from.
/// </summary>
public class SegmentStitcherTests
{
	[Fact]
	public void FallsBackAtAMarkedSegmentWithNothingWrittenOrCharged()
	{
		var marks = new[]
		{
			SegmentMark.Markup, SegmentMark.Unfinished, SegmentMark.Error, SegmentMark.BlockCap, SegmentMark.ExtrasCap, SegmentMark.Anchor, SegmentMark.FloatingArea,
			SegmentMark.Unsplit,
		};
		foreach (var mark in marks)
		{
			using var budget = Budget.Open(1000);
			var stitcher = new SegmentStitcher(3, true, null, budget.Stream);
			var captured = Fill(int.MaxValue);
			var result = new SegmentResult { End = SegmentEnd.Marked, Mark = mark, FirstRow = 9, LastRow = 9, Extras = 5 };

			Assert.Equal(Stitch.Fallback, stitcher.Stitch(result, 100, Block((9, ["a"])), new CapturingTarget(captured)));
			Assert.Empty(captured.Records);
			Assert.Equal(0, budget.Charged);
			Assert.Equal((3L, 4L), (stitcher.LastRow, stitcher.NextMissingRow));
		}
	}

	[Fact]
	public void AppendsAnAnchoredSegmentAfterTheRowsMissingBeforeIt()
	{
		using var budget = Budget.Open(1000);
		var kept = new SegmentStitcher(3, true, null, budget.Stream);
		var captured = Fill(int.MaxValue);
		var result = Anchored(7, 8, extras: 2);

		Assert.Equal(Stitch.Appended, kept.Stitch(result, 50, Block((7, ["a"]), (8, ["b", "c"])), new CapturingTarget(captured)));
		Assert.Equal(["4: ()", "5: ()", "6: ()", "7: a", "8: b | c"], Describe(captured));
		Assert.Equal(3 + 50 + 2, budget.Charged);
		Assert.Equal((8L, 9L), (kept.LastRow, kept.NextMissingRow));

		// With missing rows not kept, none are written or charged.
		using var other = Budget.Open(1000);
		var skipped = new SegmentStitcher(3, false, null, other.Stream);
		var records = Fill(int.MaxValue);
		Assert.Equal(Stitch.Appended, skipped.Stitch(result, 50, Block((7, ["a"]), (8, ["b", "c"])), new CapturingTarget(records)));
		Assert.Equal(["7: a", "8: b | c"], Describe(records));
		Assert.Equal(50 + 2, other.Charged);
	}

	[Fact]
	public void FallsBackAtAnAnchoredFirstRowNotAfterTheLastRow()
	{
		var stitcher = new SegmentStitcher(5, true, null, null);
		foreach (var first in new long[] { 5, 4, 1 })
		{
			var captured = Fill(int.MaxValue);
			Assert.Equal(Stitch.Fallback, stitcher.Stitch(Anchored(first, 9), 0, Block((first, ["a"])), new CapturingTarget(captured)));
			Assert.Empty(captured.Records);
		}

		Assert.Equal(Stitch.Appended, stitcher.Stitch(Anchored(6, 6), 0, Block((6, ["a"])), new CapturingTarget(Fill(int.MaxValue))));
	}

	[Fact]
	public void WritesMissingRowsOnlyInTheArea()
	{
		var area = new SheetLayout(5, 10, 0, 3);

		// Rows above the area were skipped: missing rows start at its first.
		var stitcher = new SegmentStitcher(2, true, area, null);
		Assert.Equal(5, stitcher.NextMissingRow);
		var captured = Fill(int.MaxValue);
		Assert.Equal(Stitch.Appended, stitcher.Stitch(Anchored(3, 4), 0, Block(), new CapturingTarget(captured)));
		Assert.Empty(captured.Records);
		Assert.Equal((4L, 5L), (stitcher.LastRow, stitcher.NextMissingRow));
		Assert.Equal(Stitch.Appended, stitcher.Stitch(Anchored(7, 8), 0, Block((7, ["a"]), (8, ["b"])), new CapturingTarget(captured)));
		Assert.Equal(["5: ()", "6: ()", "7: a", "8: b"], Describe(captured));

		// None before a row below the area, which ends the rows, and moves nothing.
		var below = Fill(int.MaxValue);
		var ended = new SegmentResult { End = SegmentEnd.Ended, FirstRow = 12, LastRow = 0 };
		Assert.Equal(Stitch.Ended, stitcher.Stitch(ended, 0, Block(), new CapturingTarget(below)));
		Assert.Empty(below.Records);
		Assert.Equal((8L, 9L), (stitcher.LastRow, stitcher.NextMissingRow));
	}

	[Fact]
	public void RebasesAFloatingSegmentsRowsOnTheLastRow()
	{
		var stitcher = new SegmentStitcher(10, true, null, null);
		var captured = Fill(int.MaxValue);
		var floating = new SegmentResult { End = SegmentEnd.Done, FirstRow = 0, LastRow = 4 };

		// No missing rows: a floating segment's rows follow the last row.
		Assert.Equal(Stitch.Appended, stitcher.Stitch(floating, 0, Block((1, ["a"]), (2, ["b"]), (4, ["d"])), new CapturingTarget(captured)));
		Assert.Equal(["11: a", "12: b", "14: d"], Describe(captured));
		Assert.Equal((14L, 15L), (stitcher.LastRow, stitcher.NextMissingRow));

		// Up to the sheet's last row, and not past it.
		var last = new SegmentStitcher(CellReference.MaxRow - 2, false, null, null);
		var past = new SegmentResult { End = SegmentEnd.Done, FirstRow = 0, LastRow = 3 };
		Assert.Equal(Stitch.Fallback, last.Stitch(past, 0, Block((3, ["c"])), new CapturingTarget(Fill(int.MaxValue))));
		var to = new SegmentResult { End = SegmentEnd.Done, FirstRow = 0, LastRow = 2 };
		Assert.Equal(Stitch.Appended, last.Stitch(to, 0, Block((2, ["b"])), new CapturingTarget(Fill(int.MaxValue))));
		Assert.Equal(CellReference.MaxRow, last.LastRow);

		// Under an area that bounds rows, a floating row's place isn't known; the whole sheet's layout bounds none.
		Assert.Equal(Stitch.Fallback, new SegmentStitcher(3, false, new SheetLayout(1, 100, 0, 3), null).Stitch(floating, 0, Block((1, ["a"])), new CapturingTarget(Fill(int.MaxValue))));
		Assert.Equal(Stitch.Fallback, new SegmentStitcher(3, false, new SheetLayout(2, CellReference.MaxRow, 0, 3), null).Stitch(floating, 0, Block((1, ["a"])), new CapturingTarget(Fill(int.MaxValue))));
		Assert.Equal(Stitch.Appended, new SegmentStitcher(3, false, SheetLayout.Sheet, null).Stitch(floating, 0, Block((1, ["a"])), new CapturingTarget(Fill(int.MaxValue))));
	}

	[Fact]
	public void WritesNothingForASegmentWithoutARow()
	{
		using var budget = Budget.Open(1000);
		var stitcher = new SegmentStitcher(4, true, null, budget.Stream);
		var captured = Fill(int.MaxValue);
		var none = new SegmentResult { End = SegmentEnd.Done, FirstRow = -1, LastRow = 0 };

		Assert.Equal(Stitch.Appended, stitcher.Stitch(none, 30, Block(), new CapturingTarget(captured)));
		Assert.Equal(Stitch.Ended, stitcher.Stitch(none with { End = SegmentEnd.Ended }, 20, Block(), new CapturingTarget(captured)));
		Assert.Empty(captured.Records);
		Assert.Equal(50, budget.Charged);
		Assert.Equal((4L, 5L), (stitcher.LastRow, stitcher.NextMissingRow));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void FallsBackWithNothingChargedWhenTheChargesWouldPassTheLimit(bool kept)
	{
		// The bytes, the missing rows (3, when kept) and the extras: exactly what the limit has left is joined.
		var missing = kept ? 3 : 0;
		foreach (var left in new long[] { 30 + missing + 7, 30 + missing + 6 })
		{
			using var budget = Budget.Open(left);
			var stitcher = new SegmentStitcher(3, kept, null, budget.Stream);
			var captured = Fill(int.MaxValue);
			var outcome = stitcher.Stitch(Anchored(7, 7, extras: 7), 30, Block((7, ["a"])), new CapturingTarget(captured));
			if (left == 30 + missing + 7)
			{
				Assert.Equal(Stitch.Appended, outcome);
				Assert.Equal(left, budget.Charged);
				Assert.Equal(0, budget.Stream.Remaining);
			}
			else
			{
				Assert.Equal(Stitch.Fallback, outcome);
				Assert.Equal(0, budget.Charged);
				Assert.Empty(captured.Records);
				Assert.Equal((3L, 4L), (stitcher.LastRow, stitcher.NextMissingRow));
			}
		}
	}

	[Fact]
	public void WritesTheRowsMissingBeforeASegmentAcrossFullTargets()
	{
		using var budget = Budget.Open(1000);
		var stitcher = new SegmentStitcher(3, true, null, budget.Stream);
		var result = Anchored(7, 8, extras: 2);
		var block = Block((7, ["a"]), (8, ["b"]));
		var captured = Fill(1);
		var target = new CapturingTarget(captured);

		// A fill a missing row, each charged as it is written; then the block, and its bytes and extras.
		var outcomes = new List<(Stitch Outcome, long Charged, long Next)>();
		while (true)
		{
			captured.StartFill();
			var outcome = stitcher.Stitch(result, 40, block, target);
			captured.EndFill(outcome != Stitch.Ended);
			outcomes.Add((outcome, budget.Charged, stitcher.NextMissingRow));
			if (outcome != Stitch.Full)
			{
				break;
			}
		}

		Assert.Equal([(Stitch.Full, 1L, 5L), (Stitch.Full, 2L, 6L), (Stitch.Full, 3L, 7L), (Stitch.Appended, 3L + 40 + 2, 9L)], outcomes);
		Assert.Equal(["4: ()", "5: ()", "6: ()", "7: a", "8: b"], Describe(captured));

		// A target full after the missing rows leaves the block to the next call; one still full writes nothing more.
		var again = new SegmentStitcher(5, true, null, null);
		var one = Fill(1);
		Assert.Equal(Stitch.Full, again.Stitch(result, 0, block, new CapturingTarget(one)));
		Assert.Equal(["6: ()"], Describe(one));
		Assert.Equal(Stitch.Full, again.Stitch(result, 0, block, new CapturingTarget(one)));
		Assert.Equal(["6: ()"], Describe(one));
		Assert.Equal((5L, 7L), (again.LastRow, again.NextMissingRow));
		one.StartFill();
		Assert.Equal(Stitch.Appended, again.Stitch(result, 0, block, new CapturingTarget(one)));
		Assert.Equal(["6: ()", "7: a", "8: b"], Describe(one));
	}

	[Fact]
	public void LeavesTheStateAFallbackResumesFrom()
	{
		var stitcher = new SegmentStitcher(3, true, null, null);
		var captured = Fill(1);
		captured.StartFill();
		Assert.Equal(Stitch.Full, stitcher.Stitch(Anchored(7, 8), 0, Block((7, ["a"])), new CapturingTarget(captured)));

		// Among the rows missing before the segment: the serial read writes the rest of them before its first row.
		var marked = new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Error };
		captured.StartFill();
		Assert.Equal(Stitch.Fallback, stitcher.Stitch(marked, 0, Block(), new CapturingTarget(captured)));
		Assert.Equal((3L, 5L), (stitcher.LastRow, stitcher.NextMissingRow));
	}

	[Fact]
	public void ChecksItsArguments()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentStitcher(-1, true, null, null));
		var stitcher = new SegmentStitcher(0, true, null, null);
		Assert.Equal(1, stitcher.NextMissingRow);
		var captured = Fill(int.MaxValue);
		Assert.Throws<ArgumentNullException>(() => stitcher.Stitch(Anchored(1, 1), 0, null!, new CapturingTarget(captured)));
		Assert.Throws<ArgumentOutOfRangeException>(() => stitcher.Stitch(Anchored(1, 1), -1, Block(), new CapturingTarget(captured)));
		Assert.True(SegmentStitcher.BoundsRows(new SheetLayout(1, 5, 0, 0)));
		Assert.False(SegmentStitcher.BoundsRows(SheetLayout.Sheet));
		Assert.False(SegmentStitcher.BoundsRows(null));
	}

	/// <summary>The scan of an anchored segment that is done.</summary>
	/// <param name="first">Its first row.</param>
	/// <param name="last">Its last row.</param>
	/// <param name="extras">Its extras.</param>
	/// <returns>The result.</returns>
	private static SegmentResult Anchored(long first, long last, long extras = 0) => new() { End = SegmentEnd.Done, FirstRow = first, LastRow = last, Extras = extras };

	/// <summary>A block of records of text fields.</summary>
	/// <param name="records">Each record's line and fields.</param>
	/// <returns>The block.</returns>
	private static SegmentBlock Block(params (long Line, string[] Fields)[] records)
	{
		var block = new SegmentBlock();
		foreach (var (line, fields) in records)
		{
			foreach (var field in fields)
			{
				block.AddField(Encoding.UTF8.GetBytes(field), SheetCellKind.Text);
			}

			block.EndRecord(line);
		}

		return block;
	}

	/// <summary>A capturing target's records, a fill begun.</summary>
	/// <param name="recordsPerFill">The records a fill takes.</param>
	/// <returns>The records.</returns>
	private static Captured Fill(int recordsPerFill)
	{
		var captured = new Captured(recordsPerFill);
		captured.StartFill();

		return captured;
	}

	/// <summary>Describes the records captured.</summary>
	/// <param name="captured">The records.</param>
	/// <returns>Each record (<see cref="CapturedRecord.ToString"/>).</returns>
	private static List<string> Describe(Captured captured) => [.. captured.Records.Select(r => r.ToString())];

	/// <summary>A part's stream that counts toward a package's guard, with a limit a number of bytes past its total.</summary>
	private sealed class Budget : IDisposable
	{
		private readonly PackageArchive archive;
		private readonly long start;

		/// <summary>Creates the budget.</summary>
		/// <param name="archive">The package.</param>
		/// <param name="left">What the limit leaves.</param>
		private Budget(PackageArchive archive, long left)
		{
			this.archive = archive;
			start = archive.Guard.Total;
			Stream = SegmentReads.Budget(archive, left)(new MemoryStream());
		}

		/// <summary>The part's stream.</summary>
		public LimitedReadStream Stream { get; }

		/// <summary>What has been charged since it was made.</summary>
		public long Charged => archive.Guard.Total - start;

		/// <summary>Opens a budget.</summary>
		/// <param name="left">What the limit leaves.</param>
		/// <returns>The budget.</returns>
		public static Budget Open(long left) => new(SegmentReads.OpenBudget(), left);

		/// <inheritdoc/>
		public void Dispose()
		{
			Stream.Dispose();
			archive.Dispose();
		}
	}
}
