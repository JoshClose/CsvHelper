using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>An order of text, a number, a date and time, a note and a code, for how a sheet looks.</summary>
[CsvRecord]
public partial class LookOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public decimal Amount { get; set; }

	/// <summary>A date cell, whose column gets a width.</summary>
	public DateTime When { get; set; }

	/// <summary>A text cell, with a line break.</summary>
	public string Note { get; set; } = "";

	/// <summary>A text cell.</summary>
	public string Code { get; set; } = "";
}

/// <summary>An order whose members' <see cref="ExcelColumnAttribute"/>s give their columns' options.</summary>
[CsvRecord]
public partial class AttributedLookOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number in a percent format.</summary>
	[ExcelColumn(Format = "0.0%")]
	public double Rate { get; set; }

	/// <summary>A hidden column 12 characters wide.</summary>
	[ExcelColumn(Width = 12, Hidden = true)]
	public string Code { get; set; } = "";
}

/// <summary>An order whose date has its member's format.</summary>
[CsvRecord]
public partial class FormattedLookOrder
{
	/// <summary>A date in a format Excel shows, which a column's format replaces.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime When { get; set; }
}

/// <summary>
/// How a written sheet looks (Phase 5's step 2), synchronously and asynchronously: the bold header, the frozen panes, the
/// title row and its merge, and the columns' formats, wrap, widths and hidden columns, from <see cref="ExcelSheetOptions.Columns"/>
/// and <see cref="ExcelColumnAttribute"/>. Each workbook passes the Open XML SDK's validator, and its records read back
/// as without the look, through CsvHelper.Excel, ClosedXML and ExcelDataReader.
/// </summary>
public class SheetLookTests
{
	/// <summary>The attributes of a width auto-fit gives.</summary>
	private const string Fitted = " bestFit=\"1\" customWidth=\"1\"";

	/// <summary>The XML of Calibri 11, bold Calibri 11 and bold Calibri 14.</summary>
	private const string Regular = "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>";
	private const string Bold = "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>";
	private const string TitleFont = "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/><family val=\"2\"/></font>";

