using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The records of a worksheet as a <see cref="CsvReader"/> reads them: its <see cref="SheetScanner"/>'s fills, and the
/// locations of its records and fields (<c>Orders!C14</c>). The reader owns the source, and disposing it disposes the
/// sheet's stream, and the workbook when the reader came from <see cref="ExcelReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// A source holds its sheet's stream, not the workbook: after the workbook is disposed, the next fill throws
/// <see cref="ObjectDisposedException"/> (the records the reader holds already can still be read).
/// </para>
/// <para>
/// Under <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>, between <see cref="BeginParallel"/> and
/// <see cref="EndParallel"/>, its fills are <see cref="ParallelSheet"/>'s: they deliver a failing fill's whole records
/// before its error, and read the sheet's rows in segments on threads of their own once the sheet is large enough.
/// </para>
/// </remarks>
internal sealed class SheetRecordSource : CsvRecordSource
{
	private readonly SheetScanner scanner;
	private readonly ExcelWorkbook workbook;
	private readonly bool ownsWorkbook;
	private readonly long maxPartSize;
	// The fills of the enumeration running over the reader; null outside one, when the fills are the scanner's.
	private ParallelSheet? parallel;
	// Whether the rows ended in a stitched segment: no fill reads again.
	private bool rowsEnded;
	// Whether segments have run: they run once a source.
	private bool segmentsUsed;
	// The workers' cells read by the tokenizer's path, and the segments' counters, until they go to the workbook.
	private long workerFallbackCells;
	private long segmentsStitched;
	private long segmentFallbacks;
	private long segmentStarts;

	/// <summary>Creates the source.</summary>
	/// <param name="scanner">The sheet's scanner, which the source owns.</param>
	/// <param name="workbook">The workbook of the sheet.</param>
	/// <param name="ownsWorkbook">Whether disposing the source disposes the workbook.</param>
	/// <param name="cellOptions">How the reader converts the sheet's typed cells: the workbook's date system and the reader's options.</param>
	/// <param name="maxPartSize">The reader's <see cref="ExcelReaderOptions.MaxPartSize"/>, which bounds a segment.</param>
	public SheetRecordSource(SheetScanner scanner, ExcelWorkbook workbook, bool ownsWorkbook, CsvCellOptions cellOptions, long maxPartSize)
	{
		this.scanner = scanner;
		this.workbook = workbook;
		this.ownsWorkbook = ownsWorkbook;
		this.maxPartSize = maxPartSize;
		CellOptions = cellOptions;
	}

	/// <inheritdoc/>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		ThrowIfWorkbookDisposed();

		return parallel is null ? !rowsEnded && scanner.Fill(new RecordBatchTarget(batch)) : parallel.Fill(batch);
	}

	/// <inheritdoc/>
	public override void BeginParallel(int degree) => parallel = new ParallelSheet(scanner, workbook, degree, maxPartSize, !segmentsUsed && !rowsEnded);

	/// <inheritdoc/>
	/// <remarks>The segments' threads are stopped and joined; unless the rows ended, the sheet reads on serially from the first segment not delivered.</remarks>
	public override void EndParallel()
	{
		var ended = parallel;
		if (ended is null)
		{
			return;
		}

		parallel = null;
		ended.End();
		Collect(ended);
	}

	/// <inheritdoc/>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken)
	{
		ThrowIfWorkbookDisposed();

		return rowsEnded ? new ValueTask<bool>(false) : scanner.FillAsync(new RecordBatchTarget(batch), cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field's column comes from the scanner's layout: field <c>i</c> is column <c>i</c> of a whole sheet, of an area the
	/// column <c>i</c> after its first, and with hidden columns skipped the <c>i</c>th shown column. The layout is final
	/// before the first record is written, so before any field is located. A field the area has no column for, and a record
	/// without a row in the sheet (line 0, such as a table's names when its header row is hidden), are located by their
	/// row, or not at all.
	/// </remarks>
	public override string? Locate(long line, int field)
	{
		if (line < 1 || line > CellReference.MaxRow)
		{
			return null;
		}

		var location = scanner.Location;
		var layout = scanner.Layout;
		var column = layout is null ? field : layout.ColumnOf(field);

		return (uint)column < CellReference.MaxColumns ? location.Cell(column, line) : location.Row(line);
	}

	/// <summary>Disposes the sheet's stream, and the workbook when the source owns it; the segments' threads are stopped and joined first.</summary>
	public override void Dispose()
	{
		StopParallel();
		workbook.fallbackCells += scanner.FallbackCells;
		scanner.Dispose();
		if (ownsWorkbook)
		{
			workbook.Dispose();
		}
	}

	/// <summary>Asynchronously disposes the sheet's stream, and the workbook when the source owns it.</summary>
	/// <returns>A task that completes when they are disposed.</returns>
	public override async ValueTask DisposeAsync()
	{
		StopParallel();
		workbook.fallbackCells += scanner.FallbackCells;
		await scanner.DisposeAsync().ConfigureAwait(false);
		if (ownsWorkbook)
		{
			await workbook.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Stops and joins the segments' threads of an enumeration that didn't end (the reader is disposed inside it), and adds
	/// the segments' counters to the workbook's.
	/// </summary>
	private void StopParallel()
	{
		if (parallel is not null)
		{
			parallel.Dispose();
			Collect(parallel);
			parallel = null;
		}

		workbook.fallbackCells += workerFallbackCells;
		workbook.segmentsStitched += segmentsStitched;
		workbook.segmentFallbacks += segmentFallbacks;
		workbook.segmentStarts += segmentStarts;
		workerFallbackCells = segmentsStitched = segmentFallbacks = segmentStarts = 0;
	}

	/// <summary>Takes an enumeration's counters and state: whether its rows ended, and whether segments ran.</summary>
	/// <param name="ended">The enumeration's fills.</param>
	private void Collect(ParallelSheet ended)
	{
		rowsEnded |= ended.RowsEnded;
		segmentsUsed |= ended.SegmentsStarted;
		workerFallbackCells += ended.FallbackCells;
		segmentsStitched += ended.Stitched;
		segmentFallbacks += ended.Fallbacks;
		segmentStarts += ended.SegmentsStarted ? 1 : 0;
	}

	/// <summary>Throws when the workbook is disposed, before a fill reads the sheet's stream.</summary>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	private void ThrowIfWorkbookDisposed()
	{
		if (workbook.IsDisposed)
		{
			throw new ObjectDisposedException(nameof(ExcelWorkbook), "The workbook is disposed.");
		}
	}
}
