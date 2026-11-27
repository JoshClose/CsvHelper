using System;
using System.Threading;

namespace CsvHelper.Excel;

/// <summary>
/// What a parallel write's workers read of their sheet (<see cref="SheetChunkSink"/>): frozen on the writing thread from
/// the sheet's sink when the workers start (<see cref="SheetSink.Freeze"/>), and frozen again after a chunk written
/// serially added a cell format or the link style. Freezing adds nothing to the workbook. Nothing in it changes once it is
/// published (<see cref="SheetViewSlot"/>), so any number of workers read it at once; the arrays are its own copies, and
/// the objects in them are read only through what never changes (a column's format and culture, its style's format and
/// wrap): each worker keeps its own caches over them.
/// </summary>
internal sealed class SheetView
{
	/// <summary>The workbook's options: <see cref="ExcelWriterOptions.LongNumbers"/>, <see cref="ExcelWriterOptions.DateTimeOffsets"/>, <see cref="ExcelWriterOptions.OutOfRangeDates"/> and <see cref="ExcelWriterOptions.Links"/>.</summary>
	public ExcelWriterOptions Options { get; init; } = ExcelWriterOptions.Default;

	/// <summary>The writer's culture's signs and separator, or <see langword="null"/> for the invariant ones.</summary>
	public NumberCulture? Culture { get; init; }

	/// <summary>Each column's member's format and culture, or <see langword="null"/> when no column has one: the sink's, copied.</summary>
	public ColumnFormat?[]? ColumnFormats { get; init; }

	/// <summary>Each column's style below the header, or <see langword="null"/> when no column has one: the sink's, copied.</summary>
	public ColumnStyle?[]? ColumnStyles { get; init; }

	/// <summary>Each column's default style (0 for none), or <see langword="null"/> when no column has one: copied.</summary>
	public int[]? ColumnDefaults { get; init; }

	/// <summary>The header's columns, whose cells below it are unlocked; 0 without <see cref="ExcelSheetOptions.Protect"/>.</summary>
	public int UnlockedColumns { get; init; }

	/// <summary>Whether a cell's style is more than its own number format below the header (the sink's <c>styling</c> there).</summary>
	public bool Styling { get; init; }

	/// <summary>Whether a cell below the header without a style of its own takes its column's start (the sink's <c>stylesColumns</c> there).</summary>
	public bool StylesColumns { get; init; }

	/// <summary>Each column's start of a cell without a style of its own, as the sink had made them (<c>null</c> for those it hadn't); or <see langword="null"/>: copied.</summary>
	public byte[]?[]? ColumnStarts { get; init; }

	/// <summary>Each column's placeholder below the header, as the sink had made them (<c>null</c> for those it hadn't); or <see langword="null"/>: copied.</summary>
	public byte[]?[]? Placeholders { get; init; }

	/// <summary>The workbook's cell formats as they were, whose lookups add nothing.</summary>
	public StyleSnapshot Styles { get; init; } = null!;

	/// <summary>The cell format of a link's cell, or 0 when no link has been written in the workbook yet (a link's chunk is then written again serially).</summary>
	public int LinkStyle { get; init; }

	/// <summary>Whether the sheet has as many links as Excel opens, so that a URI is text without one.</summary>
	public bool LinksFull { get; init; }

	/// <summary>Each column's last type, the seed of a worker's own cache (<c>null</c> for none): copied.</summary>
	public Type?[] ColumnTypes { get; init; } = [];

	/// <summary>The kind of cell each of <see cref="ColumnTypes"/> makes: copied.</summary>
	public CellKind[] ColumnKinds { get; init; } = [];

	/// <summary>
	/// The length a worker reports for each chunk in place of its XML's (<see cref="CsvCellSink.TakeChunk"/>), which sizes
	/// the parallel write's batches: 0, its XML's, but in tests (<see cref="ExcelWorkbookWriter.ParallelChunkLength"/>),
	/// where a large one makes batches of one item and 1 makes them of 4,096.
	/// </summary>
	public int ChunkLength { get; init; }

	/// <summary>The view's number in its parallel write, from 1: a worker rebuilds its caches when the view it reads has another.</summary>
	public int Version { get; init; }
}

/// <summary>
/// The view a parallel write's workers read (<see cref="SheetView"/>): the owner publishes each one on the writing thread
/// before the workers start and after a refreeze, and a worker reads the latest as each chunk begins.
/// </summary>
internal sealed class SheetViewSlot
{
	/// <summary>The view, or <see langword="null"/> before the first.</summary>
	private SheetView? view;

	/// <summary>The latest view.</summary>
	/// <exception cref="InvalidOperationException">No view has been published.</exception>
	public SheetView View => Volatile.Read(ref view) ?? throw new InvalidOperationException("The parallel write's view isn't frozen yet.");

	/// <summary>Publishes a view, which workers read from their next chunk on.</summary>
	/// <param name="view">The view.</param>
	public void Publish(SheetView view) => Volatile.Write(ref this.view, view);
}
