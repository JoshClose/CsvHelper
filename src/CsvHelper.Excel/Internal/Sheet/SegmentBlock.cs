using System;

namespace CsvHelper.Excel;

/// <summary>
/// The records a segment's scan writes on a thread of its own, apart from the reader, in the form the reader's batch
/// appends them in one call (<see cref="IBlockTarget.AppendBlock"/>): each field's value followed by one byte, the end of
/// each field, its kind and its style, and the end and line of each record. The pipeline keeps one per slot of its ring
/// and clears it before each segment, so its arrays are reused from one segment to the next.
/// </summary>
/// <remarks>
/// <para>
/// Field <c>i</c>'s value is <c>Bytes[start .. Ends[i]]</c>, where <c>start</c> is 0 for the first field and
/// <c>Ends[i - 1] + 1</c> after it: the byte at <c>Ends[i]</c> follows the value, and the append sets it to the reader's
/// separator, or to LF after a record's last field. Record <c>r</c> is the fields from <c>Records[r - 1]</c> (0 for the
/// first) up to <c>Records[r]</c>, at line <c>Lines[r]</c>. Every record has a field: one ended without any gets an empty
/// one, as the reader's batch gives it.
/// </para>
/// <para>
/// A block is bounded: when its bytes plus four for each field would pass its limit (<see cref="SegmentSizes.MaxBlock"/>),
/// it throws <see cref="SegmentMarkException"/> with <see cref="SegmentMark.BlockCap"/>, which marks its segment, so that
/// a few bytes of XML (a row with a value at <c>XFD</c>, repeated) can't make a worker hold gigabytes.
/// </para>
/// </remarks>
internal sealed class SegmentBlock
{
	/// <summary>A field without a style, as <see cref="Styles"/> holds it.</summary>
	public const int NoStyle = -1;

	private readonly long maxSize;
	private byte[] bytes;
	private int byteCount;
	private int[] ends;
	private SheetCellKind[] kinds;
	private int[] styles;
	private int fieldCount;
	private bool hasStyles;
	private int[] records;
	private long[] lines;
	private int recordCount;
	// The fields of the record being written.
	private int recordFields;

	/// <summary>Creates an empty block.</summary>
	/// <param name="maxSize">The most its bytes plus four for each field may take: <see cref="SegmentSizes.MaxBlock"/>, or less in tests.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> is 0 or less.</exception>
	public SegmentBlock(long maxSize = SegmentSizes.MaxBlock)
	{
		if (maxSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "The limit must be more than 0.");
		}

