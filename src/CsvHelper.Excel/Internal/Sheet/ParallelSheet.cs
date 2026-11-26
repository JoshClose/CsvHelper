using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace CsvHelper.Excel;

/// <summary>
/// A sheet's fills while <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> runs over its reader,
/// between the source's <c>BeginParallel</c> and <c>EndParallel</c>: core's workers call them one at a time, under core's
/// lock. They read serially until the segments can begin, then stitch the segments the pipeline's threads scan into the
/// reader's batches, and read serially again from a segment that can't be stitched to the sheet's end.
/// </summary>
/// <remarks>
/// <para>
/// <b>Errors.</b> A serial fill that throws after writing whole records delivers them first: the batch's open record is
/// dropped (<c>RecordBatch.Reset</c>), the fill returns, and the next fill throws the same exception, which the scanner
/// keeps (its fault). So the enumeration yields every record before the failing row, then the error, at every degree,
/// with segments or without. A segment that can't be finished exactly is read serially, so every error is the serial
/// read's, at its place.
/// </para>
/// <para>
/// <b>Segments</b> begin at the end of a fill that leaves the serial scanner between rows past the part's first
/// <c>segmentMinimum</c> bytes, once a record with a value is written (<see cref="SheetScanner.CanStartSegments"/>), and
/// only when the enumeration has at least two workers. They are used once: after a fallback, or after the enumeration
/// ends, the sheet reads serially.
/// </para>
/// </remarks>
internal sealed class ParallelSheet
{
	private readonly SheetScanner scanner;
	private readonly ExcelWorkbook workbook;
	private readonly int degree;
	private readonly long maxPartSize;
	// Whether segments may still begin: the switches allow them and none have run.
	private bool segmentsAllowed;
	private SegmentPipeline? pipeline;
	private SegmentStitcher? stitcher;
	// The slot being stitched: its missing rows filled a batch, and the next fill goes on with it.
	private SegmentSlot? pending;
	// Whether the rows ended in a stitched segment: every later fill returns false.
	private bool rowsEnded;
	// The records the fill in progress has written, stitched or serial, through CountingTarget.
	private int ended;
	// The error of a serial fill that wrote whole records first: thrown by every fill after it.
	private ExceptionDispatchInfo? deferred;

	/// <summary>Creates the state of one enumeration.</summary>
	/// <param name="scanner">The sheet's serial scanner.</param>
	/// <param name="workbook">The workbook, for its switches.</param>
	/// <param name="degree">The enumeration's number of workers.</param>
	/// <param name="maxPartSize">The reader's <see cref="ExcelReaderOptions.MaxPartSize"/>, which bounds a segment (<see cref="SegmentSizes.SegmentCap"/>).</param>
	/// <param name="segmentsAllowed">Whether this source may still read in segments: they run at most once a source.</param>
	public ParallelSheet(SheetScanner scanner, ExcelWorkbook workbook, int degree, long maxPartSize, bool segmentsAllowed)
	{
		this.scanner = scanner;
		this.workbook = workbook;
		this.degree = degree;
		this.maxPartSize = maxPartSize;
		this.segmentsAllowed = segmentsAllowed && workbook.segmentScans && degree >= 2;
	}

	/// <summary>Whether segments began in this enumeration.</summary>
	public bool SegmentsStarted { get; private set; }

	/// <summary>Whether the rows ended in a stitched segment: the source's later fills return false.</summary>
	public bool RowsEnded => rowsEnded;

	/// <summary>The segments appended to batches.</summary>
	public long Stitched { get; private set; }

	/// <summary>The fallbacks to a serial read taken.</summary>
	public long Fallbacks { get; private set; }

	/// <summary>The cells the workers read by the tokenizer's path.</summary>
	public long FallbackCells { get; private set; }

