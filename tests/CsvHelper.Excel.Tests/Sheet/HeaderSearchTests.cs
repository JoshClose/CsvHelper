namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The header search (<see cref="HeaderSearch"/>) and the read after it, as opening a reader runs them: the row it finds
/// or is given, the columns it clips to, the rows it tries and the messages when none will do, with areas, hidden rows
/// and columns, merged cells, typed cells, formulas and the reader's options. Each test runs in every
/// <see cref="SheetMode"/>.
/// </summary>
public class HeaderSearchTests
{
	/// <summary>A report: a title and a note above the header at row 4 (row 3 missing), and a note beside the data in <c>F5</c>.</summary>
	private static readonly string Report =
		$"<row r=\"1\">{T("A1", "Orders by region")}</row>" +
		$"<row r=\"2\">{T("A2", "Printed today")}</row>" +
		$"<row r=\"4\">{T("B4", "Name")}{T("C4", "Quantity")}{T("D4", "Price")}</row>" +
		$"<row r=\"5\">{T("B5", "apple")}{N("C5", "3")}{N("D5", "1.5")}{T("F5", "note")}</row>" +
		$"<row r=\"6\">{T("B6", "pear")}{N("C6", "10")}{N("D6", "0.25")}</row>";

	/// <summary>The report's records from its header row.</summary>
	private static readonly string[] ReportRecords = ["4: Name | Quantity | Price", "5: apple | number:3 | number:1.5", "6: pear | number:10 | number:0.25"];

	[Fact]
	public Task FindsTheFirstRowWithTheNames() => Sheets.InEveryModeAsync(async mode =>
	{
		var xml = Sheets.Worksheet(Report);
		Assert.Equal(ReportRecords, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name", "Price"])));

		// Matched as TryGetFieldIndex matches them: ignoring case and the white space around them.
		Assert.Equal(ReportRecords, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["name", " PRICE "])));

		// The same row, given.
		Assert.Equal(ReportRecords, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(4)));

		// No names: the first row with a value, whose one column is all that is read.
		Assert.Equal(["1: Orders by region", "2: Printed today"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find([])));
		Assert.Equal(["2: Printed today"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(2)));

