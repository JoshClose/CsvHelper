using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>A record a <see cref="SheetScanner"/> wrote: its row and its fields.</summary>
/// <param name="row">The row it ended with.</param>
/// <param name="fields">The text and kind of each field.</param>
/// <param name="styles">The style of each field under <see cref="ExcelText.Formatted"/>, or -1 for none.</param>
internal sealed class CapturedRecord(long row, List<(string Text, SheetCellKind Kind)> fields, List<int> styles)
{
	/// <summary>Creates a record whose fields have no style.</summary>
	/// <param name="row">The row it ended with.</param>
	/// <param name="fields">The text and kind of each field.</param>
	public CapturedRecord(long row, List<(string Text, SheetCellKind Kind)> fields)
		: this(row, fields, [.. fields.Select(_ => -1)])
	{
	}

	/// <summary>The row the record ended with.</summary>
	public long Row => row;

	/// <summary>The text and kind of each field.</summary>
	public List<(string Text, SheetCellKind Kind)> Fields => fields;

	/// <summary>The style of each field under <see cref="ExcelText.Formatted"/>, or -1 for none.</summary>
	public List<int> Styles => styles;

	/// <summary>The record with its styles: as <see cref="ToString"/>, each field with a style followed by <c>@</c> and the style.</summary>
	/// <returns>The description.</returns>
	public string Styled() => $"{row}: {string.Join(" | ", fields.Select((f, i) => styles[i] < 0 ? Describe(f) : $"{Describe(f)}@{styles[i]}"))}";

	/// <summary>
	/// The record as the tests compare it: <c>row: field | field</c>, where a text field is its text, an empty field is
	/// <c>()</c>, and any other field is <c>kind:text</c>, such as <c>number:1.5</c>.
	/// </summary>
	/// <returns>The description.</returns>
	public override string ToString() => $"{row}: {string.Join(" | ", fields.Select(Describe))}";

	/// <summary>Describes a field.</summary>
	/// <param name="field">The field.</param>
	/// <returns>Its description.</returns>
	public static string Describe((string Text, SheetCellKind Kind) field) => field.Kind switch
	{
		SheetCellKind.Text => field.Text,
		SheetCellKind.Empty => field.Text.Length == 0 ? "()" : $"(empty:{field.Text})",
		SheetCellKind.Number => $"number:{field.Text}",
		SheetCellKind.DateNumber => $"date:{field.Text}",
		SheetCellKind.Bool => $"bool:{field.Text}",
		SheetCellKind.Error => $"error:{field.Text}",
		SheetCellKind.Duration => $"duration:{field.Text}",
		_ => $"{field.Kind}:{field.Text}",
	};
}

/// <summary>
/// Keeps what a <see cref="SheetScanner"/> writes through <see cref="CapturingTarget"/>, and checks that it keeps the
/// target's rules: whole records in each fill, no call between <see cref="GetFieldSpan"/> and its commit, no value longer
/// than the room it asked for, no style out of range, and no more records in a fill than it takes. A segment's block
/// (<see cref="AppendBlock"/>) is appended whole, between records, into a fill that isn't full: the fill then ends after it,
/// however many records it has.
/// </summary>
/// <param name="recordsPerFill">The number of records after which the target is full.</param>
internal sealed class Captured(int recordsPerFill)
{
	private List<(string Text, SheetCellKind Kind)> fields = [];
	private List<int> styles = [];
	private byte[]? room;
	private int recordsThisFill;
	private bool blockThisFill;

	/// <summary>The records, in order.</summary>
	public List<CapturedRecord> Records { get; } = [];

	/// <summary>The number of fills.</summary>
	public int Fills { get; private set; }

	/// <summary>Whether the fill has written as many records as it takes.</summary>
	public bool IsFull => recordsThisFill >= recordsPerFill;

	/// <summary>The number of segments' blocks appended (<see cref="AppendBlock"/>).</summary>
	public int Blocks { get; private set; }

	/// <summary>Starts a fill.</summary>
	public void StartFill()
	{
		recordsThisFill = 0;
		blockThisFill = false;
		Fills++;
	}

	/// <summary>
	/// Checks a fill that returned: whole records, at least one when more are to come, and no more than it takes, unless a
	/// block was appended, which is appended whole (and only into a fill that isn't full).
	/// </summary>
	/// <param name="more">What the fill returned.</param>
	public void EndFill(bool more)
	{
		Assert.True(fields.Count == 0 && room is null, "A fill left a record unended.");
		if (!blockThisFill)
		{
			Assert.True(recordsThisFill <= recordsPerFill, $"A fill wrote {recordsThisFill} records, past the {recordsPerFill} it takes.");
		}

		if (more)
		{
			Assert.True(recordsThisFill > 0, "A fill that said more records are to come wrote none.");
		}
	}

