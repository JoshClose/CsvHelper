using System.Globalization;
using System.Reflection;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>A field of a <see cref="TestBlock"/>.</summary>
/// <param name="Value">The field's value.</param>
/// <param name="Kind">What it holds.</param>
/// <param name="Style">Its style, or <see cref="CsvReader.NoStyle"/>.</param>
internal readonly record struct BlockField(byte[] Value, CsvCellKind Kind, int Style);

/// <summary>A record of a <see cref="TestBlock"/>.</summary>
/// <param name="Line">Its line, before the block's line offset is added.</param>
/// <param name="Fields">Its fields: at least one.</param>
internal sealed record BlockRecord(long Line, BlockField[] Fields);

/// <summary>
/// Whole records in the form <see cref="CsvReader.RecordBatch.AppendRecords"/> takes (CsvHelper.Excel's
/// <c>SegmentBlock</c>): the values each followed by a junk byte that the append must overwrite, the ends, kinds and
/// styles of the fields, and the ends and lines of the records. A test appends them, or writes them field by field through
/// the batch's other members, to compare the two.
/// </summary>
internal sealed class TestBlock
{
	/// <summary>The byte after each value in <see cref="Bytes"/>.</summary>
	private const byte Junk = 0xEE;

	/// <summary>Builds the block.</summary>
	/// <param name="records">The records.</param>
	/// <param name="stylesAlways">Whether <see cref="Styles"/> has an entry for each field even when none has a style.</param>
	public TestBlock(IReadOnlyList<BlockRecord> records, bool stylesAlways = false)
	{
		Records = records;
		var fields = records.SelectMany(r => r.Fields).ToArray();
		var bytes = new List<byte>();
		Ends = new int[fields.Length];
		for (var i = 0; i < fields.Length; i++)
		{
			bytes.AddRange(fields[i].Value);
			Ends[i] = bytes.Count;
			bytes.Add(Junk);
		}

		Bytes = bytes.ToArray();
		Kinds = fields.Select(f => f.Kind).ToArray();
		Styles = stylesAlways || fields.Any(f => f.Style != CsvReader.NoStyle) ? fields.Select(f => f.Style).ToArray() : [];
		RecordEnds = new int[records.Count];
		var end = 0;
		for (var r = 0; r < records.Count; r++)
		{
			end += records[r].Fields.Length;
			RecordEnds[r] = end;
		}

		Lines = records.Select(r => r.Line).ToArray();
	}

	/// <summary>The records the block was built from.</summary>
	public IReadOnlyList<BlockRecord> Records { get; }

	/// <summary>The values, each followed by a junk byte.</summary>
	public byte[] Bytes { get; }

	/// <summary>The offset in <see cref="Bytes"/> of the byte after each value.</summary>
	public int[] Ends { get; }

	/// <summary>The kind of each field.</summary>
	public CsvCellKind[] Kinds { get; }

	/// <summary>The style of each field; empty when none has one, unless the block was built with styles always.</summary>
	public int[] Styles { get; }

	/// <summary>For each record, the index in <see cref="Ends"/> after its last field.</summary>
	public int[] RecordEnds { get; }

	/// <summary>The line of each record, before the offset.</summary>
	public long[] Lines { get; }

	/// <summary>Appends the records in one call.</summary>
	/// <param name="batch">The batch.</param>
	/// <param name="lineOffset">Added to each line.</param>
	public void Append(CsvReader.RecordBatch batch, long lineOffset) =>
		batch.AppendRecords(Bytes, Ends, Kinds, Styles, RecordEnds, Lines, lineOffset);