		this.maxSize = maxSize;
		bytes = new byte[4096];
		ends = new int[256];
		kinds = new SheetCellKind[256];
		styles = [];
		records = new int[64];
		lines = new long[64];
	}

	/// <summary>The fields' values, each followed by one byte.</summary>
	public ReadOnlySpan<byte> Bytes => bytes.AsSpan(0, byteCount);

	/// <summary>For each field, the offset in <see cref="Bytes"/> of the byte after its value.</summary>
	public ReadOnlySpan<int> Ends => ends.AsSpan(0, fieldCount);

	/// <summary>For each field, what it holds.</summary>
	public ReadOnlySpan<SheetCellKind> Kinds => kinds.AsSpan(0, fieldCount);

	/// <summary>For each field, its style or <see cref="NoStyle"/>; empty when no field has a style.</summary>
	public ReadOnlySpan<int> Styles => hasStyles ? styles.AsSpan(0, fieldCount) : default;

	/// <summary>For each record, the index in <see cref="Ends"/> after its last field.</summary>
	public ReadOnlySpan<int> Records => records.AsSpan(0, recordCount);

	/// <summary>For each record, its line: its row, from the segment's start when the segment floats (<see cref="SegmentResult.FirstRow"/>).</summary>
	public ReadOnlySpan<long> Lines => lines.AsSpan(0, recordCount);

	/// <summary>The number of records.</summary>
	public int RecordCount => recordCount;

	/// <summary>The number of fields, of the records and of the one being written.</summary>
	public int FieldCount => fieldCount;

	/// <summary>The number of bytes, of the records and of the one being written.</summary>
	public int ByteCount => byteCount;

	/// <summary>What the block's limit bounds: its bytes plus four for each field.</summary>
	public long Size => byteCount + 4L * fieldCount;

	/// <summary>Empties the block, keeping its arrays.</summary>
	public void Clear()
	{
		byteCount = 0;
		fieldCount = 0;
		recordCount = 0;
		recordFields = 0;
		hasStyles = false;
	}

	/// <summary>Adds a field to the current record, copying its value.</summary>
	/// <param name="utf8">The value, as UTF-8.</param>
	/// <param name="kind">What the field holds.</param>
	/// <exception cref="SegmentMarkException">The block would pass its limit.</exception>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
	{
		utf8.CopyTo(GetFieldSpan(utf8.Length));
		CommitField(utf8.Length, kind);
	}

	/// <summary>Adds a field to the current record, copying its value, with a style.</summary>
	/// <param name="utf8">The value, as UTF-8.</param>
	/// <param name="kind">What the field holds.</param>
	/// <param name="style">The field's style, 0 or more.</param>
	/// <exception cref="SegmentMarkException">The block would pass its limit.</exception>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
	{
		utf8.CopyTo(GetFieldSpan(utf8.Length));
		CommitField(utf8.Length, kind, style);
	}

	/// <summary>Gets room for the next field's value, which <see cref="CommitField(int, SheetCellKind)"/> then adds.</summary>
	/// <param name="sizeHint">The most bytes the value can take.</param>
	/// <returns>At least <paramref name="sizeHint"/> bytes, valid until the next call.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint"/> is negative.</exception>
	/// <exception cref="SegmentMarkException">The block would pass its limit.</exception>
	public Span<byte> GetFieldSpan(int sizeHint)
	{
		if (sizeHint < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "The size can't be negative.");
		}

		// The value, the byte after it, and the field's four.
		if (byteCount + (long)sizeHint + 1 + 4L * (fieldCount + 1) > maxSize)
		{
			throw new SegmentMarkException(SegmentMark.BlockCap);
		}

		if (bytes.Length - byteCount <= sizeHint)
		{
			Array.Resize(ref bytes, (int)Math.Min(Math.Max((long)bytes.Length * 2, (long)byteCount + sizeHint + 1), Array.MaxLength));
		}

		return bytes.AsSpan(byteCount, bytes.Length - byteCount - 1);
	}

	/// <summary>Adds the field whose value was written at the start of <see cref="GetFieldSpan"/>'s span.</summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or more than the span had room for.</exception>
	public void CommitField(int length, SheetCellKind kind) => Commit(length, kind, NoStyle);

	/// <summary>Adds the field whose value was written at the start of <see cref="GetFieldSpan"/>'s span, with a style.</summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	/// <param name="style">The field's style, 0 or more.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="length"/> is negative or more than the span had room for, or <paramref name="style"/> is negative.
	/// </exception>
	public void CommitField(int length, SheetCellKind kind, int style)
	{
		if (style < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(style), style, "A style can't be negative.");
		}

		if (!hasStyles)
		{
			StartStyles();
		}

		Commit(length, kind, style);
	}

	/// <summary>Ends the current record; a record without fields gets one empty field.</summary>
	/// <param name="line">The record's line: its row, absolute or from the segment's start.</param>
	/// <exception cref="SegmentMarkException">The block would pass its limit.</exception>
	public void EndRecord(long line)
	{
		if (recordFields == 0)
		{
			AddField(default, SheetCellKind.Empty);
		}

		if (recordCount == records.Length)
		{
			Array.Resize(ref records, records.Length * 2);
			Array.Resize(ref lines, lines.Length * 2);
		}

		records[recordCount] = fieldCount;
		lines[recordCount] = line;
		recordCount++;
		recordFields = 0;
	}

	/// <summary>Adds a field: its end, kind and style.</summary>
	/// <param name="length">The number of bytes of the value.</param>
	/// <param name="kind">What the field holds.</param>
	/// <param name="style">The field's style, or <see cref="NoStyle"/>.</param>
	private void Commit(int length, SheetCellKind kind, int style)
	{
		if ((uint)length >= (uint)(bytes.Length - byteCount))
		{
			throw new ArgumentOutOfRangeException(nameof(length), length, "The field is longer than the room that GetFieldSpan gave.");
		}

		if (fieldCount == ends.Length)
		{
			Array.Resize(ref ends, ends.Length * 2);
			Array.Resize(ref kinds, ends.Length);
			if (hasStyles)
			{
				Array.Resize(ref styles, ends.Length);
			}
		}

		var end = byteCount + length;
		ends[fieldCount] = end;
		kinds[fieldCount] = kind;
		if (hasStyles)
		{
			styles[fieldCount] = style;
		}

		fieldCount++;
		byteCount = end + 1;
		recordFields++;
	}

	/// <summary>Starts keeping styles, at the first field with one: the fields before it have none.</summary>
	private void StartStyles()
	{
		if (styles.Length < ends.Length)
		{
			styles = new int[ends.Length];
		}

		styles.AsSpan(0, fieldCount).Fill(NoStyle);
		hasStyles = true;
	}
}

/// <summary>
/// The <see cref="IRecordTarget"/> of a segment's scan: a struct over its <see cref="SegmentBlock"/>. It is never full, so
/// the scan writes the whole segment in one call; the block's limit bounds it instead.
/// </summary>
internal readonly struct BlockTarget : IRecordTarget
{
	private readonly SegmentBlock block;

	/// <summary>Creates the target.</summary>
	/// <param name="block">The block written.</param>
	public BlockTarget(SegmentBlock block) => this.block = block;

	/// <inheritdoc/>
	public bool IsFull => false;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => block.AddField(utf8, kind);

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style) => block.AddField(utf8, kind, style);

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint) => block.GetFieldSpan(sizeHint);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind) => block.CommitField(length, kind);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind, int style) => block.CommitField(length, kind, style);

	/// <inheritdoc/>
	public void EndRecord(long row) => block.EndRecord(row);
}

/// <summary>
/// An <see cref="IRecordTarget"/> that also takes a segment's records whole: the reader's batch
/// (<see cref="RecordBatchTarget"/>, through <c>CsvReader.RecordBatch.AppendRecords</c>), or a test's capturing target.
/// </summary>
internal interface IBlockTarget : IRecordTarget
{
	/// <summary>
	/// Appends every record of <paramref name="block"/>, as writing their fields one at a time would, with each record's
	/// line plus <paramref name="lineOffset"/>. The target holds no record being written.
	/// </summary>
	/// <param name="block">The records.</param>
	/// <param name="lineOffset">Added to each record's line: the row before a floating segment's first, else 0.</param>
	void AppendBlock(SegmentBlock block, long lineOffset);
}