	/// <summary>Writes the next records into <paramref name="batch"/>.</summary>
	/// <param name="batch">The reader's batch.</param>
	/// <returns><see langword="false"/> when the sheet has no more records; otherwise <see langword="true"/>.</returns>
	/// <exception cref="CsvFormatException">The sheet's content can't be read.</exception>
	/// <exception cref="InvalidDataException">The part passes a limit, or can't be inflated.</exception>
	public bool Fill(CsvReader.RecordBatch batch)
	{
		deferred?.Throw();
		if (rowsEnded)
		{
			return false;
		}

		ended = 0;

		if (pipeline is not null)
		{
			var stitched = StitchSegments(batch);
			if (stitched is { } more)
			{
				return more;
			}

			// A fallback: the serial scanner reads on from the segment's start, in this fill.
		}

		var result = FillSerially(batch);
		if (result && segmentsAllowed && scanner.CanStartSegments && scanner.Position >= workbook.segmentMinimum)
		{
			Start();
		}

		return result;
	}

	/// <summary>
	/// Ends the enumeration, once core's workers have stopped: stops and joins the pipeline's threads, and, unless the rows
	/// ended, has the serial scanner read on from the first segment not delivered, as a read after an early stop goes on.
	/// </summary>
	public void End()
	{
		if (pipeline is null)
		{
			return;
		}

		pipeline.Stop();
		if (rowsEnded)
		{
			pipeline.ChargeUnread();
		}
		else
		{
			// From the slot being stitched (its missing rows partly written), or the first one not taken.
			var stream = pending is not null ? pipeline.FallbackFrom(pending) : pipeline.FallbackAfterDelivered();
			if (stream is not null)
			{
				scanner.ResumeAt(stream, stream.Offset, stitcher!.LastRow, stitcher.NextMissingRow);
			}
		}

		Release();
	}

	/// <summary>Stops and joins the pipeline's threads and returns its buffers, when the source is disposed.</summary>
	public void Dispose()
	{
		if (pipeline is not null)
		{
			pipeline.Stop();
			Release();
		}
	}

	/// <summary>
	/// A serial fill through a counting target, which delivers the whole records the fill wrote before its error: its own,
	/// and those stitched before a fallback in the same fill.
	/// </summary>
	/// <param name="batch">The reader's batch.</param>
	/// <returns>What the scanner's fill returned; <see langword="true"/> when it threw after whole records.</returns>
	private bool FillSerially(CsvReader.RecordBatch batch)
	{
		try
		{
			return scanner.Fill(new CountingTarget(batch, this));
		}
		catch (Exception e) when (ended > 0)
		{
			// The next fill throws it, after these records (the scanner's fault would too, for its own errors).
			batch.Reset();
			deferred = ExceptionDispatchInfo.Capture(e);

			return true;
		}
	}

	/// <summary>Starts the pipeline from the serial scanner's position.</summary>
	private void Start()
	{
		segmentsAllowed = false;
		var budget = scanner.Budget!;
		var threads = workbook.segmentThreads > 0 ? workbook.segmentThreads : Math.Min(degree, Environment.ProcessorCount);
		var extrasLimit = budget.Remaining;
		var scanners = new ISegmentScanner[threads];
		for (var i = 0; i < threads; i++)
		{
			scanners[i] = scanner.CreateSegmentScanner(extrasLimit);
		}

		// The cut target below the cap, so that a small MaxPartSize still cuts segments, if smaller ones.
		var cap = SegmentSizes.SegmentCap(maxPartSize);
		var size = Math.Max(Math.Min(workbook.segmentSize, cap / 2), 1);
		stitcher = new SegmentStitcher(scanner.LastRow, scanner.KeepsMissingRows, scanner.Layout, budget);
		pipeline = new SegmentPipeline(budget, scanners, scanner.RowStart!, size, cap, workbook.segmentPool);
		pipeline.Start(scanner.Unread, scanner.Position);
		SegmentsStarted = true;
	}

