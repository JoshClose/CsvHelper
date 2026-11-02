using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper.Linq;

namespace CsvHelper.Tests;

/// <summary>An order read from (and written as) typed cells.</summary>
[CsvRecord]
public partial class CellOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell, or a text cell in the culture of the reader.</summary>
	public double Price { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Total { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>An ISO 8601 text cell.</summary>
	public DateTime Ordered { get; set; }

	/// <summary>A date cell, read as the serial number it is.</summary>
	public double Serial { get; set; }

	/// <summary>A text cell that is a link.</summary>
	public Uri? Link { get; set; }

	/// <summary>An empty cell, or text.</summary>
	public string? Note { get; set; }
}

/// <summary>A record whose second column must be a number.</summary>
[CsvRecord]
public partial class CellQuantity
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }
}

/// <summary>A row of a <see cref="FakeRecordSource"/>: its line (row number) and its cells.</summary>
/// <param name="Line">The 1-based row number.</param>
/// <param name="Cells">The text and kind of each cell.</param>
internal sealed record FakeRow(long Line, params (string Text, CsvCellKind Kind)[] Cells);

/// <summary>
/// A record source over rows in memory, like a sheet: it writes the rows into the reader's batches, a few at a time, half
/// of the fields with <see cref="CsvReader.RecordBatch.AddField"/> and half through <see cref="CsvReader.RecordBatch.GetFieldSpan"/>.
/// </summary>
/// <param name="sheet">The name of the sheet, for locations.</param>
/// <param name="rows">The rows.</param>
/// <param name="rowsPerFill">The most rows one fill writes (it also stops when the batch is full).</param>
internal sealed class FakeRecordSource(string sheet, IReadOnlyList<FakeRow> rows, int rowsPerFill = int.MaxValue) : CsvRecordSource
{
	private int next;

	/// <summary>The number of fills, sync and async.</summary>
	public int Fills { get; private set; }

	/// <summary>The number of async fills.</summary>
	public int AsyncFills { get; private set; }

	/// <summary>Whether the reader disposed the source.</summary>
	public bool Disposed { get; private set; }

	/// <summary>Whether the next fill writes a field and returns without ending its record (a broken source).</summary>
	public bool LeaveRecordOpen { get; set; }

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		Fills++;
		if (LeaveRecordOpen)
		{
			batch.AddField("open"u8, CsvCellKind.Text);

			return true;
		}

		var written = 0;
		while (next < rows.Count && written < rowsPerFill && !batch.IsFull)
		{
			var row = rows[next++];
			for (var i = 0; i < row.Cells.Length; i++)
			{
				var (text, kind) = row.Cells[i];
				var bytes = Encoding.UTF8.GetBytes(text);
				if (i % 2 == 0)
				{
					batch.AddField(bytes, kind);
				}
				else
				{
					bytes.CopyTo(batch.GetFieldSpan(bytes.Length));
					batch.CommitField(bytes.Length, kind);
				}
			}

			Assert.Equal(row.Cells.Length, batch.FieldCount);
			batch.EndRecord(row.Line);
			written++;
		}

		return next < rows.Count;
	}

	/// <inheritdoc/>
	public override async ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken)
	{
		AsyncFills++;
		await Task.Yield();

		return Fill(batch);
	}

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => field < 0 ? $"{sheet}!{line}:{line}" : $"{sheet}!{Column(field)}{line}";

	/// <inheritdoc/>
	public override void Dispose() => Disposed = true;

	/// <summary>The letters of a column, as a spreadsheet names it: A to Z, then AA and so on.</summary>
	/// <param name="index">The 0-based index of the column.</param>
	/// <returns>The column's letters.</returns>
	private static string Column(int index)
	{
		var name = "";
		for (var n = index + 1; n > 0; n = (n - 1) / 26)
		{
			name = (char)('A' + ((n - 1) % 26)) + name;
		}

		return name;
	}
}

/// <summary>A field that a <see cref="FakeCellSink"/> received.</summary>
/// <param name="Type">The type the field was formatted from.</param>
/// <param name="Text">The field's UTF-8, decoded.</param>
internal readonly record struct FakeCell(Type Type, string Text);

/// <summary>A record that a <see cref="FakeCellSink"/> received.</summary>
/// <param name="Cells">The fields of the record.</param>
internal sealed record FakeRecord(List<FakeCell> Cells);

/// <summary>A cell sink that keeps what it is given: the records and the calls that are not fields.</summary>
internal sealed class FakeCellSink : CsvCellSink
{
	private List<FakeCell> current = [];

	/// <summary>The records that ended.</summary>
	public List<FakeRecord> Records { get; } = [];

	/// <summary>The calls other than fields: End, Discard, Flush, FlushAsync, Complete and CompleteAsync.</summary>
	public List<string> Events { get; } = [];

	/// <summary>Ask the writer for a flush after every this many records; 0 for never.</summary>
	public int FlushEvery { get; set; }

	/// <summary>The fields of the record being written.</summary>
	public IReadOnlyList<FakeCell> Current => current;