		// In a range: from its first row, in its columns.
		Assert.Equal(ReportRecords, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"]), reading: CellReading.Area("B2:F200")));
		Assert.Equal(["4: Name | Quantity", "5: apple | number:3"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"]), reading: CellReading.Area("A3:C5")));
	});

	[Fact]
	public Task ClipsTheColumnsToTheHeaders() => Sheets.InEveryModeAsync(async mode =>
	{
		// A gap in the header, notes either side of the data, and rows the file lacks.
		var xml = Sheets.Worksheet(
			$"<row r=\"3\">{T("B3", "Name")}{T("D3", "Price")}</row>" +
			$"<row r=\"4\">{T("A4", "left note")}{T("B4", "apple")}{N("C4", "3")}{N("D4", "1.5")}{T("E4", "right note")}</row>" +
			$"<row r=\"6\">{T("A6", "only a note")}</row>" +
			$"<row r=\"7\">{N("D7", "2")}</row>");
		var header = ExcelHeaderRow.Find(["Name", "Price"]);

		Assert.Equal(["3: Name | () | Price", "4: apple | number:3 | number:1.5", "7: () | () | number:2"], await Sheets.ReadAfterHeaderAsync(xml, mode, header));

		// With empty rows kept, the rows after the header, and none above it.
		Assert.Equal(
			["3: Name | () | Price", "4: apple | number:3 | number:1.5", "5: ()", "6: ()", "7: () | () | number:2"],
			await Sheets.ReadAfterHeaderAsync(xml, mode, header, skipEmptyRows: false));
	});

	[Fact]
	public Task TriesTheFirstRowsByNumber() => Sheets.InEveryModeAsync(async mode =>
	{
		var xml = Sheets.Worksheet(Report);

		// Row 3, which the file lacks, counts: the header at row 4 is the fourth row.
		var e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"], 3)));
		Assert.Equal("No row of Data!1:3 has the columns 'Name'. The closest, row 1, lacks 'Name'.", e.Message);
		Assert.Equal(ReportRecords, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"], 4)));

		// The closest row is the first that lacks the fewest columns; in a range, its rows and columns are named.
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name", "Total", "Price", "Tax"])));
		Assert.Equal("No row of Data!1:20 has the columns 'Name', 'Total', 'Price', 'Tax'. The closest, row 4, lacks 'Total', 'Tax'.", e.Message);
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name", "Total"]), reading: CellReading.Area("B2:F200")));
		Assert.Equal("No row of Data!B2:F21 has the columns 'Name', 'Total'. The closest, row 4, lacks 'Total'.", e.Message);
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"]), reading: CellReading.Area("B5:F6")));
		Assert.Equal("No row of Data!B5:F6 has the columns 'Name'. The closest, row 5, lacks 'Name'.", e.Message);
	});

	[Fact]
	public Task FailsWhenNoRowHasAValue() => Sheets.InEveryModeAsync(async mode =>
	{
		var xml = Sheets.Worksheet($"<row r=\"3\"><c r=\"A3\"/><c r=\"B3\" t=\"inlineStr\"><is><t></t></is></c></row><row r=\"30\">{T("A30", "Name")}</row>");

		var e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"])));
		Assert.Equal("No row of Data!1:20 has the columns 'Name': none of them has a value.", e.Message);
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find([])));
		Assert.Equal("No row of Data!1:20 has a value.", e.Message);

		var format = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(3)));
		Assert.Equal("The header row, Data!3:3, has no value.", format.Message);
		Assert.Equal("Data!3:3", format.Location);
		Assert.Equal(3, format.LineNumber);
		format = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAfterHeaderAsync(Sheets.Worksheet(""), mode, ExcelHeaderRow.At(1048576), sheetName: "Q1 Orders"));
		Assert.Equal("The header row, 'Q1 Orders'!1048576:1048576, has no value.", format.Message);

		// A cell whose value is a space by reference is an empty field: its row is a record, but without a value.
		xml = Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\"><v>&#32;</v></c></row><row r=\"2\">{T("B2", "Name")}</row><row r=\"3\">{T("B3", "apple")}</row>");
		Assert.Equal(["2: Name", "3: apple"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find([])));
		format = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(1)));
		Assert.Equal("The header row, Data!1:1, has no value.", format.Message);
	});

	[Fact]
	public void RefusesARowGivenOutsideTheArea()
	{
		var area = SheetLayout.Of(ExcelRange.Parse("B2:F200"));
		foreach (var row in new[] { 1, 201 })
		{
			var e = Assert.Throws<ArgumentOutOfRangeException>(() => HeaderSearch.SearchArea(ExcelHeaderRow.At(row), area));
			Assert.StartsWith($"The header row, {row}, is not in the range B2:F200.", e.Message);
			Assert.Equal("options", e.ParamName);
		}

		// A row given is searched alone, in the area's columns; a search tries the area's first rows.
		var given = HeaderSearch.SearchArea(ExcelHeaderRow.At(200), area);
		Assert.Equal((200L, 200L, 1, 5), (given.FirstRow, given.LastRow, given.FirstColumn, given.LastColumn));
		var searched = HeaderSearch.SearchArea(ExcelHeaderRow.Find([], 500), area);
		Assert.Equal((2L, 200L, 1, 5), (searched.FirstRow, searched.LastRow, searched.FirstColumn, searched.LastColumn));
		searched = HeaderSearch.SearchArea(ExcelHeaderRow.Find([], 1048576), SheetLayout.Sheet);
		Assert.Equal((1L, 1048576L), (searched.FirstRow, searched.LastRow));
		searched = HeaderSearch.SearchArea(ExcelHeaderRow.Find([], 1048576), SheetLayout.Of(ExcelRange.Parse("A1048576")));
		Assert.Equal((1048576L, 1048576L), (searched.FirstRow, searched.LastRow));
	}

	[Fact]
	public Task SkipsHiddenRowsAndColumns() => Sheets.InEveryModeAsync(async mode =>
	{
		// Column C hidden, and a hidden row of other names above the header.
		var xml = Sheets.Worksheet(
			"",
			"<col min=\"3\" max=\"3\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>",
			$"<row r=\"1\">{T("A1", "Report")}</row>" +
			$"<row r=\"2\" hidden=\"1\">{T("B2", "name")}{T("C2", "qty")}{T("D2", "price")}</row>" +
			$"<row r=\"4\">{T("B4", "Name")}{T("C4", "Quantity")}{T("D4", "Price")}</row>" +
			$"<row r=\"5\">{T("B5", "apple")}{N("C5", "3")}{N("D5", "1.5")}</row>");
		var header = ExcelHeaderRow.Find(["Name", "Price"]);

		Assert.Equal(["4: Name | Price", "5: apple | number:1.5"], await Sheets.ReadAfterHeaderAsync(xml, mode, header, reading: CellReading.Hidden(true, true)));

		// Shown, the hidden row comes first.
		Assert.Equal(["2: name | price", "4: Name | Price", "5: apple | number:1.5"], await Sheets.ReadAfterHeaderAsync(xml, mode, header, reading: CellReading.Hidden(false, true)));
		Assert.Equal(
			["2: name | qty | price", "4: Name | Quantity | Price", "5: apple | number:3 | number:1.5"],
			await Sheets.ReadAfterHeaderAsync(xml, mode, header, reading: CellReading.Default));

		// A row given is the header even when hidden.
		Assert.Equal(["2: name | price", "4: Name | Price", "5: apple | number:1.5"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(2), reading: CellReading.Hidden(true, true)));
		Assert.Equal(
			["2: name | price", "3: ()", "4: Name | Price", "5: apple | number:1.5"],
			await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.At(2), skipEmptyRows: false, reading: CellReading.Hidden(true, true)));

		// The header's first column hidden: its first shown one starts the fields.
		var first = xml.Replace("min=\"3\" max=\"3\"", "min=\"2\" max=\"2\"");
		Assert.Equal(["4: Quantity | Price", "5: number:3 | number:1.5"], await Sheets.ReadAfterHeaderAsync(first, mode, ExcelHeaderRow.Find(["Quantity"]), reading: CellReading.Hidden(true, true)));
	});

	[Fact]
	public Task ReadsMergedCellsFilled() => Sheets.InEveryModeAsync(async mode =>
	{
		// A title merged across, "Region" merged down into the header row, and a region merged down the data.
		var xml = MergedScannerTests.Worksheet(
			$"<row r=\"1\">{T("A1", "Orders")}</row>" +
			$"<row r=\"2\">{T("A2", "Region")}</row>" +
			$"<row r=\"3\">{T("B3", "Name")}</row>" +
			$"<row r=\"4\">{T("A4", "North")}{T("B4", "apple")}</row>" +
			$"<row r=\"5\">{T("B5", "pear")}</row>",
			"A1:C1", "A2:A3", "A4:A5");
		var header = ExcelHeaderRow.Find(["Region", "Name"]);

		Assert.Equal(["3: Region | Name", "4: North | apple", "5: North | pear"], await Sheets.ReadAfterHeaderAsync(xml, mode, header, reading: CellReading.Merged()));
		Assert.Equal(
			["1: Orders | Orders | Orders", "2: Region | () | ()", "3: Region | Name | ()", "4: North | apple | ()", "5: North | pear | ()"],
			await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find([]), reading: CellReading.Merged()));

		// Not filled, no row has both names.
		var e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, header));
		Assert.Equal("No row of Data!1:20 has the columns 'Region', 'Name'. The closest, row 2, lacks 'Name'.", e.Message);
	});

	[Fact]
	public Task NamesColumnsByTheTextsOfTypedCells() => Sheets.InEveryModeAsync(async mode =>
	{
		var xml = Sheets.Worksheet(
			$"<row r=\"1\">{T("A1", "Year")}</row>" +
			$"<row r=\"2\">{N("A2", "2024")}<c r=\"B2\" t=\"b\"><v>1</v></c>{T("C2", "Name")}<c r=\"D2\" t=\"e\"><v>#N/A</v></c></row>" +
			$"<row r=\"3\">{N("A3", "5")}<c r=\"B3\" t=\"b\"><v>0</v></c>{T("C3", "apple")}</row>");

		// The held header keeps its kinds, as the reader's own header would have them.
		string[] expected = ["2: number:2024 | bool:1 | Name | error:#N/A", "3: number:5 | bool:0 | apple | ()"];
		Assert.Equal(expected, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["2024", "TRUE", "#N/A"])));
		Assert.Equal(expected, await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"]), reading: new CellReading(errorCells: ExcelErrorCells.Error)));

		// Errors read as null are no values, so no fields.
		Assert.Equal(["2: number:2024 | bool:1 | Name", "3: number:5 | bool:0 | apple"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"]), reading: new CellReading(errorCells: ExcelErrorCells.Null)));
	});

	[Fact]
	public Task ReadsFormulasDefinedAboveTheHeader() => Sheets.InEveryModeAsync(async mode =>
	{
		// The shared formula is defined in row 1, above the header; its follower is in row 3, below it.
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A3\" si=\"0\">B1*2</f><v>2</v></c><c r=\"B1\"><v>1</v></c></row>" +
			$"<row r=\"2\">{T("A2", "Double")}{T("B2", "Value")}</row>" +
			"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\"/><v>6</v></c><c r=\"B3\"><v>3</v></c></row>");

		Assert.Equal(["2: Double | Value", "3: =B3*2 | number:3"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Value"]), reading: CellReading.FormulaText));
		Assert.Equal(["2: Double | Value", "3: number:6 | number:3"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Value"])));
	});

	[Fact]
	public Task MatchesNamesWithTheReadersOptions() => Sheets.InEveryModeAsync(async mode =>
	{
		var xml = Sheets.Worksheet($"<row r=\"1\">{T("A1", "Unit Price")}</row><row r=\"2\">{T("A2", "Unit-Price")}</row><row r=\"3\">{N("A3", "1")}</row>");
		var csv = new CsvReaderOptions { NormalizeHeaderName = n => n.Replace("-", " ") };

		Assert.Equal(["1: Unit Price", "2: Unit-Price", "3: number:1"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Unit-Price"]), csv: csv));
		Assert.Equal(["2: Unit-Price", "3: number:1"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Unit-Price"])));
	});

	[Fact]
	public Task ReadsNoRowAfterThoseTried() => Sheets.InEveryModeAsync(async mode =>
	{
		// Row 21's value can't be read; the search tries rows 1 to 20, so it never reads it.
		var xml = Sheets.Worksheet($"<row r=\"1\">{T("A1", "Name")}</row><row r=\"21\"><c r=\"A21\"><v>&bogus;</v></c></row>");

		var e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Total"])));
		Assert.Equal("No row of Data!1:20 has the columns 'Total'. The closest, row 1, lacks 'Total'.", e.Message);

		// The read after the header does.
		await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Name"])));

		// A million rows the file lacks: the search ends at the first row after those tried.
		xml = Sheets.Worksheet($"<row r=\"1\">{T("A1", "Name")}</row><row r=\"1048576\">{T("A1048576", "Total")}</row>");
		e = await Assert.ThrowsAsync<KeyNotFoundException>(() => Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Total"]), skipEmptyRows: false));
		Assert.Equal("No row of Data!1:20 has the columns 'Total'. The closest, row 1, lacks 'Total'.", e.Message);
		Assert.Equal(["1048576: Total"], await Sheets.ReadAfterHeaderAsync(xml, mode, ExcelHeaderRow.Find(["Total"], 1048576)));
	});

	/// <summary>An inline string cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string T(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";

	/// <summary>A number cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="number">The number's text.</param>
	/// <returns>The cell's XML.</returns>
	private static string N(string reference, string number) => $"<c r=\"{reference}\"><v>{number}</v></c>";
}