	/// <summary>Two orders.</summary>
	private static readonly LookOrder[] Orders =
	[
		new() { Name = "apple", Amount = 1234.5m, When = new DateTime(2024, 1, 15, 10, 30, 0), Note = "Fresh\nfrom the farm", Code = "A1" },
		new() { Name = "pear", Amount = 0.25m, When = new DateTime(2024, 1, 16, 10, 30, 0), Note = "Ripe", Code = "P2" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesABoldFrozenHeaderByDefault(bool async)
	{
		var workbook = await Write(Orders, async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/></sheetView></sheetViews><cols>", sheet);
		Assert.Contains("<row r=\"1\"><c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" s=\"1\" t=\"inlineStr\"><is><t>Amount</t></is></c>", sheet);
		Assert.Contains("<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\"><v>1234.5</v></c><c r=\"C2\" s=\"2\"><v>45306.4375</v></c>", sheet);
		var styles = Written.Entry(workbook, "xl/styles.xml");
		Assert.Contains($"<fonts count=\"2\">{Regular}{Bold}</fonts>", styles);
		Assert.Contains("<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>", styles);

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var worksheet = book.Worksheet(1);
			Assert.True(worksheet.Cell("A1").Style.Font.Bold);
			Assert.True(worksheet.Cell("E1").Style.Font.Bold);
			Assert.False(worksheet.Cell("A2").Style.Font.Bold);
			Assert.Equal((1, 0), (worksheet.SheetView.SplitRow, worksheet.SheetView.SplitColumn));
		}

		await ReadsBackAsWithoutTheLook(workbook, async);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesPhase4sLookWithBoldAndFreezeOff(bool async)
	{
		var workbook = await Write(Orders, async, Written.Phase4Look);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.DoesNotContain("sheetViews", sheet);
		Assert.Contains("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\">", sheet);
		Assert.Contains($"<fonts count=\"1\">{Regular}</fonts>", Written.Entry(workbook, "xl/styles.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATitleAboveTheHeader(bool async)
	{
		var workbook = await Write(Orders, async, new ExcelSheetOptions { Title = "Orders 2024" });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		// The title's font comes first, as it is the first cell written; the header's is bold, and the rows follow from row 3.
		Assert.Contains("<sheetData><row r=\"1\"><c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Orders 2024</t></is></c></row><row r=\"2\"><c r=\"A2\" s=\"2\" t=\"inlineStr\"><is><t>Name</t></is></c>", sheet);
		Assert.Contains("<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>apple</t></is></c>", sheet);
		Assert.Contains("</sheetData><mergeCells count=\"1\"><mergeCell ref=\"A1:E1\"/></mergeCells><pageMargins ", sheet);
		Assert.Contains("<pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/>", sheet);
		Assert.Contains($"<fonts count=\"3\">{Regular}{TitleFont}{Bold}</fonts>", Written.Entry(workbook, "xl/styles.xml"));

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var worksheet = book.Worksheet(1);
			Assert.Equal((true, 14.0), (worksheet.Cell("A1").Style.Font.Bold, worksheet.Cell("A1").Style.Font.FontSize));
			Assert.Equal("A1:E1", Assert.Single(worksheet.MergedRanges).RangeAddress.ToString());
			Assert.True(worksheet.Cell("A2").Style.Font.Bold);
			Assert.Equal(2, worksheet.SheetView.SplitRow);
		}

		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(Orders.Select(Text), ReadRecords(workbook, new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) }).Select(Text));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MergesTheTitleOverTwoColumnsOrMore(bool async)
	{
		// A header of one column has no merge; nor has a sheet without records.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("One", new ExcelSheetOptions { Title = "One column" });
			writer.WriteField("Name");
			await Written.EndRecordAsync(writer, a);
			writer.WriteField("apple");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
			var empty = book.AddSheet("Empty", new ExcelSheetOptions { Title = "Nothing yet" });
			await Written.DisposeAsync(empty, a);
		}, async);

		var one = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.DoesNotContain("mergeCells", one);
		Assert.Contains("<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>apple</t></is></c></row>", one);
		var empty = Written.Entry(workbook, "xl/worksheets/sheet2.xml");
		Assert.Contains("<sheetData><row r=\"1\"><c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Nothing yet</t></is></c></row></sheetData><pageMargins ", empty);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATitleAboveRecordsWithoutAHeader(bool async)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };

		var workbook = await Write(Orders, async, new ExcelSheetOptions { Title = "Orders" }, options);

		// The title spans the first record's columns; nothing is bold or frozen without a header.
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.DoesNotContain("sheetViews", sheet);
		Assert.Contains("<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c>", sheet);
		Assert.Contains("<mergeCell ref=\"A1:E1\"/>", sheet);
		Assert.Contains($"<fonts count=\"2\">{Regular}{TitleFont}</fonts>", Written.Entry(workbook, "xl/styles.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData(true, true, 0, "<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>")]
	[InlineData(true, true, 2, "<pane xSplit=\"2\" ySplit=\"1\" topLeftCell=\"C2\" activePane=\"bottomRight\" state=\"frozen\"/><selection pane=\"bottomRight\"/>")]
	[InlineData(true, false, 1, "<pane xSplit=\"1\" topLeftCell=\"B1\" activePane=\"topRight\" state=\"frozen\"/><selection pane=\"topRight\"/>")]
	[InlineData(true, true, 16383, "<pane xSplit=\"16383\" ySplit=\"1\" topLeftCell=\"XFD2\" activePane=\"bottomRight\" state=\"frozen\"/><selection pane=\"bottomRight\"/>")]
	[InlineData(false, true, 1, "<pane xSplit=\"1\" topLeftCell=\"B1\" activePane=\"topRight\" state=\"frozen\"/><selection pane=\"topRight\"/>")]
	[InlineData(true, false, 0, null)]
	[InlineData(false, true, 0, null)]
	public void FreezesTheHeaderAndTheFirstColumns(bool hasHeader, bool freezeHeader, int freezeColumns, string? pane)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = hasHeader } };
		var stream = new MemoryStream();

		using (var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = book.AddSheet("Data", new ExcelSheetOptions { FreezeHeader = freezeHeader, FreezeColumns = freezeColumns });
			for (var row = 0; row < 3; row++)
			{
				writer.WriteField("a" + row);
				writer.WriteField(row);
				writer.EndRecord();
			}
		}

		var workbook = stream.ToArray();
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		if (pane is null)
		{
			Assert.DoesNotContain("sheetViews", sheet);
		}
		else
		{
			Assert.Contains("<sheetViews><sheetView workbookViewId=\"0\">" + pane + "</sheetView></sheetViews><cols>", sheet);
			using var book = new XLWorkbook(new MemoryStream(workbook));
			var view = book.Worksheet(1).SheetView;
			Assert.Equal((hasHeader && freezeHeader ? 1 : 0, freezeColumns), (view.SplitRow, view.SplitColumn));
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StylesColumnsByTheirOptions(bool async)
	{
		var options = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["Amount"] = new() { Format = "#,##0.00" },
				["When"] = new() { Format = "dd/mm/yyyy hh:mm" },
				["Note"] = new() { Wrap = true, Width = 30 },
				["Code"] = new() { Hidden = true },
			},
		};

		var workbook = await Write(Orders, async, options);

		// The header stays bold in General; below it, each column's cells carry its format or wrap, whatever their own.
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"B1\" s=\"1\" t=\"inlineStr\">", sheet);
		Assert.Contains("<c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\" s=\"2\"><v>1234.5</v></c><c r=\"C2\" s=\"3\"><v>45306.4375</v></c><c r=\"D2\" s=\"4\" t=\"inlineStr\">", sheet);
		Assert.Contains("<c r=\"E2\" t=\"inlineStr\"><is><t>A1</t></is></c>", sheet);

		// Auto-fit's widths, the numbers and dates in their columns' formats; Note's own width; Code's, hidden.
		Assert.Contains(
			$"<cols><col min=\"1\" max=\"1\" width=\"{W(Fit(Px("Name", bold: true), Px("apple"), Px("pear")))}\"{Fitted}/>" +
			$"<col min=\"2\" max=\"2\" width=\"{W(Fit(Px("Amount", bold: true), Px("1.234.50"), Px("0.25")))}\" style=\"2\"{Fitted}/>" +
			$"<col min=\"3\" max=\"3\" width=\"{W(Fit(Px("When", bold: true), Px("15/01/2024 10:30")))}\" style=\"3\"{Fitted}/>" +
			"<col min=\"4\" max=\"4\" width=\"30.7109375\" style=\"4\" customWidth=\"1\"/>" +
			$"<col min=\"5\" max=\"5\" width=\"{W(Fit(Px("Code", bold: true), Px("A1"), Px("P2")))}\" hidden=\"1\"{Fitted}/></cols>", sheet);

		// The date's own format isn't added: the column's takes its place.
		var styles = Written.Entry(workbook, "xl/styles.xml");
		Assert.Contains("<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy hh:mm\"/></numFmts>", styles);
		Assert.Contains(
			"<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
			"<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
			"<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment wrapText=\"1\"/></xf></cellXfs>", styles);

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var worksheet = book.Worksheet(1);
			Assert.Equal(4, worksheet.Cell("B3").Style.NumberFormat.NumberFormatId);
			Assert.Equal("dd/mm/yyyy hh:mm", worksheet.Cell("C3").Style.NumberFormat.Format);
			Assert.True(worksheet.Cell("D2").Style.Alignment.WrapText);
			Assert.False(worksheet.Cell("D1").Style.Alignment.WrapText);
			Assert.True(worksheet.Column(5).IsHidden);
			Assert.False(worksheet.Column(4).IsHidden);
		}