	/// <inheritdoc/>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type) =>
		current.Add(new FakeCell(type, Encoding.UTF8.GetString(utf8.ToArray())));

	/// <inheritdoc/>
	public override bool EndRecord()
	{
		Events.Add("End");
		Records.Add(new FakeRecord(current));
		current = [];

		return FlushEvery > 0 && Records.Count % FlushEvery == 0;
	}

	/// <inheritdoc/>
	public override void DiscardRecord()
	{
		Events.Add("Discard");
		current = [];
	}

	/// <inheritdoc/>
	public override void Flush() => Events.Add("Flush");

	/// <inheritdoc/>
	public override ValueTask FlushAsync(CancellationToken cancellationToken)
	{
		Events.Add("FlushAsync");

		return default;
	}

	/// <inheritdoc/>
	public override void Complete() => Events.Add("Complete");

	/// <inheritdoc/>
	public override ValueTask CompleteAsync()
	{
		Events.Add("CompleteAsync");

		return default;
	}
}

/// <summary>A record writer that writes one field and then throws, for a record that fails halfway.</summary>
internal sealed class FailingRecordWriter : CsvRecordWriter<string>
{
	/// <inheritdoc/>
	public override int FieldCount => 2;

	/// <inheritdoc/>
	public override void WriteHeader(CsvWriter writer)
	{
		WriteColumnName(writer, "A");
		WriteColumnName(writer, "B");
	}

	/// <inheritdoc/>
	public override void Write(CsvWriter writer, string record)
	{
		writer.WriteField(record);
		if (record == "bad")
		{
			throw new FormatException("bad record");
		}

		writer.WriteField(record.Length);
	}
}

// The core hooks of CsvHelper.Excel (docs/plans/excel.md, phase 0): a reader over a record source with typed cells, a
// writer into a cell sink, and errors that name the cell.
public class CellHookTests
{
	private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

	private static (string, CsvCellKind) Text(string text) => (text, CsvCellKind.Text);

	private static (string, CsvCellKind) Number(string text) => (text, CsvCellKind.Number);

	private static (string, CsvCellKind) Date(string text) => (text, CsvCellKind.DateNumber);

	private static (string, CsvCellKind) Bool(string text) => (text, CsvCellKind.Bool);

	private static (string, CsvCellKind) Empty() => ("", CsvCellKind.Empty);

	/// <summary>A sheet of orders: a header and three rows, with a gap in the row numbers, and German text where the cell is text.</summary>
	private static List<FakeRow> OrderRows() =>
	[
		new(1, Text("Name"), Text("Quantity"), Text("Price"), Text("Total"), Text("Paid"), Text("Ordered"), Text("Serial"), Text("Link"), Text("Note")),
		new(2, Text("Widget"), Number("3"), Number("1.5"), Number("4.5"), Bool("1"), Text("2024-03-01"), Date("45352.5"), Text("https://example.com/a?x=1"), Empty()),
		new(3, Text("Gadget, large"), Number("12"), Text("2,25"), Number("27"), Bool("0"), Text("2024-03-02T10:30:00"), Date("45353"), Text("docs/readme.md"), Text("rush")),
		new(7, Text("Gizmo"), Number("1"), Number("1234.5"), Number("1234.5"), Bool("1"), Text("2024-03-05"), Date("45356.25"), Empty(), Text("Ä \"quoted\"")),
	];

	private static CsvReader OrderReader(int rowsPerFill = 2, CsvReaderOptions? options = null) =>
		new(new FakeRecordSource("Orders", OrderRows(), rowsPerFill), options ?? new CsvReaderOptions { FormatProvider = German });

	private static void AssertOrders(IReadOnlyList<CellOrder> orders)
	{
		Assert.Equal(["Widget", "Gadget, large", "Gizmo"], orders.Select(o => o.Name));
		Assert.Equal([3, 12, 1], orders.Select(o => o.Quantity));
		// Number cells are invariant; the text cell is German.
		Assert.Equal([1.5, 2.25, 1234.5], orders.Select(o => o.Price));
		Assert.Equal([4.5m, 27m, 1234.5m], orders.Select(o => o.Total));
		Assert.Equal([true, false, true], orders.Select(o => o.Paid));
		Assert.Equal([new DateTime(2024, 3, 1), new DateTime(2024, 3, 2, 10, 30, 0), new DateTime(2024, 3, 5)], orders.Select(o => o.Ordered));
		// Date cells read as their serial numbers, until typed reading converts them.
		Assert.Equal([45352.5, 45353, 45356.25], orders.Select(o => o.Serial));
		Assert.Equal(new Uri("https://example.com/a?x=1"), orders[0].Link);
		Assert.Equal("docs/readme.md", orders[1].Link!.OriginalString);
		Assert.Null(orders[2].Link);
		Assert.Equal(["", "rush", "Ä \"quoted\""], orders.Select(o => o.Note));
	}

