namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Tables: each worksheet's <c>table</c> relationships and their parts, read with the workbook part. Each test loads
/// synchronously and asynchronously.
/// </summary>
public class TablePartTests
{
	/// <summary>Excel's table with a totals row (from a workbook Excel 365 saved on 2026-11-04).</summary>
	public const string Sales =
		"<table xmlns=\"" + TestPackage.MainNamespace + "\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"xr xr3\" " +
		"xmlns:xr=\"http://schemas.microsoft.com/office/spreadsheetml/2014/revision\" xmlns:xr3=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision3\" " +
		"id=\"1\" xr:uid=\"{6EE14435-6E50-4DBA-8050-7209FCEF06C9}\" name=\"Sales\" displayName=\"Sales\" ref=\"A1:B4\" totalsRowCount=\"1\">" +
		"<autoFilter ref=\"A1:B3\" xr:uid=\"{6EE14435-6E50-4DBA-8050-7209FCEF06C9}\"/><tableColumns count=\"2\">" +
		"<tableColumn id=\"1\" xr3:uid=\"{9C245089-5E0C-4CAB-B1E1-736034AC81BE}\" name=\"Product\" totalsRowLabel=\"Total\"/>" +
		"<tableColumn id=\"2\" xr3:uid=\"{A215F473-831F-4972-A768-EFD80BC129BD}\" name=\"Qty\" totalsRowFunction=\"sum\"/></tableColumns>" +
		"<tableStyleInfo name=\"TableStyleMedium2\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/></table>";

	/// <summary>Excel's table with its header row hidden: the names are only in <c>tableColumns</c>.</summary>
	public const string NoHeader =
		"<table xmlns=\"" + TestPackage.MainNamespace + "\" id=\"2\" name=\"NoHeader\" displayName=\"NoHeader\" ref=\"D6:E6\" headerRowCount=\"0\" totalsRowShown=\"0\">" +
		"<tableColumns count=\"2\"><tableColumn id=\"1\" name=\"X\"/><tableColumn id=\"2\" name=\"Y\"/></tableColumns>" +
		"<tableStyleInfo name=\"TableStyleMedium2\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/></table>";

