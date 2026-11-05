using System.Data;
using System.Globalization;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order of the report without its name, read with a generated mapper.</summary>
[CsvRecord]
public partial class HiddenOrder
{
	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }
}

/// <summary>
/// Skipping hidden rows and columns through the public API: what each option reads, with a sheet, a range and a table,
/// through every way of reading, and the cells that errors name. Each test runs synchronously and asynchronously.
/// </summary>
public class HiddenReadingTests
{
	/// <summary>
	/// A workbook of orders with a hidden column <c>D</c> ("Secret"), a hidden row 3 (pear), a row a filter hid (row 5,
	/// fig) and a collapsed group (row 6, kiwi), under the table <c>Orders</c> (<c>A1:E7</c>); and "Shown", which hides
	/// every row and column it doesn't list, as Excel writes a sheet with rows hidden from 4 and columns from C.
	/// </summary>
	/// <param name="paid">Plum's paid cell, as it goes in the cell.</param>
	/// <returns>The package.</returns>
	internal static TestPackage Report(string paid = "<v>1</v>") => TestPackage.Workbook(
			new TestSheet("Report", xml: Sheet.Sheets.Worksheet(
				"",
				"<col min=\"1\" max=\"3\" width=\"9\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>",
				$"<row r=\"1\">{T("A1", "Name")}{T("B1", "Quantity")}{T("C1", "Price")}{T("D1", "Secret")}{T("E1", "Paid")}</row>" +
				Order(2, "", "apple", 3, "1.5", "<v>1</v>") +
				Order(3, " hidden=\"1\"", "pear", 10, "0.25", "<v>0</v>") +
				Order(4, "", "plum", 7, "2", paid) +
				Order(5, " hidden=\"1\"", "fig", 1, "3", "<v>0</v>") +
				Order(6, " outlineLevel=\"1\" hidden=\"1\"", "kiwi", 2, "4", "<v>1</v>") +
				Order(7, " collapsed=\"1\"", "lime", 4, "0.5", "<v>0</v>"))),
			new TestSheet("Shown", xml: Sheet.Sheets.Worksheet(
				"defaultColWidth=\"0\" zeroHeight=\"1\"",
				"<col min=\"1\" max=\"2\" width=\"9\" customWidth=\"1\"/><col min=\"3\" max=\"16384\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>",
				$"<row r=\"1\">{T("A1", "a")}{T("B1", "b")}{T("C1", "c")}</row><row r=\"3\">{T("A3", "x")}</row>" +
				$"<row r=\"5\" hidden=\"1\">{T("A5", "y")}</row><row r=\"6\">{T("A6", "z")}</row>")))
		.Table("xl/worksheets/sheet1.xml", TestPackage.Declaration + TablePartTests.Table("displayName=\"Orders\" ref=\"A1:E7\"", "Name", "Quantity", "Price", "Secret", "Paid"));

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SkipsHiddenRows(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);

		// The rows Excel shows: each keeps its row as its line.
		using (var reader = await workbook.OpenReaderAsync(async, "Report", new ExcelReaderOptions { SkipHiddenRows = true }))
		{
			var orders = new List<string>();
			var lines = new List<long>();
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				var order = reader.GetRecord<AreaOrder>();
				orders.Add($"{order.Name} {order.Quantity}");
				lines.Add(reader.LineNumber);
			}

