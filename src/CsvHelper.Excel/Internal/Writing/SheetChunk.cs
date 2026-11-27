using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// The rows a parallel write's worker wrote for one batch (<see cref="SheetChunkSink"/>, <see cref="CsvCellSink.TakeChunk"/>),
/// which the sheet's sink appends in order (<see cref="CsvCellSink.WriteChunk"/>), or refuses, so that the batch is
/// written again serially. Its rows are the serial write's for the same records, but for what depends on the rows before
/// the chunk, which the owner adds: the empty rows before its first record with a cell, the rows' place in the sheet, and
/// the used range.
/// </summary>
internal sealed class SheetChunk
{
	/// <summary>
	/// The XML of the chunk's rows, without <c>r</c>: from its first record with a cell's <c>&lt;row&gt;</c> to its last's
	/// <c>&lt;/row&gt;</c>, with a <c>&lt;row/&gt;</c> for each record without a cell between them; the empty segment when no
	/// record has a cell. The chunk's own until <see cref="Release"/> or the entry takes it.
	/// </summary>
	public OwnedSegment Xml { get; set; }

	/// <summary>The records ended in the chunk: its rows, the empty ones included.</summary>
	public int Records { get; set; }

	/// <summary>The index in the chunk (from 0) of its first record with a cell, or -1 when none has one.</summary>
	public int FirstCellRecord { get; set; } = -1;

	/// <summary>The index in the chunk of its last record with a cell, or -1 when none has one.</summary>
	public int LastCellRecord { get; set; } = -1;

	/// <summary>The column (from 0) of the chunk's last cell furthest right, or -1 when no record has a cell.</summary>
	public int LastColumn { get; set; } = -1;

	/// <summary>Whether a text cell of the chunk starts like a number (Excel's warning triangle, which the sheet's <c>ignoredErrors</c> turns off).</summary>
	public bool NumberLikeText { get; set; }

	/// <summary>The links of the chunk's cells, in cell order: each one's record in the chunk, column and target; <see langword="null"/> for none.</summary>
	public List<ChunkLink>? Links { get; set; }

	/// <summary>Why the chunk must be written again serially; <see cref="SheetChunkRedo.None"/> when it can be appended as it is.</summary>
	public SheetChunkRedo Redo { get; set; }

	/// <summary>The record number core gave the batch's first item (<see cref="CsvCellSink.BeginChunk"/>).</summary>
	public long FirstRecord { get; set; }

	/// <summary>The version of the view the chunk was written with (<see cref="SheetView.Version"/>).</summary>
	public int ViewVersion { get; set; }

	/// <summary>
	/// Whether the batch ended on a record that failed after a field of it had reached the worker, which core discarded:
	/// the serial write would have met the sheet's last row at that field, before the failure, when the record is past it.
	/// </summary>
	public bool DiscardedFields { get; set; }

	/// <summary>Returns the chunk's XML to the pool, unless the entry has taken it: a chunk refused or dropped. Calling it again does nothing.</summary>
	public void Release()
	{
		Xml.Return();
		Xml = default;
	}
}

/// <summary>A link of a chunk's cell (<see cref="SheetChunk.Links"/>).</summary>
/// <param name="Record">The cell's record in the chunk, from 0.</param>
/// <param name="Column">The cell's column, from 0.</param>
/// <param name="Target">The link's target (<see cref="SheetLinks.Target"/>).</param>
internal readonly record struct ChunkLink(int Record, int Column, string Target);

/// <summary>Why a worker's chunk must be written again serially (<see cref="SheetChunk.Redo"/>).</summary>
internal enum SheetChunkRedo : byte
{
	/// <summary>It needn't be.</summary>
	None,

	/// <summary>A cell needs a cell format the view's snapshot doesn't have: the serial write adds it, in its order.</summary>
	Style,

	/// <summary>A cell is a link, and the workbook has no link style yet: the serial write adds it.</summary>
	LinkStyle,

	/// <summary>
	/// A field fails as the sheet's own rules say (text too long for a cell, <see cref="ExcelLongNumbers.Throw"/>,
	/// <see cref="ExcelOutOfRangeDates.Throw"/>, a field past XFD): the serial write throws the serial write's exception, at
	/// its cell.
	/// </summary>
	Error,
}