	[Fact]
	public void GeneratedMappersReadTheRecordsOfASource()
	{
		var source = new FakeRecordSource("Orders", OrderRows(), rowsPerFill: 2);
		List<CellOrder> orders;
		using (var reader = new CsvReader(source, new CsvReaderOptions { FormatProvider = German }))
		{
			Assert.Equal(["Name", "Quantity", "Price", "Total", "Paid", "Ordered", "Serial", "Link", "Note"], reader.Headers);
			orders = reader.GetRecords<CellOrder>().ToList();
			Assert.False(reader.Read());
		}

		AssertOrders(orders);
		// The header and the first row, then the last two, which end the source: no fill after it says so.
		Assert.Equal(2, source.Fills);
		Assert.True(source.Disposed);
	}

	[Fact]
	public async Task GeneratedMappersReadTheRecordsOfASourceAsynchronously()
	{
		var source = new FakeRecordSource("Orders", OrderRows(), rowsPerFill: 1);
		var orders = new List<CellOrder>();
		await using (var reader = new CsvReader(source, new CsvReaderOptions { FormatProvider = German }))
		{
			await foreach (var order in reader.GetRecordsAsync<CellOrder>())
			{
				orders.Add(order);
			}
		}

		AssertOrders(orders);
		Assert.Equal(source.Fills, source.AsyncFills);
		Assert.True(source.Disposed);
	}

	[Fact]
	public void FieldsOfASourceReadAsTheirText()
	{
		using var reader = OrderReader();
		Assert.True(reader.Read());
		Assert.Equal(9, reader.FieldCount);
		Assert.Equal("Widget", reader.GetString(0));
		Assert.Equal("1.5", reader.GetString(2));
		// Booleans and dates keep their bytes for now (they are flagged for typed reading's text).
		Assert.Equal("1", reader.GetString(4));
		Assert.Equal("45352.5", reader.GetString(6));
		Assert.Equal("45352.5", reader[6].ToString());
		Assert.True(reader.IsEmpty(8));
		Assert.Equal("", reader.GetString(8));
		Assert.Equal("45352.5"u8.ToArray(), reader.GetSpan(6).ToArray());
		// The fields with the separator between them, as a debugging view.
		Assert.Equal("Widget,3,1.5,4.5,1,2024-03-01,45352.5,https://example.com/a?x=1,", Encoding.UTF8.GetString(reader.RawRecord.ToArray()));
		// A field past the end of the record is empty text.
		Assert.Equal("", reader.GetString(20));
		Assert.Equal(1, reader.RowNumber);
		Assert.Equal(',', reader.Separator);
	}

	[Fact]
	public void NumberCellsParseInvariantAndTextCellsInTheReadersCulture()
	{
		var rows = new List<FakeRow>
		{
			new(1, Text("A"), Text("B")),
			new(2, Number("1.5"), Text("1,5")),
			new(3, Number("1234.5"), Text("1.234,5")),
			new(4, Bool("1"), Text("Ja")),
		};
		using var reader = new CsvReader(new FakeRecordSource("Data", rows),
			new CsvReaderOptions { FormatProvider = German, TrueValues = ["Ja"], FalseValues = ["Nein"] });

		Assert.True(reader.Read());
		Assert.Equal(1.5, reader.Parse<double>(0));
		Assert.Equal(1.5, reader.Parse<double>(1));
		Assert.Equal(1.5m, reader.Parse<decimal>(0));
		Assert.Equal(1.5m, reader.Parse<decimal>(1));
		Assert.Equal(1.5f, reader.Parse<float>(0));
		Assert.Equal(1.5, reader.Parse<double>("A"));
		Assert.Equal(1.5, reader[0].Parse<double>());
		Assert.Equal(1.5, reader[1].Parse<double>());
		Assert.Equal(1.5, reader[0].ParseOrNull<double>());
		Assert.True(reader.TryParse<double>(0, out var number) && number == 1.5);
		Assert.True(reader.TryParse<double>(1, out var text) && text == 1.5);
		Assert.True(reader[0].TryParse<decimal>(out var money) && money == 1.5m);
		// A provider of the caller's applies to text: a number cell is invariant whatever it is.
		Assert.Equal(1.5, reader.Parse<double>(0, German));
		Assert.Equal(1.5, reader.Parse<double>(0, CultureInfo.GetCultureInfo("fr-FR")));
		Assert.Equal(1.5, reader.Parse<double>(1, German));
		Assert.Equal("1.5", reader.GetString(0));
#if NET10_0_OR_GREATER
		// Types the reader parses from chars (ISpanParsable) too.
		Assert.Equal((Half)1.5, reader.Parse<Half>(0));
		Assert.Equal((Half)1.5, reader.Parse<Half>(1));
		Assert.True(reader.TryParse<Half>(0, out var half) && half == (Half)1.5);
#endif

		Assert.True(reader.Read());
		Assert.Equal(1234.5, reader.Parse<double>(0));
		Assert.Equal(1234.5, reader.Parse<double>(1));
		Assert.False(reader.TryParse<int>(0, out _));

		// A boolean cell is 1 or 0, whatever the reader's true and false values; a text cell uses them.
		Assert.True(reader.Read());
		Assert.True(reader.Parse<bool>(0));
		Assert.True(reader.Parse<bool>(1));
		Assert.True(reader.TryParse<bool>(0, out var flag) && flag);
		Assert.Equal(1, reader.Parse<int>(0));

		// The same text in a CSV file is German.
		using var csv = CsvReader.FromText("A,B\n1,5,\"1,5\"\n", new CsvReaderOptions { FormatProvider = German });
		Assert.True(csv.Read());
		Assert.Equal(1.5, csv.Parse<double>(2));
	}

