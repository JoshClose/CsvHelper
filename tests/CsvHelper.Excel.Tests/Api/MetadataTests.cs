using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// The workbook's metadata through the public API: its tables, its defined names and the blocks they refer to, and its
/// sheets' dimensions. Each test opens synchronously and asynchronously.
/// </summary>
public class MetadataTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsTheTables(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("Chart", type: "chartsheet"), new TestSheet("Messy"), new TestSheet("Tables"))
			.Table("xl/worksheets/sheet3.xml", TestPackage.Declaration + TablePartTests.Sales)
			.Table("xl/worksheets/sheet3.xml", TestPackage.Declaration + TablePartTests.NoHeader)
			.Table("xl/worksheets/sheet2.xml", TablePartTests.Table("name=\"First\" displayName=\"First\" ref=\"C3:D5\"", "a", "Line_x000a_Break"));

		using var workbook = await Workbooks.OpenAsync(package, async);

		Assert.Equal(["First", "Sales", "NoHeader"], workbook.Tables.Select(t => t.ToString()));
		Assert.Equal(
			["First Messy C3:D5 1 0 a|Line\nBreak", "Sales Tables A1:B4 1 1 Product|Qty", "NoHeader Tables D6:E6 0 0 X|Y"],
			workbook.Tables.Select(Describe));
		Assert.Empty(workbook.Sheets[0].Tables);
		Assert.Equal(["First"], workbook.Sheets[1].Tables.Select(t => t.Name));
		Assert.Equal(["Sales", "NoHeader"], workbook.Sheets[2].Tables.Select(t => t.Name));
		Assert.Same(workbook.Sheets[2], workbook.Tables[1].Sheet);
		Assert.Same(workbook.Sheets[2].Tables[0], workbook.Tables[1]);
		Assert.True(((ICollection<ExcelTable>)workbook.Tables).IsReadOnly);
		Assert.True(((ICollection<ExcelTable>)workbook.Sheets[2].Tables).IsReadOnly);
		Assert.True(((ICollection<string>)workbook.Tables[0].Columns).IsReadOnly);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsTheDefinedNamesAndTheirBlocks(bool async)
	{
		var package = TestPackage.Workbook("Messy", "Tables", "Hidden All").DefinedNames(DefinedNamesTests.ExcelsNames);

		using var workbook = await Workbooks.OpenAsync(package, async);

		Assert.Equal(
			[
				"'Hidden All'!_FilterDatabase | Hidden All | hidden | 'Hidden All'!$A$1:$A$6 | Hidden All A1:A6",
				"Const | - | shown | 0.05 | - -",
				"Tables!Data | Tables | shown | Tables!$A$1:$B$3 | Tables A1:B3",
				"Data | - | shown | Messy!$B$4:$F$14 | Messy B4:F14",
				"Dyn | - | shown | OFFSET(Messy!$B$4,0,0,COUNTA(Messy!$B:$B),5) | - -",
				"HiddenName | - | hidden | Messy!$B$5 | Messy B5",
				"Messy!Local | Messy | shown | Messy!$B$5:$C$6 | Messy B5:C6",
				"Messy!Print_Area | Messy | shown | Messy!$A$1:$F$14 | Messy A1:F14",
				"Rel | - | shown | Messy!B2 | Messy B2",
				"Spaced | - | shown | 'Hidden All'!$A$1:$A$6 | Hidden All A1:A6",
				"TableRef | - | shown | Sales[Qty] | - -",
				"ThreeD | - | shown | Messy:Tables!$A$1 | - -",
				"ToGone | - | shown | #REF! | - -",
				"Union | - | shown | Messy!$B$5,Messy!$C$6 | - -",
				"WholeCol | - | shown | Messy!$B:$C | Messy B:C",
				"WholeRow | - | shown | Messy!$4:$4 | Messy 4:4",
			],
			workbook.DefinedNames.Select(Describe));
		Assert.Equal(["_FilterDatabase", "Print_Area"], workbook.DefinedNames.Where(n => n.Name.Contains("_")).Select(n => n.Name));
		Assert.Same(workbook.Sheets[2], workbook.DefinedNames[0].Scope);
		Assert.Same(workbook.Sheets[2], workbook.DefinedNames[0].Sheet);
		Assert.True(((ICollection<ExcelDefinedName>)workbook.DefinedNames).IsReadOnly);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FindsTheSheetOfABlockAsExcelDoes(bool async)
	{
		// Twin and TWIN differ only in case, which Excel doesn't allow, so a name of either is the first's, as a search in tab
		// order finds it; Plot is a chartsheet before the worksheet PLOT, so a name of plot is the worksheet's.
		var package = TestPackage.Workbook(
				new TestSheet("Chart", type: "chartsheet"), new TestSheet("Data"), new TestSheet("Q1 &amp; Orders"), new TestSheet("Twin"), new TestSheet("TWIN"),
				new TestSheet("Plot", type: "chartsheet"), new TestSheet("PLOT"))
			.DefinedNames(
				"<definedName name=\"Lower\">data!$a$1:$b$2</definedName>",
				"<definedName name=\"Quoted\">'Q1 &amp; Orders'!$C$3</definedName>",
				"<definedName name=\"ToChart\">Chart!$A$1</definedName>",
				"<definedName name=\"Missing\">Gone!$A$1</definedName>",
				"<definedName name=\"OfChart\" localSheetId=\"0\">Data!$A$1</definedName>",
				"<definedName name=\"OfQuoted\" localSheetId=\"2\">Data!$A$1</definedName>",
				"<definedName name=\"NoSheet\">$A$1:$B$2</definedName>",
				"<definedName name=\"_XLNM.Print_Titles\" localSheetId=\"1\">Data!$1:$1</definedName>",
				"<definedName name=\"OfTwin\">TWIN!$A$1</definedName>",
				"<definedName name=\"OfPlot\">plot!$B$2</definedName>");

		using var workbook = await Workbooks.OpenAsync(package, async);

		Assert.Equal(
			[
				"Lower | - | shown | data!$a$1:$b$2 | Data A1:B2",
				"Quoted | - | shown | 'Q1 & Orders'!$C$3 | Q1 & Orders C3",
				"ToChart | - | shown | Chart!$A$1 | - -",
				"Missing | - | shown | Gone!$A$1 | - -",
				"Chart!OfChart | Chart | shown | Data!$A$1 | Data A1",
				"'Q1 & Orders'!OfQuoted | Q1 & Orders | shown | Data!$A$1 | Data A1",
				"NoSheet | - | shown | $A$1:$B$2 | - -",
				"Data!Print_Titles | Data | shown | Data!$1:$1 | Data 1:1",
				"OfTwin | - | shown | TWIN!$A$1 | Twin A1",
				"OfPlot | - | shown | plot!$B$2 | PLOT B2",
			],
			workbook.DefinedNames.Select(Describe));
		Assert.Same(workbook.Sheets[3], workbook.DefinedNames[8].Sheet);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheCorpussTablesAndNames(bool async)
	{
		foreach (var file in new[] { "excel/typed.xlsx", "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "libreoffice/typed.xlsx", "xlsxwriter/typed.xlsx" })
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(CorpusFiles.PathOf(file)) : ExcelWorkbook.Open(CorpusFiles.PathOf(file));

			Assert.Equal(["Orders Formulas A20:D23 1 0 Item|Qty|Price|Total"], workbook.Tables.Select(Describe));
			Assert.Equal(["Rate | - | shown | 0.5 | - -"], workbook.DefinedNames.Select(Describe));
		}

		// The messy workbooks' tables and names are compared with Excel's answers (StructureAnswersTests).
		foreach (var file in CorpusFiles.Workbooks.Where(f => (!f.Contains("typed") || f.StartsWith("openpyxl/", StringComparison.Ordinal) || f.StartsWith("poi/", StringComparison.Ordinal)) && !f.Contains("messy")))
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(CorpusFiles.PathOf(file)) : ExcelWorkbook.Open(CorpusFiles.PathOf(file));

			Assert.Empty(workbook.Tables);
			Assert.All(workbook.DefinedNames, n => Assert.Null(n.Range));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheDimensionsTheSheetsState(bool async)
	{
		static string Sheet(string beforeData) =>
			TestPackage.Declaration + $"<worksheet xmlns=\"{TestPackage.MainNamespace}\">{beforeData}<sheetData><row r=\"1\"><c r=\"A1\"><v>1</v></c></row></sheetData></worksheet>";

		var package = TestPackage.Workbook(
			new TestSheet("Chart", type: "chartsheet"),
			new TestSheet("Excel", xml: Sheets.Worksheet("")),
			new TestSheet("Empty", xml: Sheet("<dimension ref=\"A1\"/>")),
			new TestSheet("Lower", xml: Sheet("<sheetPr/><dimension ref=\" $c$3:$b$20 \"/>")),
			new TestSheet("None", xml: Sheet("<sheetViews/>")),
			new TestSheet("Invalid", xml: Sheet("<dimension ref=\"A0:B2\"/>")),
			new TestSheet("NoRef", xml: Sheet("<dimension/>")),
			new TestSheet("After", xml: TestPackage.Declaration + $"<worksheet xmlns=\"{TestPackage.MainNamespace}\"><sheetData/><dimension ref=\"A1:B2\"/></worksheet>"),
			new TestSheet("Prefixed", xml: TestPackage.Declaration + $"<x:worksheet xmlns:x=\"{TestPackage.StrictMainNamespace}\"><x:dimension ref=\"A1:XFD1048576\"/><x:sheetData/></x:worksheet>"),
			new TestSheet("NotASheet", xml: TestPackage.Declaration + $"<chartsheet xmlns=\"{TestPackage.MainNamespace}\"><dimension ref=\"A1:B2\"/></chartsheet>"),
			new TestSheet("Broken", xml: TestPackage.Declaration + "<!DOCTYPE worksheet><worksheet/>"));

		using var workbook = await Workbooks.OpenAsync(package, async);

		Assert.Equal(
			["Chart -", "Excel A1:C2", "Empty A1", "Lower B3:C20", "None -", "Invalid -", "NoRef -", "After -", "Prefixed 1:1048576", "NotASheet -", "Broken -"],
			workbook.Sheets.Select(s => $"{s.Name} {s.Dimension?.ToString() ?? "-"}"));

		// A sheet whose start can't be read has no dimension; reading it says why.
		using var reader = await workbook.OpenReaderAsync(async, "Broken");
		Assert.Throws<CsvFormatException>(() => reader.Read());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsADimensionOnceFromTheStartOfItsSheet(bool async)
	{
		// A sheet past the workbook's limit: its dimension is read from its start, which stays within it.
		var rows = string.Concat(Enumerable.Range(1, 40000).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\"><v>{i}</v></c></row>"));
		var package = TestPackage.Workbook(new TestSheet("Big", xml: Sheets.Worksheet(rows)), new TestSheet("Other", xml: Sheets.Worksheet("")))
			.Replace("xl/worksheets/sheet1.xml", "ref=\"A1:C2\"", "ref=\"A1:A40000\"");
		Assert.True(package.Text("xl/worksheets/sheet1.xml").Length > 1_500_000);
		var options = new ExcelReaderOptions { MaxUncompressedSize = 1024 * 1024 };

		var workbook = await Workbooks.OpenAsync(package, async, options);
		Assert.Equal("A1:A40000", workbook.Sheets[0].Dimension?.ToString());

		// Kept after the workbook is disposed; one not read before can't be read after.
		workbook.Dispose();
		Assert.Equal("A1:A40000", workbook.Sheets[0].Dimension?.ToString());
		var e = Assert.Throws<ObjectDisposedException>(() => workbook.Sheets[1].Dimension);
		Assert.Equal("The workbook is disposed.", e.Message.Split('\n')[0].Trim());

		// The sheet itself passes the limit when it is read.
		using var again = await Workbooks.OpenAsync(package, async, options);
		using var reader = await again.OpenReaderAsync(async, "Big");
		Assert.Throws<InvalidDataException>(() =>
		{
			while (reader.Read())
			{
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesSheetsWithoutAPartNoDimension(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet("")), new TestSheet("Gone", xml: Sheets.Worksheet("")))
			.Remove("xl/worksheets/sheet2.xml");

		using var workbook = await Workbooks.OpenAsync(package, async);

		Assert.Equal("A1:C2", workbook.Sheets[0].Dimension?.ToString());
		Assert.Null(workbook.Sheets[1].Dimension);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheLimitsOfTablesAtOpen(bool async)
	{
		var package = TestPackage.Workbook("Data").Table("xl/worksheets/sheet1.xml", TablePartTests.Table("displayName=\"T\" ref=\"A1:A2\"", new string('a', 5000)));
		var options = new ExcelReaderOptions { MaxPartSize = 4096 };

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Workbooks.OpenAsync(package, async, options));
		Assert.StartsWith("xl/tables/table1.xml is larger than ExcelReaderOptions.MaxPartSize (4096 bytes uncompressed)", e.Message);
	}

	/// <summary>Describes a table for comparisons: <c>name sheet range header totals columns</c>.</summary>
	/// <param name="table">The table.</param>
	/// <returns>The description.</returns>
	private static string Describe(ExcelTable table) =>
		$"{table.Name} {table.Sheet.Name} {table.Range} {table.HeaderRowCount} {table.TotalsRowCount} {string.Join("|", table.Columns)}";

	/// <summary>Describes a defined name for comparisons: <c>text | scope | visibility | refers to | sheet range</c>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The description.</returns>
	private static string Describe(ExcelDefinedName name) =>
		$"{name} | {name.Scope?.Name ?? "-"} | {(name.Hidden ? "hidden" : "shown")} | {name.RefersTo} | {name.Sheet?.Name ?? "-"} {name.Range?.ToString() ?? "-"}";
}