			Assert.Equal(["apple 3", "plum 7", "lime 4"], orders);
			Assert.Equal([2L, 4L, 7L], lines);
		}

		// By default, every row.
		using (var reader = await workbook.OpenReaderAsync(async, "Report"))
		{
			Assert.Equal(6, (await reader.ReadAllAsync(async)).Count);
		}

		// A sheet that hides the rows it lacks: none of them is a record.
		using (var reader = await workbook.OpenReaderAsync(async, "Shown", new ExcelReaderOptions { SkipHiddenRows = true, Csv = new() { HasHeader = false, SkipEmptyLines = false } }))
		{
			Assert.Equal(["a|b|c", "x||", "z||"], await reader.ReadAllAsync(async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, "Shown", new ExcelReaderOptions { Csv = new() { HasHeader = false, SkipEmptyLines = false } }))
		{
			Assert.Equal(["a|b|c", "", "x||", "", "y||", "z||"], await reader.ReadAllAsync(async));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SkipsHiddenColumns(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);

		// The columns Excel shows: Paid is field 3.
		using (var reader = await workbook.OpenReaderAsync(async, "Report", new ExcelReaderOptions { SkipHiddenColumns = true, SkipHiddenRows = true }))
		{
			Assert.Equal(["apple|3|1.5|TRUE", "plum|7|2|TRUE", "lime|4|0.5|FALSE"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Quantity", "Price", "Paid"], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, "Report", new ExcelReaderOptions { SkipHiddenColumns = true }))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal(6, records.Count);
			Assert.Equal("plum", records[2]["Name"]);
			Assert.Equal(["Name", "Quantity", "Price", "Paid"], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, "Shown", new ExcelReaderOptions { SkipHiddenColumns = true, Csv = new() { HasHeader = false } }))
		{
			Assert.Equal(["a|b", "x|", "y|", "z|"], await reader.ReadAllAsync(async));
		}

		// In a range: its shown columns, from its first.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Report!C1:E4", SkipHiddenColumns = true }))
		{
			Assert.Equal(["Price", "Paid"], reader.Headers);
			Assert.Equal(["1.5|TRUE", "0.25|FALSE", "2|TRUE"], await reader.ReadAllAsync(async));
		}

		// By default, every column.
		using (var reader = await workbook.OpenReaderAsync(async, "Report"))
		{
			Assert.Equal("apple|3|1.5|s2|TRUE", (await reader.ReadAllAsync(async))[0]);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsATablesShownCells(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var options = new ExcelReaderOptions { Table = "Orders", SkipHiddenRows = true, SkipHiddenColumns = true };

		// Its header leaves out the name of its hidden column.
		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(["apple|3|1.5|TRUE", "plum|7|2|TRUE", "lime|4|0.5|FALSE"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Quantity", "Price", "Paid"], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var orders = async ? await ListAsync(reader.GetRecordsAsync<AreaOrder>()) : reader.GetRecords<AreaOrder>().ToList();
			Assert.Equal(["apple 3 1.5 True", "plum 7 2 True", "lime 4 0.5 False"], orders.Select(o => $"{o.Name} {o.Quantity} {o.Price.ToString(CultureInfo.InvariantCulture)} {o.Paid}"));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			using var data = new CsvDataReader(reader);
			var table = new DataTable();
			table.Load(data);
			Assert.Equal(["Name", "Quantity", "Price", "Paid"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
			Assert.Equal(3, table.Rows.Count);
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(["plum", "lime"], reader.Query<AreaOrder>().Where(o => o.Quantity > 3).Select(o => o.Name).ToList());
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
		{
			Assert.Equal(["apple", "lime", "plum"], reader.ParallelEnumerate<AreaOrder>(2).Select(o => o.Name).OrderBy(n => n, StringComparer.Ordinal).ToList());
		}

		// In one call.
		var stream = Report().ToStream();
		using (var reader = async ? await ExcelReader.CreateAsync(stream, options) : ExcelReader.Create(stream, options))
		{
			Assert.Equal(3, reader.GetRecords<AreaOrder>().Count());
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheCellsOfFieldsThatDontParse(bool async)
	{
		// Plum's Paid is field 3 of the sheet and the table with D hidden, and field 2 of B:E; its cell is E4.
		using var workbook = await Workbooks.OpenAsync(Report(paid: "<is><t>maybe</t></is>"), async);
		const string cell = "Report!E4";
		foreach (var options in new[]
		{
			new ExcelReaderOptions { Sheet = "Report", SkipHiddenColumns = true, SkipHiddenRows = true },
			new ExcelReaderOptions { Table = "Orders", SkipHiddenColumns = true },
			new ExcelReaderOptions { Range = "Report!B1:E7", SkipHiddenColumns = true },
		})
		{
			using (var reader = await workbook.OpenReaderAsync(async, options))
			{
				var e = Assert.Throws<CsvFormatException>(() => Read(reader, options));
				Assert.Equal(cell, e.Location);
				Assert.Equal(4, e.LineNumber);
			}

			var errors = new List<CsvRecordError>();
			using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = options.Csv with { OnRecordError = error => { errors.Add(error); return true; } } }))
			{
				Read(reader, options);
				Assert.Equal(cell, Assert.Single(errors).Location);
			}

			using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
			{
				var e = Assert.ThrowsAny<Exception>(() => options.Range is null ? reader.ParallelEnumerate<AreaOrder>(2).Count() : reader.ParallelEnumerate<HiddenOrder>(2).Count());
				var format = e as CsvFormatException ?? e.InnerException as CsvFormatException ?? Assert.IsType<CsvFormatException>(Assert.Single(((AggregateException)e).InnerExceptions));
				Assert.Equal(cell, format.Location);
			}
		}

		static void Read(CsvReader reader, ExcelReaderOptions options)
		{
			if (options.Range is null)
			{
				_ = reader.GetRecords<AreaOrder>().ToList();
			}
			else
			{
				_ = reader.GetRecords<HiddenOrder>().ToList();
			}
		}
	}

	/// <summary>A row of an order.</summary>
	/// <param name="row">The row.</param>
	/// <param name="attributes">The row's other attributes, with a space before each.</param>
	/// <param name="name">The name.</param>
	/// <param name="quantity">The quantity.</param>
	/// <param name="price">The price.</param>
	/// <param name="paid">The paid cell's content.</param>
	/// <returns>The row's XML.</returns>
	private static string Order(int row, string attributes, string name, int quantity, string price, string paid) =>
		$"<row r=\"{row}\"{attributes}>{T($"A{row}", name)}<c r=\"B{row}\"><v>{quantity}</v></c><c r=\"C{row}\"><v>{price}</v></c>{T($"D{row}", $"s{row}")}" +
		$"<c r=\"E{row}\"{(paid.StartsWith("<is>", StringComparison.Ordinal) ? " t=\"inlineStr\"" : " t=\"b\"")}>{paid}</c></row>";

	/// <summary>Collects the items of an asynchronous sequence.</summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The sequence.</param>
	/// <returns>The items.</returns>
	private static async Task<List<T>> ListAsync<T>(IAsyncEnumerable<T> items)
	{
		var list = new List<T>();
		await foreach (var item in items)
		{
			list.Add(item);
		}

		return list;
	}

	/// <summary>An inline string cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string T(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";
}
