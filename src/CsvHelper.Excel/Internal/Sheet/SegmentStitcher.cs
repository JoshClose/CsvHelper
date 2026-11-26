using System;
using Outcome = CsvHelper.Excel.Stitch;

namespace CsvHelper.Excel;

/// <summary>
/// Joins the segments of a sheet into the reader's records, in order, each checked against the rows before it: the rows
/// missing between segments, floating segments' rows rebased, the segments' charges toward the workbook's limit, and the
/// end of the rows. It is synchronous and free of threads: the record source calls it in segment order, under core's lock,
/// with each segment's scan (<see cref="ISegmentScanner"/>) done.
/// </summary>
/// <remarks>
/// <para>
/// Whatever the stitcher can't join exactly falls back (<see cref="Outcome.Fallback"/>), with nothing of the segment written
/// or charged: the sheet's scanner then reads on from the segment's start (<see cref="SheetScanner.ResumeAt"/>, with
/// <see cref="LastRow"/> and <see cref="NextMissingRow"/>), so that its errors, and the limit's trip, are the serial read's.
/// That is a marked segment, an anchored segment whose first row isn't after the last row delivered, a floating segment
/// under an area that bounds rows or past the sheet's last row once rebased, and a segment whose bytes, missing rows and
/// extras would pass what the workbook's limit has left.
/// </para>
/// <para>
/// The rows missing before an anchored segment's first row are written first when missing rows are kept, from the area's
/// first and not before a row below the area, charged 1 each as each is written, as the serial read writes them; they stop
/// when the target is full (<see cref="Outcome.Full"/>), and the next call with the same segment goes on with them. Then the
/// segment's records are appended whole, into a target that isn't full, its floating rows numbered from the last row
/// delivered, and its uncounted bytes and extras charged.
/// </para>
/// </remarks>
internal sealed class SegmentStitcher
{
	private readonly bool keepsMissingRows;
	private readonly SheetLayout? layout;
	private readonly LimitedReadStream? budget;
	private long lastRow;
	private long nextMissingRow;

	/// <summary>Creates the stitcher of a sheet's segments, from the sheet's scanner where they begin.</summary>
	/// <param name="lastRow">The last row the sheet's scanner read (<see cref="SheetScanner.LastRow"/>): 0 before the first.</param>
	/// <param name="keepsMissingRows">Whether the rows the file lacks are written as empty records (<see cref="SheetScanner.KeepsMissingRows"/>).</param>
	/// <param name="layout">The area read, final (<see cref="SheetScanner.Layout"/>), or <see langword="null"/> for the whole sheet.</param>
	/// <param name="budget">The part's stream, which the segments' bytes and extras are charged to (<see cref="SheetScanner.Budget"/>); <see langword="null"/> to charge nothing.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="lastRow"/> is negative.</exception>
	public SegmentStitcher(long lastRow, bool keepsMissingRows, SheetLayout? layout, LimitedReadStream? budget)
	{
		if (lastRow < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(lastRow), lastRow, "The last row can't be negative.");
		}

		this.lastRow = lastRow;
		this.keepsMissingRows = keepsMissingRows;
		this.layout = layout;
		this.budget = budget;