	/// <summary>
	/// Appends a segment's records, replaying each field and record as a scanner would have written them, with each line plus
	/// <paramref name="lineOffset"/>; and checks the block's form (<see cref="SegmentBlock"/>) and that it comes between
	/// records, into a fill that isn't full.
	/// </summary>
	/// <param name="block">The records.</param>
	/// <param name="lineOffset">Added to each record's line.</param>
	public void AppendBlock(SegmentBlock block, long lineOffset)
	{
		Assert.True(fields.Count == 0 && room is null, "A block appended inside a record.");
		Assert.False(IsFull, "A block appended into a full fill.");
		var bytes = block.Bytes;
		var ends = block.Ends;
		var kinds = block.Kinds;
		var blockStyles = block.Styles;
		var records = block.Records;
		var lines = block.Lines;
		Assert.Equal(ends.Length, kinds.Length);
		Assert.True(blockStyles.IsEmpty || blockStyles.Length == ends.Length, "A block's styles are neither none nor one a field.");
		Assert.Equal(records.Length, lines.Length);
		Assert.Equal(records.Length == 0 ? 0 : ends.Length, records.Length == 0 ? 0 : records[records.Length - 1]);
		var field = 0;
		for (var record = 0; record < records.Length; record++)
		{
			Assert.True(records[record] > field, "A block's record has no field.");
			for (; field < records[record]; field++)
			{
				var start = field == 0 ? 0 : ends[field - 1] + 1;
				AddField(bytes.Slice(start, ends[field] - start), kinds[field], blockStyles.IsEmpty ? -1 : blockStyles[field]);
			}

			EndRecord(lines[record] + lineOffset);
		}

		blockThisFill = true;
		Blocks++;
	}

	/// <summary>Adds a field.</summary>
	/// <param name="utf8">The value.</param>
	/// <param name="kind">The kind.</param>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => AddField(utf8, kind, -1);

	/// <summary>Adds a field with a style, or -1 for none.</summary>
	/// <param name="utf8">The value.</param>
	/// <param name="kind">The kind.</param>
	/// <param name="style">The style.</param>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
	{
		CheckNoRoom();
		CheckStyle(style);
		fields.Add((Encoding.UTF8.GetString(utf8.ToArray()), kind));
		styles.Add(style);
	}

	/// <summary>Gives exactly the room asked for, so that a value longer than its hint fails.</summary>
	/// <param name="sizeHint">The room asked for.</param>
	/// <returns>The room.</returns>
	public Span<byte> GetFieldSpan(int sizeHint)
	{
		CheckNoRoom();
		Assert.True(sizeHint >= 0);
		room = new byte[sizeHint];

		return room;
	}

	/// <summary>Adds the field written into the room.</summary>
	/// <param name="length">The value's length.</param>
	/// <param name="kind">The kind.</param>
	public void CommitField(int length, SheetCellKind kind) => CommitField(length, kind, -1);

	/// <summary>Adds the field written into the room, with a style, or -1 for none.</summary>
	/// <param name="length">The value's length.</param>
	/// <param name="kind">The kind.</param>
	/// <param name="style">The style.</param>
	public void CommitField(int length, SheetCellKind kind, int style)
	{
		Assert.True(room is not null, "CommitField without GetFieldSpan.");
		Assert.InRange(length, 0, room!.Length);
		CheckStyle(style);
		fields.Add((Encoding.UTF8.GetString(room, 0, length), kind));
		styles.Add(style);
		room = null;
	}

	/// <summary>Ends the record.</summary>
	/// <param name="row">Its row.</param>
	public void EndRecord(long row)
	{
		CheckNoRoom();
		if (fields.Count == 0)
		{
			fields.Add(("", SheetCellKind.Empty));
			styles.Add(-1);
		}

		Records.Add(new CapturedRecord(row, fields, styles));
		fields = [];
		styles = [];
		recordsThisFill++;
	}

	/// <summary>Fails when a call comes between <see cref="GetFieldSpan"/> and its commit.</summary>
	private void CheckNoRoom() => Assert.True(room is null, "A call between GetFieldSpan and CommitField.");

	/// <summary>Fails for a style that is neither -1 nor one core takes: up to 2^23 − 1, core's <c>CsvCellFormatter.MaxStyle</c>.</summary>
	/// <param name="style">The style.</param>
	private static void CheckStyle(int style) => Assert.InRange(style, -1, (1 << 23) - 1);
}

/// <summary>
/// The <see cref="IRecordTarget"/> of the tests: a struct over <see cref="Captured"/>, as the reader's is over its batch. It
/// takes segments' blocks too (<see cref="IBlockTarget"/>), replaying their fields, so a test sees the same records either way.
/// </summary>
/// <param name="captured">What it keeps the records in.</param>
internal readonly struct CapturingTarget(Captured captured) : IBlockTarget
{
	/// <inheritdoc/>
	public bool IsFull => captured.IsFull;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => captured.AddField(utf8, kind);

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
	{
		Assert.True(style >= 0, "A field with a style of -1 given as styled.");
		captured.AddField(utf8, kind, style);
	}

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint) => captured.GetFieldSpan(sizeHint);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind) => captured.CommitField(length, kind);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind, int style)
	{
		Assert.True(style >= 0, "A field with a style of -1 given as styled.");
		captured.CommitField(length, kind, style);
	}

	/// <inheritdoc/>
	public void EndRecord(long row) => captured.EndRecord(row);

	/// <inheritdoc/>
	public void AppendBlock(SegmentBlock block, long lineOffset) => captured.AppendBlock(block, lineOffset);
}
