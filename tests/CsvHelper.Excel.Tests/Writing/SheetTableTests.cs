using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>An order whose type's <see cref="ExcelSheetAttribute"/> asks for a table of a given name.</summary>
[CsvRecord]
[ExcelSheet(Table = true, TableName = "Orders")]
public partial class NamedTableOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Amount { get; set; }
}

/// <summary>
/// A written sheet's filter and table (Phase 5's step 4), synchronously and asynchronously: the filter over the header's
/// columns and its hidden <c>_xlnm._FilterDatabase</c> name; the table's part, relationship and content type, its name,
/// style and options, its header named as Excel names a table's columns, and both in the new sheets of
/// <see cref="ExcelRowLimit.NewSheet"/>. Each workbook passes the Open XML SDK's validator, ClosedXML and CsvHelper.Excel
/// report what was written, and the records read back as without them.
/// </summary>
public class SheetTableTests
{
	/// <summary>Two orders.</summary>
	private static readonly LookOrder[] Orders =
	[
		new() { Name = "apple", Amount = 1234.5m, When = new DateTime(2024, 1, 15, 10, 30, 0), Note = "Fresh", Code = "A1" },
		new() { Name = "pear", Amount = 0.25m, When = new DateTime(2024, 1, 16, 10, 30, 0), Note = "Ripe", Code = "P2" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesAFilterOverTheHeaderAndTheRows(bool async)
	{
		var workbook = await Write(Orders, async, new ExcelSheetOptions { AutoFilter = true });

		// The filter after the rows, before the page margins, and the workbook's hidden name for it, as Excel writes them.
		Assert.Contains("</sheetData><autoFilter ref=\"A1:E3\"/><pageMargins ", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains(
			"</sheets><definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">Data!$A$1:$E$3</definedName></definedNames></workbook>",
			Written.Entry(workbook, "xl/workbook.xml"));

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			Assert.Equal("A1:E3", book.Worksheet(1).AutoFilter.Range.RangeAddress.ToString());
		}

		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			var name = Assert.Single(book.DefinedNames);
			Assert.Equal(("_FilterDatabase", true, "Data", "A1:E3"), (name.Name, name.Hidden, name.Scope?.Name, name.Range.ToString()));
		}

		await ReadsBackAsWithoutThem(workbook, async);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesAFilterBelowATitleInASheetWhoseNameNeedsQuotes(bool async)
	{
		var workbook = await Write(Orders, async, new ExcelSheetOptions { AutoFilter = true, Title = "Orders" }, sheet: "Bob's orders");

		Assert.Contains("<mergeCells count=\"1\"><mergeCell ref=\"A1:E1\"/></mergeCells>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains("</sheetData><autoFilter ref=\"A2:E4\"/><mergeCells ", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains(">'Bob''s orders'!$A$2:$E$4</definedName>", Written.Entry(workbook, "xl/workbook.xml"));
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal("A2:E4", Assert.Single(book.DefinedNames).Range.ToString());
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FiltersAHeaderAloneAndNoSheetWithoutOne(bool async)
	{
		// A header of three fields, the last empty: each has a button. A sheet with no record has no filter.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Header", new ExcelSheetOptions { AutoFilter = true });
			writer.WriteField("A");
			writer.WriteField("B");
			writer.WriteField("");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
			await Written.DisposeAsync(book.AddSheet("Empty", new ExcelSheetOptions { AutoFilter = true }), a);
		}, async);

		Assert.Contains("<autoFilter ref=\"A1:C1\"/>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.DoesNotContain("autoFilter", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Contains(">Header!$A$1:$C$1</definedName></definedNames>", Written.Entry(workbook, "xl/workbook.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATableNamedAfterItsSheet(bool async)
	{
		var workbook = await Write(Orders, async, new ExcelSheetOptions { Table = new() }, sheet: "Q1 Orders");

		// The table's part after the sheet's, with the sheet's relationships between, and their content type.
		var entries = Written.Entries(workbook).Select(e => e.Name).ToList();
		Assert.Equal(["xl/worksheets/sheet1.xml", "xl/worksheets/_rels/sheet1.xml.rels", "xl/tables/table1.xml", "xl/styles.xml"], entries.Take(4));
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.EndsWith("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/><tableParts count=\"1\"><tablePart r:id=\"rId1\"/></tableParts></worksheet>", sheet);
		Assert.Contains("Target=\"../tables/table1.xml\"", Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		Assert.Contains("<Override PartName=\"/xl/tables/table1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml\"/>", Written.Entry(workbook, "[Content_Types].xml"));
		Assert.Contains("name=\"Q1_Orders\" displayName=\"Q1_Orders\" ref=\"A1:E3\"", Written.Entry(workbook, "xl/tables/table1.xml"));

		// A styled table's header isn't bold: its style sets it. No sheet filter, and no name for one.
		Assert.Contains("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c>", sheet);
		Assert.DoesNotContain("<autoFilter", sheet);
		Assert.DoesNotContain("definedNames", Written.Entry(workbook, "xl/workbook.xml"));

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var table = Assert.Single(book.Worksheet(1).Tables);
			Assert.Equal(("Q1_Orders", "A1:E3", "TableStyleMedium2", true, true), (table.Name, table.RangeAddress.ToString(), table.Theme.Name, table.ShowAutoFilter, table.ShowRowStripes));
		}

		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			var table = Assert.Single(book.Tables);
			Assert.Equal(("Q1_Orders", "A1:E3"), (table.Name, table.Range.ToString()));
			Assert.Equal(["Name", "Amount", "When", "Note", "Code"], table.Columns);
			using var reader = book.OpenReader(new ExcelReaderOptions { Table = "Q1_Orders" });
			Assert.Equal(Orders.Select(Text), reader.GetRecords<LookOrder>().Select(Text));
		}

		await ReadsBackAsWithoutThem(workbook, async, "Q1 Orders");
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATablesOptions(bool async)
	{
		var options = new ExcelSheetOptions { Table = new() { Name = "Sales", Style = "tablestylelight9", BandedRows = false, FilterButtons = false } };
		var unstyled = new ExcelSheetOptions { Table = new() { Style = null } };

		var workbook = await Write(Orders, async, options);
		var plain = await Write(Orders, async, unstyled);

		var table = Written.Entry(workbook, "xl/tables/table1.xml");
		Assert.Contains("name=\"Sales\" displayName=\"Sales\" ref=\"A1:E3\" totalsRowShown=\"0\"><tableColumns count=\"5\">", table);
		Assert.Contains("<tableStyleInfo name=\"TableStyleLight9\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"0\" showColumnStripes=\"0\"/>", table);

		// A table without a style keeps the bold header.
		Assert.Contains("<tableStyleInfo showFirstColumn=\"0\"", Written.Entry(plain, "xl/tables/table1.xml"));
		Assert.Contains("<c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Name</t></is></c>", Written.Entry(plain, "xl/worksheets/sheet1.xml"));

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var written = Assert.Single(book.Worksheet(1).Tables);
			Assert.Equal(("Sales", "TableStyleLight9", false, false), (written.Name, written.Theme.Name, written.ShowAutoFilter, written.ShowRowStripes));
		}

		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Empty(Written.ValidationErrors(plain));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesATablesHeaderCellsAsExcelDoes(bool async)
	{
		// Repeats, an empty field, a number and a boolean (their text as written), a line break: Excel kept these names
		// in Csv\_bench\results\phase5\step4's 05-table-renames.xlsx.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Renames", new ExcelSheetOptions { Table = new() });
			writer.WriteField("Name");
			writer.WriteField("Name");
			writer.WriteField("Name2");
			writer.WriteField("");
			writer.WriteField(2024);
			writer.WriteField(true);
			writer.WriteField("Two\nLines");
			writer.WriteField("NAME");
			await Written.EndRecordAsync(writer, a);
			for (var i = 0; i < 8; i++)
			{
				writer.WriteField(i);
			}

			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		string[] names = ["Name", "Name3", "Name2", "Column4", "2024", "True", "Two\nLines", "NAME5"];
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"B1\" t=\"inlineStr\"><is><t>Name3</t></is></c>", sheet);
		Assert.Contains("<c r=\"E1\" t=\"inlineStr\"><is><t>2024</t></is></c>", sheet);
		Assert.Contains("<tableColumn id=\"7\" name=\"Two_x000A_Lines\"/>", Written.Entry(workbook, "xl/tables/table1.xml"));
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal(names, Assert.Single(book.Tables).Columns);
		}

		Assert.Equal(string.Join("|", names), Written.ReadOne(workbook)[0]);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RefusesAHeaderFieldTooLongToNameATablesColumn(bool async)
	{
		// The field fails, as any field: no cell, and an empty field's name if the record goes on.
		InvalidOperationException? e = null;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Table = new() });
			writer.WriteField(new string('n', 255));
			e = Assert.Throws<InvalidOperationException>(() => writer.WriteField(new string('n', 256)));
			writer.WriteField("Code");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		Assert.Equal("Cell Data!B1 has 256 characters, and the header's cells name the table's columns, which Excel opens with 255 at most.", e!.Message);
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal([new string('n', 255), "Column1", "Code"], Assert.Single(book.Tables).Columns);
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATableOverAHeaderAloneAndAnEmptyRow(bool async)
	{
		var workbook = await Write(Array.Empty<LookOrder>(), async, new ExcelSheetOptions { Table = new() });

		Assert.Contains("ref=\"A1:E2\"", Written.Entry(workbook, "xl/tables/table1.xml"));
		Assert.DoesNotContain("<row r=\"2\"", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesTablesUniquelyInTheWorkbook(bool async)
	{
		// Default names get a number while the workbook has them; a name given that it has throws, and the sheet isn't added.
		ArgumentException? given = null;
		InvalidOperationException? attribute = null;
		InvalidOperationException? defaults = null;
		IReadOnlyList<string> sheets = [];
		var options = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { Table = new() { Name = "orders" } } };
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			foreach (var name in new[] { "Q1 Orders", "Q1_Orders", "2024" })
			{
				var writer = book.AddSheet(name, new ExcelSheetOptions { Table = new() });
				writer.WriteField("Name");
				await Written.EndRecordAsync(writer, a);
				await Written.DisposeAsync(writer, a);
			}

			var defaultsWriter = book.AddSheet("Defaults");
			defaultsWriter.WriteField("Name");
			await Written.EndRecordAsync(defaultsWriter, a);
			await Written.DisposeAsync(defaultsWriter, a);
			given =Assert.Throws<ArgumentException>(() => book.AddSheet("Other", new ExcelSheetOptions { Table = new() { Name = "Q1_ORDERS2" } }));
			attribute = Assert.Throws<InvalidOperationException>(() => book.AddSheet<NamedTableOrder>("Typed"));
			defaults = Assert.Throws<InvalidOperationException>(() => book.AddSheet("More"));
			sheets = [.. book.SheetNames];
		}, async, options);

		Assert.StartsWith("ExcelSheetOptions: the workbook already has the table \"Q1_Orders2\", which Excel takes \"Q1_ORDERS2\" to be: a workbook's tables have names unique ignoring case. Leave the table's name out for one made from the sheet's.", given!.Message);
		Assert.Equal("options", given.ParamName);
		Assert.StartsWith("The [ExcelSheet] of NamedTableOrder: the workbook already has the table \"orders\", which Excel takes \"Orders\" to be", attribute!.Message);
		Assert.StartsWith("ExcelWriterOptions.Sheets: the workbook already has the table \"orders\"", defaults!.Message);
		Assert.Equal(["Q1 Orders", "Q1_Orders", "2024", "Defaults"], sheets);
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal(["Q1_Orders", "Q1_Orders2", "_2024", "orders"], book.Tables.Select(t => t.Name));
		}

		Assert.Equal(["xl/tables/table1.xml", "xl/tables/table2.xml", "xl/tables/table3.xml", "xl/tables/table4.xml"], Written.Entries(workbook).Select(e => e.Name).Where(n => n.StartsWith("xl/tables/", StringComparison.Ordinal)));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MakesATablesButtonsTheFilter(bool async)
	{
		// Excel opens no sheet with a filter beside a table (decision 9).
		var workbook = await Write(Orders, async, new ExcelSheetOptions { AutoFilter = true, Table = new() { FilterButtons = false } });

		Assert.DoesNotContain("autoFilter", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.DoesNotContain("autoFilter", Written.Entry(workbook, "xl/tables/table1.xml"));
		Assert.DoesNotContain("definedNames", Written.Entry(workbook, "xl/workbook.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RepeatsTheFilterAndTheTableInNewSheets(bool async)
	{
		var records = Enumerable.Range(0, 7).Select(i => new LookOrder { Name = "n" + i, Amount = i, When = new DateTime(2024, 1, 15 + i), Note = "", Code = "c" + i }).ToList();
		var options = new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet, Strings = ExcelStrings.Shared };

		var filtered = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 5;
			await WriteSheet(book, "Orders", records, a, new ExcelSheetOptions { AutoFilter = true, Title = "Orders" });
		}, async, options);
		var tabled = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 4;
			await WriteSheet(book, "AB", records, a, new ExcelSheetOptions { Table = new() });
		}, async, options);

		// Each sheet's filter from the header below the title to its last row, and its own name.
		Assert.Contains("<autoFilter ref=\"A2:E5\"/>", Written.Entry(filtered, "xl/worksheets/sheet2.xml"));
		Assert.Contains("<autoFilter ref=\"A2:E3\"/>", Written.Entry(filtered, "xl/worksheets/sheet3.xml"));
		Assert.Contains(
			"<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">Orders!$A$2:$E$5</definedName>" +
			"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"1\" hidden=\"1\">'Orders (2)'!$A$2:$E$5</definedName>" +
			"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"2\" hidden=\"1\">'Orders (3)'!$A$2:$E$3</definedName></definedNames>",
			Written.Entry(filtered, "xl/workbook.xml"));

		// Each sheet's table, with a number after the first's name (AB2 would be a cell), its parts after its sheet's.
		using (var book = ExcelWorkbook.Open(new MemoryStream(tabled)))
		{
			Assert.Equal(["AB", "AB_2", "AB_3"], book.Tables.Select(t => t.Name));
			Assert.Equal(["A1:E4", "A1:E4", "A1:E2"], book.Tables.Select(t => t.Range.ToString()));
		}

		Assert.Equal(
			[
				"xl/worksheets/sheet1.xml", "xl/worksheets/_rels/sheet1.xml.rels", "xl/tables/table1.xml",
				"xl/worksheets/sheet2.xml", "xl/worksheets/_rels/sheet2.xml.rels", "xl/tables/table2.xml",
				"xl/worksheets/sheet3.xml", "xl/worksheets/_rels/sheet3.xml.rels", "xl/tables/table3.xml",
			],
			Written.Entries(tabled).Select(e => e.Name).Take(9));
		Assert.Empty(Written.ValidationErrors(filtered));
		Assert.Empty(Written.ValidationErrors(tabled));

		var read = new List<LookOrder>();
		using (var book = ExcelWorkbook.Open(new MemoryStream(tabled)))
		{
			foreach (var table in book.Tables)
			{
				using var reader = book.OpenReader(new ExcelReaderOptions { Table = table.Name });
				read.AddRange(reader.GetRecords<LookOrder>());
			}
		}

		Assert.Equal(records.Select(Text), read.Select(Text));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesATableByItsTypesAttribute(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) => await WriteSheet(book, "Data", [new NamedTableOrder { Name = "a", Amount = 1 }], a, null), async);

		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal("Orders", Assert.Single(book.Tables).Name);
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	/// <summary>The text of an order's values, to compare orders.</summary>
	/// <param name="order">The order.</param>
	/// <returns>Its values, joined.</returns>
	private static string Text(LookOrder order) =>
		string.Join("|", order.Name, order.Amount.ToString(CultureInfo.InvariantCulture), order.When.ToString("O", CultureInfo.InvariantCulture), order.Note, order.Code);

