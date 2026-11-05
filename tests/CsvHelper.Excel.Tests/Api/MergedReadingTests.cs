using System.Data;
using System.Globalization;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Sheet;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order of the report by region, read with a generated mapper.</summary>
[CsvRecord]
public partial class MergedOrder
{
	/// <summary>A text cell, merged down the region's orders.</summary>
	public string? Region { get; set; }

	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }
}

/// <summary>
/// Filling merged cells through the public API: what the option reads, through every way of reading; rows the file
/// lacks; ranges Excel can't open, refused when the reader opens; the cells that errors name; the second read of the
/// sheet, counted; and sheets without ranges. Each test runs synchronously and asynchronously.
/// </summary>
public class MergedReadingTests
{
	/// <summary>
	/// A report as Excel lays one out: a title merged across <c>A1:D1</c>, the header at row 3, and each region merged down
	/// its orders (<c>A4:A6</c>, North, whose other cells are empty and styled, as Excel writes them; <c>A7:A8</c>, South,
	/// whose other cell holds a stale value, as other producers can write); "Lacks", whose group is merged down rows the
	/// file leaves out; and "Overlap", whose ranges overlap.
	/// </summary>
	/// <param name="southPrice">The price cell of fig (<c>D7</c>), as it goes in the cell.</param>
	/// <returns>The package.</returns>
	internal static TestPackage Report(string southPrice = "<v>3</v>") => TestPackage.Workbook(
		new TestSheet("Report", xml: MergedScannerTests.Worksheet(
			$"<row r=\"1\">{T("A1", "Orders by region")}<c r=\"B1\" s=\"1\"/><c r=\"C1\" s=\"1\"/><c r=\"D1\" s=\"1\"/></row>" +
			$"<row r=\"3\">{T("A3", "Region")}{T("B3", "Name")}{T("C3", "Quantity")}{T("D3", "Price")}</row>" +
			Order(4, T("A4", "North"), "apple", 3, "<v>1.5</v>") +
			Order(5, "<c r=\"A5\" s=\"1\"/>", "pear", 10, "<v>0.25</v>") +
			Order(6, "<c r=\"A6\" s=\"1\"/>", "plum", 7, "<v>2</v>") +
			Order(7, T("A7", "South"), "fig", 1, southPrice) +
			Order(8, T("A8", "stale"), "kiwi", 2, "<v>4</v>"),
			"A1:D1", "A4:A6", "A7:A8")),
		new TestSheet("Lacks", xml: MergedScannerTests.Worksheet(
			$"<row r=\"1\">{T("A1", "Group")}{T("B1", "Item")}</row><row r=\"2\">{T("A2", "G1")}{T("B2", "a")}</row><row r=\"4\">{T("B4", "c")}</row>",
			"A2:A4")),
		new TestSheet("Overlap", xml: MergedScannerTests.Worksheet($"<row r=\"1\">{T("A1", "a")}</row>", "A1:B2", "B2:C3")));

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FillsMergedCells(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var options = new ExcelReaderOptions { Range = "Report!A3:D8", FillMergedCells = true };

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var orders = async ? await ListAsync(reader.GetRecordsAsync<MergedOrder>()) : reader.GetRecords<MergedOrder>().ToList();
			Assert.Equal(
				["North apple 3", "North pear 10", "North plum 7", "South fig 1", "South kiwi 2"],
				orders.Select(o => $"{o.Region} {o.Name} {o.Quantity.ToString(CultureInfo.InvariantCulture)}"));
		}

