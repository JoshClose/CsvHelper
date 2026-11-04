using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>A record a <see cref="SheetScanner"/> wrote: its row and its fields.</summary>
/// <param name="row">The row it ended with.</param>
/// <param name="fields">The text and kind of each field.</param>
internal sealed class CapturedRecord(long row, List<(string Text, SheetCellKind Kind)> fields)
{
	/// <summary>The row the record ended with.</summary>
	public long Row => row;

	/// <summary>The text and kind of each field.</summary>
	public List<(string Text, SheetCellKind Kind)> Fields => fields;

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
/// target's rules: whole records in each fill, no call between <see cref="GetFieldSpan"/> and its
/// <see cref="CommitField"/>, no value longer than the room it asked for, and no more records in a fill than it takes.
/// </summary>
/// <param name="recordsPerFill">The number of records after which the target is full.</param>
internal sealed class Captured(int recordsPerFill)
{
	private List<(string Text, SheetCellKind Kind)> fields = [];
	private byte[]? room;
	private int recordsThisFill;

	/// <summary>The records, in order.</summary>
	public List<CapturedRecord> Records { get; } = [];

	/// <summary>The number of fills.</summary>
	public int Fills { get; private set; }

	/// <summary>Whether the fill has written as many records as it takes.</summary>
	public bool IsFull => recordsThisFill >= recordsPerFill;

	/// <summary>Starts a fill.</summary>
	public void StartFill()
	{
		recordsThisFill = 0;
		Fills++;
	}

	/// <summary>Checks a fill that returned: whole records, at least one when more are to come, and no more than it takes.</summary>
	/// <param name="more">What the fill returned.</param>
	public void EndFill(bool more)
	{
		Assert.True(fields.Count == 0 && room is null, "A fill left a record unended.");
		Assert.True(recordsThisFill <= recordsPerFill, $"A fill wrote {recordsThisFill} records, past the {recordsPerFill} it takes.");
		if (more)
		{
			Assert.True(recordsThisFill > 0, "A fill that said more records are to come wrote none.");
		}
	}

	/// <summary>Adds a field.</summary>
	/// <param name="utf8">The value.</param>
	/// <param name="kind">The kind.</param>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
	{
		CheckNoRoom();
		fields.Add((Encoding.UTF8.GetString(utf8.ToArray()), kind));
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
	public void CommitField(int length, SheetCellKind kind)
	{
		Assert.True(room is not null, "CommitField without GetFieldSpan.");
		Assert.InRange(length, 0, room!.Length);
		fields.Add((Encoding.UTF8.GetString(room, 0, length), kind));
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
		}

		Records.Add(new CapturedRecord(row, fields));
		fields = [];
		recordsThisFill++;
	}

	/// <summary>Fails when a call comes between <see cref="GetFieldSpan"/> and its <see cref="CommitField"/>.</summary>
	private void CheckNoRoom() => Assert.True(room is null, "A call between GetFieldSpan and CommitField.");
}

/// <summary>The <see cref="IRecordTarget"/> of the tests: a struct over <see cref="Captured"/>, as the reader's is over its batch.</summary>
/// <param name="captured">What it keeps the records in.</param>
internal readonly struct CapturingTarget(Captured captured) : IRecordTarget
{
	/// <inheritdoc/>
	public bool IsFull => captured.IsFull;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => captured.AddField(utf8, kind);

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint) => captured.GetFieldSpan(sizeHint);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind) => captured.CommitField(length, kind);

	/// <inheritdoc/>
	public void EndRecord(long row) => captured.EndRecord(row);
}
