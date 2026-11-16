using System.Data;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>A line read from styled cells: text and numbers whose text a formatter gives.</summary>
[CsvRecord]
public partial class StyledLine
{
	/// <summary>A text cell, read as its text.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell, read as its value.</summary>
	public double Amount { get; set; }

	/// <summary>A number cell, read as its text.</summary>
	public string Shown { get; set; } = "";

	/// <summary>A boolean cell, read as its value.</summary>
	public bool Paid { get; set; }
}

/// <summary>A cell of a <see cref="StyledRecordSource"/>.</summary>
/// <param name="Text">The stored value.</param>
/// <param name="Kind">What the cell holds.</param>
/// <param name="Style">The cell's style, or <see cref="CsvReader.NoStyle"/>.</param>
internal readonly record struct StyledCell(string Text, CsvCellKind Kind, int Style = CsvReader.NoStyle);

/// <summary>A row of a <see cref="StyledRecordSource"/>: its line and its cells.</summary>
/// <param name="Line">The 1-based row number.</param>
/// <param name="Cells">The cells.</param>
internal sealed record StyledRow(long Line, params StyledCell[] Cells);

/// <summary>
/// A record source over rows in memory that gives its fields styles: a few rows a fill; half of the fields through
/// <see cref="CsvReader.RecordBatch.AddField(ReadOnlySpan{byte}, CsvCellKind, int)"/> and half through
/// <see cref="CsvReader.RecordBatch.GetFieldSpan"/> and <see cref="CsvReader.RecordBatch.CommitField(int, CsvCellKind, int)"/>,
/// or the overloads without a style for a field without one.
/// </summary>
internal sealed class StyledRecordSource : CsvRecordSource
{
	private readonly IReadOnlyList<StyledRow> rows;
	private readonly int rowsPerFill;
	private int next;

	/// <summary>Creates the source.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="formatter">The formatter of the styled fields, or <see langword="null"/> for none.</param>
	/// <param name="rowsPerFill">The most rows one fill writes (it also stops when the batch is full).</param>
	/// <param name="throwOnErrorCells">Whether reading an error cell fails for every target.</param>
	public StyledRecordSource(IReadOnlyList<StyledRow> rows, CsvCellFormatter? formatter, int rowsPerFill = int.MaxValue, bool throwOnErrorCells = false)
	{
		this.rows = rows;
		this.rowsPerFill = rowsPerFill;
		CellOptions = new CsvCellOptions { Formatter = formatter, ThrowOnErrorCells = throwOnErrorCells };
	}

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		var written = 0;
		while (next < rows.Count && written < rowsPerFill && !batch.IsFull)
		{
			var row = rows[next++];
			for (var i = 0; i < row.Cells.Length; i++)
			{
				var cell = row.Cells[i];
				var bytes = Encoding.UTF8.GetBytes(cell.Text);
				if (i % 2 == 0)
				{
					if (cell.Style == CsvReader.NoStyle)
					{
						batch.AddField(bytes, cell.Kind);
					}
					else
					{
						batch.AddField(bytes, cell.Kind, cell.Style);
					}
				}
				else
				{
					bytes.CopyTo(batch.GetFieldSpan(bytes.Length));
					if (cell.Style == CsvReader.NoStyle)
					{
						batch.CommitField(bytes.Length, cell.Kind);
					}
					else
					{
						batch.CommitField(bytes.Length, cell.Kind, cell.Style);
					}
				}
			}

			batch.EndRecord(row.Line);
			written++;
		}

		return next < rows.Count;
	}

	/// <inheritdoc/>
	public override async ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken)
	{
		await Task.Yield();

		return Fill(batch);
	}

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => $"Styled!{field}:{line}";

	/// <inheritdoc/>
	public override void Dispose()
	{
	}
}