		// Missing rows are written from the area's first row, as the sheet's scanner writes them.
		nextMissingRow = Math.Max(lastRow + 1, layout is null ? 1 : layout.FirstRow);
	}

	/// <summary>The last row delivered, written or not: the sheet's scanner's at the start, then each joined segment's last.</summary>
	public long LastRow => lastRow;

	/// <summary>
	/// The next missing row to write before a row after <see cref="LastRow"/>, when missing rows are kept: the row after it,
	/// the area's first when that is later, or past them among the rows missing before a segment that a full target cut.
	/// </summary>
	public long NextMissingRow => nextMissingRow;

	/// <summary>Whether an area bounds rows: it starts after the sheet's first row, or ends before its last.</summary>
	/// <param name="layout">The area, or <see langword="null"/> for the whole sheet.</param>
	/// <returns>Whether it bounds rows.</returns>
	internal static bool BoundsRows(SheetLayout? layout) => layout is not null && (layout.FirstRow > 1 || layout.LastRow < CellReference.MaxRow);

	/// <summary>Joins the next segment, as the remarks say.</summary>
	/// <typeparam name="TTarget">The target's type: a struct, so that the stitcher is specialized for it.</typeparam>
	/// <param name="result">The segment's scan.</param>
	/// <param name="uncounted">The segment's bytes that no stream counted toward the workbook's limit.</param>
	/// <param name="block">The segment's records.</param>
	/// <param name="target">Takes the records.</param>
	/// <returns>
	/// <see cref="Outcome.Appended"/> when the segment is joined; <see cref="Outcome.Full"/> when the target is full first, and
	/// the next call goes on with the same segment; <see cref="Outcome.Ended"/> when it is joined and the rows end in it;
	/// <see cref="Outcome.Fallback"/> when it can't be joined, with nothing of it written or charged.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="uncounted"/> is negative.</exception>
	public Stitch Stitch<TTarget>(in SegmentResult result, long uncounted, SegmentBlock block, TTarget target)
		where TTarget : struct, IBlockTarget
	{
		ArgumentNullException.ThrowIfNull(block);
		if (uncounted < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(uncounted), uncounted, "The bytes can't be negative.");
		}

		if (result.End == SegmentEnd.Marked)
		{
			return Outcome.Fallback;
		}

		// The rows missing before the segment, up to its first row (exclusive), and what its records' lines are added to.
		var missingEnd = nextMissingRow;
		var lineOffset = 0L;
		if (result.FirstRow > 0)
		{
			// The serial read throws its order error at this row.
			if (result.FirstRow <= lastRow)
			{
				return Outcome.Fallback;
			}

			// None before a row below the area, which ends the rows.
			if (keepsMissingRows && (layout is null || layout.Place(result.FirstRow) != RowPlace.Below))
			{
				missingEnd = Math.Max(result.FirstRow, nextMissingRow);
			}
		}
		else if (result.FirstRow == 0)
		{
			// A floating row's place in such an area isn't known, and past the sheet's last row the serial read throws.
			if (BoundsRows(layout) || lastRow + result.LastRow > CellReference.MaxRow)
			{
				return Outcome.Fallback;
			}

			lineOffset = lastRow;
		}

		// All of it must fit what the limit has left, or the serial read trips where it trips.
		if (budget is not null && uncounted + (missingEnd - nextMissingRow) + result.Extras > budget.Remaining)
		{
			return Outcome.Fallback;
		}

		while (nextMissingRow < missingEnd)
		{
			// Never at the start of a fill, whose target is empty: each fill writes at least one record.
			if (target.IsFull)
			{
				return Outcome.Full;
			}

			// Counted before it is written, as the sheet's scanner counts a missing row.
			budget?.Charge(1);
			target.EndRecord(nextMissingRow++);
		}

		if (block.RecordCount > 0)
		{
			if (target.IsFull)
			{
				return Outcome.Full;
			}

			target.AppendBlock(block, lineOffset);
		}

		if (budget is not null)
		{
			if (uncounted > 0)
			{
				budget.Charge(uncounted);
			}

			if (result.Extras > 0)
			{
				budget.Charge(result.Extras);
			}
		}

		// A segment without a row (only the end of the rows, or text), or whose only row is below the area, moves nothing.
		if (result.FirstRow >= 0 && result.LastRow > 0)
		{
			lastRow = result.FirstRow > 0 ? result.LastRow : lastRow + result.LastRow;
		}

		nextMissingRow = Math.Max(lastRow + 1, nextMissingRow);

		return result.End == SegmentEnd.Ended ? Outcome.Ended : Outcome.Appended;
	}
}

/// <summary>What joining a segment did (<see cref="SegmentStitcher.Stitch"/>).</summary>
internal enum Stitch : byte
{
	/// <summary>The segment is joined: the next one goes on.</summary>
	Appended,

	/// <summary>The target filled first, among the rows missing before the segment or before its records: the next call goes on with the same segment.</summary>
	Full,

	/// <summary>The segment is joined, and the rows end in it: no later segment is read.</summary>
	Ended,

	/// <summary>The segment can't be joined exactly, and nothing of it is written or charged: the sheet's scanner reads on from its start.</summary>
	Fallback,
}