	/// <summary>
	/// Writes the records field by field, as a source without the bulk append writes them: alternately with
	/// <see cref="CsvReader.RecordBatch.AddField(ReadOnlySpan{byte}, CsvCellKind)"/> and through
	/// <see cref="CsvReader.RecordBatch.GetFieldSpan"/> and <see cref="CsvReader.RecordBatch.CommitField(int, CsvCellKind)"/>,
	/// with the overloads with a style for a field with one; then <see cref="CsvReader.RecordBatch.EndRecord"/> with each
	/// line plus <paramref name="lineOffset"/>.
	/// </summary>
	/// <param name="batch">The batch.</param>
	/// <param name="lineOffset">Added to each line.</param>
	public void Write(CsvReader.RecordBatch batch, long lineOffset)
	{
		var n = 0;
		foreach (var record in Records)
		{
			foreach (var field in record.Fields)
			{
				var styled = field.Style != CsvReader.NoStyle;
				if (n++ % 2 == 0)
				{
					if (styled)
					{
						batch.AddField(field.Value, field.Kind, field.Style);
					}
					else
					{
						batch.AddField(field.Value, field.Kind);
					}
				}
				else
				{
					field.Value.AsSpan().CopyTo(batch.GetFieldSpan(field.Value.Length));
					if (styled)
					{
						batch.CommitField(field.Value.Length, field.Kind, field.Style);
					}
					else
					{
						batch.CommitField(field.Value.Length, field.Kind);
					}
				}
			}

			batch.EndRecord(record.Line + lineOffset);
		}
	}
}

/// <summary>
/// What a reader's batch holds: the reader's buffer and index up to their counts (the bytes, the ends with their flags,
/// the kinds and styles at each field's first entry, the ends and lines of the records), read from its private fields.
/// </summary>
internal sealed class BatchState
{
	private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;

	/// <summary>The reader's index keeps this many entries free past its count (the reader's <c>EndsSlack</c>).</summary>
	private const int EndsSlack = 64;

	/// <summary>Creates the state.</summary>
	/// <param name="buffer">The bytes up to the end of the data.</param>
	/// <param name="ends">The index's entries.</param>
	/// <param name="kinds">The kinds, at every entry but the last.</param>
	/// <param name="styles">The styles, as the kinds, or <see langword="null"/>.</param>
	/// <param name="rowEnds">The last entry of each record.</param>
	/// <param name="lines">The line of each record.</param>
	private BatchState(byte[] buffer, int[] ends, CsvCellKind[] kinds, int[]? styles, int[] rowEnds, long[] lines)
	{
		Buffer = buffer;
		Ends = ends;
		Kinds = kinds;
		Styles = styles;
		RowEnds = rowEnds;
		Lines = lines;
	}

	/// <summary>The bytes up to the end of the data.</summary>
	public byte[] Buffer { get; }

	/// <summary>The index's entries, with their flags.</summary>
	public int[] Ends { get; }

	/// <summary>The kinds, at every entry but the last (each field's first entry).</summary>
	public CsvCellKind[] Kinds { get; }

	/// <summary>The styles, as the kinds; <see langword="null"/> when the reader keeps none.</summary>
	public int[]? Styles { get; }

	/// <summary>The last entry of each record.</summary>
	public int[] RowEnds { get; }

	/// <summary>The line of each record.</summary>
	public long[] Lines { get; }

	/// <summary>Reads the state of <paramref name="reader"/>'s batch, and checks that its arrays have the room the reader counts on.</summary>
	/// <param name="reader">A reader over a record source.</param>
	/// <returns>The state.</returns>
	public static BatchState Of(CsvReader reader)
	{
		var buffer = Get<byte[]>(reader, "buffer");
		var dataEnd = Get<int>(reader, "dataEnd");
		var ends = Get<int[]>(reader, "ends");
		var endsCount = Get<int>(reader, "endsCount");
		var kinds = Get<CsvCellKind[]>(reader, "kinds");
		var styles = Get<int[]?>(reader, "styles");
		var rowEnds = Get<int[]>(reader, "rowEnds");
		var rowLines = Get<long[]>(reader, "rowLines");
		var rowCount = Get<int>(reader, "rowCount");

		// The room that the commits keep (CommitField and EndRecord grow before they write past it).
		Assert.True(buffer.Length >= dataEnd);
		Assert.True(ends.Length - EndsSlack >= endsCount);
		Assert.True(kinds.Length >= endsCount);
		Assert.True(styles is null || styles.Length >= kinds.Length);
		Assert.True(rowEnds.Length >= rowCount && rowLines.Length >= rowCount);

		return new BatchState(
			buffer.AsSpan(0, dataEnd).ToArray(),
			ends.AsSpan(0, endsCount).ToArray(),
			kinds.AsSpan(0, endsCount - 1).ToArray(),
			styles?.AsSpan(0, endsCount - 1).ToArray(),
			rowEnds.AsSpan(0, rowCount).ToArray(),
			rowLines.AsSpan(0, rowCount).ToArray());
	}