	[Fact]
	public void DynamicRecordsKeepTheKindsOfTheirFields()
	{
		using var reader = OrderReader();
		var records = reader.GetRecords<CsvDynamicRecord>().ToList();

		Assert.Equal(3, records.Count);
		// These records have booleans and dates, flagged fields that the reader copies decoded; the kinds go along.
		Assert.Equal(1.5, records[0].Parse<double>("Price"));
		Assert.Equal(2.25, records[1].Parse<double>("Price"));
		Assert.Equal(1234.5, records[2].Parse<double>("Price"));
		Assert.Equal(4.5m, records[0].Parse<decimal>("Total"));
		Assert.True(records[0].Parse<bool>("Paid"));
		Assert.Equal(45352.5, records[0].Parse<double>("Serial"));
		Assert.Equal("1.5", records[0]["Price"]);
		Assert.Equal("Gadget, large", records[1]["Name"]);
		Assert.Equal("", records[0]["Note"]);
		Assert.Equal(1.5, records[0].Detach().Parse<double>("Price"));
		Assert.Equal(2.25, records[1].Detach().Parse<double>("Price"));
		dynamic gizmo = records[2];
		Assert.Equal("Gizmo", (string)gizmo.Name);

		// Records without flagged fields, whose bytes are copied as they are.
		var options = new CsvReaderOptions { FormatProvider = German };
		var rows = new List<FakeRow> { new(1, Text("Name"), Text("Price")), new(2, Text("x"), Number("1.5")), new(3, Text("y"), Text("2,5")) };
		List<CsvDynamicRecord> plain;
		using (var cells = new CsvReader(new FakeRecordSource("Plain", rows), options))
		{
			plain = cells.GetRecords<CsvDynamicRecord>().ToList();
		}

		Assert.Equal([1.5, 2.5], plain.Select(r => r.Parse<double>("Price")));
		Assert.Equal([1.5, 2.5], plain.Select(r => r.Detach().Parse<double>("Price")));

		// Records of CSV text with the same header and the same options have their own schema: the text is German.
		using var csv = CsvReader.FromText("Name,Price\nx,\"1,5\"\n", options);
		Assert.Equal(1.5, csv.GetRecords<CsvDynamicRecord>().Single().Parse<double>("Price"));
	}

	[Fact]
	public void DataReadersReadTheCellsOfASource()
	{
		using var reader = OrderReader();
		using var data = new CsvDataReader(reader);

		Assert.Equal(9, data.FieldCount);
		Assert.Equal("Price", data.GetName(2));
		Assert.True(data.Read());
		Assert.Equal("Widget", data.GetString(0));
		Assert.Equal(3, data.GetInt32(1));
		Assert.Equal(3L, data.GetInt64(1));
		Assert.Equal(1.5, data.GetDouble(2));
		Assert.Equal(4.5m, data.GetDecimal(3));
		Assert.True(data.GetBoolean(4));
		Assert.Equal("1.5", data.GetValue(2));
		Assert.True(data.Read());
		Assert.Equal(2.25, data.GetDouble(2));
		var table = new DataTable();
		table.Load(data);
		Assert.Single(table.Rows);
		Assert.Equal("Gizmo", table.Rows[0]["Name"]);
	}

	[Fact]
	public void LinqQueriesReadTheRecordsOfASource()
	{
		using var reader = OrderReader();

		var cheap = reader.Query<CellOrder>().Where(o => o.Price < 100).Select(o => new { o.Name, o.Price }).ToList();

		Assert.Equal(["Widget", "Gadget, large"], cheap.Select(o => o.Name));
		Assert.Equal([1.5, 2.25], cheap.Select(o => o.Price));
	}

	[Fact]
	public void ParallelEnumerationReadsTheBatchesOfASource()
	{
		var rows = new List<FakeRow> { new(1, Text("Id"), Text("Value"), Text("Flag")) };
		for (var i = 0; i < 5000; i++)
		{
			// Every third record has a flagged field (a boolean).
			var flag = i % 3 == 0 ? Bool("1") : Text("no");
			rows.Add(new FakeRow(i * 2 + 2, Number(i.ToString(CultureInfo.InvariantCulture)), Number($"{i}.5"), flag));
		}

		using (var reader = new CsvReader(new FakeRecordSource("Big", rows, rowsPerFill: 97), new CsvReaderOptions { FormatProvider = German }))
		{
			var results = reader.ParallelEnumerate(r => (Id: r.Parse<int>(0), Value: r.Parse<double>(1), Line: r.LineNumber, Text: r.GetString(2)), 4).ToList();

			Assert.Equal(Enumerable.Range(0, 5000), results.Select(r => r.Id));
			Assert.Equal(Enumerable.Range(0, 5000).Select(i => i + 0.5), results.Select(r => r.Value));
			Assert.Equal(Enumerable.Range(0, 5000).Select(i => (long)(i * 2 + 2)), results.Select(r => r.Line));
			Assert.Equal(Enumerable.Range(0, 5000).Select(i => i % 3 == 0 ? "1" : "no"), results.Select(r => r.Text));
		}

		// Records mapped on the workers, after records read on this thread.
		using (var reader = OrderReader(rowsPerFill: 1))
		{
			Assert.True(reader.Read());
			var orders = reader.ParallelEnumerate<CellOrder>(2).ToList();

			Assert.Equal(["Gadget, large", "Gizmo"], orders.Select(o => o.Name));
			Assert.Equal([2.25, 1234.5], orders.Select(o => o.Price));
		}
	}

