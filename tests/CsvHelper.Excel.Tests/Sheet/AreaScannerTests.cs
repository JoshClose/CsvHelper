using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The sheet scanner reading an area: its rows and columns, fields from its first column, missing rows, the early stop, a
/// held first record, and shared formulas defined outside it. Each test runs in every <see cref="SheetMode"/>.
/// </summary>
public class AreaScannerTests
{
	/// <summary>A cell whose text is its reference, such as <c>B2</c>.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <returns>The cell's XML.</returns>
	private static string Cell(string reference) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{reference}</t></is></c>";

	/// <summary>A row of cells whose texts are their references.</summary>
	/// <param name="row">The row.</param>
	/// <param name="columns">The columns' letters, such as <c>ABC</c>.</param>
	/// <returns>The row's XML.</returns>
	private static string Row(int row, string columns) => $"<row r=\"{row}\">" + string.Concat(columns.Select(c => Cell($"{c}{row}"))) + "</row>";

	[Fact]
	public async Task ReadsTheAreasRowsAndColumns()
	{
		var xml = Sheets.Worksheet(string.Concat(Enumerable.Range(1, 6).Select(r => Row(r, "ABCDE"))));

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["2: B2 | C2 | D2", "3: B3 | C3 | D3", "4: B4 | C4 | D4"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("B2:D4")));
			Assert.Equal(["5: A5 | B5 | C5 | D5 | E5", "6: A6 | B6 | C6 | D6 | E6"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("5:9")));
			Assert.Equal([.. Enumerable.Range(1, 6).Select(r => $"{r}: E{r}")], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("E:XFD")));
			Assert.Empty(await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("F1:G6")));
			Assert.Empty(await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A7:E9")));
		});
	}

	[Fact]
	public async Task PlacesFieldsFromTheAreasFirstColumn()
	{
		var xml = Sheets.Worksheet(Row(1, "BCDE") + Row(2, "D") + Row(3, "AF") + Row(4, "ABCDEF") + Row(5, "C"));

		await Sheets.InEveryModeAsync(async mode =>
		{
			// Gap fields come from the area's first column, rows are padded to the first record's width, and a row whose only
			// cells are outside the area has no value.
			Assert.Equal(
				["1: B1 | C1 | D1 | E1", "2: () | () | D2 | ()", "4: B4 | C4 | D4 | E4", "5: () | C5 | () | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("B1:E5")));
			Assert.Equal(
				["1: B1 | C1 | D1 | E1", "2: () | () | D2 | ()", "3: ()", "4: B4 | C4 | D4 | E4", "5: () | C5 | () | ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("B1:E5")));

			// The first record's width is at most the area's.
			Assert.Equal(["4: C4 | D4", "5: C5 | ()"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("C4:D5")));
		});
	}

	[Fact]
	public async Task WritesMissingRowsOnlyInTheArea()
	{
		var xml = Sheets.Worksheet(Row(1, "A") + Row(2, "A") + Row(4, "A") + Row(6, "A") + Row(8, "A"));

		await Sheets.InEveryModeAsync(async mode =>
		{
			// Record n is the area's row n, up to the last row the file has in the area: no rows above, none after row 6.
			var expected = new[] { "3: ()", "4: A4", "5: ()", "6: A6" };
			Assert.Equal(expected, await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("A3:B6")));
			Assert.Equal(expected, await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("A3:B7")));
			Assert.Equal(["4: A4", "6: A6"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:B7")));
			Assert.Equal(["8: A8"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("A8:A1048576")));
		});
	}

	[Fact]
	public async Task NumbersRowsWithoutReferences()
	{
		// Rows numbered by their first cell's reference, or by the row before's.
		var xml = Sheets.Worksheet(
			"<row><c r=\"A1\" t=\"inlineStr\"><is><t>first</t></is></c></row>" +
			"<row><c r=\"A3\" t=\"inlineStr\"><is><t>third</t></is></c></row>" +
			"<row/>" +
			"<row><c t=\"inlineStr\"><is><t>fifth</t></is></c><c t=\"inlineStr\"><is><t>fifth B</t></is></c></row>" +
			"<row></row>" +
			"<row><c t=\"inlineStr\"><is><t>seventh</t></is></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["3: third", "5: fifth"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A2:A5")));
			Assert.Equal(["3: third", "4: ()", "5: fifth", "6: ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("A3:A6")));
			Assert.Equal(["5: fifth B", "6: ()", "7: ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("B5:B7")));
		});
	}

	[Fact]
	public async Task ReadsNothingAfterTheFirstRowBelowTheArea()
	{
		// Markup the tokenizer rejects as soon as it reaches it, after the row below the area.
		var xml = Sheets.Worksheet(Row(1, "A") + Row(2, "A") + Row(5, "A") + "<!not XML>" + Row(6, "A"));

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: A1", "2: A2"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A1:A4")));
			await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A1:A5")));
		});

		// And it isn't inflated: a stream of one byte per read is read no further than the first refill after that row's tag.
		var tail = Sheets.Worksheet(Row(1, "A") + Row(5, "A") + string.Concat(Enumerable.Range(6, 1000).Select(r => Row(r, "AB"))));
		var bytes = Encoding.UTF8.GetBytes(tail);
		var stream = new TrickleStream(bytes);
		var reading = CellReading.Area("A1:A4");
		using (var sheet = new SheetScanner(new XmlScanner(new XmlWindow(stream, Sheets.PartName, 1 << 20, 16)), "Data", SharedStrings.Empty, StyleTable.Empty, true, null, layout: reading.Layout))
		{
			var captured = new Captured(int.MaxValue);
			await Sheets.FillAllAsync(sheet, captured, false);
			Assert.Equal(["1: A1"], captured.Records.Select(r => r.ToString()));
		}

		Assert.InRange(stream.Delivered, 0, tail.IndexOf("<row r=\"5\">") + "<row r=\"5\">".Length + 16);
	}

	[Fact]
	public async Task ReadsAnAreaWithoutRows()
	{
		var xml = Sheets.Worksheet(Row(1, "AB") + Row(2, "AB"));
		var empty = new CellReading(layout: new SheetLayout(2, 1, 0, 1));
		var held = new CellReading(layout: new SheetLayout(2, 1, 0, 1), held: HeldRecord.Names(["X", "Y"], 0, 2, 1));

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Empty(await Sheets.ReadAsync(xml, mode, reading: empty));
			Assert.Empty(await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: empty));
			Assert.Equal(["1: X | Y"], await Sheets.ReadAsync(xml, mode, reading: held));
		});
	}

	[Fact]
	public async Task WritesTheHeldRecordFirst()
	{
		var xml = Sheets.Worksheet(Row(1, "ABCD") + Row(2, "AB") + Row(4, "C"));
		var shown = CellReading.Area("A2:D4", held: HeldRecord.Names(["Id", "", "Name"], 0, 4, 1));
		var hidden = CellReading.Area("A2:B4", held: HeldRecord.Names(["X", "Y", "Z"], 0, 2, 0));

		await Sheets.InEveryModeAsync(async mode =>
		{
			// The names head the area's columns, an empty field for a column without one, and set the width rows are padded to.
			Assert.Equal(["1: Id | () | Name | ()", "2: A2 | B2 | () | ()", "4: () | () | C4 | ()"], await Sheets.ReadAsync(xml, mode, reading: shown));
			Assert.Equal(["1: Id | () | Name | ()", "2: A2 | B2 | () | ()", "3: ()", "4: () | () | C4 | ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: shown));

			// A header the sheet doesn't show has no row; names past the area's columns are left out.
			Assert.Equal(["0: X | Y", "2: A2 | B2"], await Sheets.ReadAsync(xml, mode, reading: hidden));
		});
	}

	[Fact]
	public async Task LeavesCellsOutsideTheAreaUndecoded()
	{
		// An undefined entity throws only in a cell that is decoded.
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&bad;</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>b</t></is></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"str\"><v>&bad;</v></c><c r=\"B2\" t=\"str\"><v>c</v></c></row>" +
			"<row r=\"3\"><c r=\"B3\" t=\"str\"><v>&bad;</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: b", "2: c"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("B1:B2")));
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Area("B1:B3")));
			Assert.Equal("Data!B3", e.Location);
		});
	}

	[Fact]
	public async Task NamesTheCellsOfErrorsInTheArea()
	{
		var strings = Sheets.Strings("a");
		var index = Sheets.Worksheet("<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"C2\" t=\"s\"><v>5</v></c></row>");
		var order = Sheets.Worksheet("<row r=\"2\"><c r=\"F2\"><v>1</v></c><c r=\"E2\"><v>2</v></c></row>");
		var rows = Sheets.Worksheet("<row r=\"3\"><c r=\"A3\"><v>1</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(index, mode, strings: strings, reading: CellReading.Area("B1:C3")));
			Assert.Equal("Data!C2", e.Location);

			// Cells and rows are checked for order outside the area too.
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(order, mode, reading: CellReading.Area("A1:B3")));
			Assert.Equal("Data!E2", e.Location);
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(rows, mode, reading: CellReading.Area("A5:B6")));
			Assert.StartsWith("Row Data!2:2 can't be read", e.Message);
		});
	}

	[Fact]
	public async Task ReadsSharedFormulasDefinedOutsideTheArea()
	{
		// Groups defined above the area and left of it: their cells in the area read the formula moved to them.
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A4\" si=\"0\">C1*2</f><v>2</v></c><c r=\"C1\"><v>1</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/><v>4</v></c><c r=\"C2\"><v>2</v></c><c r=\"D2\"><f t=\"shared\" ref=\"D2:E3\" si=\"1\">$C2+1</f><v>3</v></c><c r=\"E2\"><f t=\"shared\" si=\"1\"/><v>3</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\"/><v>6</v></c><c r=\"C3\"><v>3</v></c><c r=\"D3\"><f t=\"shared\" si=\"1\"/><v>4</v></c><c r=\"E3\"><f t=\"shared\" si=\"1\"/><v>4</v></c></row>" +
			"<row r=\"4\"><c r=\"A4\"><f t=\"shared\" si=\"0\"/><v>8</v></c><c r=\"C4\"><v>4</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["3: =C3*2 | () | number:3", "4: =C4*2 | () | number:4"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:C4", ExcelFormulas.FormulaText)));
			Assert.Equal(
				["2: =$C2+1", "3: =$C3+1"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("E2:E3", ExcelFormulas.FormulaText)));
			Assert.Equal(["3: number:6 | () | number:3", "4: number:8 | () | number:4"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:C4")));
		});
	}
}