	/// <summary>Sets one of the reader's private <see cref="int"/> fields, for a test of a limit.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="name">The field.</param>
	/// <param name="value">Its value.</param>
	public static void Set(CsvReader reader, string name, int value) => typeof(CsvReader).GetField(name, Instance)!.SetValue(reader, value);

	/// <summary>Gets one of the reader's private fields.</summary>
	/// <typeparam name="T">The field's type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="name">The field.</param>
	/// <returns>Its value.</returns>
	public static T Get<T>(CsvReader reader, string name) => (T)typeof(CsvReader).GetField(name, Instance)!.GetValue(reader)!;

	/// <summary>Asserts that <paramref name="actual"/> holds what this holds, byte for byte.</summary>
	/// <param name="actual">The other state.</param>
	/// <param name="stylesKept">
	/// Whether a reader that keeps no styles must keep none in the other too; otherwise it counts as one whose fields have
	/// none.
	/// </param>
	public void AssertSame(BatchState actual, bool stylesKept = true)
	{
		Assert.Equal(Buffer, actual.Buffer);
		Assert.Equal(Ends, actual.Ends);
		Assert.Equal(Kinds, actual.Kinds);
		if (stylesKept)
		{
			Assert.Equal(Styles is null, actual.Styles is null);
		}

		Assert.Equal(Styles ?? Enumerable.Repeat(CsvReader.NoStyle, Kinds.Length).ToArray(), actual.Styles ?? Enumerable.Repeat(CsvReader.NoStyle, Kinds.Length).ToArray());
		Assert.Equal(RowEnds, actual.RowEnds);
		Assert.Equal(Lines, actual.Lines);
	}
}

/// <summary>A record source whose fills are actions on the batch, one a fill, to write the batch as a test needs.</summary>
/// <param name="fills">The fills, in order.</param>
internal sealed class ScriptedRecordSource(IReadOnlyList<Action<CsvReader.RecordBatch>> fills) : CsvRecordSource
{
	private int next;

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		if (next < fills.Count)
		{
			fills[next++](batch);
		}

		return next < fills.Count;
	}

	/// <inheritdoc/>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken) => new(Fill(batch));

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => null;
}

/// <summary>
/// A record source over rows in memory that appends them in blocks (<see cref="CsvReader.RecordBatch.AppendRecords"/>), as
/// CsvHelper.Excel's segments are appended: blocks of 1 to 40 rows, their lines numbered from the block's start with the
/// row before it as the offset, until the batch is full (at least one block a fill).
/// </summary>
internal sealed class BlockRecordSource : CsvRecordSource
{
	private readonly IReadOnlyList<StyledRow> rows;
	private readonly Random rng;
	private int next;

	/// <summary>Creates the source.</summary>
	/// <param name="rows">The rows: each with a cell.</param>
	/// <param name="formatter">The formatter of the styled fields, or <see langword="null"/> for none.</param>
	/// <param name="seed">The seed of the blocks' sizes.</param>
	public BlockRecordSource(IReadOnlyList<StyledRow> rows, CsvCellFormatter? formatter, int seed)
	{
		this.rows = rows;
		rng = new Random(seed);
		CellOptions = new CsvCellOptions { Formatter = formatter };
	}

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		while (next < rows.Count)
		{
			var count = Math.Min(rng.Next(1, 41), rows.Count - next);
			var offset = rows[next].Line - 1;
			var records = new List<BlockRecord>();
			for (var i = next; i < next + count; i++)
			{
				var fields = rows[i].Cells.Select(c => new BlockField(Encoding.UTF8.GetBytes(c.Text), c.Kind, c.Style)).ToArray();
				records.Add(new BlockRecord(rows[i].Line - offset, fields));
			}

			new TestBlock(records).Append(batch, offset);
			next += count;
			if (batch.IsFull)
			{
				break;
			}
		}

		return next < rows.Count;
	}

	/// <inheritdoc/>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken) => new(Fill(batch));

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => $"Blocks!{field}:{line}";
}

/// <summary>
/// Core's bulk append (Phase 9 step 8): <see cref="CsvReader.RecordBatch.AppendRecords"/> makes the buffer and index that
/// writing the same fields one at a time makes, byte for byte, and a reader over appended blocks reads what a reader over
/// the same fields written one at a time reads, sequentially and under
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>.
/// </summary>
public class AppendRecordsTests
{
	/// <summary>One step of a fill: records written field by field into both readers, or appended into one of them.</summary>
	/// <param name="Block">The records.</param>
	/// <param name="Offset">Their line offset.</param>
	/// <param name="Append">Whether the reader under test appends them; otherwise both write them field by field.</param>
	private sealed record Step(TestBlock Block, long Offset, bool Append);

