namespace CsvHelper.Excel;

/// <summary>
/// The segment scanner of a sheet (<see cref="ISegmentScanner"/>): a worker of its <see cref="SheetScanner"/>, made when the
/// segments begin (<see cref="SheetScanner.CreateSegmentScanner"/>), which scans one segment after another on one of the
/// pipeline's scan threads, reusing its window, XML scanner and state from one to the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>A segment's scan.</b> The block is cleared. A segment with <c>&lt;!</c> or <c>&lt;?</c> anywhere in its bytes is
/// marked (<see cref="SegmentMark.Markup"/>) before anything is scanned. The worker's window is put over the bytes at their
/// offset in the part (it never reads more, and its bytes are never the part's last), its XML scanner in <c>sheetData</c>
/// (depth 2, the root open), and the worker between rows, with no row before the segment's, no missing row to write and
/// nothing counted. It scans the bytes once, into a <see cref="BlockTarget"/>, which is never full:
/// </para>
/// <list type="bullet">
/// <item><description>at <c>&lt;/sheetData&gt;</c>, or at a row below the area, the rows end (<see cref="SegmentEnd.Ended"/>);</description></item>
/// <item><description>
/// with every byte read, between rows in <c>sheetData</c>, and more of the part to come, the segment is done
/// (<see cref="SegmentEnd.Done"/>);
/// </description></item>
/// <item><description>
/// with the bytes ending inside a row, a tag or a skip, or the part's last bytes ending before the rows do, it is marked
/// (<see cref="SegmentMark.Unfinished"/>);
/// </description></item>
/// <item><description>
/// any exception marks it: a <see cref="SegmentMarkException"/>'s mark (the block's cap, the extras' limit, a row it can't
/// place), else <see cref="SegmentMark.Error"/>. Nothing is thrown out of the scan.
/// </description></item>
/// </list>
/// <para>
/// <b>Rows.</b> The segment's first row anchors it when its number comes from an <c>r</c>, its own or its first cell's:
/// it is not checked against the row before (the stitcher checks it, and writes the rows missing before it), but its place
/// in the area is, and the rows after it are the sheet's, checked as the serial read checks them, with the rows missing
/// between them written by the worker. Otherwise the first row floats: it is row 1 of the segment, the rows after it are
/// numbered from it, and the stitcher adds the row before the segment's; under an area that bounds rows the segment is
/// marked (<see cref="SegmentMark.FloatingArea"/>), and so is a row with an <c>r</c> after floating ones
/// (<see cref="SegmentMark.Anchor"/>). The scanner's paths read every cell as the serial read does: these rules are in its
/// <c>SetRow</c>, which every row's number goes through.
/// </para>
/// <para>
/// <b>Charges.</b> The worker has no budget: what the serial read would charge (gaps, padding, missing rows between its
/// rows, shared strings' bytes beyond their cells) counts toward the segment's <see cref="SegmentResult.Extras"/>, which the
/// stitcher charges once it joins the segment. Extras past what the workbook's limit had left when the segments began mark
/// the segment (<see cref="SegmentMark.ExtrasCap"/>).
/// </para>
/// </remarks>
internal sealed class SheetSegmentScanner : ISegmentScanner
{
	private readonly SheetScanner worker;

	/// <summary>Creates the segment scanner of a worker.</summary>
	/// <param name="worker">The worker, over a window for segments.</param>
	internal SheetSegmentScanner(SheetScanner worker) => this.worker = worker;

	/// <inheritdoc/>
	public SegmentResult Scan(byte[] bytes, int length, long offset, bool final, SegmentBlock block) => worker.ScanSegment(bytes, length, offset, final, block);
}
