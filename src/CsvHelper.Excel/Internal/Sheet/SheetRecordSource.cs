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
/// A source holds its sheet's stream, not the workbook: after the workbook is disposed, the next fill throws
/// <see cref="ObjectDisposedException"/> (the records the reader holds already can still be read).
/// </remarks>
internal sealed class SheetRecordSource : CsvRecordSource
{
	private readonly SheetScanner scanner;
	private readonly ExcelWorkbook workbook;
	private readonly bool ownsWorkbook;

	/// <summary>Creates the source.</summary>
	/// <param name="scanner">The sheet's scanner, which the source owns.</param>
	/// <param name="workbook">The workbook of the sheet.</param>
	/// <param name="ownsWorkbook">Whether disposing the source disposes the workbook.</param>
	public SheetRecordSource(SheetScanner scanner, ExcelWorkbook workbook, bool ownsWorkbook)
	{
		this.scanner = scanner;
		this.workbook = workbook;
		this.ownsWorkbook = ownsWorkbook;
	}

	/// <inheritdoc/>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		ThrowIfWorkbookDisposed();

		return scanner.Fill(new RecordBatchTarget(batch));
	}

	/// <inheritdoc/>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken)
	{
		ThrowIfWorkbookDisposed();

		return scanner.FillAsync(new RecordBatchTarget(batch), cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>Field <c>i</c> is column <c>i</c>: Phase 1 reads every sheet from column <c>A</c>.</remarks>
	public override string? Locate(long line, int field)
	{
		if (line < 1 || line > CellReference.MaxRow)
		{
			return null;
		}

		var location = scanner.Location;

		return (uint)field < CellReference.MaxColumns ? location.Cell(field, line) : location.Row(line);
	}

	/// <summary>Disposes the sheet's stream, and the workbook when the source owns it.</summary>
	public override void Dispose()
	{
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
		await scanner.DisposeAsync().ConfigureAwait(false);
		if (ownsWorkbook)
		{
			await workbook.DisposeAsync().ConfigureAwait(false);
		}
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
