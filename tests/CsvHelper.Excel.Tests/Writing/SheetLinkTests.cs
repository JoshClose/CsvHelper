using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A site: a name, and a URI, which is a link.</summary>
[CsvRecord]
public partial class LinkedSite
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A URI's cell.</summary>
	public Uri? Site { get; set; }
}

/// <summary>
/// A written sheet's links (Phase 5's step 5), synchronously and asynchronously: each policy's links, in the
/// <c>Hyperlink</c> cell style, with a relationship for each distinct target after the table's; the header's URIs as
/// text; a failed record's links dropped; Excel's limits; and the new sheets of <see cref="ExcelRowLimit.NewSheet"/>.
/// Each workbook passes the Open XML SDK's validator, ClosedXML reports the links, and the records read back as without
/// them.
/// </summary>
public class SheetLinkTests
{
	/// <summary>A letter past ASCII: e with an acute accent.</summary>
	private static readonly string E = ((char)0xE9).ToString();

	/// <summary>A backslash.</summary>
	private static readonly string Backslash = ((char)92).ToString();

	/// <summary>Sites of each kind of URI that <see cref="ExcelLinks.Web"/> tells apart, and one without.</summary>
	private static readonly LinkedSite[] Sites =
	[
		new() { Name = "home", Site = new Uri("https://example.com/a?b=1&c=2#frag") },
		new() { Name = "mail", Site = new Uri("mailto:someone@example.com?subject=Hi there") },
		new() { Name = "again", Site = new Uri("https://example.com/a?b=1&c=2#frag") },
		new() { Name = "share", Site = new Uri("file://server/share/report.txt") },
		new() { Name = "relative", Site = new Uri("docs/readme.txt", UriKind.Relative) },
		new() { Name = "none" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LinksWebUris(bool async)
	{
		var workbook = await Write(Sites, async);

		// The links after the rows, before the page margins, the two of one target sharing its relationship.
		Assert.Contains(
			"</sheetData><hyperlinks><hyperlink ref=\"B2\" r:id=\"rId1\"/><hyperlink ref=\"B3\" r:id=\"rId2\"/><hyperlink ref=\"B4\" r:id=\"rId1\"/></hyperlinks><pageMargins ",
			Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.EndsWith(
			Relationships(Relationship("rId1", "https://example.com/a?b=1&amp;c=2#frag"), Relationship("rId2", "mailto:someone@example.com?subject=Hi%20there")),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		Assert.Contains(
			"<cellStyles count=\"2\"><cellStyle name=\"Hyperlink\" xfId=\"1\" builtinId=\"8\"/><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>",
			Written.Entry(workbook, "xl/styles.xml"));

		// The links' cells in the Hyperlink style, and the others not; a cell's text as written.
		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var sheet = book.Worksheet(1);
			Assert.Equal("https://example.com/a?b=1&c=2#frag", Link(sheet.Cell("B2")));
			Assert.Equal("mailto:someone@example.com?subject=Hi%20there", Link(sheet.Cell("B3")));
			Assert.Equal("mailto:someone@example.com?subject=Hi there", sheet.Cell("B3").GetString());
			Assert.Equal("https://example.com/a?b=1&c=2#frag", Link(sheet.Cell("B4")));
			foreach (var cell in new[] { "B2", "B3", "B4" })
			{
				Assert.Equal(XLFontUnderlineValues.Single, sheet.Cell(cell).Style.Font.Underline);
				Assert.Equal(unchecked((int)0xFF0563C1), sheet.Cell(cell).Style.Font.FontColor.Color.ToArgb());
			}

			foreach (var cell in new[] { "A2", "B1", "B5", "B6", "B7" })
			{
				Assert.False(sheet.Cell(cell).HasHyperlink, cell);
				Assert.Equal(XLFontUnderlineValues.None, sheet.Cell(cell).Style.Font.Underline);
			}
		}

		await ReadsBackAsWithoutThem(workbook, Sites, async);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LinksEveryUriWithAll(bool async)
	{
		LinkedSite[] sites =
		[
			new() { Name = "share", Site = new Uri(Backslash + Backslash + "server" + Backslash + "share" + Backslash + "a b.txt") },
			new() { Name = "file", Site = new Uri("C:" + Backslash + "Temp" + Backslash + "r" + E + "port.txt") },
			new() { Name = "relative", Site = new Uri("docs/r%C3%A9sum%C3%A9 1.txt", UriKind.Relative) },
			new() { Name = "script", Site = new Uri("javascript:alert(1)") },
			new() { Name = "jump", Site = new Uri("#Sheet2!A1", UriKind.Relative) },
		];

		var options = new ExcelWriterOptions { Links = ExcelLinks.All };
		var workbook = await Write(sites, async, options);

		// An absolute URI's AbsoluteUri, a relative one's text, each with its letters past ASCII as themselves.
		Assert.EndsWith(
			Relationships(
				Relationship("rId1", "file://server/share/a%20b.txt"),
				Relationship("rId2", "file:///C:/Temp/r" + E + "port.txt"),
				Relationship("rId3", "docs/r" + E + "sum" + E + " 1.txt"),
				Relationship("rId4", "javascript:alert(1)"),
				Relationship("rId5", "#Sheet2!A1")),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		Assert.Equal(5, Regex.Matches(Written.Entry(workbook, "xl/worksheets/sheet1.xml"), "<hyperlink ").Count);
		await ReadsBackAsWithoutThem(workbook, sites, async, options);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesNoLinksWithNone(bool async)
	{
		var workbook = await Write(Sites, async, new ExcelWriterOptions { Links = ExcelLinks.None });

		Assert.DoesNotContain("<hyperlinks>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.DoesNotContain(Written.Entries(workbook), e => e.Name.Contains("_rels/sheet"));
		Assert.Contains("<cellStyles count=\"1\">", Written.Entry(workbook, "xl/styles.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NumbersLinksAfterTheTable(bool async)
	{
		var workbook = await Write(Sites, async, look: new ExcelSheetOptions { Table = new() });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<hyperlinks><hyperlink ref=\"B2\" r:id=\"rId2\"/><hyperlink ref=\"B3\" r:id=\"rId3\"/><hyperlink ref=\"B4\" r:id=\"rId2\"/></hyperlinks><pageMargins ", sheet);
		Assert.Contains("<tableParts count=\"1\"><tablePart r:id=\"rId1\"/></tableParts></worksheet>", sheet);
		Assert.EndsWith(
			Relationships(
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table1.xml\"/>",
				Relationship("rId2", "https://example.com/a?b=1&amp;c=2#frag"),
				Relationship("rId3", "mailto:someone@example.com?subject=Hi%20there")),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			Assert.Equal("Sites", Assert.Single(book.Worksheet(1).Tables).Name);
			Assert.Equal("https://example.com/a?b=1&c=2#frag", Link(book.Worksheet(1).Cell("B4")));
		}

		await ReadsBackAsWithoutThem(workbook, Sites, async, look: new ExcelSheetOptions { Table = new() });
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StylesALinkInItsColumnsFormatAndWrap(bool async)
	{
		var look = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Site"] = new() { Format = "@", Wrap = true } } };

		var workbook = await Write(Sites, async, look: look);

		// The column's format and wrap, in the link's font and cell style.
		Assert.Contains(
			"<xf numFmtId=\"49\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\"/></xf>",
			Written.Entry(workbook, "xl/styles.xml"));
		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var cell = book.Worksheet(1).Cell("B2");
			Assert.Equal((49, true, XLFontUnderlineValues.Single), (cell.Style.NumberFormat.NumberFormatId, cell.Style.Alignment.WrapText, cell.Style.Font.Underline));
		}

		await ReadsBackAsWithoutThem(workbook, Sites, async, look: look);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTheHeadersUrisAsTextInEachNewSheet(bool async)
	{
		// The header names its columns: its URI is text, bold, in each new sheet, and the links below it are each sheet's.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			var writer = book.AddSheet("Sites");
			writer.WriteField(new Uri("https://example.com/header"));
			writer.WriteField("Name");
			await Written.EndRecordAsync(writer, a);
			for (var i = 0; i < 5; i++)
			{
				writer.WriteField(new Uri("https://example.com/" + (i % 2)));
				writer.WriteField("n" + i);
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		string[][] targets = [["https://example.com/0", "https://example.com/1"], ["https://example.com/0", "https://example.com/1"], ["https://example.com/0"]];
		for (var n = 1; n <= 3; n++)
		{
			var sheet = Written.Entry(workbook, $"xl/worksheets/sheet{n}.xml");
			var expected = n < 3
				? "<hyperlinks><hyperlink ref=\"A2\" r:id=\"rId1\"/><hyperlink ref=\"A3\" r:id=\"rId2\"/></hyperlinks>"
				: "<hyperlinks><hyperlink ref=\"A2\" r:id=\"rId1\"/></hyperlinks>";
			Assert.Contains(expected, sheet);
			Assert.EndsWith(
				Relationships([.. targets[n - 1].Select((t, i) => Relationship("rId" + (i + 1), t))]),
				Written.Entry(workbook, $"xl/worksheets/_rels/sheet{n}.xml.rels"));
		}

		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			foreach (var sheet in book.Worksheets)
			{
				Assert.False(sheet.Cell("A1").HasHyperlink);
				Assert.True(sheet.Cell("A1").Style.Font.Bold);
				Assert.Equal("https://example.com/header", sheet.Cell("A1").GetString());
				Assert.True(sheet.Cell("A2").HasHyperlink);
			}
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesEachNewSheetItsTableAndLinks(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			await WriteSheet(book, "Sites", Sites, a, new ExcelSheetOptions { Table = new() });
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		// Sites: home, mail; Sites (2): again, share; Sites (3): relative, none.
		Assert.EndsWith(
			Relationships(
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table1.xml\"/>",
				Relationship("rId2", "https://example.com/a?b=1&amp;c=2#frag"),
				Relationship("rId3", "mailto:someone@example.com?subject=Hi%20there")),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		Assert.EndsWith(
			Relationships(
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table2.xml\"/>",
				Relationship("rId2", "https://example.com/a?b=1&amp;c=2#frag")),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet2.xml.rels"));
		Assert.EndsWith(
			Relationships("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table3.xml\"/>"),
			Written.Entry(workbook, "xl/worksheets/_rels/sheet3.xml.rels"));
		Assert.Contains("<hyperlinks><hyperlink ref=\"B2\" r:id=\"rId2\"/></hyperlinks>", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.DoesNotContain("<hyperlinks>", Written.Entry(workbook, "xl/worksheets/sheet3.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DropsTheLinksOfAFailedRecord(bool async)
	{
		// The second record fails at its second field, after its link: the link and its target go with it.
		var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, Written.Ragged(), leaveOpen: true);
		var writer = book.AddSheet("Sites");
		writer.WriteField("Site");
		writer.WriteField("Note");
		await Written.EndRecordAsync(writer, async);
		Assert.Throws<InvalidOperationException>(() => writer.WriteRecord(new Dictionary<string, object?> { ["Site"] = new Uri("https://dropped.example/"), ["Note"] = new string('x', 40000) }));
		writer.WriteRecord(new Dictionary<string, object?> { ["Site"] = new Uri("https://kept.example/"), ["Note"] = "n" });
		await Written.DisposeAsync(writer, async);
		book.Dispose();

		var workbook = stream.ToArray();
		Assert.Contains("<hyperlinks><hyperlink ref=\"A2\" r:id=\"rId1\"/></hyperlinks>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.EndsWith(Relationships(Relationship("rId1", "https://kept.example/")), Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		Assert.Equal(["Site|Note", "https://kept.example/|n"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public async Task LinksAsManyUrisAsExcelOpens()
	{
		// Excel opens 65,530 links in a sheet: the next URI is text, in no style.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Many");
			for (var i = 0; i <= SheetLinks.MaxLinks; i++)
			{
				writer.WriteField(new Uri("https://example.com/" + i));
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, false, new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false }, Strings = ExcelStrings.Inline });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Equal(65_530, Regex.Matches(sheet, "<hyperlink ").Count);
		Assert.Contains("<hyperlink ref=\"A65530\" r:id=\"rId65530\"/></hyperlinks>", sheet);
		Assert.Contains("<c r=\"A65530\" s=\"1\" t=\"inlineStr\">", sheet);
		Assert.Contains("<c r=\"A65531\" t=\"inlineStr\"><is><t>https://example.com/65530</t></is></c>", sheet);
		Assert.Equal(65_530, Regex.Matches(Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"), "<Relationship ").Count);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LeavesAUriWhoseTargetIsTooLongAsText(bool async)
	{
		// Excel opens a target of 8,192 characters at most.
		var longest = "https://example.com/" + new string('a', SheetLinks.MaxTargetLength - 20);
		LinkedSite[] sites = [new() { Name = "longest", Site = new Uri(longest) }, new() { Name = "longer", Site = new Uri(longest + "b") }];

		var workbook = await Write(sites, async);

		Assert.Contains("<hyperlinks><hyperlink ref=\"B2\" r:id=\"rId1\"/></hyperlinks>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.EndsWith(Relationships(Relationship("rId1", longest)), Written.Entry(workbook, "xl/worksheets/_rels/sheet1.xml.rels"));
		await ReadsBackAsWithoutThem(workbook, sites, async);
	}

	/// <summary>Checks that a workbook passes the validator, and reads back as written without links, through CsvHelper.Excel, ClosedXML and ExcelDataReader.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sites">The sites it was written from.</param>
	/// <param name="async">Whether to write the one without links asynchronously.</param>
	/// <param name="options">The options it was written with, or <see langword="null"/>.</param>
	/// <param name="look">How its sheet looks, or <see langword="null"/>.</param>
	/// <returns>A task that completes when the checks have.</returns>
	private static async Task ReadsBackAsWithoutThem(byte[] workbook, LinkedSite[] sites, bool async, ExcelWriterOptions? options = null, ExcelSheetOptions? look = null)
	{
		var plain = await Write(sites, async, (options ?? ExcelWriterOptions.Default) with { Links = ExcelLinks.None }, look);

		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(Written.ReadOne(plain), Written.ReadOne(workbook));
		Assert.Equal(Written.ClosedXmlValues(plain), Written.ClosedXmlValues(workbook));
		Assert.Equal(Written.ExcelDataReaderValues(plain), Written.ExcelDataReaderValues(workbook));
	}

	/// <summary>A cell's link's target, as ClosedXML reads it.</summary>
	/// <param name="cell">The cell.</param>
	/// <returns>The target.</returns>
	private static string Link(IXLCell cell)
	{
		Assert.True(cell.HasHyperlink, cell.Address.ToString());

		return cell.GetHyperlink().ExternalAddress.OriginalString;
	}

	/// <summary>A link's relationship, as written.</summary>
	/// <param name="id">Its id.</param>
	/// <param name="target">Its target, as XML.</param>
	/// <returns>The XML.</returns>
	private static string Relationship(string id, string target) =>
		$"<Relationship Id=\"{id}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{target}\" TargetMode=\"External\"/>";

	/// <summary>A sheet's relationships part after its declaration.</summary>
	/// <param name="relationships">Its relationships.</param>
	/// <returns>The XML.</returns>
	private static string Relationships(params string[] relationships) =>
		"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + string.Concat(relationships) + "</Relationships>";

	/// <summary>Writes sites into one sheet, <c>Sites</c>.</summary>
	/// <param name="sites">The sites.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write(IEnumerable<LinkedSite> sites, bool async, ExcelWriterOptions? options = null, ExcelSheetOptions? look = null) =>
		Written.WorkbookAsync((book, a) => WriteSheet(book, "Sites", sites, a, look), async, options);

	/// <summary>Writes a sheet of sites, as the mode says.</summary>
	/// <param name="book">The workbook writer.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="sites">The sites.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <returns>A task that completes when the sheet is.</returns>
	private static async Task WriteSheet(ExcelWorkbookWriter book, string sheet, IEnumerable<LinkedSite> sites, bool async, ExcelSheetOptions? look)
	{
		if (async)
		{
			await book.WriteSheetAsync(sheet, sites, look);
		}
		else
		{
			book.WriteSheet(sheet, sites, look);
		}
	}
}