		// Without the option, each cell reads what it stores.
		using (var reader = await workbook.OpenReaderAsync(async, options with { FillMergedCells = false }))
		{
			Assert.Equal(["North", "", "", "South", "stale"], reader.GetRecords<MergedOrder>().Select(o => o.Region ?? ""));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal(["North", "North", "North", "South", "South"], records.Select(r => (string?)r["Region"]));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			using var data = new CsvDataReader(reader);
			var table = new DataTable();
			table.Load(data);
			Assert.Equal(["North", "North", "North", "South", "South"], table.Rows.Cast<DataRow>().Select(r => (string)r["Region"]));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(["apple", "pear", "plum"], reader.Query<MergedOrder>().Where(o => o.Region == "North").Select(o => o.Name).ToList());
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
		{
			Assert.Equal(
				["North apple", "North pear", "North plum", "South fig", "South kiwi"],
				reader.ParallelEnumerate<MergedOrder>(2).Select(o => $"{o.Region} {o.Name}").OrderBy(n => n, StringComparer.Ordinal).ToList());
		}

		// The whole sheet: the title across its columns.
		using (var reader = await workbook.OpenReaderAsync(async, "Report", new ExcelReaderOptions { FillMergedCells = true, Csv = new() { HasHeader = false } }))
		{
			var rows = await reader.ReadAllAsync(async);
			Assert.Equal("Orders by region|Orders by region|Orders by region|Orders by region", rows[0]);
			Assert.Equal("North|plum|7|2", rows[4]);
		}

		// In one call.
		var stream = Report().ToStream();
		using (var reader = async ? await ExcelReader.CreateAsync(stream, options) : ExcelReader.Create(stream, options))
		{
			Assert.Equal(5, reader.GetRecords<MergedOrder>().Count(o => o.Region is not null));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FillsTheRowsTheFileLacks(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);

		// Row 3 isn't in the file: the group fills it, with its row as its line.
		using var reader = await workbook.OpenReaderAsync(async, "Lacks", new ExcelReaderOptions { FillMergedCells = true });
		var rows = new List<string>();
		var lines = new List<long>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			rows.Add(Workbooks.Fields(reader));
			lines.Add(reader.LineNumber);
		}

		Assert.Equal(["G1|a", "G1|", "G1|c"], rows);
		Assert.Equal([2L, 3, 4], lines);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefusesRangesThatOverlapWhenTheReaderOpens(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);

		var e = await Assert.ThrowsAsync<CsvFormatException>(() => workbook.OpenReaderAsync(async, "Overlap", new ExcelReaderOptions { FillMergedCells = true }));
		Assert.Matches("^Sheet Overlap can't be read at byte [0-9]+ of xl/worksheets/sheet3.xml: its merged cells A1:B2 and B2:C3 overlap.$", e.Message);

		// Without the option, the ranges aren't read.
		using var reader = await workbook.OpenReaderAsync(async, "Overlap", new ExcelReaderOptions { Csv = new() { HasHeader = false } });
		Assert.Equal(["a"], await reader.ReadAllAsync(async));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheCellsOfFieldsThatDontParse(bool async)
	{
		// Fig's price is field 3 of the range, after its region's fill; its cell is D7.
		using var workbook = await Workbooks.OpenAsync(Report(southPrice: "<is><t>cheap</t></is>"), async);
		var options = new ExcelReaderOptions { Range = "Report!A3:D8", FillMergedCells = true };
		const string cell = "Report!D7";

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<MergedOrder>().ToList());
			Assert.Equal(cell, e.Location);
			Assert.Equal(7, e.LineNumber);
		}

		var errors = new List<CsvRecordError>();
		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = options.Csv with { OnRecordError = error => { errors.Add(error); return true; } } }))
		{
			Assert.Equal(4, reader.GetRecords<MergedOrder>().Count());
			Assert.Equal(cell, Assert.Single(errors).Location);
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
		{
			var e = Assert.ThrowsAny<Exception>(() => reader.ParallelEnumerate<MergedOrder>(2).Count());
			var format = e as CsvFormatException ?? e.InnerException as CsvFormatException ?? Assert.IsType<CsvFormatException>(Assert.Single(((AggregateException)e).InnerExceptions));
			Assert.Equal(cell, format.Location);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsTheSecondReadOfTheSheet(bool async)
	{
		// A limit the sheet passes once, but not twice.
		var package = TestPackage.Workbook(new TestSheet("Data", xml: MergedScannerTests.Worksheet(Workbooks.Numbers(5000), "A2:A3")));
		var size = package.Text(Sheets.PartName).Length;
		var options = new ExcelReaderOptions { MaxUncompressedSize = size + (size / 2) };

		using (var workbook = await Workbooks.OpenAsync(package, async, options))
		using (var reader = await workbook.OpenReaderAsync(async))
		{
			Assert.Equal(5000, (await reader.ReadAllAsync(async)).Count);
		}

		using (var workbook = await Workbooks.OpenAsync(package, async, options))
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
			{
				using var reader = await workbook.OpenReaderAsync(async, options with { FillMergedCells = true });
				await reader.ReadAllAsync(async);
			});
			Assert.Contains("ExcelReaderOptions.MaxUncompressedSize", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsASheetWithoutRangesAsWithoutTheOption(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);

		using var reader = await workbook.OpenReaderAsync(async, "Orders", new ExcelReaderOptions { FillMergedCells = true });
		Assert.Equal(Workbooks.OrderRows, await reader.ReadAllAsync(async));
	}

	/// <summary>A row of an order.</summary>
	/// <param name="row">The row.</param>
	/// <param name="region">The region's cell.</param>
	/// <param name="name">The name.</param>
	/// <param name="quantity">The quantity.</param>
	/// <param name="price">The price cell's content.</param>
	/// <returns>The row's XML.</returns>
	private static string Order(int row, string region, string name, int quantity, string price) =>
		$"<row r=\"{row}\">{region}{T($"B{row}", name)}<c r=\"C{row}\"><v>{quantity}</v></c>" +
		$"<c r=\"D{row}\"{(price.StartsWith("<is>", StringComparison.Ordinal) ? " t=\"inlineStr\"" : "")}>{price}</c></row>";

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
