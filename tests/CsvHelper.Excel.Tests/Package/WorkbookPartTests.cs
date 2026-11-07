using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The workbook part and what it names: content types, the package's and the workbook's relationships, the sheets with
/// their states and kinds, the 1904 date system, and the styles and shared strings parts. Each test loads synchronously
/// and asynchronously.
/// </summary>
public class WorkbookPartTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsTheSheetsInTabOrder(bool async)
	{
		var package = TestPackage.Workbook(
			new TestSheet("Chart", type: "chartsheet"),
			new TestSheet("Data"),
			new TestSheet("Hidden", "hidden"),
			new TestSheet("VeryHidden", "veryHidden"),
			new TestSheet("Chart 2", type: "chartsheet"),
			new TestSheet("Dialog", type: "dialogsheet"),
			new TestSheet("Macro", type: "xlMacrosheet"),
			new TestSheet("Shown", "visible"));

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(
			[
				"0 Chart Visible Chartsheet xl/chartsheets/sheet1.xml",
				"1 Data Visible Worksheet xl/worksheets/sheet2.xml",
				"2 Hidden Hidden Worksheet xl/worksheets/sheet3.xml",
				"3 VeryHidden VeryHidden Worksheet xl/worksheets/sheet4.xml",
				"4 Chart 2 Visible Chartsheet xl/chartsheets/sheet5.xml",
				"5 Dialog Visible DialogSheet xl/dialogsheets/sheet6.xml",
				"6 Macro Visible MacroSheet xl/macrosheets/sheet7.xml",
				"7 Shown Visible Worksheet xl/worksheets/sheet8.xml",
			],
			workbook.Sheets.Select(Packages.Describe));
		Assert.Equal("xl/workbook.xml", workbook.PartName);
		Assert.False(workbook.Date1904);
		Assert.Equal("xl/styles.xml", workbook.StylesPart);
		Assert.Null(workbook.SharedStringsPart);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DecodesSheetNames(bool async)
	{
		var package = TestPackage.Workbook("Q1 &amp; Orders", "Bob's", "&quot;Quoted&quot;", "日本語", "&#x1F600; Emoji", "Tab&#9;Name");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(["Q1 & Orders", "Bob's", "\"Quoted\"", "日本語", char.ConvertFromUtf32(0x1F600) + " Emoji", "Tab\tName"], workbook.Sheets.Select(s => s.Name));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DecodesEscapesInSheetNames(bool async)
	{
		// Excel decodes the _xHHHH_ escapes of sheet names, as of text (checked through COM on 2026-11-06).
		var package = TestPackage.Workbook("a_x0041_b", "_x005F_x0041_", "_X0041_", "a_x0009_b", "_x0041", "_xD83D__xDE00_");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(["aAb", "_x0041_", "_X0041_", "a\tb", "_x0041", char.ConvertFromUtf32(0x1F600)], workbook.Sheets.Select(s => s.Name));
	}

	[Theory]
	[InlineData("1", true)]
	[InlineData("0", false)]
	[InlineData("true", true)]
	[InlineData("false", false)]
	[InlineData(" true ", true)]
	[InlineData("&#49;", true)]
	public async Task ReadsTheDateSystem(string value, bool expected)
	{
		var package = TestPackage.Workbook("Data").Replace("xl/workbook.xml", "<workbookPr ", $"<workbookPr date1904=\"{value}\" ");

		foreach (var async in new[] { false, true })
		{
			Assert.Equal(expected, (await package.LoadWorkbookAsync(async)).Date1904);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsADateSystemThatIsNotABoolean(bool async)
	{
		var package = TestPackage.Workbook("Data").Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"yes\" ");

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
		Assert.Matches(@"^The XML of xl/workbook\.xml can't be read at byte \d+: the attribute date1904=""yes"" is not a boolean\.$", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsStrictWorkbooks(bool async)
	{
		var package = TestPackage.Workbook("Data", "More");
		foreach (var name in package.Names.ToList())
		{
			package.Edit(name, xml => xml
				.Replace(TestPackage.MainNamespace, TestPackage.StrictMainNamespace)
				.Replace(TestPackage.RelationshipsNamespace, TestPackage.StrictRelationshipsNamespace));
		}

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Contains(TestPackage.StrictRelationshipsNamespace + "/worksheet", package.Text("xl/_rels/workbook.xml.rels"));
		Assert.Equal(["0 Data Visible Worksheet xl/worksheets/sheet1.xml", "1 More Visible Worksheet xl/worksheets/sheet2.xml"], workbook.Sheets.Select(Packages.Describe));
		Assert.Equal("xl/styles.xml", workbook.StylesPart);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsPrefixedElements(bool async)
	{
		var package = TestPackage.Workbook("Data").Set(
			"xl/workbook.xml",
			TestPackage.Declaration + $"<x:workbook xmlns:x=\"{TestPackage.MainNamespace}\" xmlns:rel=\"{TestPackage.RelationshipsNamespace}\">" +
			"<x:workbookPr x:date1904=\"1\"/><x:sheets><x:sheet name=\"Data\" sheetId=\"1\" rel:id=\"rId1\"/></x:sheets></x:workbook>");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.True(workbook.Date1904);
		Assert.Equal(["0 Data Visible Worksheet xl/worksheets/sheet1.xml"], workbook.Sheets.Select(Packages.Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsWhatLibreOfficeWrites(bool async)
	{
		// Boolean attributes as true and false, the sheet as rId3, and the content types as the last zip entry.
		var package = TestPackage.Workbook("Sheet1")
			.Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr backupFile=\"false\" showObjects=\"all\" date1904=\"false\" ")
			.Replace("xl/workbook.xml", "r:id=\"rId1\"", "r:id=\"rId3\"")
			.Replace("xl/_rels/workbook.xml.rels", "Id=\"rId1\"", "Id=\"rId3\"")
			.Replace("xl/_rels/workbook.xml.rels", "Id=\"rId2\"", "Id=\"rId1\"")
			.MoveLast("[Content_Types].xml");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.False(workbook.Date1904);
		Assert.Equal(["0 Sheet1 Visible Worksheet xl/worksheets/sheet1.xml"], workbook.Sheets.Select(Packages.Describe));
		Assert.Equal("xl/styles.xml", workbook.StylesPart);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheSharedStringsPart(bool async)
	{
		var package = TestPackage.Workbook("Data")
			.Replace("xl/_rels/workbook.xml.rels", "</Relationships>", $"<Relationship Id=\"rId9\" Type=\"{TestPackage.RelationshipType("sharedStrings")}\" Target=\"sharedStrings.xml\"/></Relationships>");

		Assert.Equal("xl/sharedStrings.xml", (await package.LoadWorkbookAsync(async)).SharedStringsPart);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FallsBackToTheUsualWorkbookPartWithoutPackageRelationships(bool async)
	{
		var workbook = await TestPackage.Workbook("Data").Remove("_rels/.rels").LoadWorkbookAsync(async);

		Assert.Equal("xl/workbook.xml", workbook.PartName);
		Assert.Single(workbook.Sheets);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() =>
			TestPackage.Workbook("Data").Remove("_rels/.rels").Rename("xl/workbook.xml", "xl/book.xml").LoadWorkbookAsync(async));
		Assert.Equal("The package has no workbook: its workbook part, xl/workbook.xml, is missing.", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FollowsTheOfficeDocumentRelationship(bool async)
	{
		var package = TestPackage.Workbook("Data")
			.Rename("xl/workbook.xml", "data/book.xml")
			.Rename("xl/_rels/workbook.xml.rels", "data/_rels/book.xml.rels")
			.Replace("_rels/.rels", "Target=\"xl/workbook.xml\"", "Target=\"/data/book.xml\"")
			.Replace("[Content_Types].xml", "/xl/workbook.xml", "/data/book.xml")
			.Replace("data/_rels/book.xml.rels", "Target=\"worksheets/sheet1.xml\"", "Target=\"../xl/worksheets/sheet1.xml\"")
			.Replace("data/_rels/book.xml.rels", "Target=\"styles.xml\"", "Target=\"/xl/styles.xml\"");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal("data/book.xml", workbook.PartName);
		Assert.Equal(["0 Data Visible Worksheet xl/worksheets/sheet1.xml"], workbook.Sheets.Select(Packages.Describe));
		Assert.Equal("xl/styles.xml", workbook.StylesPart);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() =>
			TestPackage.Workbook("Data").Replace("_rels/.rels", "/officeDocument\"", "/somethingElse\"").LoadWorkbookAsync(async));
		Assert.Equal("The package has no workbook: _rels/.rels has no officeDocument relationship.", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MatchesPartNamesAsOpcDoes(bool async)
	{
		// A target with a percent-encoded space and different case, to an entry with backslashes.
		var package = TestPackage.Workbook("Data")
			.Rename("xl/worksheets/sheet1.xml", "xl\\Worksheets\\Sheet 1.xml")
			.Replace("xl/_rels/workbook.xml.rels", "Target=\"worksheets/sheet1.xml\"", "Target=\"worksheets/sheet%201.xml\"");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(["0 Data Visible Worksheet xl/worksheets/sheet 1.xml"], workbook.Sheets.Select(Packages.Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsSheetsItCantRead(bool async)
	{
		var package = TestPackage.Workbook("Missing", "NoRel", "NoId", "External", "Unknown", "Fine")
			.Remove("xl/worksheets/sheet1.xml")
			.Replace("xl/_rels/workbook.xml.rels", "<Relationship Id=\"rId2\" ", "<Relationship Id=\"rIdX\" ")
			.Replace("xl/workbook.xml", " r:id=\"rId3\"", "")
			.Replace("xl/_rels/workbook.xml.rels", "Target=\"worksheets/sheet4.xml\"", "Target=\"http://example.com/sheet.xml\" TargetMode=\"External\"")
			.Replace("xl/_rels/workbook.xml.rels", TestPackage.RelationshipType("worksheet") + "\" Target=\"worksheets/sheet5.xml\"", TestPackage.RelationshipType("image") + "\" Target=\"worksheets/sheet5.xml\"");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(
			[
				"0 Missing Visible Worksheet (its part xl/worksheets/sheet1.xml is missing)",
				"1 NoRel Visible Worksheet (its relationship rId2 is missing from xl/_rels/workbook.xml.rels)",
				"2 NoId Visible Worksheet (it has no relationship id (r:id))",
				"3 External Visible Worksheet (its relationship rId4 is to a file outside the workbook)",
				$"4 Unknown Visible Worksheet (its relationship rId5 has the type {TestPackage.RelationshipType("image")}, which is not a sheet's)",
				"5 Fine Visible Worksheet xl/worksheets/sheet6.xml",
			],
			workbook.Sheets.Select(Packages.Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsSheetsWithoutWorkbookRelationships(bool async)
	{
		var workbook = await TestPackage.Workbook("Data").Remove("xl/_rels/workbook.xml.rels").LoadWorkbookAsync(async);

		Assert.Equal(["0 Data Visible Worksheet (its relationship rId1 is missing from xl/_rels/workbook.xml.rels)"], workbook.Sheets.Select(Packages.Describe));
		Assert.Null(workbook.StylesPart);
	}

	[Theory]
	[InlineData(ContentTypes.Workbook)]
	[InlineData(ContentTypes.Template)]
	[InlineData(ContentTypes.MacroEnabledWorkbook)]
	[InlineData(ContentTypes.MacroEnabledTemplate)]
	[InlineData(ContentTypes.AddIn)]
	[InlineData("APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.SPREADSHEETML.SHEET.MAIN+XML")]
	[InlineData(ContentTypes.Workbook + "; charset=utf-8")]
	public async Task AcceptsEveryKindOfWorkbook(string contentType)
	{
		var package = TestPackage.Workbook("Data").Replace("[Content_Types].xml", ContentTypes.Workbook, contentType);

		foreach (var async in new[] { false, true })
		{
			Assert.Single((await package.LoadWorkbookAsync(async)).Sheets);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsWithoutContentTypesOrWithOnlyADefault(bool async)
	{
		Assert.Single((await TestPackage.Workbook("Data").Remove("[Content_Types].xml").LoadWorkbookAsync(async)).Sheets);

		// No override for the workbook: the default for .xml, application/xml, is not a workbook's.
		var defaultOnly = TestPackage.Workbook("Data").Replace("[Content_Types].xml", "<Override PartName=\"/xl/workbook.xml\"", "<Override PartName=\"/xl/other.xml\"");
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => defaultOnly.LoadWorkbookAsync(async));
		Assert.Equal("The package is not a spreadsheet: its main part, xl/workbook.xml, has the content type application/xml.", e.Message);

		var noDefault = defaultOnly.Replace("[Content_Types].xml", "<Default Extension=\"xml\" ContentType=\"application/xml\"/>", "");
		Assert.Single((await noDefault.LoadWorkbookAsync(async)).Sheets);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsBinaryWorkbooksAndOtherPackages(bool async)
	{
		var binary = TestPackage.Workbook("Data").Replace("[Content_Types].xml", ContentTypes.Workbook, ContentTypes.BinaryWorkbook);
		var notSupported = await Assert.ThrowsAsync<NotSupportedException>(() => binary.LoadWorkbookAsync(async));
		Assert.Equal("The workbook is an Excel binary workbook (.xlsb), which CsvHelper.Excel doesn't read.", notSupported.Message);

		var document = new TestPackage()
			.Set("[Content_Types].xml", $"<Types xmlns=\"{TestPackage.ContentTypesNamespace}\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>")
			.Set("_rels/.rels", $"<Relationships xmlns=\"{TestPackage.PackageRelationshipsNamespace}\"><Relationship Id=\"rId1\" Type=\"{TestPackage.RelationshipType("officeDocument")}\" Target=\"word/document.xml\"/></Relationships>")
			.Set("word/document.xml", "<document/>");
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => document.LoadWorkbookAsync(async));
		Assert.Equal("The package is not a spreadsheet: its main part, word/document.xml, has the content type application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml.", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsSheetsItCantList(bool async)
	{
		var noName = TestPackage.Workbook("Data").Replace("xl/workbook.xml", "name=\"Data\" ", "");
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => noName.LoadWorkbookAsync(async));
		Assert.Matches(@"^The XML of xl/workbook\.xml can't be read at byte \d+: a sheet has no name\.$", e.Message);

		var badState = TestPackage.Workbook(new TestSheet("Data", "Hidden"));
		e = await Assert.ThrowsAsync<InvalidDataException>(() => badState.LoadWorkbookAsync(async));
		Assert.Matches(@"^The XML of xl/workbook\.xml can't be read at byte \d+: the attribute state=""Hidden"" is not a sheet state \(visible, hidden or veryHidden\)\.$", e.Message);

		var notAWorkbook = TestPackage.Workbook("Data").Set("xl/workbook.xml", "<worksheet/>");
		e = await Assert.ThrowsAsync<InvalidDataException>(() => notAWorkbook.LoadWorkbookAsync(async));
		Assert.Equal("The XML of xl/workbook.xml can't be read at byte 0: its root element is not workbook.", e.Message);
	}

	[Theory]
	[InlineData("[Content_Types].xml", "its root element is not Types")]
	[InlineData("_rels/.rels", "its root element is not Relationships")]
	[InlineData("xl/_rels/workbook.xml.rels", "its root element is not Relationships")]
	public async Task RejectsPartsWithTheWrongRoot(string part, string detail)
	{
		var package = TestPackage.Workbook("Data").Set(part, "<Other/>");

		foreach (var async in new[] { false, true })
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
			Assert.Equal($"The XML of {part} can't be read at byte 0: {detail}.", e.Message);
		}
	}

	[Theory]
	[InlineData("[Content_Types].xml")]
	[InlineData("_rels/.rels")]
	[InlineData("xl/workbook.xml")]
	[InlineData("xl/_rels/workbook.xml.rels")]
	public async Task RejectsADoctypeInEachPart(string part)
	{
		var package = TestPackage.Workbook("Data").Edit(part, xml => xml.Replace(TestPackage.Declaration, TestPackage.Declaration + "<!DOCTYPE x [<!ENTITY a \"aaaa\">]>"));

		foreach (var async in new[] { false, true })
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
			Assert.Equal($"The XML of {part} can't be read at byte {TestPackage.Declaration.Length}: it has a DOCTYPE, which Office Open XML parts must not have.", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsUtf16AndByteOrderMarks(bool async)
	{
		var package = TestPackage.Workbook("Données");
		var workbook = package.Text("xl/workbook.xml").Replace("encoding=\"UTF-8\"", "encoding=\"UTF-16\"");
		package.Set("xl/workbook.xml", [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(workbook)]);
		package.Set("_rels/.rels", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(package.Text("_rels/.rels"))]);

		Assert.Equal(["0 Données Visible Worksheet xl/worksheets/sheet1.xml"], (await package.LoadWorkbookAsync(async)).Sheets.Select(Packages.Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksThePartsAgainstTheLimits(bool async)
	{
		// A comment makes the workbook part the largest, so that it is the one past the part limit.
		var package = TestPackage.Workbook("Data").Replace("xl/workbook.xml", "<calcPr ", "<!--" + new string('-', 2000).Replace("--", "- ") + "--><calcPr ");
		var workbookSize = Encoding.UTF8.GetByteCount(package.Text("xl/workbook.xml"));

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async, new PartLimits(workbookSize - 1, 1L << 30)));
		Assert.Equal($"xl/workbook.xml is larger than ExcelReaderOptions.MaxPartSize ({workbookSize - 1} bytes uncompressed): its size in the zip is {workbookSize} bytes.", e.Message);
		Assert.Single((await package.LoadWorkbookAsync(async, new PartLimits(workbookSize, 1L << 30))).Sheets);

		// Content types, package relationships, workbook relationships, workbook: the limit is passed in the last.
		var total = new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels" }.Sum(p => Encoding.UTF8.GetByteCount(package.Text(p)));
		e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async, new PartLimits(1 << 20, total - 1)));
		Assert.Equal($"Reading xl/workbook.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({total - 1} bytes uncompressed).", e.Message);
		Assert.Single((await package.LoadWorkbookAsync(async, new PartLimits(1 << 20, total))).Sheets);
	}
}
