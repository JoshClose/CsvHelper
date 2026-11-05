using System.Data;
using System.Globalization;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order of the report's block, read with a generated mapper.</summary>
[CsvRecord]
public partial class AreaOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }
}

/// <summary>A row of the Sales table.</summary>
[CsvRecord]
public partial class AreaSale
{
	/// <summary>A text cell.</summary>
	public string Product { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Qty { get; set; }
}

/// <summary>
/// Reading a range, a table and a defined name through the public API: what each reads, how it is chosen, the errors of
/// choosing, and the cells that errors name. Each test runs synchronously and asynchronously.
/// </summary>
public class AreaReadingTests
{
	/// <summary>
	/// A workbook laid out as people lay out reports: a chartsheet first; "Messy", with a title and a note above a block
	/// of orders at <c>B4:E7</c>, row labels left of it, notes right of it and a total below it; "Tables", with the table
	/// <c>Sales</c> (<c>A1:B4</c>, a totals row) and the table <c>NoHeader</c> (<c>D6:E6</c>, its header row hidden); and
	/// "Q1 Orders". Its defined names include the workbook's <c>Data</c>, sheets' names, a name of two sheets, a formula
	/// and <c>Print_Area</c>.
	/// </summary>
	/// <param name="quantity">The second order's quantity, as it goes in its cell.</param>
	/// <param name="qty">The second sale's quantity, as it goes in its cell.</param>
	/// <returns>The package.</returns>
	internal static TestPackage Report(string quantity = "<v>10</v>", string qty = "<v>2</v>") => TestPackage.Workbook(
			new TestSheet("Chart", type: "chartsheet"),
			new TestSheet("Messy", xml: Sheet.Sheets.Worksheet(
				$"<row r=\"1\">{T("A1", "Quarterly report")}</row>" +
				$"<row r=\"2\">{T("A2", "for Q1")}</row>" +
				$"<row r=\"4\">{T("B4", "Name")}{T("C4", "Quantity")}{T("D4", "Price")}{T("E4", "Paid")}{T("G4", "notes")}</row>" +
				$"<row r=\"5\">{T("A5", "x")}{T("B5", "apple")}<c r=\"C5\"><v>3</v></c><c r=\"D5\"><v>1.5</v></c><c r=\"E5\" t=\"b\"><v>1</v></c>{T("G5", "n5")}</row>" +
				$"<row r=\"6\">{T("B6", "pear")}<c r=\"C6\"{(quantity.StartsWith("<is>") ? " t=\"inlineStr\"" : "")}>{quantity}</c><c r=\"D6\"><v>0.25</v></c><c r=\"E6\" t=\"b\"><v>0</v></c></row>" +
				$"<row r=\"7\">{T("B7", "plum")}<c r=\"C7\"><v>7</v></c><c r=\"D7\"><v>2</v></c><c r=\"E7\" t=\"b\"><v>1</v></c></row>" +
				$"<row r=\"9\">{T("B9", "Total")}<c r=\"C9\"><v>20</v></c></row>")),
			new TestSheet("Tables", xml: Sheet.Sheets.Worksheet(
				$"<row r=\"1\">{T("A1", "Product")}{T("B1", "Qty")}</row>" +
				$"<row r=\"2\">{T("A2", "a")}<c r=\"B2\"><v>1</v></c></row>" +
				$"<row r=\"3\">{T("A3", "b")}<c r=\"B3\"{(qty.StartsWith("<is>") ? " t=\"inlineStr\"" : "")}>{qty}</c></row>" +
				$"<row r=\"4\">{T("A4", "Total")}<c r=\"B4\"><v>3</v></c></row>" +
				"<row r=\"6\"><c r=\"D6\"><v>1</v></c><c r=\"E6\"><v>2</v></c></row>")),
			new TestSheet("Q1 Orders", xml: Sheet.Sheets.Worksheet($"<row r=\"1\">{T("A1", "q1")}</row>")))
		.Table("xl/worksheets/sheet3.xml", TestPackage.Declaration + TablePartTests.Sales)
		.Table("xl/worksheets/sheet3.xml", TestPackage.Declaration + TablePartTests.NoHeader)
		.DefinedNames(
			"<definedName name=\"Data\">Messy!$B$4:$E$7</definedName>",
			"<definedName name=\"Local\" localSheetId=\"1\">Messy!$B$4:$C$5</definedName>",
			"<definedName name=\"Data\" localSheetId=\"2\">Tables!$A$1:$B$3</definedName>",
			"<definedName name=\"_xlnm.Print_Area\" localSheetId=\"1\">Messy!$A$1:$G$9</definedName>",
			"<definedName name=\"Twice\" localSheetId=\"1\">Messy!$B$4:$B$5</definedName>",
			"<definedName name=\"Twice\" localSheetId=\"2\">Tables!$A$1:$A$2</definedName>",
			"<definedName name=\"Dyn\">OFFSET(Messy!$B$4,0,0,3,4)</definedName>");

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsARange(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var orders = new[] { "apple 3 1.5 True", "pear 10 0.25 False", "plum 7 2 True" };

		using (var reader = await workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { Range = "B4:E7" }))
		{
			Assert.Equal(orders, await ReadOrdersAsync(reader, async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Messy!$B$4:$E$7" }))
		{
			Assert.Equal(orders, await ReadOrdersAsync(reader, async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "'Q1 Orders'!A1", Csv = new() { HasHeader = false } }))
		{
			Assert.Equal(["q1"], await reader.ReadAllAsync(async));
		}

		// Whole columns: the rows whose cells are all outside them have no value, so the header is row 4.
		using (var reader = await workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { Range = "b:e" }))
		{
			Assert.Equal(["apple|3|1.5|TRUE", "pear|10|0.25|FALSE", "plum|7|2|TRUE", "Total|20||"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Quantity", "Price", "Paid"], reader.Headers);
		}

		// Whole rows: field 0 is column A, and rows are padded to the header's width.
		using (var reader = await workbook.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { Range = "4:6" }))
		{
			Assert.Equal(["x|apple|3|1.5|TRUE||n5", "|pear|10|0.25|FALSE||"], await reader.ReadAllAsync(async));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepsARangesEmptyRows(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var options = new ExcelReaderOptions { Range = "Messy!B3:C6", Csv = new() { HasHeader = false, SkipEmptyLines = false } };

		using var reader = await workbook.OpenReaderAsync(async, options);
		var records = new List<string>();
		var lines = new List<long>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			records.Add(Workbooks.Fields(reader));
			lines.Add(reader.LineNumber);
		}

		// Record n is the range's row n.
		Assert.Equal(["", "Name|Quantity", "apple|3", "pear|10"], records);
		Assert.Equal([3L, 4L, 5L, 6L], lines);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsATable(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);

		// Its data rows, without its totals row, headed by its column names.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "sales" }))
		{
			var sales = async ? await ListAsync(reader.GetRecordsAsync<AreaSale>()) : reader.GetRecords<AreaSale>().ToList();
			Assert.Equal(["a 1", "b 2"], sales.Select(s => $"{s.Product} {s.Qty}"));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Sales", Csv = new() { HasHeader = false } }))
		{
			Assert.Equal(["a|1", "b|2"], await reader.ReadAllAsync(async));
		}

		// A table whose header row is hidden is headed by its names all the same; they have no row in the sheet (line 0).
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "NoHeader" }))
		{
			Assert.Equal(["X", "Y"], reader.Headers);
			Assert.Equal(0, reader.LineNumber);
			Assert.Equal(["1|2"], await reader.ReadAllAsync(async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Sales" }))
		{
			Assert.Equal(["Product", "Qty"], reader.Headers);
			Assert.Equal(1, reader.LineNumber);
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Sales" }))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal(["a", "b"], records.Select(r => r["Product"]));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "NoHeader" }))
		{
			using var data = new CsvDataReader(reader);
			var table = new DataTable();
			table.Load(data);
			Assert.Equal(["X", "Y"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
			Assert.Equal(["1|2"], table.Rows.Cast<DataRow>().Select(r => string.Join("|", r.ItemArray)));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Sales" }))
		{
			Assert.Equal(["b"], reader.Query<AreaSale>().Where(s => s.Qty > 1).Select(s => s.Product).ToList());
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task HeadsATableWithAColumnForEachOfItsCells(bool async)
	{
		// More cells than names: the extra columns have empty names. Fewer: the names past them are left out.
		var short1 = TablePartTests.Table("displayName=\"Short\" ref=\"A1:C3\"", "Product", "Qty");
		var long1 = TablePartTests.Table("displayName=\"Long\" ref=\"A1:A3\"", "Product", "Qty", "Extra");
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheet.Sheets.Worksheet(
				"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Product</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Qty</t></is></c></row>" +
				"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>a</t></is></c><c r=\"B2\"><v>1</v></c><c r=\"C2\"><v>9</v></c></row>")))
			.Table("xl/worksheets/sheet1.xml", short1)
			.Table("xl/worksheets/sheet1.xml", long1);
		using var workbook = await Workbooks.OpenAsync(package, async);

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Short" }))
		{
			Assert.Equal(["a|1|9"], await reader.ReadAllAsync(async));
			Assert.Equal(["Product", "Qty", ""], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Long" }))
		{
			Assert.Equal(["a"], await reader.ReadAllAsync(async));
			Assert.Equal(["Product"], reader.Headers);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsADefinedName(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var orders = new[] { "apple 3 1.5 True", "pear 10 0.25 False", "plum 7 2 True" };

		foreach (var name in new[] { "Data", "data" })
		{
			using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { DefinedName = name });
			Assert.Equal(orders, await ReadOrdersAsync(reader, async));
		}

		// A sheet's own name, with its sheet or, as the only one of that name, without.
		foreach (var name in new[] { "Messy!Local", "'Messy'!Local", "messy!LOCAL", "Local" })
		{
			using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { DefinedName = name });
			Assert.Equal(["apple|3"], await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Quantity"], reader.Headers);
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { DefinedName = "Tables!Data" }))
		{
			Assert.Equal(["a 1", "b 2"], reader.GetRecords<AreaSale>().Select(s => $"{s.Product} {s.Qty}"));
		}

		foreach (var name in new[] { "Print_Area", "_xlnm.Print_Area", "Messy!Print_Area", "Messy!_XLNM.print_area" })
		{
			using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { DefinedName = name, Csv = new() { HasHeader = false } });
			Assert.Equal("Quarterly report", (await reader.ReadAllAsync(async))[0]);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsNamesAndTablesThatArentThere(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var cases = new (ExcelReaderOptions Options, string Message)[]
		{
			(new() { DefinedName = "Twice" }, "The defined name \"Twice\" is a name of several sheets: Messy!Twice, Tables!Twice. Give one of them, such as Messy!Twice."),
			(new() { DefinedName = "Dyn" }, "The defined name \"Dyn\" refers to OFFSET(Messy!$B$4,0,0,3,4), which is not a block of cells on a worksheet."),
			(new() { DefinedName = "Missing" }, "The workbook has no defined name \"Missing\"."),
			(new() { DefinedName = "Tables!Local" }, "The workbook has no defined name \"Tables!Local\"."),
			(new() { DefinedName = "'Messy!Local" }, "The workbook has no defined name \"'Messy!Local\"."),
			(new() { DefinedName = "" }, "The workbook has no defined name \"\"."),
			(new() { Table = "Missing" }, "The workbook has no table named \"Missing\". Its tables are \"Sales\" (Tables!A1:B4), \"NoHeader\" (Tables!D6:E6)."),
			(new() { Range = "Gone!A1" }, "The workbook has no sheet named \"Gone\". Its sheets are \"Chart\" (chartsheet), \"Messy\", \"Tables\", \"Q1 Orders\"."),
		};
		foreach (var (options, message) in cases)
		{
			var e = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, options));
			Assert.StartsWith(message, e.Message);
			Assert.Equal("options", e.ParamName);
		}

		using var plain = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		var none = await Assert.ThrowsAsync<ArgumentException>(() => plain.OpenReaderAsync(async, new ExcelReaderOptions { Table = "Sales" }));
		Assert.StartsWith("The workbook has no table named \"Sales\". It has no tables.", none.Message);

		var chart = await Assert.ThrowsAsync<NotSupportedException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "Chart!A1" }));
		Assert.Equal("The sheet \"Chart\" is a chartsheet, and only worksheets have cells to read.", chart.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheTargetsOptions(bool async)
	{
		var cases = new (ExcelReaderOptions Options, string Message)[]
		{
			(new() { Range = "A1", Table = "Sales" }, "ExcelReaderOptions.Range, ExcelReaderOptions.Table and ExcelReaderOptions.DefinedName can't be set together."),
			(new() { Table = "Sales", DefinedName = "Data" }, "ExcelReaderOptions.Range, ExcelReaderOptions.Table and ExcelReaderOptions.DefinedName can't be set together."),
			(new() { Range = "B5:" }, "ExcelReaderOptions.Range, \"B5:\", is not a range of cells, such as B5:F200, B:F or 5:200."),
			(new() { Range = "Messy!A1,Messy!B2" }, "ExcelReaderOptions.Range, \"Messy!A1,Messy!B2\", is not a range of cells, such as B5:F200, B:F or 5:200."),
			(new() { Table = "Sales", Sheet = "Tables" }, "ExcelReaderOptions.Table chooses its own sheet, so ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't be set with it."),
			(new() { DefinedName = "Data", SheetIndex = 1 }, "ExcelReaderOptions.DefinedName chooses its own sheet, so ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't be set with it."),
			(new() { Range = "Messy!A1", Sheet = "Messy" }, "ExcelReaderOptions.Range names its sheet, so ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't be set with it."),
		};
		foreach (var (options, message) in cases)
		{
			// At open, as the workbook's options, and at a reader's open.
			var open = await Assert.ThrowsAsync<ArgumentException>(() => Workbooks.OpenAsync(Report(), async, options));
			Assert.StartsWith(message, open.Message);
			using var workbook = await Workbooks.OpenAsync(Report(), async);
			var e = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, options));
			Assert.StartsWith(message, e.Message);
			Assert.Equal("options", e.ParamName);
		}

		// A range without a sheet's name is on the sheet chosen as usual.
		using (var workbook = await Workbooks.OpenAsync(Report(), async))
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Range = "A1", Sheet = "Q1 Orders", Csv = new() { HasHeader = false } }))
		{
			Assert.Equal(["q1"], await reader.ReadAllAsync(async));
		}

		// A sheet given to OpenReader with an option that chooses its own.
		using var report = await Workbooks.OpenAsync(Report(), async);
		var arguments = new (Func<Task<CsvReader>> Open, string Parameter, string Message)[]
		{
			(() => report.OpenReaderAsync(async, "Messy", new ExcelReaderOptions { Table = "Sales" }), "sheetName", "ExcelReaderOptions.Table chooses its own sheet, so no sheet can be given with it."),
			(() => report.OpenReaderAsync(async, 1, new ExcelReaderOptions { DefinedName = "Data" }), "sheetIndex", "ExcelReaderOptions.DefinedName chooses its own sheet, so no sheet can be given with it."),
			(() => report.OpenReaderAsync(async, report.Sheets[1], new ExcelReaderOptions { Range = "Messy!A1" }), "sheet", "ExcelReaderOptions.Range names its sheet, so no sheet can be given with it."),
		};
		foreach (var (open, parameter, message) in arguments)
		{
			var e = await Assert.ThrowsAsync<ArgumentException>(open);
			Assert.StartsWith(message, e.Message);
			Assert.Equal(parameter, e.ParamName);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheCellsOfFieldsThatDontParse(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(quantity: "<is><t>ten</t></is>", qty: "<is><t>two</t></is>"), async);
		var cases = new (ExcelReaderOptions Options, string Cell, long Row)[]
		{
			(new() { Range = "Messy!B4:E7" }, "Messy!C6", 6),
			(new() { DefinedName = "Data" }, "Messy!C6", 6),
			(new() { Range = "Messy!4:7" }, "Messy!C6", 6),
			(new() { Table = "Sales" }, "Tables!B3", 3),
		};
		foreach (var (options, cell, row) in cases)
		{
			using (var reader = await workbook.OpenReaderAsync(async, options))
			{
				var e = Assert.Throws<CsvFormatException>(() => Read(reader, options.Table is null));
				Assert.Equal(cell, e.Location);
				Assert.Equal(row, e.LineNumber);
				Assert.Contains($"({cell})", e.Message);
			}

			var errors = new List<CsvRecordError>();
			using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { OnRecordError = error => { errors.Add(error); return true; } } }))
			{
				Read(reader, options.Table is null);
				Assert.Equal(cell, Assert.Single(errors).Location);
			}

			using (var reader = await workbook.OpenReaderAsync(async, options with { Csv = new() { BufferSize = 256 } }))
			{
				var e = Assert.ThrowsAny<Exception>(() => (options.Table is null ? reader.ParallelEnumerate<AreaOrder>(2).Count() : reader.ParallelEnumerate<AreaSale>(2).Count()));
				var format = e as CsvFormatException ?? e.InnerException as CsvFormatException ?? Assert.IsType<CsvFormatException>(Assert.Single(((AggregateException)e).InnerExceptions));
				Assert.Equal(cell, format.Location);
			}
		}

		static void Read(CsvReader reader, bool orders)
		{
			if (orders)
			{
				_ = reader.GetRecords<AreaOrder>().ToList();
			}
			else
			{
				_ = reader.GetRecords<AreaSale>().ToList();
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsATableInOneCall(bool async)
	{
		var options = new ExcelReaderOptions { Table = "Sales" };
		using var reader = async ? await ExcelReader.CreateAsync(Report().ToStream(), options) : ExcelReader.Create(Report().ToStream(), options);

		Assert.Equal(["a 1", "b 2"], reader.GetRecords<AreaSale>().Select(s => $"{s.Product} {s.Qty}"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsEverySheetInALoop(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Report(), async);
		var firsts = new List<string>();
		foreach (var sheet in workbook.Sheets)
		{
			if (sheet.Kind != ExcelSheetKind.Worksheet)
			{
				continue;
			}

			using var reader = await workbook.OpenReaderAsync(async, sheet, new ExcelReaderOptions { Csv = new() { HasHeader = false } });
			firsts.Add($"{sheet.Name}: {(await reader.ReadAllAsync(async))[0]}");
		}

		Assert.Equal(["Messy: Quarterly report", "Tables: Product|Qty", "Q1 Orders: q1"], firsts);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsNoFurtherThanTheRange(bool async)
	{
		// A sheet past the workbook's limit: a range at its start reads, as the sheet stops there.
		var package = Workbooks.Data(Workbooks.Numbers(60000));
		var options = new ExcelReaderOptions { MaxUncompressedSize = 1024 * 1024 };
		Assert.True(package.Text("xl/worksheets/sheet1.xml").Length > 2 * 1024 * 1024);
		using var workbook = await Workbooks.OpenAsync(package, async, options);

		using (var reader = await workbook.OpenReaderAsync(async, options with { Range = "A1:B100" }))
		{
			Assert.Equal(99, (await reader.ReadAllAsync(async)).Count);
		}

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAllAsync(async));
		}
	}

	/// <summary>Reads a reader's orders, described as <c>name quantity price paid</c>.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The orders.</returns>
	private static async Task<List<string>> ReadOrdersAsync(CsvReader reader, bool async)
	{
		var orders = async ? await ListAsync(reader.GetRecordsAsync<AreaOrder>()) : reader.GetRecords<AreaOrder>().ToList();

		return [.. orders.Select(o => $"{o.Name} {o.Quantity} {o.Price.ToString(CultureInfo.InvariantCulture)} {o.Paid}")];
	}

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
