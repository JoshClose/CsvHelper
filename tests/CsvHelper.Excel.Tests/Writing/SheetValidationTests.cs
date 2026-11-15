using System.Diagnostics.CodeAnalysis;
using System.Text;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>An order whose columns validate by type, with a list and a prompt by attribute.</summary>
[CsvRecord]
public partial class ValidatedOrder
{
	/// <summary>Whole numbers.</summary>
	public int Id { get; set; }

	/// <summary>A dropdown of the enum's names.</summary>
	public TicketStatus Status { get; set; }

	/// <summary>A date and time, which widens its column.</summary>
	public DateTime When { get; set; }

	/// <summary>A list in the lists sheet, with a prompt.</summary>
	[ExcelColumn(List = new[] { "North, East", "South, West" }, Prompt = "The order's region.")]
	public string Region { get; set; } = "";

	/// <summary>Text.</summary>
	public string Note { get; set; } = "";
}

/// <summary>A record of times and durations, whose columns are as wide as their widest values.</summary>
[CsvRecord]
public partial class ValidatedTimes
{
	/// <summary>A date and time.</summary>
	public DateTime When { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A date and time with its own width.</summary>
	[ExcelColumn(Width = 5)]
	public DateTime Narrow { get; set; }

	/// <summary>A date and time in its column's own format.</summary>
	[ExcelColumn(Format = "d mmmm yyyy")]
	public DateTime Long { get; set; }
}

/// <summary>
/// Validations, lists and prompts through the public API (Phase 7's step 3), synchronously and asynchronously: with
/// <see cref="ExcelSheetOptions.Validate"/> on the sheets of <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>,
/// <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/> and <see cref="ExcelConvert.ToExcel{T}(Stream, Stream, CsvToExcelOptions)"/>;
/// lists and prompts on sheets without types; the lists sheet, shared by the workbook's sheets; the types' formats as the
/// columns' default styles, and the dates' widths. Each workbook passes the Open XML SDK's validator, and ClosedXML reads
/// its validations as written.
/// </summary>
public class SheetValidationTests
{
	/// <summary>Two orders.</summary>
	private static readonly ValidatedOrder[] Orders =
	[
		new() { Id = 1, Status = TicketStatus.OnHold, When = new DateTime(2024, 1, 15, 10, 30, 0), Region = "North, East", Note = "00123" },
		new() { Id = 2, Status = TicketStatus.Closed, When = new DateTime(2024, 1, 16), Region = "South, West", Note = "fine" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesASheetOfRecordsBelowItsRecords(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Validate = true }), async);