		await ReadsBackAsWithoutTheLook(workbook, async);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StylesColumnsByTheirMembersAttributesUnlessColumnsHasThem(bool async)
	{
		AttributedLookOrder[] records = [new() { Name = "a", Rate = 0.125, Code = "X" }];

		var attributes = await Write(records, async);
		var replaced = await Write(records, async, new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Rate"] = new() { Wrap = true } } });

		var sheet = Written.Entry(attributes, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"B2\" s=\"2\"><v>0.125</v></c>", sheet);
		Assert.Contains(
			$"<cols><col min=\"1\" max=\"1\" width=\"{W(Fit(Px("Name", bold: true), Px("a")))}\"{Fitted}/>" +
			$"<col min=\"2\" max=\"2\" width=\"{W(Fit(Px("Rate", bold: true), Px("12.5%")))}\" style=\"2\"{Fitted}/>" +
			"<col min=\"3\" max=\"3\" width=\"12.7109375\" hidden=\"1\" customWidth=\"1\"/></cols>", sheet);
		Assert.Contains("formatCode=\"0.0%\"", Written.Entry(attributes, "xl/styles.xml"));

		// Columns' entry is taken whole, in place of the attribute: Rate wraps, in General; Code keeps its attribute.
		sheet = Written.Entry(replaced, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"B2\" s=\"2\"><v>0.125</v></c>", sheet);
		Assert.Contains("<col min=\"3\" max=\"3\" width=\"12.7109375\" hidden=\"1\" customWidth=\"1\"/>", sheet);
		var styles = Written.Entry(replaced, "xl/styles.xml");
		Assert.DoesNotContain("0.0%", styles);
		Assert.Contains("<alignment wrapText=\"1\"/>", styles);
		Assert.Empty(Written.ValidationErrors(attributes));
		Assert.Empty(Written.ValidationErrors(replaced));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesAMembersDateInItsColumnsFormat(bool async)
	{
		var options = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["When"] = new() { Format = "yyyy-mm-dd" } } };

		var workbook = await Write([new FormattedLookOrder { When = new DateTime(2024, 1, 15) }], async, options);

		// The member's format still reads the text back; the cell shows the column's.
		Assert.Contains("<c r=\"A2\" s=\"2\"><v>45306</v></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		var styles = Written.Entry(workbook, "xl/styles.xml");
		Assert.Contains("<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd\"/></numFmts>", styles);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DiscardsAHeaderWithoutAColumnsName(bool async)
	{
		var options = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions> { ["Totla"] = new() { Width = 10 }, ["Amount"] = new() { Width = 12 } },
		};
		var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true);

		var e = async
			? await Assert.ThrowsAsync<InvalidOperationException>(() => book.WriteSheetAsync("Data", Orders, options).AsTask())
			: Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Data", Orders, options));
		book.Dispose();

		Assert.Equal("ExcelSheetOptions.Columns names columns that the header doesn't have: \"Totla\". Names match the header's ordinally.", e.Message);
		var workbook = stream.ToArray();
		Assert.Contains("<sheetData></sheetData>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FindsColumnsByTheRecordTypesNamesWithoutAHeader(bool async)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };
		var columns = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Amount"] = new() { Format = "#,##0.00" } } };
		IReadOnlyList<string> names = [];
		InvalidOperationException? e = null;

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Data", Orders, columns);
			}
			else
			{
				book.WriteSheet("Data", Orders, columns);
			}

			// Without a record type, nothing names the columns.
			e = Assert.Throws<InvalidOperationException>(() => book.AddSheet("Plain", columns));
			names = [.. book.SheetNames];
		}, async, options);

		Assert.Contains("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B1\" s=\"1\"><v>1234.5</v></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Equal("ExcelSheetOptions.Columns names columns, and the sheet has no header to find them by (CsvWriterOptions.HasHeader is off): \"Amount\".", e!.Message);
		Assert.Equal(["Data"], names);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RepeatsTheTitleAndTheHeaderInNewSheets(bool async)
	{
		var records = Enumerable.Range(0, 5).Select(i => new LookOrder { Name = "n" + i, Amount = i, When = new DateTime(2024, 1, 15 + i), Note = "", Code = "c" }).ToList();
		var options = new ExcelWriterOptions { Strings = ExcelStrings.Shared, RowLimit = ExcelRowLimit.NewSheet };
		var look = new ExcelSheetOptions { Title = "Orders", Columns = new Dictionary<string, ExcelColumnOptions> { ["Amount"] = new() { Format = "0.00" } } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 4;
			if (a)
			{
				await book.WriteSheetAsync("Orders", records, look);
			}
			else
			{
				book.WriteSheet("Orders", records, look);
			}
		}, async, options);

		// Each sheet has the title, its merge, the bold header, the frozen panes and the columns, and two records.
		var sheets = Enumerable.Range(1, 3).Select(n => Written.Entry(workbook, $"xl/worksheets/sheet{n}.xml")).ToList();
		var top = Regex.Match(sheets[0], "<sheetViews>.*?<row r=\"3\">").Value;
		var merge = "<mergeCells count=\"1\"><mergeCell ref=\"A1:E1\"/></mergeCells>";
		Assert.Contains("<pane ySplit=\"2\" topLeftCell=\"A3\"", top);
		Assert.All(sheets, s => Assert.Contains(top, s));
		Assert.All(sheets, s => Assert.Contains(merge, s));
		Assert.Contains("<c r=\"B3\" s=\"3\"><v>2</v></c>", sheets[1]);
		Assert.Contains("<c r=\"B4\" s=\"3\"><v>3</v></c>", sheets[1]);
		Assert.DoesNotContain("<row r=\"5\">", sheets[0]);
		Assert.Empty(Written.ValidationErrors(workbook));

		var titled = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) };
		var read = new List<LookOrder>();
		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			foreach (var name in new[] { "Orders", "Orders (2)", "Orders (3)" })
			{
				using var reader = book.OpenReader(name, titled);
				read.AddRange(reader.GetRecords<LookOrder>());
			}
		}

		Assert.Equal(records.Select(Text), read.Select(Text));
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
	/// <param name="workbook">The workbook, of <see cref="Orders"/> in a sheet named <c>Data</c>.</param>
	/// <param name="async">Whether to write the plain one asynchronously.</param>
	/// <returns>A task that completes when the checks have.</returns>
	private static async Task ReadsBackAsWithoutTheLook(byte[] workbook, bool async)
	{
		var plain = await Write(Orders, async, Written.Phase4Look);

		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(Orders.Select(Text), ReadRecords(workbook).Select(Text));
		Assert.Equal(Written.ReadOne(plain), Written.ReadOne(workbook));
		Assert.Equal(Written.ClosedXmlValues(plain), Written.ClosedXmlValues(workbook));
		Assert.Equal(Written.ExcelDataReaderValues(plain), Written.ExcelDataReaderValues(workbook));
	}

	/// <summary>Reads a workbook's first sheet's orders through CsvHelper.Excel.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="options">The reader's options, or <see langword="null"/>.</param>
	/// <returns>The orders.</returns>
	private static List<LookOrder> ReadRecords(byte[] workbook, ExcelReaderOptions? options = null)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(options);

		return [.. reader.GetRecords<LookOrder>()];
	}

	/// <summary>Writes records into a sheet named <c>Data</c>, with a look.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <param name="options">The workbook's options, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(IEnumerable<T> records, bool async, ExcelSheetOptions? look = null, ExcelWriterOptions? options = null) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Data", records, look);
			}
			else
			{
				book.WriteSheet("Data", records, look);
			}
		}, async, options);

	/// <summary>A width as the writer writes it.</summary>
	/// <param name="width">The width.</param>
	/// <returns>Its shortest text.</returns>
	private static string W(double width) => width.ToString("R", CultureInfo.InvariantCulture);

	/// <summary>The width auto-fit gives the widest of some texts' pixels.</summary>
	/// <param name="pixels">The texts' pixels.</param>
	/// <returns>The width.</returns>
	private static double Fit(params double[] pixels) => ColumnWidths.Fit(pixels.Max());

	/// <summary>A text's pixels.</summary>
	/// <param name="text">The text: a number's or a date's in its widest form.</param>
	/// <param name="bold">Whether it is bold.</param>
	/// <returns>The pixels.</returns>
	private static double Px(string text, bool bold = false) => ColumnWidths.TextPixels(text.AsSpan(), bold);
}
