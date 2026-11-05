using System.Data;
using System.Globalization;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order with a column the report doesn't have.</summary>
[CsvRecord]
public partial class AreaOrderWithTotal
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A column no row has.</summary>
	public decimal Total { get; set; }
}

/// <summary>A type without a mapper.</summary>
public class UnmappedOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";
}

/// <summary>
/// The header row (<see cref="ExcelReaderOptions.HeaderRow"/>) through the public API: a report's header found below its
/// title or given, through every way of reading; the cells errors name after the columns are clipped; the messages when
/// no row will do; the options' checks; <see cref="ExcelHeaderRow"/> itself; and the search's read, counted. Each test
/// runs synchronously and asynchronously, on <see cref="AreaReadingTests.Report"/>'s "Messy" sheet: a title and a note
/// above the header at <c>B4</c>, a row label left of the orders, notes right of them, and a total below them.
/// </summary>
public class HeaderReadingTests
{
	/// <summary>The orders of the report's block.</summary>
	private static readonly string[] Orders = ["apple 3 1.5 True", "pear 10 0.25 False", "plum 7 2 True"];

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FindsTheHeaderBelowATitle(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(AreaReadingTests.Report(), async);
		var options = new ExcelReaderOptions { Range = "Messy!A1:G7", HeaderRow = ExcelHeaderRow.Find<AreaOrder>() };

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(Orders, await ReadOrdersAsync(reader, async));
			Assert.Equal(["Name", "Quantity", "Price", "Paid", "", "notes"], reader.Headers);
		}