/// <summary>A record source whose one fill is a function of the batch, to call the batch's methods as a test needs.</summary>
/// <param name="fill">Writes the records of the one fill.</param>
internal sealed class CallbackRecordSource(Action<CsvReader.RecordBatch> fill) : CsvRecordSource
{
	private bool filled;

	/// <summary>The formatter of the styled fields.</summary>
	public CsvCellFormatter? Formatter
	{
		init => CellOptions = new CsvCellOptions { Formatter = value };
	}

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		if (!filled)
		{
			filled = true;
			fill(batch);
		}

		return false;
	}

	/// <inheritdoc/>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken) => new(Fill(batch));

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => null;

	/// <inheritdoc/>
	public override void Dispose()
	{
	}
}

/// <summary>
/// A formatter whose text says which style it rendered: style 0 is the value in brackets; style 1 the field's own text;
/// style 2 600 <c>x</c> and the value, longer than the reader's stack; style 3 a euro sign and the value, which isn't
/// ASCII; others the kind, the value and the style. It counts its calls, and can ask for less room than it needs.
/// </summary>
internal sealed class MarkingFormatter : CsvCellFormatter
{
	private int calls;

	/// <summary>The number of calls of <see cref="TryFormat"/>, from any thread.</summary>
	public int Calls => Volatile.Read(ref calls);

	/// <summary>Whether it asks for one byte more than any room it is given, and never writes: a broken formatter.</summary>
	public bool Lies { get; init; }

	/// <inheritdoc/>
	public override bool TryFormat(ReadOnlySpan<byte> value, CsvCellKind kind, int style, Span<byte> destination, out int length)
	{
		Interlocked.Increment(ref calls);
		var stored = Encoding.UTF8.GetString(value.ToArray());
		var text = style switch
		{
			0 => "[" + stored + "]",
			1 => null,
			2 => new string('x', 600) + stored,
			3 => "€" + stored,
			_ => $"{kind}:{stored}:{style}",
		};
		if (text is null)
		{
			length = -1;

			return false;
		}

		var bytes = Encoding.UTF8.GetBytes(text);
		if (Lies || bytes.Length > destination.Length)
		{
			length = Lies ? destination.Length + 1 : bytes.Length;

			return false;
		}

		bytes.CopyTo(destination);
		length = bytes.Length;

		return true;
	}
}

/// <summary>
/// Core's styled fields (Phase 8's step 1): a record source's fields with a style read as their formatter's text wherever
/// text is read, and as their values wherever a type is.
/// </summary>
public class CellStyleTests
{
	private static StyledCell Text(string text, int style = CsvReader.NoStyle) => new(text, CsvCellKind.Text, style);

	private static StyledCell Number(string text, int style = CsvReader.NoStyle) => new(text, CsvCellKind.Number, style);

	private static StyledCell Bool(string text, int style = CsvReader.NoStyle) => new(text, CsvCellKind.Bool, style);

	/// <summary>A header and three lines: styles 0, 3 and 4 on the first; none, and the formatter's own text (style 1), on the second; long and non-ASCII text on the third.</summary>
	private static List<StyledRow> Lines() =>
	[
		new(1, Text("Name"), Text("Amount"), Text("Shown"), Text("Paid")),
		new(2, Text("Widget", 0), Number("1.5", 0), Number("1.5", 4), Bool("1", 0)),
		new(3, Text("Gadget"), Number("2.25"), Number("2.25", 1), Bool("0", 1)),
		new(5, Text("Gizmo", 3), Number("1234.5", 2), Number("1234.5", 3), Bool("1")),
	];

	private static void AssertLines(IReadOnlyList<StyledLine> lines)
	{
		Assert.Equal(["[Widget]", "Gadget", "€Gizmo"], lines.Select(l => l.Name));
		Assert.Equal([1.5, 2.25, 1234.5], lines.Select(l => l.Amount));
		Assert.Equal(["Number:1.5:4", "2.25", "€1234.5"], lines.Select(l => l.Shown));
		Assert.Equal([true, false, true], lines.Select(l => l.Paid));
	}