	[Fact]
	public void TheLinesOfASourceAreItsRowNumbers()
	{
		// Without TrackLineNumbers: a source always numbers its records.
		using var reader = OrderReader(options: new CsvReaderOptions());
		var lines = new List<long>();
		while (reader.Read())
		{
			lines.Add(reader.LineNumber);
		}

		Assert.Equal([2L, 3L, 7L], lines);
	}

	[Fact]
	public void ErrorsOfASourceSayWhichCell()
	{
		var rows = new List<FakeRow>
		{
			new(1, Text("Name"), Text("Quantity")),
			new(2, Text("A"), Number("1")),
			new(5, Text("B"), Text("many")),
			new(6, Text("C"), Number("3")),
		};
		using (var reader = new CsvReader(new FakeRecordSource("Orders", rows), null))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<CellQuantity>().ToList());

			Assert.Equal("Orders!B5", e.Location);
			Assert.Equal(5, e.LineNumber);
			// The header is record 1.
			Assert.Equal(3, e.RecordNumber);
			Assert.Equal("CellQuantity.Quantity", e.Member);
			Assert.Equal(1, e.FieldIndex);
			Assert.Contains("of record 3 (Orders!B5)", e.Message);
		}

		var errors = new List<CsvRecordError>();
		using (var reader = new CsvReader(new FakeRecordSource("Orders", rows), new CsvReaderOptions
		{
			OnRecordError = error =>
			{
				errors.Add(error);

				return true;
			},
		}))
		{
			Assert.Equal(["A", "C"], reader.GetRecords<CellQuantity>().Select(r => r.Name));
		}

		var only = Assert.Single(errors);
		Assert.Equal("Orders!B5", only.Location);
		Assert.Equal("Orders!B5", ((CsvFormatException)only.Exception).Location);
		Assert.Equal(2, only.RowNumber);
		Assert.Equal("B,many", only.RawRecord);

		// A record with the wrong number of fields is a whole row.
		rows.Add(new FakeRow(9, Text("D"), Number("4"), Text("extra")));
		using (var reader = new CsvReader(new FakeRecordSource("Orders", rows), new CsvReaderOptions { FieldCountMismatch = CsvFieldCountHandling.Throw }))
		{
			var e = Assert.Throws<CsvFormatException>(() =>
			{
				while (reader.Read())
				{
				}
			});

			Assert.Equal("Orders!9:9", e.Location);
			Assert.Equal(9, e.LineNumber);
			Assert.Contains("(Orders!9:9) has 3 fields but 2 were expected", e.Message);
		}
	}

	[Fact]
	public void ErrorsOfCsvHaveNoLocation()
	{
		var errors = new List<CsvRecordError>();
		using (var reader = CsvReader.FromText("Name,Quantity\nA,1\nB,many\n", new CsvReaderOptions
		{
			OnRecordError = error =>
			{
				errors.Add(error);

				return true;
			},
		}))
		{
			Assert.Single(reader.GetRecords<CellQuantity>());
		}

		Assert.Null(Assert.Single(errors).Location);
		using var throwing = CsvReader.FromText("Name,Quantity\nB,many\n", new CsvReaderOptions { TrackLineNumbers = true });
		var e = Assert.Throws<CsvFormatException>(() => throwing.GetRecords<CellQuantity>().ToList());
		Assert.Null(e.Location);
		Assert.Contains("of record 2 (line 2)", e.Message);
		Assert.Null(new CsvFormatException("x").Location);
		Assert.Equal("Sheet1!A1", new CsvFormatException("x") { Location = "Sheet1!A1" }.Location);
	}

	[Fact]
	public void SourcesFillAsMuchAsTheBatchHolds()
	{
		// More bytes, fields and records than one batch holds, from a source that writes until the batch is full.
		var rows = new List<FakeRow> { new(1, Text("A"), Text("B")) };
		for (var i = 0; i < 40_000; i++)
		{
			rows.Add(new FakeRow(i + 2, Number(i.ToString(CultureInfo.InvariantCulture)), Text(new string('x', i % 50))));
		}

		var source = new FakeRecordSource("Big", rows);
		var count = 0;
		using (var reader = new CsvReader(source, null))
		{
			while (reader.Read())
			{
				Assert.Equal(count, reader.Parse<int>(0));
				Assert.Equal(count % 50, reader.GetSpan(1).Length);
				count++;
			}
		}

		Assert.Equal(40_000, count);
		Assert.True(source.Fills > 3);
	}

	[Fact]
	public void SourcesWriteFieldsAndRecordsOfAnySize()
	{
		// One record of more fields than the index holds at first, and one field larger than the buffer.
		var wide = Enumerable.Range(0, 20_000).Select(i => Number(i.ToString(CultureInfo.InvariantCulture))).ToArray();
		var large = new string('y', 300_000);
		var rows = new List<FakeRow> { new(1, wide), new(2, Text(large), Bool("1")), new(3) };
		using var reader = new CsvReader(new FakeRecordSource("Wide", rows), new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false });

		Assert.True(reader.Read());
		Assert.Equal(20_000, reader.FieldCount);
		Assert.Equal(19_999, reader.Parse<int>(19_999));
		Assert.True(reader.Read());
		Assert.Equal(large, reader.GetString(0));
		Assert.True(reader.Parse<bool>(1));
		// A row without cells is a record of one empty field, as an empty line is.
		Assert.True(reader.Read());
		Assert.Equal(1, reader.FieldCount);
		Assert.True(reader.IsEmpty(0));
		Assert.False(reader.Read());
	}

	[Fact]
	public void SourcesFollowTheReaderOptionsThatApplyToFields()
	{
		var rows = new List<FakeRow>
		{
			new(1, Text(" Name "), Text("Value")),
			new(2, Text("  padded  "), Number("2")),
			new(3),
			new(4, Text("   ")),
			new(5, Text("NULL"), Number("5")),
			new(6, Text("#a\\,b"), Number("6")),
		};
		using var reader = new CsvReader(new FakeRecordSource("Opts", rows),
			new CsvReaderOptions { TrimWhitespace = true, NullValues = ["NULL"], CommentPrefix = '#', Mode = CsvMode.Escape });

		// Trimmed names, and the CSV dialect (comments, escapes) does not apply to decoded fields.
		Assert.Equal(["Name", "Value"], reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal("padded", reader.GetString("Name"));
		Assert.Equal(2, reader.Parse<int>("Value"));
		// Empty rows and rows of whitespace are skipped, as empty lines are.
		Assert.True(reader.Read());
		Assert.Equal(5, reader.LineNumber);
		Assert.Null(reader.GetStringOrNull(0));
		Assert.True(reader.IsNull(0));
		// Not a comment, and no escapes: the text as the source gave it.
		Assert.True(reader.Read());
		Assert.Equal("#a\\,b", reader.GetString(0));
		Assert.False(reader.Read());
	}

	[Fact]
	public void ASourceMustEndTheRecordsItStarts()
	{
		var source = new FakeRecordSource("Broken", [new FakeRow(1, Text("A"))]) { LeaveRecordOpen = true };
		using var reader = new CsvReader(source, null);

		Assert.Throws<InvalidOperationException>(() => reader.Read());
	}

	[Fact]
	public void GeneratedRecordWritersWriteTypedCells()
	{
		List<CellOrder> orders;
		using (var reader = OrderReader())
		{
			orders = reader.GetRecords<CellOrder>().ToList();
		}

		var sink = new FakeCellSink();
		using (var writer = new CsvWriter(sink, null))
		{
			writer.WriteRecords(orders);
			Assert.Equal(4, writer.RecordCount);
		}

		Assert.Equal(4, sink.Records.Count);
		// The header is the first record (the writer does not mark it).
		Assert.Equal(["Name", "Quantity", "Price", "Total", "Paid", "Ordered", "Serial", "Link", "Note"], sink.Records[0].Cells.Select(c => c.Text));
		Assert.All(sink.Records[0].Cells, c => Assert.Equal(typeof(string), c.Type));

		// Each field as CSV formats it (the invariant culture, dates in "O"), with its type.
		var widget = sink.Records[1].Cells;
		Assert.Equal(new FakeCell(typeof(string), "Widget"), widget[0]);
		Assert.Equal(new FakeCell(typeof(int), "3"), widget[1]);
		Assert.Equal(new FakeCell(typeof(double), "1.5"), widget[2]);
		Assert.Equal(new FakeCell(typeof(decimal), "4.5"), widget[3]);
		Assert.Equal(new FakeCell(typeof(bool), "True"), widget[4]);
		Assert.Equal(new FakeCell(typeof(DateTime), "2024-03-01T00:00:00.0000000"), widget[5]);
		Assert.Equal(new FakeCell(typeof(double), "45352.5"), widget[6]);
		// The generated writer writes a Uri as a Uri, so the sink can make it a link.
		Assert.Equal(new FakeCell(typeof(Uri), "https://example.com/a?x=1"), widget[7]);
		Assert.Equal(new FakeCell(typeof(string), ""), widget[8]);
		Assert.Equal(new FakeCell(typeof(Uri), "docs/readme.md"), sink.Records[2].Cells[7]);
		// Null members are empty fields.
		Assert.Equal(new FakeCell(typeof(string), ""), sink.Records[3].Cells[7]);
		// Disposing flushes, then completes.
		Assert.Equal(["End", "End", "End", "End", "Flush", "Complete"], sink.Events);
	}

	[Fact]
	public void WriteFieldOverloadsTellTheSinkWhatTheyWrite()
	{
		var sink = new FakeCellSink();
		using (var writer = new CsvWriter(sink, new CsvWriterOptions { NullValue = "NULL", TrueValue = "yes", ValidateFieldCount = false }))
		{
			writer.WriteHeader("A", "B");
			writer.WriteField("text");
			writer.WriteField((string?)null);
			writer.WriteField("quoted", CsvQuoting.Always);
			writer.WriteField(null, CsvQuoting.Always);
			writer.WriteField("span".AsSpan(), CsvQuoting.WhenNeededOrWhitespace);
			writer.WriteField("utf8"u8);
			// Of any size: the buffer grows as for CSV, and holds no field once the sink has it.
			writer.WriteField("");
			writer.WriteField(ReadOnlySpan<byte>.Empty);
			writer.WriteField(new string('x', 100_000));
			Assert.Equal(9, writer.FieldCount);
			writer.EndRecord();

			writer.WriteField(5);
			writer.WriteField(1.5, "F2");
			writer.WriteField(2.5, "F1", German);
			writer.WriteField(3.5, default, null);
			writer.WriteField((int?)null);
			writer.WriteField((long?)7);
			writer.WriteField(new DateTime(2024, 1, 2));
			writer.WriteField(new DateTime(2024, 1, 2), "yyyyMMdd");
			writer.WriteField(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
#if NET
			writer.WriteField(new DateOnly(2024, 1, 2));
#endif
			writer.EndRecord();

			writer.WriteField(true);
			writer.WriteField(new Uri("https://example.com/"));
			writer.WriteField((Uri?)null);
			writer.WriteField((object)42);
			writer.WriteField((object?)null);
			writer.WriteRecord("x", null);
		}

		Assert.Equal(["End", "End", "End", "End", "Flush", "Complete"], sink.Events);
		// No quotes, and no NullValue: a null is an empty field.
		Assert.Equal(
		[
			new FakeCell(typeof(string), "text"), new FakeCell(typeof(string), ""), new FakeCell(typeof(string), "quoted"),
			new FakeCell(typeof(string), ""), new FakeCell(typeof(string), "span"), new FakeCell(typeof(string), "utf8"),
			new FakeCell(typeof(string), ""), new FakeCell(typeof(string), ""), new FakeCell(typeof(string), new string('x', 100_000)),
		], sink.Records[1].Cells);

		// Formatted as CSV formats them: a format string or a culture applies, and dates without one are "O".
		var values = sink.Records[2].Cells;
		Assert.Equal(new FakeCell(typeof(int), "5"), values[0]);
		Assert.Equal(new FakeCell(typeof(double), "1.50"), values[1]);
		Assert.Equal(new FakeCell(typeof(double), "2,5"), values[2]);
		Assert.Equal(new FakeCell(typeof(double), "3.5"), values[3]);
		Assert.Equal(new FakeCell(typeof(int), ""), values[4]);
		Assert.Equal(new FakeCell(typeof(long), "7"), values[5]);
		Assert.Equal(new FakeCell(typeof(DateTime), "2024-01-02T00:00:00.0000000"), values[6]);
		Assert.Equal(new FakeCell(typeof(DateTime), "20240102"), values[7]);
		Assert.Equal(new FakeCell(typeof(DateTimeOffset), "2024-01-02T03:04:05.0000000+00:00"), values[8]);
#if NET
		Assert.Equal(new FakeCell(typeof(DateOnly), "2024-01-02"), values[9]);
#endif

		// No TrueValue: booleans as bool.ToString writes them.
		Assert.Equal(
		[
			new FakeCell(typeof(bool), "True"), new FakeCell(typeof(Uri), "https://example.com/"), new FakeCell(typeof(string), ""),
			new FakeCell(typeof(int), "42"), new FakeCell(typeof(object), ""), new FakeCell(typeof(string), "x"),
			new FakeCell(typeof(string), ""),
		], sink.Records[3].Cells);
	}

	[Fact]
	public void CsvOptionsDoNotChangeTheFieldsOfASink()
	{
		var options = new[]
		{
			new CsvWriterOptions { Mode = CsvMode.Escape },
			new CsvWriterOptions { Injection = CsvInjection.Escape, ShouldQuote = (_, _, _, _) => true },
			new CsvWriterOptions { Quoting = CsvQuoting.Always, NullValue = "NULL", TrueValue = "yes" },
			new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [2, 2, 2, 2] },
		};
		foreach (var option in options)
		{
			var sink = new FakeCellSink();
			using (var writer = new CsvWriter(sink, option))
			{
				writer.WriteField("=a,b\"c\r\n  long text  ");
				writer.WriteField(1.5);
				writer.WriteField((string?)null);
				writer.WriteField(true);
				writer.EndRecord();
			}

			var cells = Assert.Single(sink.Records).Cells;
			Assert.Equal(new FakeCell(typeof(string), "=a,b\"c\r\n  long text  "), cells[0]);
			Assert.Equal(new FakeCell(typeof(double), "1.5"), cells[1]);
			Assert.Equal(new FakeCell(typeof(string), ""), cells[2]);
			Assert.Equal(new FakeCell(typeof(bool), "True"), cells[3]);
		}
	}

	[Fact]
	public void WritersWithASinkCheckRecordsAsForCsv()
	{
		var sink = new FakeCellSink();
		var writer = new CsvWriter(sink, null);

		writer.WriteField("a");
		writer.WriteField("b");
		writer.EndRecord();
		writer.WriteField("c");
		Assert.Throws<InvalidOperationException>(() => writer.EndRecord());
		Assert.Equal(0, writer.FieldCount);
		Assert.Throws<InvalidOperationException>(() => writer.EndRecord());

		// A record that fails while its fields are written is discarded.
		Assert.Throws<FormatException>(() => writer.WriteRecords(["ok", "bad", "never"], new FailingRecordWriter()));
		Assert.Throws<NotSupportedException>(() => writer.WriteComment("note"));
		Assert.Throws<NotSupportedException>(() => writer.ParallelWriteRecords(new[] { "a" }, new FailingRecordWriter()));
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(() => writer.WriteField("late"));
		Assert.Throws<ObjectDisposedException>(() => writer.WriteField(1));

		Assert.Equal(["End", "Discard", "End", "Discard", "Flush", "Complete"], sink.Events);
		Assert.Equal([["a", "b"], ["ok", "2"]], sink.Records.Select(r => r.Cells.Select(c => c.Text).ToArray()));
	}

	[Fact]
	public async Task SinksAreFlushedAsTheCallerWrites()
	{
		var records = Enumerable.Range(0, 7).Select(i => new CellQuantity { Name = "n" + i, Quantity = i }).ToList();

		var sync = new FakeCellSink { FlushEvery = 3 };
		using (var writer = new CsvWriter(sync, null))
		{
			writer.WriteRecords(records);
			writer.WriteField("by hand");
			writer.WriteField(1);
			writer.EndRecord();
			writer.Flush();
		}

		// The header, 7 records and one by hand: a flush after every third (the header counts), the explicit one, and one
		// on disposal.
		Assert.Equal(
			["End", "End", "End", "Flush", "End", "End", "End", "Flush", "End", "End", "End", "Flush", "Flush", "Flush", "Complete"],
			sync.Events);

		var async = new FakeCellSink { FlushEvery = 3 };
		await using (var writer = new CsvWriter(async, null))
		{
			await writer.WriteRecordsAsync(records);
			writer.WriteField("by hand");
			writer.WriteField(1);
			await writer.EndRecordAsync();
			await writer.FlushAsync();
		}

		Assert.Equal(
			[
				"End", "End", "End", "FlushAsync", "End", "End", "End", "FlushAsync", "End", "End", "End", "FlushAsync",
				"FlushAsync", "FlushAsync", "CompleteAsync",
			],
			async.Events);
		Assert.Equal(9, async.Records.Count);
	}

	[Fact]
	public void UrisAreWrittenToCsvAsTheirOriginalString()
	{
		var uris = new[] { new Uri("https://example.com/a b?x=1,2"), new Uri("docs/read me.md", UriKind.Relative), new Uri("\"quoted\"", UriKind.Relative) };
		foreach (var options in new[] { new CsvWriterOptions(), new CsvWriterOptions { Injection = CsvInjection.Escape }, new CsvWriterOptions { Mode = CsvMode.Escape } })
		{
			using var asUris = new MemoryStream();
			using var asStrings = new MemoryStream();
			using (var a = CsvWriter.Create(asUris, options, leaveOpen: true))
			using (var b = CsvWriter.Create(asStrings, options, leaveOpen: true))
			{
				foreach (var uri in uris)
				{
					a.WriteField(uri);
					b.WriteField(uri.OriginalString);
				}

				a.WriteField((Uri?)null);
				b.WriteField((string?)null);
				a.EndRecord();
				b.EndRecord();
			}

			Assert.Equal(asStrings.ToArray(), asUris.ToArray());
		}

		// The generated writer: byte for byte what it wrote before WriteField(Uri), the original strings.
		var orders = new List<CellOrder>
		{
			new() { Name = "a", Link = new Uri("https://example.com/a?x=1,2") },
			new() { Name = "b", Link = new Uri("docs/read me.md", UriKind.Relative) },
			new() { Name = "c" },
		};
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords(orders);
		}

		var lines = output.ToString().Split(["\r\n"], StringSplitOptions.None);
		Assert.EndsWith(",\"https://example.com/a?x=1,2\",", lines[1]);
		Assert.EndsWith(",docs/read me.md,", lines[2]);
		Assert.EndsWith(",0,,", lines[3]);
	}
}