	/// <summary>
	/// Checks that a workbook's records read back as the same records written with Phase 4's look do: through CsvHelper.Excel,
	/// ClosedXML and ExcelDataReader, and that it passes the validator.
	/// </summary>
	/// <param name="workbook">The workbook, of <see cref="Orders"/> in one sheet.</param>
	/// <param name="async">Whether to write the plain one asynchronously.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>A task that completes when the checks have.</returns>
	private static async Task ReadsBackAsWithoutThem(byte[] workbook, bool async, string sheet = "Data")
	{
		var plain = await Write(Orders, async, Written.Phase4Look, sheet);

		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(Written.ReadOne(plain), Written.ReadOne(workbook));
		Assert.Equal(Written.ClosedXmlValues(plain), Written.ClosedXmlValues(workbook));
		Assert.Equal(Written.ExcelDataReaderValues(plain), Written.ExcelDataReaderValues(workbook));
	}

	/// <summary>Writes records into one sheet, with a look.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(IEnumerable<T> records, bool async, ExcelSheetOptions? look, string sheet = "Data") =>
		Written.WorkbookAsync((book, a) => WriteSheet(book, sheet, records, a, look), async);

	/// <summary>Writes a sheet of records, as the mode says.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="book">The workbook writer.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <returns>A task that completes when the sheet is.</returns>
	private static async Task WriteSheet<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelWorkbookWriter book, string sheet, IEnumerable<T> records, bool async, ExcelSheetOptions? look)
	{
		if (async)
		{
			await book.WriteSheetAsync(sheet, records, look);
		}
		else
		{
			book.WriteSheet(sheet, records, look);
		}
	}
}