	/// <summary>Stitches segments into the batch, until it is full or the rows end.</summary>
	/// <param name="batch">The reader's batch.</param>
	/// <returns>The fill's result; <see langword="null"/> after a fallback, when the serial scanner reads on.</returns>
	private bool? StitchSegments(CsvReader.RecordBatch batch)
	{
		var target = new CountingTarget(batch, this);
		while (true)
		{
			var slot = pending ?? pipeline!.Next();
			pending = null;
			if (slot is null)
			{
				// No segment after the last delivered, though the rows haven't ended: a final segment is ended or marked, so
				// this isn't reached; the serial read takes over after the last delivered.
				Fallback(null);

				return null;
			}

			// A marked segment (an unsplit one too) is a fallback.
			switch (stitcher!.Stitch(slot.Result, slot.Uncounted, slot.Block, target))
			{
				case Stitch.Full:
					pending = slot;

					return true;
				case Stitch.Fallback:
					Fallback(slot);

					return null;
				case Stitch.Ended:
					Stitched++;
					FallbackCells += slot.Result.FallbackCells;
					pipeline!.Release(slot);
					rowsEnded = true;
					pipeline.Stop();
					pipeline.ChargeUnread();
					Release();

					return false;
				default:
					Stitched++;
					FallbackCells += slot.Result.FallbackCells;
					pipeline!.Release(slot);
					if (batch.IsFull)
					{
						return true;
					}

					break;
			}
		}
	}

	/// <summary>Stops the pipeline and has the serial scanner read from a slot's start (or after the last delivered) to the sheet's end.</summary>
	/// <param name="slot">The slot that can't be stitched, or <see langword="null"/> for the first not delivered.</param>
	private void Fallback(SegmentSlot? slot)
	{
		Fallbacks++;
		pipeline!.Stop();
		var stream = slot is not null ? pipeline.FallbackFrom(slot) : pipeline.FallbackAfterDelivered();
		if (stream is not null)
		{
			scanner.ResumeAt(stream, stream.Offset, stitcher!.LastRow, stitcher.NextMissingRow);
		}

		Release();
	}

	/// <summary>Disposes the pipeline, once stopped: its threads are joined, and the buffers a fallback stream took are that stream's.</summary>
	private void Release()
	{
		pipeline!.Dispose();
		pipeline = null;
		pending = null;
	}

	/// <summary>
	/// The target of the fills under <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>, the serial
	/// scanner's and the stitcher's: the reader's batch, counting the records written. A struct of its own, so that the
	/// scanner's code for it is a separate instantiation and the sequential read's (<see cref="RecordBatchTarget"/>) is
	/// untouched.
	/// </summary>
	private readonly struct CountingTarget : IBlockTarget
	{
		private readonly CsvReader.RecordBatch batch;
		private readonly ParallelSheet owner;

		/// <summary>Creates the target.</summary>
		/// <param name="batch">The reader's batch.</param>
		/// <param name="owner">The state that counts the records.</param>
		public CountingTarget(CsvReader.RecordBatch batch, ParallelSheet owner)
		{
			this.batch = batch;
			this.owner = owner;
		}

		/// <inheritdoc/>
		public bool IsFull => batch.IsFull;

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => batch.AddField(utf8, kind.ToCsv());

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style) => batch.AddField(utf8, kind.ToCsv(), style);

		/// <inheritdoc/>
		public Span<byte> GetFieldSpan(int sizeHint) => batch.GetFieldSpan(sizeHint);

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind) => batch.CommitField(length, kind.ToCsv());

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind, int style) => batch.CommitField(length, kind.ToCsv(), style);

		/// <inheritdoc/>
		public void EndRecord(long row)
		{
			batch.EndRecord(row);
			owner.ended++;
		}

		/// <inheritdoc/>
		public void AppendBlock(SegmentBlock block, long lineOffset)
		{
			new RecordBatchTarget(batch).AppendBlock(block, lineOffset);
			owner.ended += block.RecordCount;
		}
	}
}