	/// <summary>The seeds of the random scripts, each with trimming on or off and a separator.</summary>
	/// <returns>Sixteen cases: every separator with trimming on and with it off, twice.</returns>
	public static TheoryData<int, bool, char> Cases()
	{
		var data = new TheoryData<int, bool, char>();
		for (var seed = 0; seed < 16; seed++)
		{
			// Every separator, with trimming on and off, twice.
			data.Add(seed, seed % 8 >= 4, ",;\t|"[seed % 4]);
		}

		return data;
	}

	/// <summary>
	/// Runs <paramref name="script"/> through a reader, one list of steps a fill, and reads its records to the end.
	/// </summary>
	/// <param name="script">The fills.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="append">Whether the steps that append do; otherwise they write field by field.</param>
	/// <returns>The batch's state after each step.</returns>
	private static List<BatchState> Run(IReadOnlyList<IReadOnlyList<Step>> script, CsvReaderOptions options, bool append)
	{
		CsvReader reader = null!;
		var states = new List<BatchState>();
		var fills = script.Select(steps => (Action<CsvReader.RecordBatch>)(batch =>
		{
			foreach (var step in steps)
			{
				if (append && step.Append)
				{
					step.Block.Append(batch, step.Offset);
				}
				else
				{
					step.Block.Write(batch, step.Offset);
				}

				states.Add(BatchState.Of(reader));
			}
		})).ToList();
		using (reader = new CsvReader(new ScriptedRecordSource(fills), options))
		{
			while (reader.Read())
			{
			}
		}

		return states;
	}

	/// <summary>Asserts that appending the script's blocks makes what writing them field by field makes, after every step.</summary>
	/// <param name="script">The fills.</param>
	/// <param name="options">The reader's options.</param>
	private static void AssertAppendsAsCommits(IReadOnlyList<IReadOnlyList<Step>> script, CsvReaderOptions options)
	{
		var expected = Run(script, options, append: false);
		var actual = Run(script, options, append: true);

		Assert.Equal(expected.Count, actual.Count);
		for (var i = 0; i < expected.Count; i++)
		{
			expected[i].AssertSame(actual[i]);
		}
	}

	/// <summary>A random field: any bytes, any kind, and a style in about <paramref name="stylePercent"/> of them.</summary>
	/// <param name="rng">The random numbers.</param>
	/// <param name="maxValue">The longest value.</param>
	/// <param name="stylePercent">How often the field has a style, in percent.</param>
	/// <returns>The field.</returns>
	private static BlockField RandomField(Random rng, int maxValue, int stylePercent)
	{
		var value = new byte[rng.Next(maxValue + 1)];
		rng.NextBytes(value);
		var style = rng.Next(100) < stylePercent ? (rng.Next(4) == 0 ? CsvCellFormatter.MaxStyle - rng.Next(3) : rng.Next(10)) : CsvReader.NoStyle;

		return new BlockField(value, (CsvCellKind)rng.Next(7), style);
	}

