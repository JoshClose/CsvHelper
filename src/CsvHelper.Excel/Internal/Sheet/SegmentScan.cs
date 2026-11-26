using System;

namespace CsvHelper.Excel;

/// <summary>
/// Scans segments of a sheet's <c>sheetData</c>, one after another, on one of the pipeline's scan threads: a worker of the
/// sheet's <see cref="SheetScanner"/> (<see cref="SheetScanner.CreateSegmentScanner"/>), with its own window and state,
/// started from the sheet's state when the segments began. Each scan thread has its own; none is called by two threads at
/// once.
/// </summary>
internal interface ISegmentScanner
{
	/// <summary>
	/// Scans one segment's bytes into <paramref name="block"/>, which it clears first. It never throws: whatever it can't
	/// finish exactly marks the segment (<see cref="SegmentEnd.Marked"/>), and the stitcher has the sheet read serially from
	/// the segment's start.
	/// </summary>
	/// <param name="bytes">The segment's bytes: whole rows, cut at a row's start, from <paramref name="offset"/> in the part.</param>
	/// <param name="length">The number of bytes of the segment.</param>
	/// <param name="offset">The offset in the part of the segment's first byte.</param>
	/// <param name="final">Whether the segment's bytes are the last the pipeline read: the part ends after them, or they end where the part couldn't be inflated.</param>
	/// <param name="block">Takes the segment's records.</param>
	/// <returns>How the segment ended, and what the stitcher needs of it.</returns>
	SegmentResult Scan(byte[] bytes, int length, long offset, bool final, SegmentBlock block);
}

/// <summary>How a segment's scan ended (<see cref="ISegmentScanner.Scan"/>).</summary>
internal enum SegmentEnd : byte
{
	/// <summary>Every byte of the segment was read, and it ends between rows: the next segment goes on.</summary>
	Done,

	/// <summary>The rows end in the segment: at <c>&lt;/sheetData&gt;</c>, or at a row below the area. No later segment is read.</summary>
	Ended,

	/// <summary>The scan can't finish the segment exactly (<see cref="SegmentMark"/>): the sheet is read serially from its start.</summary>
	Marked,
}

/// <summary>Why a segment was marked: for tests and for "As built"; the stitcher falls back for each the same way.</summary>
internal enum SegmentMark : byte
{
	/// <summary>Not marked.</summary>
	None,

	/// <summary>The segment has <c>&lt;!</c> or <c>&lt;?</c>: a comment, CDATA section, processing instruction or DOCTYPE.</summary>
	Markup,

	/// <summary>Its bytes end inside a row, a tag or a skip; or the final segment ends before the rows do.</summary>
	Unfinished,

	/// <summary>The scan threw: the serial read throws the sheet's error, at its place.</summary>
	Error,

	/// <summary>The block would pass its limit (<see cref="SegmentSizes.MaxBlock"/>).</summary>
	BlockCap,

	/// <summary>The bytes the segment charges beyond its XML pass what the workbook's limit had left when the segments began.</summary>
	ExtrasCap,

	/// <summary>A row numbered by an <c>r</c> after rows numbered from the row before: its order can't be checked in the segment.</summary>
	Anchor,

	/// <summary>A row numbered from the row before, under an area that bounds rows: its place in the area isn't known in the segment.</summary>
	FloatingArea,

	/// <summary>The splitter found no row start before the segment's cap: the pipeline cut nothing, and nothing was scanned.</summary>
	Unsplit,
}

/// <summary>What a segment's scan gives the stitcher (<see cref="ISegmentScanner.Scan"/>).</summary>
internal readonly struct SegmentResult
{
	/// <summary>How the segment ended.</summary>
	public SegmentEnd End { get; init; }

	/// <summary>Why it was marked; <see cref="SegmentMark.None"/> unless <see cref="End"/> is <see cref="SegmentEnd.Marked"/>.</summary>
	public SegmentMark Mark { get; init; }

	/// <summary>
	/// The segment's first row: its number, when it has an <c>r</c> or its first cell has one (the segment is anchored); 0
	/// when it is numbered from the row before (the segment floats, and its rows are numbered from 1); -1 when the segment
	/// has no row.
	/// </summary>
	public long FirstRow { get; init; }

	/// <summary>
	/// The number of the last row the segment read, written or not (skipped, hidden, dropped): absolute when it is anchored,
	/// from 1 when it floats; 0 when it has no row. A row below the area that ended the rows is not one.
	/// </summary>
	public long LastRow { get; init; }

	/// <summary>The bytes the segment counts beyond its XML: empty fields of gaps and padding, missing rows, and shared strings' bytes beyond their cells.</summary>
	public long Extras { get; init; }

	/// <summary>The cells the scan read by the tokenizer's path (<see cref="SheetScanner.FallbackCells"/>).</summary>
	public long FallbackCells { get; init; }

	/// <summary>The exception that marked the segment (<see cref="SegmentMark.Error"/>), for tests; never thrown: the serial read throws its own.</summary>
	public Exception? Error { get; init; }
}

/// <summary>Marks a segment from inside its scan: thrown by <see cref="SegmentBlock"/> and a worker's charges, caught by the worker's scan.</summary>
internal sealed class SegmentMarkException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="mark">Why the segment is marked.</param>
	public SegmentMarkException(SegmentMark mark)
		: base($"The segment is marked: {mark}.") => Mark = mark;

	/// <summary>Why the segment is marked.</summary>
	public SegmentMark Mark { get; }
}

/// <summary>The sizes that bound segments and their blocks.</summary>
internal static class SegmentSizes
{
	/// <summary>The size the splitter cuts segments at, at the first row start at or after it: 256 KB (<c>ExcelWorkbook.segmentSize</c>).</summary>
	public const int DefaultSize = 256 * 1024;

	/// <summary>How far the serial read goes before segments can begin: 1 MB of the part (<c>ExcelWorkbook.segmentMinimum</c>).</summary>
	public const long DefaultMinimum = 1024 * 1024;

	/// <summary>The most bytes a segment takes, when the part's size limit doesn't bound it lower (<see cref="SegmentCap"/>): 4 MB.</summary>
	public const int MaxSegment = 4 * 1024 * 1024;

	/// <summary>The most a block's bytes plus four for each field take (<see cref="SegmentBlock.Size"/>): 4 MB.</summary>
	public const int MaxBlock = 4 * 1024 * 1024;

	/// <summary>A block larger than this halves the cut target, down to <see cref="MinimumCut"/>; smaller ones double it back: 1 MB.</summary>
	public const int LargeBlock = 1024 * 1024;

	/// <summary>The smallest cut target the halving goes to: 16 KB, or the configured size when that is less.</summary>
	public const int MinimumCut = 16 * 1024;

	/// <summary>
	/// The most bytes a segment of a part takes: <see cref="MaxSegment"/>, or half the part's size limit when that is less,
	/// so that every cell a worker reads fits in a serial window, which grows to that limit.
	/// </summary>
	/// <param name="maxPartSize">The part's size limit (<c>ExcelReaderOptions.MaxPartSize</c>).</param>
	/// <returns>The cap: at least 1.</returns>
	public static int SegmentCap(long maxPartSize) => (int)Math.Max(Math.Min(MaxSegment, maxPartSize / 2), 1);
}