		Assert.Equal(new Dictionary<string, string>
		{
			["Id"] = "whole -2147483648 2147483647 | General",
			["Status"] = "list \"Open,On hold,Closed\" | @",
			["When"] = "date 1 2958465.9999999884 | yyyy-mm-dd hh:mm:ss",
			["Region"] = "list _CsvHelperList1 title=Region prompt=The order's region. | @",
			["Note"] = "none | @",
		}, SheetValidations.Describe(workbook));
		// The cells written keep their own formats: General, and their dates'; and the records read back as written.
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"B2\" t=\"inlineStr\"><is><t>On hold</t></is></c><c r=\"C2\" s=\"", sheet);
		Assert.Equal(["Id|Status|When|Region|Note", "1|On hold|2024-01-15T10:30:00|North, East|00123", "2|Closed|2024-01-16|South, West|fine"], Written.Read(workbook)[0].Rows);
		AssertTheListsSheet(workbook, "Lists", 2, ["North, East", "South, West"]);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesTheColumnsOfAddSheetOfTByTheirHeaderNames(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			// A header by hand: its names say which member each column is; one no member has gets nothing.
			var writer = book.AddSheet<ValidatedOrder>("Orders", new ExcelSheetOptions { Validate = true, BoldHeader = false });
			foreach (var name in new[] { "Note", "Other", "Id", "When" })
			{
				writer.WriteField(name);
			}

			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		Assert.Equal(new Dictionary<string, string>
		{
			["Note"] = "none | @",
			["Other"] = "none | General",
			["Id"] = "whole -2147483648 2147483647 | General",
			["When"] = "date 1 2958465.9999999884 | yyyy-mm-dd hh:mm:ss",
		}, SheetValidations.Describe(workbook));
		// Without bold, the header's cells say General's style, as LibreOffice would give them their columns' otherwise.
		Assert.Contains("<row r=\"1\"><c r=\"A1\" s=\"0\" t=\"inlineStr\"><is><t>Note</t></is></c><c r=\"B1\" s=\"0\" t=\"inlineStr\">", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task SaysTheHeadersStyleWhenTypesFormatTheColumns(bool async)
	{
		// A type without Excel attributes: Validate alone gives its columns styles.
		var validated = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Array.Empty<LookOrder>(), a, new ExcelSheetOptions { Validate = true, BoldHeader = false }), async);
		var plain = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Array.Empty<LookOrder>(), a, new ExcelSheetOptions { BoldHeader = false }), async);

		Assert.Contains("<row r=\"1\"><c r=\"A1\" s=\"0\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" s=\"0\" t=\"inlineStr\">", Written.Entry(validated, "xl/worksheets/sheet1.xml"));
		Assert.Contains("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"inlineStr\">", Written.Entry(plain, "xl/worksheets/sheet1.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ConvertsCsvWithItsTypesValidations(bool async)
	{
		using var csv = new MemoryStream(Encoding.UTF8.GetBytes("Id,Status,When,Region,Note\r\n7,Open,2024-01-15T10:30:00,\"North, East\",x\r\n"));
		using var excel = new MemoryStream();
		var options = new CsvToExcelOptions { Sheet = new ExcelSheetOptions { Validate = true, Title = "Orders" } };
		if (async)
		{
			await ExcelConvert.ToExcelAsync<ValidatedOrder>(csv, excel, options);
		}
		else
		{
			ExcelConvert.ToExcel<ValidatedOrder>(csv, excel, options);
		}

		var workbook = excel.ToArray();

		// Below the title, from the row below the header.
		Assert.Equal("whole -2147483648 2147483647 | General", SheetValidations.Describe(workbook, headerRow: 2)["Id"]);
		Assert.Equal("list _CsvHelperList1 title=Region prompt=The order's region. | @", SheetValidations.Describe(workbook, headerRow: 2)["Region"]);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ListsAndPromptsColumnsOfSheetsWithoutTypes(bool async)
	{
		var columns = new Dictionary<string, ExcelColumnOptions>
		{
			["Color"] = new() { List = ["Red", "Green", "Blue"], Prompt = "Pick a color.\nOne only." },
			["Size"] = new() { List = ["Small, light", "Large"] },
			["Note"] = new() { Prompt = "Anything." },
		};
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes("Color,Size,Note,Count\r\nRed,Large,x,1\r\n"));
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			// Validate without types gives the lists and prompts alone: Count, inferred a number, gets nothing.
			var options = new ExcelSheetOptions { Validate = true, Columns = columns };
			var writer = book.AddSheet("Typed by hand", options);
			foreach (var name in columns.Keys)
			{
				writer.WriteField(name);
			}

			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
			if (a)
			{
				await book.WriteSheetAsync("Converted", reader, options);
			}
			else
			{
				book.WriteSheet("Converted", reader, options);
			}
		}, async);

		var expected = new Dictionary<string, string>
		{
			["Color"] = "list \"Red,Green,Blue\" title=Color prompt=Pick a color._x000A_One only. | General",
			["Size"] = "list _CsvHelperList1 | General",
			["Note"] = "any title=Note prompt=Anything. | General",
		};
		Assert.Equal(expected, SheetValidations.Describe(workbook));
		Assert.Equal(expected.Append(new("Count", "none | General")), SheetValidations.Describe(workbook, 2));
		// One list in the lists sheet, which both sheets use.
		AssertTheListsSheet(workbook, "Lists", 3, ["Small, light", "Large"]);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task SharesTheListsSheetAndNamesItAfterTheSheets(bool async)
	{
		var long1 = new ExcelColumnOptions { List = ["a, b", "c"] };
		var long2 = new ExcelColumnOptions { List = [new string('x', 255), "y"] };
		LookOrder[] orders = [new() { Name = "apple", Note = "c" }];
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			await WriteSheet(book, "Lists", orders, a, new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = long1 } });
			await WriteSheet(book, "LISTS2", orders, a, new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = long2, ["Name"] = long1 } });
		}, async);

		// The sheets' names are taken, ignoring case, so it is Lists3; and it isn't one of the sheets added.
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal([("Lists", ExcelSheetVisibility.Visible), ("LISTS2", ExcelSheetVisibility.Visible), ("Lists3", ExcelSheetVisibility.VeryHidden)], book.Sheets.Select(s => (s.Name, s.Visibility)));
		}

		Assert.Contains("<definedNames><definedName name=\"_CsvHelperList1\" hidden=\"1\">Lists3!$A$1:$A$2</definedName><definedName name=\"_CsvHelperList2\" hidden=\"1\">Lists3!$B$1:$B$2</definedName></definedNames>", Written.Entry(workbook, "xl/workbook.xml"));
		Assert.Equal(new Dictionary<string, string> { ["D"] = "list _CsvHelperList1" }, SheetValidations.Of(workbook));
		Assert.Equal(new Dictionary<string, string> { ["A"] = "list _CsvHelperList1", ["D"] = "list _CsvHelperList2" }, SheetValidations.Of(workbook, 2));
		Assert.Equal(["a, b|" + new string('x', 255), "c|y"], Written.Read(workbook)[2].Rows);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesTheSheetsWritersSheetsWithoutTheListsSheet(bool async)
	{
		using var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true);
		await WriteSheet(book, "Orders", Orders, async, null);
		if (async)
		{
			await book.DisposeAsync();
		}
		else
		{
			book.Dispose();
		}

		Assert.Equal(["Orders"], book.SheetNames);
		using var written = ExcelWorkbook.Open(new MemoryStream(stream.ToArray()));
		Assert.Equal("Lists", Assert.Single(written.Sheets, s => s.Visibility == ExcelSheetVisibility.VeryHidden).Name);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesWithoutAHeaderFromTheFirstRecordWithoutTitles(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, null), async, new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } });

		Assert.Equal(new Dictionary<string, string> { ["D"] = "list _CsvHelperList1 prompt=The order's region." }, SheetValidations.Of(workbook, firstRow: 1));
		Assert.Empty(SheetValidations.Formats(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesEachSheetOfNewSheet(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			await WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Validate = true, Title = "Orders" });
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		// Each sheet repeats the title and the header, and has the validations from row 3.
		using (var written = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal(["Orders", "Orders (2)", "Lists"], written.Sheets.Select(s => s.Name));
		}

		for (var sheet = 1; sheet <= 2; sheet++)
		{
			Assert.Equal("list _CsvHelperList1 title=Region prompt=The order's region. | @", SheetValidations.Describe(workbook, sheet, headerRow: 2)["Region"]);
		}

		Assert.Contains("<definedName name=\"_CsvHelperList1\" hidden=\"1\">Lists!$A$1:$A$2</definedName>", Written.Entry(workbook, "xl/workbook.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WidensDatesAndDurationsToTheirWidestValues(bool async)
	{
		var fitted = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Times", Array.Empty<ValidatedTimes>(), a, new ExcelSheetOptions { Validate = true }), async);
		var unfitted = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Times", Array.Empty<ValidatedTimes>(), a, new ExcelSheetOptions { Validate = true, AutoFit = 0 }), async);
		var plain = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Times", Array.Empty<ValidatedTimes>(), a, new ExcelSheetOptions { AutoFit = 0 }), async);

		// The widest date and time, Excel's last duration (71003183:59:59), and a long month's name, in their formats; a
		// column's own width beats them.
		var dateTime = ColumnWidths.OfPixels(ColumnWidths.TextPixels("9999-12-31 23:59:59".AsSpan()));
		var duration = ColumnWidths.OfPixels(ColumnWidths.TextPixels("71003183:59:59".AsSpan()));
		var widest = Serial.Days(Serial.PastLast - 1000);
		var month = ColumnWidths.OfPixels(new NumberFormatWidth("d mmmm yyyy").NumberPixels(widest));
		Assert.Equal(new Dictionary<string, double> { ["When"] = dateTime, ["Took"] = duration, ["Narrow"] = ColumnWidths.FromCharacters(5), ["Long"] = month }, SheetValidations.Widths(unfitted));
		Assert.Equal(new Dictionary<string, double>
		{
			["When"] = ColumnWidths.Fit(ColumnWidths.TextPixels("9999-12-31 23:59:59".AsSpan())),
			["Took"] = ColumnWidths.Fit(ColumnWidths.TextPixels("71003183:59:59".AsSpan())),
			["Narrow"] = ColumnWidths.FromCharacters(5),
			["Long"] = ColumnWidths.Fit(new NumberFormatWidth("d mmmm yyyy").NumberPixels(widest)),
		}, SheetValidations.Widths(fitted));
		// A column's own format is its default style, in place of its type's.
		Assert.Equal("date 1 2958465.9999999884 | d mmmm yyyy", SheetValidations.Describe(fitted)["Long"]);
		// Without Validate, a sheet without records has its own widths only.
		Assert.Equal(new Dictionary<string, double> { ["When"] = 0, ["Took"] = 0, ["Narrow"] = ColumnWidths.FromCharacters(5), ["Long"] = 0 }, SheetValidations.Widths(plain));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesNoTypesValidationsOrFormatsWithoutValidate(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, null), async);

		Assert.Equal(new Dictionary<string, string>
		{
			["Id"] = "none | General",
			["Status"] = "none | General",
			["When"] = "none | General",
			["Region"] = "list _CsvHelperList1 title=Region prompt=The order's region. | General",
			["Note"] = "none | General",
		}, SheetValidations.Describe(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ClosedXmlReadsTheValidations(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Array.Empty<ValidatedOrder>(), a, new ExcelSheetOptions { Validate = true }), async);

		using var book = new XLWorkbook(new MemoryStream(workbook));
		var sheet = book.Worksheet("Orders");
		var id = sheet.Cell("A1048576").GetDataValidation();
		Assert.Equal((XLAllowedValues.WholeNumber, "-2147483648", "2147483647"), (id.AllowedValues, id.MinValue, id.MaxValue));
		Assert.Equal((XLAllowedValues.List, "\"Open,On hold,Closed\""), (sheet.Cell("B2").GetDataValidation().AllowedValues, sheet.Cell("B2").GetDataValidation().Value));
		var region = sheet.Cell("D2").GetDataValidation();
		Assert.Equal((XLAllowedValues.List, "_CsvHelperList1", "Region", "The order's region.", XLErrorStyle.Stop), (region.AllowedValues, region.Value, region.InputTitle, region.InputMessage, region.ErrorStyle));
		Assert.False(sheet.Cell("A1").HasDataValidation);
		Assert.Equal(XLWorksheetVisibility.VeryHidden, book.Worksheet("Lists").Visibility);
		Assert.Equal("_CsvHelperList1", Assert.Single(book.DefinedNames).Name);
	}

	[Fact]
	public void RefusesATableOfAListsName()
	{
		using var stream = new MemoryStream();
		using var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true);
		book.WriteSheet("Orders", Orders);

		var e = Assert.Throws<ArgumentException>(() => book.AddSheet("Other", new ExcelSheetOptions { Table = new ExcelTableOptions { Name = "_CSVHELPERLIST1" } }));

		Assert.StartsWith("ExcelSheetOptions: the workbook already has the name \"_CsvHelperList1\", of a dropdown list in its lists sheet, which Excel takes \"_CSVHELPERLIST1\" to be: a workbook's tables and names are unique ignoring case. Give the table another name.", e.Message);
		Assert.Equal("options", e.ParamName);
		// A table named after its sheet takes the next number.
		using (var writer = book.AddSheet("_CsvHelperList1", new ExcelSheetOptions { Table = new ExcelTableOptions() }))
		{
			writer.WriteField("Name");
			writer.EndRecord();
		}

		book.Dispose();
		Assert.Contains("name=\"_CsvHelperList12\"", Written.Entry(stream.ToArray(), "xl/tables/table1.xml"));
	}

	[Fact]
	public void RefusesAListLongerThanAColumn()
	{
		var options = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { List = [.. Enumerable.Repeat("a", 1_048_577)] } } };
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());

		var e = Assert.Throws<ArgumentException>(() => book.AddSheet("Orders", options));

		Assert.StartsWith("ExcelSheetOptions: The column \"Note\" has 1,048,577 values in its list, and the lists sheet holds 1,048,576, a column's rows.", e.Message);
	}

	/// <summary>Checks the lists sheet: very hidden, protected without a password, its first column the list.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="name">The lists sheet's name.</param>
	/// <param name="number">Its sheet's number.</param>
	/// <param name="first">Its first list.</param>
	private static void AssertTheListsSheet(byte[] workbook, string name, int number, string[] first)
	{
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			var sheet = book.Sheets.Last();
			Assert.Equal((name, ExcelSheetVisibility.VeryHidden), (sheet.Name, sheet.Visibility));
		}

		var xml = Written.Entry(workbook, $"xl/worksheets/sheet{number}.xml");
		Assert.Contains("</sheetData><sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/><pageMargins ", xml);
		Assert.Equal(first, Written.Read(workbook).Last().Rows.Select(r => r.Split('|')[0]).Take(first.Length));
	}

	/// <summary>Writes a sheet of records, as the mode says.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="book">The workbook writer.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The sheet's options, or <see langword="null"/>.</param>
	/// <returns>A task that completes when the sheet is.</returns>
	private static async Task WriteSheet<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelWorkbookWriter book, string sheet, IEnumerable<T> records, bool async, ExcelSheetOptions? options)
	{
		if (async)
		{
			await book.WriteSheetAsync(sheet, records, options);
		}
		else
		{
			book.WriteSheet(sheet, records, options);
		}
	}
}
