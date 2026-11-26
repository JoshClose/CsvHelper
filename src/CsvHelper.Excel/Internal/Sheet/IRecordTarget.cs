using System;

namespace CsvHelper.Excel;

/// <summary>
/// Where <see cref="SheetScanner"/> writes the records of a sheet: the reader's batch (<see cref="RecordBatchTarget"/>,
/// over <c>CsvReader.RecordBatch</c>), or a test's capturing target. Implemented by structs and passed as a generic
/// parameter, so that the JIT specializes the scanner for each and nothing is dispatched through the interface.
/// </summary>
/// <remarks>
/// The scanner adds the fields of a record in order, then ends it; it never leaves a record unended when a fill returns.
/// Between <see cref="GetFieldSpan"/> and its <see cref="CommitField(int, SheetCellKind)"/> (or the one with a style), it
/// makes no other call.
/// </remarks>
internal interface IRecordTarget
{
	/// <summary>Whether the target holds as much as it takes at once: the scanner ends its fill after the record it is writing.</summary>
	bool IsFull { get; }

	/// <summary>Adds a field to the current record, copying its value.</summary>
	/// <param name="utf8">The value, as UTF-8.</param>
	/// <param name="kind">What the field holds.</param>
	void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind);

	/// <summary>
	/// Adds a field to the current record, copying its value, with the style its text shows in under
	/// <see cref="ExcelText.Formatted"/> (<see cref="CellFormatter.StyleOf"/>).
	/// </summary>
	/// <param name="utf8">The value, as UTF-8.</param>
	/// <param name="kind">What the field holds.</param>
	/// <param name="style">The field's style, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>.</param>
	void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style);

	/// <summary>
	/// Gets room for the next field's value, for the scanner to decode it into; <see cref="CommitField(int, SheetCellKind)"/>
	/// or <see cref="CommitField(int, SheetCellKind, int)"/> adds it.
	/// </summary>
	/// <param name="sizeHint">The most bytes the value can take.</param>
	/// <returns>At least <paramref name="sizeHint"/> bytes, valid until the next call.</returns>
	Span<byte> GetFieldSpan(int sizeHint);

	/// <summary>Adds the field whose value the scanner wrote at the start of <see cref="GetFieldSpan"/>'s span.</summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	void CommitField(int length, SheetCellKind kind);

	/// <summary>
	/// Adds the field whose value the scanner wrote at the start of <see cref="GetFieldSpan"/>'s span, with the style its
	/// text shows in under <see cref="ExcelText.Formatted"/> (<see cref="CellFormatter.StyleOf"/>).
	/// </summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	/// <param name="style">The field's style, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>.</param>
	void CommitField(int length, SheetCellKind kind, int style);

	/// <summary>Ends the current record; a record without fields gets one empty field.</summary>
	/// <param name="row">The record's row in the sheet, 1-based: its line.</param>
	void EndRecord(long row);
}

/// <summary>
/// The <see cref="IRecordTarget"/> of a reader: a struct over its <see cref="CsvReader.RecordBatch"/>, converting each
/// <see cref="SheetCellKind"/> to the <see cref="CsvCellKind"/> of the same value. It takes segments' blocks whole too
/// (<see cref="IBlockTarget"/>), through the batch's bulk append.
/// </summary>
internal readonly struct RecordBatchTarget : IBlockTarget
{
	/// <summary>
	/// 1 when a block's field without a style holds the batch's value for one. Otherwise the division by zero is a compile
	/// error, so the build breaks as soon as the two differ (the kinds are checked by <see cref="SheetCellKinds"/>).
	/// </summary>
	private const int SameNoStyle = 1 / (SegmentBlock.NoStyle == CsvReader.NoStyle ? 1 : 0);

	private readonly CsvReader.RecordBatch batch;

	/// <summary>Creates the target.</summary>
	/// <param name="batch">The reader's batch.</param>
	public RecordBatchTarget(CsvReader.RecordBatch batch) => this.batch = batch;

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
	public void EndRecord(long row) => batch.EndRecord(row);

	/// <inheritdoc/>
	/// <remarks>Through the batch's bulk append (<c>RecordBatch.AppendRecords</c>): one copy of the block's bytes, then one pass over its fields.</remarks>
	public void AppendBlock(SegmentBlock block, long lineOffset)
	{
		// The kinds are CsvHelper's byte for byte (SheetCellKinds). Named in full: on .NET Standard and .NET Framework the
		// namespace finds core's MemoryMarshal stand-in first, which has no Cast.
		var kinds = System.Runtime.InteropServices.MemoryMarshal.Cast<SheetCellKind, CsvCellKind>(block.Kinds);
		batch.AppendRecords(block.Bytes, block.Ends, kinds, block.Styles, block.Records, block.Lines, lineOffset);
	}
}