		// The whole sheet, by names; the row label in A5 is left of the header, so not read.
		using (var reader = await workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find(["name", "PAID"]) }))
		{
			Assert.Equal(["apple|3|1.5|TRUE||n5", "pear|10|0.25|FALSE||", "plum|7|2|TRUE||", "Total|20||||"], await reader.ReadAllAsync(async));
		}

		// The row given; and a defined name, whose first row is the header.
		using (var reader = await workbook.OpenReaderAsync(async, options with { HeaderRow = ExcelHeaderRow.At(4) }))
		{
			Assert.Equal(Orders, await ReadOrdersAsync(reader, async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { DefinedName = "Data", HeaderRow = ExcelHeaderRow.Find<AreaOrder>() }))
		{
			Assert.Equal(Orders, await ReadOrdersAsync(reader, async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { HeaderRow = ExcelHeaderRow.Find<dynamic>() }))
		{
			// Dynamic records require no columns: the title's row is the header, and its one column all that is read.
			Assert.Equal(["Quarterly report"], reader.Headers);
			Assert.Equal(["for Q1", "x"], reader.GetRecords<CsvDynamicRecord>().Select(r => (string?)r.Detach()["Quarterly report"]).Where(v => !string.IsNullOrEmpty(v)));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal(["apple", "pear", "plum"], records.Select(r => (string?)r["Name"]));
			Assert.Equal(["n5", "", ""], records.Select(r => (string?)r["notes"] ?? ""));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			using var data = new CsvDataReader(reader);
			var table = new DataTable();
			table.Load(data);
			Assert.Equal(6, table.Columns.Count);
			Assert.Equal(["apple", "pear", "plum"], table.Rows.Cast<DataRow>().Select(r => (string)r["Name"]));
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(["pear", "plum"], reader.Query<AreaOrder>().Where(o => o.Quantity > 5).Select(o => o.Name).ToList());
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
		{
			Assert.Equal(["apple", "pear", "plum"], reader.ParallelEnumerate<AreaOrder>(2).Select(o => o.Name).OrderBy(n => n, StringComparer.Ordinal).ToList());
		}

		// In one call.
		var stream = AreaReadingTests.Report().ToStream();
		using (var reader = async ? await ExcelReader.CreateAsync(stream, options) : ExcelReader.Create(stream, options))
		{
			Assert.Equal(Orders, await ReadOrdersAsync(reader, async));
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task NamesColumnsByTheTextsOfTypedCells(bool date1904, bool async)
	{
		// A date names its column as the reader's header names it, in the workbook's date system.
		var package = Workbooks.Data(
			$"<row r=\"1\">{T("A1", "Stock")}</row>" +
			$"<row r=\"2\">{T("A2", "Name")}<c r=\"B2\" s=\"1\"><v>{(date1904 ? 45306 - 1462 : 45306)}</v></c></row>" +
			$"<row r=\"3\">{T("A3", "apple")}<c r=\"B3\"><v>7</v></c></row>");
		if (date1904)
		{
			package = package.Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ");
		}

		using var workbook = await Workbooks.OpenAsync(package, async);

		using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find(["Name", "2024-01-15"]) });
		var record = Assert.Single(reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList());
		Assert.Equal(["Name", "2024-01-15"], reader.Headers);
		Assert.Equal("7", (string?)record["2024-01-15"]);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SkipsHiddenRowsButNotARowGiven(bool async)
	{
		// A hidden row of other names above the header.
		var package = Workbooks.Data(
			$"<row r=\"1\" hidden=\"1\">{T("A1", "Name")}{T("B1", "Code")}</row>" +
			$"<row r=\"2\">{T("A2", "Name")}{T("B2", "Quantity")}</row>" +
			$"<row r=\"3\">{T("A3", "apple")}<c r=\"B3\"><v>3</v></c></row>");
		using var workbook = await Workbooks.OpenAsync(package, async);
		var skipping = new ExcelReaderOptions { SkipHiddenRows = true, HeaderRow = ExcelHeaderRow.Find(["Name"]) };

		using (var reader = await workbook.OpenReaderAsync(async, skipping))
		{
			Assert.Equal(["apple|3"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Quantity"], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, skipping with { SkipHiddenRows = false }))
		{
			Assert.Equal(["Name|Quantity", "apple|3"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Code"], reader.Headers);
		}

		// Given, the hidden row is the header; the rows after it are read as the option says.
		using (var reader = await workbook.OpenReaderAsync(async, skipping with { HeaderRow = ExcelHeaderRow.At(1) }))
		{
			Assert.Equal(["Name|Quantity", "apple|3"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Code"], reader.Headers);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheCellsOfFieldsThatDontParse(bool async)
	{
		// Pear's quantity is field 1 of the clipped columns; its cell is C6.
		using var workbook = await Workbooks.OpenAsync(AreaReadingTests.Report(quantity: "<is><t>ten</t></is>"), async);
		var options = new ExcelReaderOptions { Range = "Messy!A1:G7", HeaderRow = ExcelHeaderRow.Find<AreaOrder>() };
		const string cell = "Messy!C6";

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<AreaOrder>().ToList());
			Assert.Equal(cell, e.Location);
			Assert.Equal(6, e.LineNumber);
		}

		var errors = new List<CsvRecordError>();
		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = options.Csv with { OnRecordError = error => { errors.Add(error); return true; } } }))
		{
			Assert.Equal(2, reader.GetRecords<AreaOrder>().Count());
			Assert.Equal(cell, Assert.Single(errors).Location);
		}

		using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
		{
			var e = Assert.ThrowsAny<Exception>(() => reader.ParallelEnumerate<AreaOrder>(2).Count());
			var format = e as CsvFormatException ?? e.InnerException as CsvFormatException ?? Assert.IsType<CsvFormatException>(Assert.Single(((AggregateException)e).InnerExceptions));
			Assert.Equal(cell, format.Location);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FailsWhenNoRowWillDo(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(AreaReadingTests.Report(), async);

		var e = await Assert.ThrowsAsync<KeyNotFoundException>(() => workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find<AreaOrderWithTotal>() }));
		Assert.Equal("No row of Messy!1:20 has the columns that AreaOrderWithTotal requires. The closest, row 4, lacks AreaOrderWithTotal.Total ('Total').", e.Message);
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Messy!B1:E3", HeaderRow = ExcelHeaderRow.Find<AreaOrder>() }));
		Assert.Equal("No row of Messy!B1:E3 has the columns that AreaOrder requires: none of them has a value.", e.Message);
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Messy!F1:F9", HeaderRow = ExcelHeaderRow.Find([]) }));
		Assert.Equal("No row of Messy!F1:F9 has a value.", e.Message);

		// A type without a mapper fails as GetRecords does, whether or not a row has a value.
		await Assert.ThrowsAsync<InvalidOperationException>(() => workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find<UnmappedOrder>() }));
		await Assert.ThrowsAsync<InvalidOperationException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Messy!F1:F9", HeaderRow = ExcelHeaderRow.Find<UnmappedOrder>() }));

		var format = await Assert.ThrowsAsync<CsvFormatException>(() => workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(3) }));
		Assert.Equal("The header row, Messy!3:3, has no value.", format.Message);
		Assert.Equal("Messy!3:3", format.Location);

		var range = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Messy!B2:G9", HeaderRow = ExcelHeaderRow.At(1) }));
		Assert.StartsWith("The header row, 1, is not in the range B2:G9.", range.Message);
		Assert.Equal("options", range.ParamName);

		// The workbook reads on after a failure: nothing was left open.
		using var reader = await workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(4) });
		Assert.Equal(4, (await reader.ReadAllAsync(async)).Count);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheOptions(bool async)
	{
		var table = new ExcelReaderOptions { Table = "Sales", HeaderRow = ExcelHeaderRow.At(1) };
		var noHeader = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(1), Csv = new() { HasHeader = false } };
		const string tableMessage = "ExcelReaderOptions.HeaderRow can't be set with ExcelReaderOptions.Table: a table's header is its column names.";
		const string noHeaderMessage = "ExcelReaderOptions.HeaderRow needs ExcelReaderOptions.Csv.HasHeader.";

		// When the workbook opens with them, and when a reader does.
		var e = await Assert.ThrowsAsync<ArgumentException>(() => Workbooks.OpenAsync(AreaReadingTests.Report(), async, table));
		Assert.StartsWith(tableMessage, e.Message);
		e = await Assert.ThrowsAsync<ArgumentException>(() => Workbooks.OpenAsync(AreaReadingTests.Report(), async, noHeader));
		Assert.StartsWith(noHeaderMessage, e.Message);

		using var workbook = await Workbooks.OpenAsync(AreaReadingTests.Report(), async);
		e = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, table));
		Assert.StartsWith(tableMessage, e.Message);
		Assert.Equal("options", e.ParamName);
		e = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, "Messy", noHeader));
		Assert.StartsWith(noHeaderMessage, e.Message);
	}

	[Fact]
	public void MakesHeaderRows()
	{
		Assert.Equal("ExcelHeaderRow.At(4)", ExcelHeaderRow.At(4).ToString());
		Assert.Equal("ExcelHeaderRow.At(1048576)", ExcelHeaderRow.At(1048576).ToString());
		Assert.Equal("ExcelHeaderRow.Find<AreaOrder>(20)", ExcelHeaderRow.Find<AreaOrder>().ToString());
		Assert.Equal("ExcelHeaderRow.Find<Object>(1)", ExcelHeaderRow.Find<dynamic>(1).ToString());
		Assert.Equal("ExcelHeaderRow.Find([\"Name\", \"Unit Price\"], 1048576)", ExcelHeaderRow.Find(["Name", "Unit Price"], 1048576).ToString());
		Assert.Equal("ExcelHeaderRow.Find([], 20)", ExcelHeaderRow.Find([]).ToString());

		foreach (var row in new[] { 0, -1, 1048577 })
		{
			var e = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelHeaderRow.At(row));
			Assert.StartsWith("A row is from 1 to 1048576.", e.Message);
			Assert.Equal("row", e.ParamName);
		}

		foreach (var maxRows in new[] { 0, -1, 1048577 })
		{
			var e = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelHeaderRow.Find<AreaOrder>(maxRows));
			Assert.StartsWith("maxRows must be from 1 to 1048576.", e.Message);
			Assert.Equal("maxRows", e.ParamName);
			e = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelHeaderRow.Find(["Name"], maxRows));
			Assert.Equal("maxRows", e.ParamName);
		}

		Assert.Equal("names", Assert.Throws<ArgumentNullException>(() => ExcelHeaderRow.Find(null!)).ParamName);
		var nullName = Assert.Throws<ArgumentException>(() => ExcelHeaderRow.Find(["Name", null!]));
		Assert.StartsWith("A name can't be null.", nullName.Message);
		Assert.Equal("names", nullName.ParamName);

		// The names are copied: changing the list after changes nothing.
		var names = new List<string> { "Name" };
		var header = ExcelHeaderRow.Find(names);
		names[0] = "Other";
		Assert.Equal("ExcelHeaderRow.Find([\"Name\"], 20)", header.ToString());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsTheSearchsRead(bool async)
	{
		// A limit the sheet passes once, but not twice: a search that reads all its rows to find "Total" at the end reads
		// the sheet twice, while one that finds "Text" in the first row reads one window of it again.
		var package = Workbooks.Data(Workbooks.Numbers(5000) + $"<row r=\"5002\">{T("A5002", "Total")}</row>");
		var size = package.Text(Sheet.Sheets.PartName).Length;
		var options = new ExcelReaderOptions { MaxUncompressedSize = size + (size / 2) };

		using (var workbook = await Workbooks.OpenAsync(package, async, options))
		using (var reader = await workbook.OpenReaderAsync(async, options with { HeaderRow = ExcelHeaderRow.Find(["Text"]) }))
		{
			Assert.Equal(5001, (await reader.ReadAllAsync(async)).Count);
		}

		using (var workbook = await Workbooks.OpenAsync(package, async, options))
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
			{
				using var reader = await workbook.OpenReaderAsync(async, options with { HeaderRow = ExcelHeaderRow.Find(["Total"], 1048576) });
				await reader.ReadAllAsync(async);
			});
			Assert.Contains("ExcelReaderOptions.MaxUncompressedSize", e.Message);
		}
	}

	/// <summary>Reads a reader's orders, described.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The orders.</returns>
	private static async Task<List<string>> ReadOrdersAsync(CsvReader reader, bool async)
	{
		var orders = new List<AreaOrder>();
		if (async)
		{
			await foreach (var order in reader.GetRecordsAsync<AreaOrder>())
			{
				orders.Add(order);
			}
		}
		else
		{
			orders.AddRange(reader.GetRecords<AreaOrder>());
		}

		return [.. orders.Select(o => $"{o.Name} {o.Quantity} {o.Price.ToString(CultureInfo.InvariantCulture)} {o.Paid}")];
	}

	/// <summary>An inline string cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string T(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";
}