	/// <summary>A table part.</summary>
	/// <param name="attributes">The root's attributes after its namespace.</param>
	/// <param name="columns">The columns' names, as they go in the XML.</param>
	/// <returns>The part's XML.</returns>
	public static string Table(string attributes, params string[] columns) =>
		TestPackage.Declaration + $"<table xmlns=\"{TestPackage.MainNamespace}\" id=\"1\" {attributes}>" +
		$"<tableColumns count=\"{columns.Length}\">" + string.Concat(columns.Select((c, i) => $"<tableColumn id=\"{i + 1}\" name=\"{c}\"/>")) +
		"</tableColumns></table>";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsExcelsTables(bool async)
	{
		var package = TestPackage.Workbook("Messy", "Tables")
			.Table("xl/worksheets/sheet2.xml", TestPackage.Declaration + Sales)
			.Table("xl/worksheets/sheet2.xml", TestPackage.Declaration + NoHeader);

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Empty(workbook.Sheets[0].Tables);
		Assert.Equal(
			["Sales A1:B4 1 1 Product|Qty xl/tables/table1.xml", "NoHeader D6:E6 0 0 X|Y xl/tables/table2.xml"],
			workbook.Sheets[1].Tables.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTablesOfEverySheetInTheirRelationshipsOrder(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("Chart", type: "chartsheet"), new TestSheet("First"), new TestSheet("Second"))
			.Table("xl/worksheets/sheet3.xml", Table("name=\"B\" displayName=\"B\" ref=\"A1:A2\"", "b"))
			.Table("xl/worksheets/sheet2.xml", Table("name=\"A\" displayName=\"A\" ref=\"C3:D5\"", "a1", "a2"))
			.Table("xl/worksheets/sheet3.xml", Table("name=\"C\" displayName=\"C\" ref=\"E1:E2\"", "c"));

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Empty(workbook.Sheets[0].Tables);
		Assert.Equal(["A C3:D5 1 0 a1|a2 xl/tables/table2.xml"], workbook.Sheets[1].Tables.Select(Describe));
		Assert.Equal(["B A1:A2 1 0 b xl/tables/table1.xml", "C E1:E2 1 0 c xl/tables/table3.xml"], workbook.Sheets[2].Tables.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsNamesAndColumns(bool async)
	{
		var cases = new (string Xml, string Expected)[]
		{
			// The display name is what formulas use; the name stands in when there is no display name.
			(Table("name=\"Internal\" displayName=\"Shown\" ref=\"A1:B3\"", "a", "b"), "Shown A1:B3 1 0 a|b"),
			(Table("name=\"Internal\" ref=\"A1:B3\"", "a", "b"), "Internal A1:B3 1 0 a|b"),
			(Table("name=\"Internal\" displayName=\"\" ref=\"A1:B3\"", "a", "b"), "Internal A1:B3 1 0 a|b"),
			(Table("displayName=\"Shown\" ref=\"A1:B3\"", "a", "b"), "Shown A1:B3 1 0 a|b"),
			(Table("displayName=\"T\" ref=\"$b$2:$c$4\" headerRowCount=\" 1 \" totalsRowCount=\"&#49;\"", "a", "b"), "T B2:C4 1 1 a|b"),
			(Table("displayName=\"T\" ref=\"A1\" headerRowCount=\"0\"", "a"), "T A1 0 0 a"),
			(Table("displayName=\"T\" ref=\"A1:A2\" headerRowCount=\"1\" totalsRowCount=\"1\"", "a"), "T A1:A2 1 1 a"),

			// Column names: entities, Excel's escape of a line break, and a literal escape.
			(Table("displayName=\"T\" ref=\"A1:D2\"", "Line_x000a_Break", "Fish &amp; Chips", "_x005F_x0041_", ""), "T A1:D2 1 0 Line\nBreak|Fish & Chips|_x0041_|"),
			(Table("displayName=\"T\" ref=\"A1:A2\""), "T A1:A2 1 0 "),
		};
		foreach (var (xml, expected) in cases)
		{
			var package = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", xml);

			var workbook = await package.LoadWorkbookAsync(async);

			Assert.Equal(expected + " xl/tables/table1.xml", Describe(workbook.Sheets[0].Tables.Single()));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsStrictAndPrefixedTables(bool async)
	{
		var xml = TestPackage.Declaration + $"<x:table xmlns:x=\"{TestPackage.StrictMainNamespace}\" id=\"1\" name=\"T\" displayName=\"T\" ref=\"A1:B2\">" +
			"<x:autoFilter ref=\"A1:B2\"/><x:tableColumns count=\"2\"><x:tableColumn id=\"1\" name=\"a\"/><x:tableColumn id=\"2\" name=\"b\"/></x:tableColumns></x:table>";
		var package = TestPackage.Workbook("Data").Set("xl/tables/table1.xml", xml)
			.Relate("xl/worksheets/sheet1.xml", TestPackage.RelationshipType("table", strict: true), "../tables/table1.xml");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(["T A1:B2 1 0 a|b xl/tables/table1.xml"], workbook.Sheets[0].Tables.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsOnlyTheTablesASheetRelatesThatExist(bool async)
	{
		var table = Table("displayName=\"T\" ref=\"A1:A2\"", "a");
		var package = TestPackage.Workbook("Data", "Other")
			.Table("xl/worksheets/sheet1.xml", table)
			.Relate("xl/worksheets/sheet1.xml", TestPackage.RelationshipType("table"), "../tables/missing.xml")
			.Relate("xl/worksheets/sheet1.xml", TestPackage.RelationshipType("table"), "https://example.com/table.xml", external: true)
			.Relate("xl/worksheets/sheet1.xml", TestPackage.RelationshipType("printerSettings"), "../printerSettings/printerSettings1.bin")
			.Relate("xl/worksheets/sheet1.xml", TestPackage.RelationshipType("drawing"), "../tables/table1.xml")
			.Set("xl/tables/table9.xml", table);

		var workbook = await package.LoadWorkbookAsync(async);

		// The table part no relationship names, and the drawing relationship to a table part, are no tables of the sheet.
		Assert.Equal(["T A1:A2 1 0 a xl/tables/table1.xml"], workbook.Sheets[0].Tables.Select(Describe));
		Assert.Empty(workbook.Sheets[1].Tables);
	}

	[Theory]
	[InlineData("displayName=\"T\"", @"the table has no ref")]
	[InlineData("ref=\"A1:A2\"", @"the table has no name")]
	[InlineData("name=\"\" displayName=\"\" ref=\"A1:A2\"", @"the table has no name")]
	[InlineData("displayName=\"T\" ref=\"A1:\"", @"the attribute ref=""A1:"" is not a range of cells, such as A1:D10")]
	[InlineData("displayName=\"T\" ref=\"Sheet1!A1:A2\"", @"the attribute ref=""Sheet1!A1:A2"" is not a range of cells, such as A1:D10")]
	[InlineData("displayName=\"T\" ref=\"A1:A2\" headerRowCount=\"-1\"", @"the attribute headerRowCount=""-1"" is not a number of rows \(0 or more\)")]
	[InlineData("displayName=\"T\" ref=\"A1:A2\" totalsRowCount=\"x\"", @"the attribute totalsRowCount=""x"" is not an integer")]
	[InlineData("displayName=\"T\" ref=\"A1:A2\" headerRowCount=\"2\" totalsRowCount=\"1\"", @"its header and totals rows, 3, are more than the 2 rows of its ref, A1:A2")]
	[InlineData("displayName=\"T\" ref=\"A1\" totalsRowCount=\"1\"", @"its header and totals rows, 2, are more than the 1 rows of its ref, A1")]
	public async Task RejectsTablesThatAreNotOnes(string attributes, string detail)
	{
		var package = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", Table(attributes, "a"));

		foreach (var async in new[] { false, true })
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
			Assert.Matches($@"^The XML of xl/tables/table1\.xml can't be read at byte \d+: {detail}\.$", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsBrokenParts(bool async)
	{
		var cases = new (string Xml, string Detail)[]
		{
			(TestPackage.Declaration + $"<tables xmlns=\"{TestPackage.MainNamespace}\"/>", "its root element is not table"),
			(TestPackage.Declaration + "<!DOCTYPE table [<!ENTITY x \"x\">]><table/>", "it has a DOCTYPE, which Office Open XML parts must not have"),
			(Table("displayName=\"T\" ref=\"A1:A2\"", "a").Replace("name=\"a\"", ""), "a table column has no name"),
			(Table("displayName=\"T\" ref=\"A1:A2\"", "a").Replace("</table>", ""), "it ends inside an element"),
		};
		foreach (var (xml, detail) in cases)
		{
			var package = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", xml);

			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
			Assert.Matches($@"^The XML of xl/tables/table1\.xml can't be read at byte \d+: {System.Text.RegularExpressions.Regex.Escape(detail)}\.$", e.Message);
		}

		// A sheet's relationships part that can't be read stops the workbook too.
		var relationships = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", Table("displayName=\"T\" ref=\"A1:A2\"", "a"))
			.Replace("xl/worksheets/_rels/sheet1.xml.rels", "<Relationships ", "<!DOCTYPE x><Relationships ");
		var broken = await Assert.ThrowsAsync<InvalidDataException>(() => relationships.LoadWorkbookAsync(async));
		Assert.StartsWith("The XML of xl/worksheets/_rels/sheet1.xml.rels can't be read at byte ", broken.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheLimits(bool async)
	{
		var columns = Enumerable.Range(0, 2000).Select(i => "column " + i).ToArray();
		var package = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", Table("displayName=\"T\" ref=\"A1:BXX2\"", columns));
		var size = package.Text("xl/tables/table1.xml").Length;

		var workbook = await package.LoadWorkbookAsync(async, new PartLimits(size, Packages.Limits.MaxUncompressedSize));
		Assert.Equal(2000, workbook.Sheets[0].Tables.Single().Columns.Count);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async, new PartLimits(size - 1, Packages.Limits.MaxUncompressedSize)));
		Assert.StartsWith($"xl/tables/table1.xml is larger than ExcelReaderOptions.MaxPartSize ({size - 1} bytes uncompressed)", e.Message);
	}

	/// <summary>Describes a table for comparisons: <c>name ref header totals columns part</c>.</summary>
	/// <param name="table">The table.</param>
	/// <returns>The description.</returns>
	private static string Describe(TablePart table) =>
		$"{table.Name} {table.Range} {table.HeaderRowCount} {table.TotalsRowCount} {string.Join("|", table.Columns)} {table.PartName}";
}