	[Fact]
	public void StyledFieldsReadAsTheirFormattersText()
	{
		var formatter = new MarkingFormatter();
		using var reader = new CsvReader(new StyledRecordSource(Lines(), formatter, rowsPerFill: 2), null);
		Assert.True(reader.Read());
		Assert.Equal("[Widget]", reader.GetString(0));
		Assert.Equal("[1.5]", reader.GetString(1));
		Assert.Equal("Number:1.5:4", reader[2].ToString());
		Assert.Equal("[1]", reader.GetString(3));

		// The stored value stays the field's bytes, and typed reads convert it, without the formatter.
		var calls = formatter.Calls;
		Assert.Equal("1.5"u8.ToArray(), reader.GetSpan(1).ToArray());
		Assert.Equal(1.5, reader.Parse<double>(1));
		Assert.Equal(1.5m, reader.Parse<decimal>(2));
		Assert.True(reader.Parse<bool>(3));
		Assert.Equal(calls, formatter.Calls);

		// Without a style, and with a style whose text is the field's own: as without styles.
		Assert.True(reader.Read());
		Assert.Equal("Gadget", reader.GetString(0));
		Assert.Equal("2.25", reader.GetString(1));
		Assert.Equal("2.25", reader.GetString(2));
		Assert.Equal("FALSE", reader.GetString(3));

		// Longer than the stack, and not ASCII.
		Assert.True(reader.Read());
		Assert.Equal("€Gizmo", reader.GetString(0));
		Assert.Equal(new string('x', 600) + "1234.5", reader.GetString(1));
		var chars = new char[700];
		Assert.Equal(606, reader.GetChars(1, chars));
		Assert.Equal(new string('x', 600) + "1234.5", new string(chars, 0, 606));
		Assert.True(reader.FieldEquals(0, Encoding.UTF8.GetBytes("€Gizmo")));
		Assert.False(reader.FieldEquals(0, "Gizmo"u8));
		Assert.True(reader.FieldEquals(1, Encoding.UTF8.GetBytes(new string('x', 600) + "1234.5")));
		Assert.Equal("TRUE", reader.GetString(3));
		Assert.Equal(1234.5, reader.Parse<double>(1));
	}