	/// <summary>A block of random records.</summary>
	/// <param name="rng">The random numbers.</param>
	/// <param name="records">The number of records.</param>
	/// <param name="maxFields">The most fields a record has (at least one).</param>
	/// <param name="maxValue">The longest value.</param>
	/// <param name="stylePercent">How often a field has a style, in percent.</param>
	/// <param name="stylesAlways">Whether the block has styles even when no field has one.</param>
	/// <returns>The block, its lines ascending from 1 with gaps.</returns>
	private static TestBlock RandomBlock(Random rng, int records, int maxFields, int maxValue, int stylePercent, bool stylesAlways = false)
	{
		var list = new List<BlockRecord>();
		var line = 0L;
		for (var r = 0; r < records; r++)
		{
			line += rng.Next(1, 4);
			var fields = Enumerable.Range(0, rng.Next(1, maxFields + 1)).Select(_ => RandomField(rng, maxValue, stylePercent)).ToArray();
			list.Add(new BlockRecord(line, fields));
		}

		return new TestBlock(list, stylesAlways);
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void AppendsTheBufferAndIndexThatCommitsMake(int seed, bool trim, char separator)
	{
		var rng = new Random(seed);
		var options = new CsvReaderOptions
		{
			HasHeader = false,
			TrimWhitespace = trim,
			Separator = separator,
			BufferSize = new[] { 256, 4096, 64 * 1024 }[rng.Next(3)],
		};

		// Up to four fills of up to five steps: field by field (in both) or appended, into empty and non-empty batches, with
		// styles none, some, or a span of none; now and then records past the buffer, the index (32,704 entries) or the
		// rows (16,384) of a fill.
		var script = new List<IReadOnlyList<Step>>();
		for (var f = rng.Next(1, 5); f > 0; f--)
		{
			var steps = new List<Step>();
			for (var s = rng.Next(1, 6); s > 0; s--)
			{
				var stylePercent = rng.Next(3) switch
				{
					0 => 0,
					1 => 5,
					_ => 40,
				};
				var stylesAlways = stylePercent == 0 && rng.Next(2) == 0;
				var block = rng.Next(20) switch
				{
					0 => new TestBlock([]),
					1 => RandomBlock(rng, rng.Next(1, 6), 3, 5000, stylePercent, stylesAlways),
					2 => RandomBlock(rng, 1, 40_000, 3, stylePercent, stylesAlways),
					3 => RandomBlock(rng, 17_000, 1, 2, stylePercent, stylesAlways),
					< 9 => RandomBlock(rng, rng.Next(1, 6), 6, 12, stylePercent, stylesAlways),
					_ => RandomBlock(rng, rng.Next(1, 200), 12, 40, stylePercent, stylesAlways),
				};
				steps.Add(new Step(block, rng.Next(3) == 0 ? 0 : rng.Next(1_000_000), rng.Next(4) != 0));
			}

			script.Add(steps);
		}

		AssertAppendsAsCommits(script, options);
	}

	[Fact]
	public void StylesStartAtTheFirstFieldWithOne()
	{
		var options = new CsvReaderOptions { HasHeader = false };
		BlockField Field(string text, int style = CsvReader.NoStyle) => new(Encoding.UTF8.GetBytes(text), CsvCellKind.Text, style);

		// Records without styles in the batch, then a block whose sixth field has the first style, and one of none.
		var plain = new TestBlock([new BlockRecord(1, [Field("a"), Field("b")]), new BlockRecord(2, [Field("c")])]);
		var late = new TestBlock(
		[
			new BlockRecord(1, [Field("d"), Field("e"), Field("f")]),
			new BlockRecord(2, [Field("g"), Field("h"), Field("i", 7), Field("j")]),
			new BlockRecord(5, [Field("k", CsvCellFormatter.MaxStyle)]),
		]);
		var after = new TestBlock([new BlockRecord(1, [Field("l"), Field("m")])]);
		var script = new List<IReadOnlyList<Step>> { new[] { new Step(plain, 0, false), new Step(late, 2, true), new Step(after, 5, true) } };
		var states = Run(script, options, append: true);
		AssertAppendsAsCommits(script, options);

		// One entry a field: its first.
		Assert.Null(states[0].Styles);
		Assert.Equal([-1, -1, -1, -1, -1, -1, -1, -1, 7, -1, CsvCellFormatter.MaxStyle], states[1].Styles!);
		Assert.Equal([.. states[1].Styles!, -1, -1], states[2].Styles!);

		// A span of styles where no field has one starts none, as commits without a style start none.
		var none = new TestBlock([new BlockRecord(1, [Field("a"), Field("b")])], stylesAlways: true);
		Assert.Equal([-1, -1], none.Styles);
		var unstyled = new List<IReadOnlyList<Step>> { new[] { new Step(none, 0, true) } };
		AssertAppendsAsCommits(unstyled, options);
		Assert.Null(Run(unstyled, options, append: true)[0].Styles);
	}

	[Fact]
	public void GrowsTheBufferTheIndexAndTheRowsAtOnce()
	{
		var rng = new Random(7);
		var options = new CsvReaderOptions { HasHeader = false, BufferSize = 256 };

		// Into a batch that holds a record: values past the buffer, a record past the index with styles from its middle,
		// and records past the rows.
		var first = RandomBlock(rng, 2, 3, 8, 0);
		var large = RandomBlock(rng, 3, 2, 100_000, 0);
		var wide = new TestBlock([new BlockRecord(1, Enumerable.Range(0, 70_000).Select(i => new BlockField([(byte)i], CsvCellKind.Number, i >= 35_000 ? i % 5 : CsvReader.NoStyle)).ToArray())]);
		var many = RandomBlock(rng, 40_000, 1, 1, 0);
		AssertAppendsAsCommits(new List<IReadOnlyList<Step>> { new[] { new Step(first, 0, false), new Step(large, 9, true), new Step(wide, 99, true), new Step(many, 999, true) } }, options);
	}

	[Fact]
	public void ReadsAsTheFieldsWouldRead()
	{
		// Trimmed text and a boolean read through their flags, LF after each record and separators between its fields, and
		// lines with the offset.
		var options = new CsvReaderOptions { HasHeader = false, TrimWhitespace = true, Separator = ';' };
		var block = new TestBlock(
		[
			new BlockRecord(1, [new BlockField(" a "u8.ToArray(), CsvCellKind.Text, CsvReader.NoStyle), new BlockField("1"u8.ToArray(), CsvCellKind.Bool, CsvReader.NoStyle)]),
			new BlockRecord(2, [new BlockField("2.5"u8.ToArray(), CsvCellKind.Number, CsvReader.NoStyle)]),
			new BlockRecord(5, [new BlockField(" b"u8.ToArray(), CsvCellKind.Text, CsvReader.NoStyle), new BlockField([], CsvCellKind.Empty, CsvReader.NoStyle)]),
		]);
		using var reader = new CsvReader(new ScriptedRecordSource([batch => block.Append(batch, 100)]), options);

		Assert.True(reader.Read());
		Assert.Equal(101, reader.LineNumber);
		Assert.Equal(" a ;1", Encoding.UTF8.GetString(reader.RawRecord.ToArray()));
		Assert.Equal("a", reader.GetString(0));
		Assert.Equal("TRUE", reader.GetString(1));
		Assert.True(reader.Parse<bool>(1));
		Assert.True(reader.Read());
		Assert.Equal(102, reader.LineNumber);
		Assert.Equal(2.5, reader.Parse<double>(0));
		Assert.True(reader.Read());
		Assert.Equal(105, reader.LineNumber);
		Assert.Equal(" b;", Encoding.UTF8.GetString(reader.RawRecord.ToArray()));
		Assert.Equal("b", reader.GetString(0));
		Assert.True(reader.IsEmpty(1));
		Assert.Equal(CsvCellKind.Empty, reader.GetCellKind(1));
		Assert.False(reader.Read());
	}

	[Fact]
	public void RejectsSpansThatDisagreeAndLeavesTheBatchAsItWas()
	{
		BlockField Field(string text, int style = CsvReader.NoStyle) => new(Encoding.UTF8.GetBytes(text), CsvCellKind.Text, style);
		// The batch holds records without styles first, so that a style before the bad one starts the reader's styles.
		var plain = new TestBlock([new BlockRecord(1, [Field("ab"), Field("c")]), new BlockRecord(2, [Field("d")])]);
		var good = new TestBlock([new BlockRecord(1, [Field("ab"), Field("c")]), new BlockRecord(2, [Field("d", 3)])]);
		var bytes = good.Bytes;
		var ends = good.Ends;
		var kinds = good.Kinds;
		var styles = good.Styles;
		var records = good.RecordEnds;
		var lines = good.Lines;
		var bad = new List<(Type Exception, Action<CsvReader.RecordBatch> Append)>
		{
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds.AsSpan(1), styles, records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles.AsSpan(1), records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles, records, lines.AsSpan(1), 0)),
			// Fields without a record.
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles, [], [], 0)),
			// The last record not at the last field.
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles, [2], [1], 0)),
			// A record without a field, first or after another.
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles, [0, 3], [1, 2], 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, ends, kinds, styles, [2, 2, 3], [1, 2, 3], 0)),
			(typeof(ArgumentException), b => b.AppendRecords([], [], [], [], [0], [1], 0)),
			// Bytes past, or short of, the byte after the last value.
			(typeof(ArgumentException), b => b.AppendRecords(bytes.Concat(new byte[] { 0 }).ToArray(), ends, kinds, styles, records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes.AsSpan(1), ends, kinds, styles, records, lines, 0)),
			// Ends out of order, or past the bytes (with the last end right).
			(typeof(ArgumentException), b => b.AppendRecords(bytes, [3, 2, ends[2]], kinds, styles, records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, [-1, 3, ends[2]], kinds, styles, records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, [bytes.Length, 3, ends[2]], kinds, styles, records, lines, 0)),
			(typeof(ArgumentException), b => b.AppendRecords(bytes, [int.MaxValue / 2, 3, ends[2]], kinds, styles, records, lines, 0)),
			// Styles out of range: first, and after a field with a style.
			(typeof(ArgumentOutOfRangeException), b => b.AppendRecords(bytes, ends, kinds, [-2, -1, 3], records, lines, 0)),
			(typeof(ArgumentOutOfRangeException), b => b.AppendRecords(bytes, ends, kinds, [1, -1, CsvCellFormatter.MaxStyle + 1], records, lines, 0)),
		};

		foreach (var (exception, append) in bad)
		{
			CsvReader reader = null!;
			var source = new ScriptedRecordSource(
			[
				batch =>
				{
					plain.Write(batch, 0);
					var before = BatchState.Of(reader);
					var e = Record.Exception(() => append(batch));

					Assert.IsType(exception, e);
					before.AssertSame(BatchState.Of(reader), stylesKept: false);
					Assert.Equal(0, batch.FieldCount);
					// The batch takes more after it.
					good.Append(batch, 2);
				},
			]);
			using (reader = new CsvReader(source, new CsvReaderOptions { HasHeader = false }))
			{
				var values = new List<string>();
				while (reader.Read())
				{
					values.Add(string.Join(",", Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)) + "@" + reader.LineNumber);
				}

				Assert.Equal(["ab,c@1", "d@2", "ab,c@3", "d@4"], values);
			}
		}

		// Nothing to append is nothing.
		var empty = new List<IReadOnlyList<Step>> { new[] { new Step(good, 0, false), new Step(new TestBlock([]), 0, true) } };
		var states = Run(empty, new CsvReaderOptions { HasHeader = false }, append: true);
		states[0].AssertSame(states[1]);
	}

	[Fact]
	public void RejectsAnAppendInsideARecord()
	{
		var block = new TestBlock([new BlockRecord(1, [new BlockField("x"u8.ToArray(), CsvCellKind.Text, CsvReader.NoStyle)])]);
		CsvReader reader = null!;
		var source = new ScriptedRecordSource(
		[
			batch =>
			{
				batch.AddField("open"u8, CsvCellKind.Text);
				var before = BatchState.Of(reader);

				Assert.Throws<InvalidOperationException>(() => block.Append(batch, 0));
				before.AssertSame(BatchState.Of(reader));
				Assert.Equal(1, batch.FieldCount);
				batch.EndRecord(1);
				block.Append(batch, 1);
			},
		]);
		using (reader = new CsvReader(source, new CsvReaderOptions { HasHeader = false }))
		{
			Assert.True(reader.Read());
			Assert.Equal("open", reader.GetString(0));
			Assert.True(reader.Read());
			Assert.Equal("x", reader.GetString(0));
			Assert.Equal(2, reader.LineNumber);
		}
	}

	[Fact]
	public void ThrowsWhenTheRecordsAreTooLargeToBuffer()
	{
		// The limits, with the reader's counts set near them (its arrays aren't that large): the buffer's, and the index's
		// bound that keeps its doubling from overflowing. The counts go back before the fill ends.
		var block = new TestBlock([new BlockRecord(1, [new BlockField("xyz"u8.ToArray(), CsvCellKind.Text, CsvReader.NoStyle)])]);
		CsvReader reader = null!;
		var source = new ScriptedRecordSource(
		[
			batch =>
			{
				var dataEnd = BatchState.Get<int>(reader, "dataEnd");
				BatchState.Set(reader, "dataEnd", int.MaxValue - 8);
				Assert.Throws<CsvFormatException>(() => block.Append(batch, 0));
				BatchState.Set(reader, "dataEnd", dataEnd);

				var endsCount = BatchState.Get<int>(reader, "endsCount");
				BatchState.Set(reader, "endsCount", (1 << 30) - 64);
				Assert.Throws<CsvFormatException>(() => block.Append(batch, 0));
				BatchState.Set(reader, "endsCount", endsCount);

				block.Append(batch, 0);
			},
		]);
		using (reader = new CsvReader(source, new CsvReaderOptions { HasHeader = false }))
		{
			Assert.True(reader.Read());
			Assert.Equal("xyz", reader.GetString(0));
			Assert.False(reader.Read());
		}
	}

	/// <summary>Random rows of valid cells of every kind (so that each reads as text), some with styles, at ascending lines with gaps.</summary>
	/// <param name="rng">The random numbers.</param>
	/// <param name="count">The number of rows.</param>
	/// <returns>The rows, each with one to ten cells.</returns>
	private static List<StyledRow> RandomRows(Random rng, int count)
	{
		const string Letters = "ab ,;\t\"xé€";
		var rows = new List<StyledRow>();
		var line = 0L;
		for (var r = 0; r < count; r++)
		{
			line += rng.Next(1, 4);
			var cells = new StyledCell[rng.Next(1, 11)];
			for (var c = 0; c < cells.Length; c++)
			{
				// Styles from 150 rows on: the reader starts keeping them inside a block.
				var style = r >= 150 && rng.Next(5) == 0 ? new[] { 0, 1, 3, 4, 9 }[rng.Next(5)] : CsvReader.NoStyle;
				cells[c] = rng.Next(7) switch
				{
					0 => new StyledCell(new string(Enumerable.Range(0, rng.Next(13)).Select(_ => Letters[rng.Next(Letters.Length)]).ToArray()), CsvCellKind.Text, style),
					1 => new StyledCell((rng.Next(-100_000, 100_000) / 4.0).ToString(CultureInfo.InvariantCulture), CsvCellKind.Number, style),
					2 => new StyledCell((rng.Next(1, 60_000) + rng.Next(4) / 4.0).ToString(CultureInfo.InvariantCulture), CsvCellKind.DateNumber, style),
					3 => new StyledCell(rng.Next(2) == 0 ? "0" : "1", CsvCellKind.Bool, style),
					4 => new StyledCell(new[] { "#N/A", "#DIV/0!", "#VALUE!" }[rng.Next(3)], CsvCellKind.Error, style),
					5 => new StyledCell("", CsvCellKind.Empty, style),
					_ => new StyledCell((rng.Next(0, 10_000) / 8.0).ToString(CultureInfo.InvariantCulture), CsvCellKind.Duration, style),
				};
			}

			rows.Add(new StyledRow(line, cells));
		}

		return rows;
	}

	/// <summary>The current record as text: its line, its raw bytes, and each field's kind and text.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <returns>The text.</returns>
	private static string Describe(CsvReader reader)
	{
		var text = new StringBuilder();
		text.Append(reader.LineNumber).Append(':').Append(Encoding.UTF8.GetString(reader.RawRecord.ToArray()));
		for (var i = 0; i < reader.FieldCount; i++)
		{
			text.Append('|').Append(reader.GetCellKind(i)).Append('=').Append(reader.GetString(i));
		}

		return text.ToString();
	}

	[Theory]
	[InlineData(1, false, ',')]
	[InlineData(2, true, ';')]
	[InlineData(3, false, '\t')]
	[InlineData(4, true, ',')]
	public void ReadersOfBlocksReadWhatReadersOfFieldsRead(int seed, bool trim, char separator)
	{
		var rows = RandomRows(new Random(seed), 3000);
		var formatter = new MarkingFormatter();
		var options = new CsvReaderOptions { HasHeader = false, TrimWhitespace = trim, Separator = separator, BufferSize = 2048 };
		var expected = new List<string>();
		using (var reader = new CsvReader(new StyledRecordSource(rows, formatter, rowsPerFill: 37), options))
		{
			while (reader.Read())
			{
				expected.Add(Describe(reader));
			}
		}

		// Rows of one empty cell are skipped, as empty lines are.
		Assert.True(expected.Count > rows.Count * 9 / 10);
		using (var reader = new CsvReader(new BlockRecordSource(rows, formatter, seed), options))
		{
			var actual = new List<string>();
			while (reader.Read())
			{
				actual.Add(Describe(reader));
			}

			Assert.Equal(expected, actual);
		}

		foreach (var degree in new[] { 1, 2, 4 })
		{
			using var reader = new CsvReader(new BlockRecordSource(rows, formatter, seed + degree), options);

			Assert.Equal(expected, reader.ParallelEnumerate(Describe, degree).ToList());
		}
	}
}
