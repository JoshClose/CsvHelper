using System;

namespace CsvHelper.Excel;

/// <summary>
/// Where <see cref="SheetScanner"/> writes the records of a sheet: the reader's batch (<see cref="RecordBatchTarget"/>,
/// over <c>CsvReader.RecordBatch</c>), or a test's capturing target. Implemented by structs and passed as a generic
/// parameter, so that the JIT specializes the scanner for each and nothing is dispatched through the interface.
/// </summary>
/// <remarks>
/// The scanner adds the fields of a record in order, then ends it; it never leaves a record unended when a fill returns.
/// Between <see cref="GetFieldSpan"/> and its <see cref="CommitField"/>, it makes no other call.
/// </remarks>
internal interface IRecordTarget
{
	/// <summary>Whether the target holds as much as it takes at once: the scanner ends its fill after the record it is writing.</summary>
	bool IsFull { get; }

	/// <summary>Adds a field to the current record, copying its value.</summary>
	/// <param name="utf8">The value, as UTF-8.</param>
	/// <param name="kind">What the field holds.</param>
	void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind);

	/// <summary>Gets room for the next field's value, for the scanner to decode it into; <see cref="CommitField"/> adds it.</summary>
	/// <param name="sizeHint">The most bytes the value can take.</param>
	/// <returns>At least <paramref name="sizeHint"/> bytes, valid until the next call.</returns>
	Span<byte> GetFieldSpan(int sizeHint);

	/// <summary>Adds the field whose value the scanner wrote at the start of <see cref="GetFieldSpan"/>'s span.</summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	void CommitField(int length, SheetCellKind kind);

	/// <summary>Ends the current record; a record without fields gets one empty field.</summary>
	/// <param name="row">The record's row in the sheet, 1-based: its line.</param>
	void EndRecord(long row);
}

/// <summary>
/// The <see cref="IRecordTarget"/> of a reader: a struct over its <see cref="CsvReader.RecordBatch"/>, converting each
/// <see cref="SheetCellKind"/> to the <see cref="CsvCellKind"/> of the same value.
/// </summary>
internal readonly struct RecordBatchTarget : IRecordTarget
{
	private readonly CsvReader.RecordBatch batch;

	/// <summary>Creates the target.</summary>
	/// <param name="batch">The reader's batch.</param>
	public RecordBatchTarget(CsvReader.RecordBatch batch) => this.batch = batch;

	/// <inheritdoc/>
	public bool IsFull => batch.IsFull;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => batch.AddField(utf8, kind.ToCsv());

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint) => batch.GetFieldSpan(sizeHint);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind) => batch.CommitField(length, kind.ToCsv());

	/// <inheritdoc/>
	public void EndRecord(long row) => batch.EndRecord(row);
}