	[Fact]
	public void GeneratedMappersReadValuesAndStyledText()
	{
		using var reader = new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter(), rowsPerFill: 1), null);

		AssertLines(reader.GetRecords<StyledLine>().ToList());
	}

	[Fact]
	public async Task GeneratedMappersReadValuesAndStyledTextAsynchronously()
	{
		var lines = new List<StyledLine>();
		await using (var reader = new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter(), rowsPerFill: 1), null))
		{
			await foreach (var line in reader.GetRecordsAsync<StyledLine>())
			{
				lines.Add(line);
			}
		}

		AssertLines(lines);
	}

	[Fact]
	public void TextAsUtf8IsStyledInTheCallersBufferOrTheReaders()
	{
		using var reader = new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter()), null);
		Span<byte> room = stackalloc byte[CsvCells.MaxTextLength];
		Assert.True(reader.Read());
		Assert.Equal("[1.5]"u8.ToArray(), reader.GetTextUtf8(1, room, roundNumbers: true).ToArray());
		Assert.Equal("[1.5]"u8.ToArray(), room.Slice(0, 5).ToArray());
		Assert.True(reader.Read());
		Assert.Equal("2.25"u8.ToArray(), reader.GetTextUtf8(2, room, roundNumbers: true).ToArray());
		Assert.Equal("FALSE"u8.ToArray(), reader.GetTextUtf8(3, room, roundNumbers: true).ToArray());
		Assert.True(reader.Read());
		Assert.Equal(Encoding.UTF8.GetBytes(new string('x', 600) + "1234.5"), reader.GetTextUtf8(1, room, roundNumbers: true).ToArray());
		Assert.Equal(Encoding.UTF8.GetBytes("€1234.5"), reader.GetTextUtf8(2, room, roundNumbers: true).ToArray());
	}

	[Fact]
	public void HeaderNamesAreTheirStyledText()
	{
		var rows = new List<StyledRow>
		{
			new(1, Text("Name", 0), Number("2024", 4), Text("Total", 3)),
			new(2, Text("a"), Number("1"), Number("2")),
		};
		using var reader = new CsvReader(new StyledRecordSource(rows, new MarkingFormatter()), null);

		Assert.Equal(["[Name]", "Number:2024:4", "€Total"], reader.Headers);
		Assert.True(reader.Read());
		Assert.True(reader.TryGetFieldIndex("€Total", out var index));
		Assert.Equal(2, index);
	}

	[Fact]
	public void DynamicRecordsAndDataReadersReadStyledText()
	{
		List<CsvDynamicRecord> records;
		using (var reader = new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter(), rowsPerFill: 2), null))
		{
			records = reader.GetRecords<CsvDynamicRecord>().ToList();
		}

		Assert.Equal(["[Widget]", "Gadget", "€Gizmo"], records.Select(r => r["Name"]));
		Assert.Equal(["[1.5]", "2.25", new string('x', 600) + "1234.5"], records.Select(r => r["Amount"]));
		Assert.Equal([1.5, 2.25, 1234.5], records.Select(r => r.Parse<double>("Amount")));
		Assert.Equal(["Number:1.5:4", "2.25", "€1234.5"], records.Select(r => r.Detach()["Shown"]));
		Assert.Equal([true, false, true], records.Select(r => r.Detach().Parse<bool>("Paid")));
		Assert.Equal("[1]", records[0]["Paid"]);
		dynamic first = records[0];
		Assert.Equal("[Widget]", (string)first.Name);

		using var data = new CsvDataReader(new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter()), null));
		Assert.True(data.Read());
		Assert.Equal("[Widget]", data.GetString(0));
		Assert.Equal(1.5, data.GetDouble(1));
		Assert.Equal("Number:1.5:4", data.GetValue(2));
		Assert.True(data.GetBoolean(3));
	}

	[Fact]
	public void ParallelEnumerationCarriesStylesWithTheBatches()
	{
		// Styles start in the middle of a fill: the first 150 rows have none, and then every third row has one.
		var rows = new List<StyledRow> { new(1, Text("Id"), Text("Value")) };
		for (var i = 0; i < 5000; i++)
		{
			var style = i >= 150 && i % 3 == 0 ? 0 : CsvReader.NoStyle;
			rows.Add(new StyledRow(i + 2, Number(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), Number($"{i}.5", style)));
		}

		var formatter = new MarkingFormatter();
		using var reader = new CsvReader(new StyledRecordSource(rows, formatter, rowsPerFill: 97), null);
		var results = reader.ParallelEnumerate(r => (Id: r.Parse<int>(0), Value: r.Parse<double>(1), Text: r.GetString(1)), 4).ToList();

		Assert.Equal(Enumerable.Range(0, 5000), results.Select(r => r.Id));
		Assert.Equal(Enumerable.Range(0, 5000).Select(i => i + 0.5), results.Select(r => r.Value));
		Assert.Equal(Enumerable.Range(0, 5000).Select(i => i >= 150 && i % 3 == 0 ? $"[{i}.5]" : $"{i}.5"), results.Select(r => r.Text));
		Assert.Equal(Enumerable.Range(150, 4850).Count(i => i % 3 == 0), formatter.Calls);
	}

	[Fact]
	public void StylesStartAtAnyFieldAndGrowWithTheIndex()
	{
		// Records of 40,000 fields, past the index's first size (32,768 entries), so that it grows within a record: the
		// first half without styles, the rest with.
		const int Fields = 40000;
		var cells = Enumerable.Range(0, Fields).Select(k => Number(k.ToString(System.Globalization.CultureInfo.InvariantCulture), k >= Fields / 2 ? 0 : CsvReader.NoStyle)).ToArray();
		var rows = new List<StyledRow> { new(1, cells), new(2, cells) };
		using var reader = new CsvReader(new StyledRecordSource(rows, new MarkingFormatter()), new CsvReaderOptions { HasHeader = false });
		for (var r = 0; r < 2; r++)
		{
			Assert.True(reader.Read());
			Assert.Equal(Fields, reader.FieldCount);
			for (var k = 0; k < Fields; k++)
			{
				Assert.Equal(k >= Fields / 2 ? $"[{k}]" : $"{k}", reader.GetString(k));
			}
		}
	}

	[Fact]
	public void StylesMustBeInRange()
	{
		void Write(int style) => new CsvReader(new CallbackRecordSource(batch =>
		{
			"1"u8.CopyTo(batch.GetFieldSpan(1));
			batch.CommitField(1, CsvCellKind.Number, style);
			batch.EndRecord(1);
		}), new CsvReaderOptions { HasHeader = false }).Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => Write(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => Write(CsvCellFormatter.MaxStyle + 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new CsvReader(new CallbackRecordSource(batch => batch.AddField("1"u8, CsvCellKind.Number, -2)), null).Read());

		// The largest style goes through a dynamic record, kept with the kind.
		using var reader = new CsvReader(new CallbackRecordSource(batch =>
		{
			batch.AddField("1"u8, CsvCellKind.Number, CsvCellFormatter.MaxStyle);
			batch.AddField("1"u8, CsvCellKind.Bool, 0);
			batch.EndRecord(1);
		})
		{
			Formatter = new MarkingFormatter(),
		}, new CsvReaderOptions { HasHeader = false });
		var record = reader.GetRecords<CsvDynamicRecord>().Single();
		Assert.Equal($"Number:1:{CsvCellFormatter.MaxStyle}", record[0]);
		Assert.Equal(1, record.Detach().Parse<int>("Field1"));
		Assert.Equal("[1]", record[1]);
		Assert.True(record.Parse<bool>("Field2"));
	}

	[Fact]
	public void WithoutAFormatterStylesChangeNothing()
	{
		using var reader = new CsvReader(new StyledRecordSource(Lines(), formatter: null), null);
		Assert.True(reader.Read());
		Assert.Equal("Widget", reader.GetString(0));
		Assert.Equal("1.5", reader.GetString(1));
		Assert.Equal("TRUE", reader.GetString(3));
		Assert.Equal(1.5, reader.Parse<double>(1));
	}

	[Fact]
	public void AFormatterThatNeedsMoreRoomThanItAsksForThrows()
	{
		using var reader = new CsvReader(new StyledRecordSource(Lines(), new MarkingFormatter { Lies = true }), null);
		Assert.True(reader.Read());

		Assert.Throws<InvalidOperationException>(() => reader.GetString(0));
		Assert.Throws<InvalidOperationException>(() => reader.FieldEquals(0, "[Widget]"u8));
		var room = new byte[CsvCells.MaxTextLength];
		Assert.Throws<InvalidOperationException>(() => reader.GetTextUtf8(1, room, roundNumbers: true).Length);
	}

	[Fact]
	public void ErrorCellsThatFailEveryReadFailWithAStyle()
	{
		var rows = new List<StyledRow>
		{
			new(1, Text("A")),
			new(2, new StyledCell("#N/A", CsvCellKind.Error, 0)),
		};
		using var reader = new CsvReader(new StyledRecordSource(rows, new MarkingFormatter(), throwOnErrorCells: true), null);
		Assert.True(reader.Read());

		Assert.Throws<FormatException>(() => reader.GetString(0));
		Assert.Throws<FormatException>(() => reader.FieldEquals(0, "#N/A"u8));
		var room = new byte[CsvCells.MaxTextLength];
		Assert.Throws<FormatException>(() => reader.GetTextUtf8(0, room, roundNumbers: true).Length);
	}
}
