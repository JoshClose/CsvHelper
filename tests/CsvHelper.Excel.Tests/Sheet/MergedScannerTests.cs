using System.Text;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The sheet scanner filling merged cells: fills across, down and both, in column order among a row's cells; top-left
/// cells outside the area, in hidden rows and columns, or without a value; other cells' values dropped; rows the file
/// lacks, filled, before its last row and after it; the area's end; ranges that fill nothing; kinds and formulas; held
/// records; errors' cells; and fills counted toward the uncompressed size to the byte. Each test runs in every
/// <see cref="SheetMode"/>.
/// </summary>
public class MergedScannerTests
{
	/// <summary>A cell whose text is its reference, such as <c>B2</c>.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <returns>The cell's XML.</returns>
	private static string Cell(string reference) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{reference}</t></is></c>";

	/// <summary>A row of cells whose texts are their references.</summary>
	/// <param name="row">The row.</param>
	/// <param name="columns">The columns' letters, such as <c>ABC</c>.</param>
	/// <param name="attributes">The row's other attributes, with a space before each.</param>
	/// <returns>The row's XML.</returns>
	private static string Row(int row, string columns, string attributes = "") =>
		$"<row r=\"{row}\"{attributes}>" + string.Concat(columns.Select(c => Cell($"{c}{row}"))) + "</row>";

	/// <summary>A <c>mergeCells</c> of these ranges, as Excel writes it.</summary>
	/// <param name="refs">The ranges' references.</param>
	/// <returns>The element's XML.</returns>
	internal static string Merges(params string[] refs) =>
		$"<mergeCells count=\"{refs.Length}\">" + string.Concat(refs.Select(r => $"<mergeCell ref=\"{r}\"/>")) + "</mergeCells>";

	/// <summary>A worksheet part as Excel writes one, around these rows, with these merged ranges after them.</summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <param name="refs">The ranges' references.</param>
	/// <returns>The part's XML.</returns>
	internal static string Worksheet(string rows, params string[] refs) => Sheets.Worksheet(rows).Replace("</sheetData>", "</sheetData>" + Merges(refs));

	/// <summary>A worksheet part with this <c>sheetFormatPr</c> and these <c>cols</c>, around these rows, with these merged ranges after them.</summary>
	/// <param name="format">The attributes of <c>sheetFormatPr</c>, as <see cref="Sheets.Worksheet(string, string, string)"/> takes them.</param>
	/// <param name="cols">The content of <c>cols</c>, or empty for none.</param>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <param name="refs">The ranges' references.</param>
	/// <returns>The part's XML.</returns>
	private static string WorksheetWith(string format, string cols, string rows, params string[] refs) =>
		Sheets.Worksheet(format, cols, rows).Replace("</sheetData>", "</sheetData>" + Merges(refs));

	[Fact]
	public async Task FillsEachCellOfARangeWithItsTopLeftCell()
	{
		// Across, down, and a block; the other cells' own values are dropped.
		var xml = Worksheet(Row(1, "ABCD") + Row(2, "AB") + Row(3, "ABDE") + Row(4, "BDE") + Row(5, "AB"), "A1:C1", "A3:A5", "D3:E4");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: A1 | A1 | A1 | D1", "2: A2 | B2 | () | ()", "3: A3 | B3 | () | D3 | D3", "4: A3 | B4 | () | D3 | D3", "5: A3 | B5 | () | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));

			// Without the option, each cell reads what it stores.
			Assert.Equal(
				["1: A1 | B1 | C1 | D1", "2: A2 | B2 | () | ()", "3: A3 | B3 | () | D3 | E3", "4: () | B4 | () | D4 | E4", "5: A5 | B5 | () | ()"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task WritesFillsInColumnOrderAmongTheRowsCells()
	{
		// The top-left cell comes after other cells of its row, and cells follow the range in its rows.
		var xml = Worksheet(Row(2, "ACDE") + Row(3, "BE") + "<row r=\"4\"/>", "C2:D3");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["2: A2 | () | C2 | C2 | E2", "3: () | B3 | C2 | C2 | E3"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));
			Assert.Equal(
				["1: ()", "2: A2 | () | C2 | C2 | E2", "3: () | B3 | C2 | C2 | E3", "4: ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Merged()));
		});
	}

	[Fact]
	public async Task FillsFromTopLeftCellsOutsideTheArea()
	{
		// Left of the area (A2), above it (C1), and a range past its last row; row 7 ends the area.
		var xml = Worksheet(Row(1, "ABCD") + Row(2, "ABCD") + Row(3, "ABCD") + Row(4, "ABCD") + Row(6, "ABCD") + Row(7, "ABCD"), "A2:B3", "C1:C4", "B6:C8");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var reading = CellReading.Merged("B2:D6");
			Assert.Equal(["2: A2 | C1 | D2", "3: A2 | C1 | D3", "4: B4 | C1 | D4", "6: B6 | B6 | D6"], await Sheets.ReadAsync(xml, mode, reading: reading));
			Assert.Equal(
				["2: A2 | C1 | D2", "3: A2 | C1 | D3", "4: B4 | C1 | D4", "5: ()", "6: B6 | B6 | D6"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));

			// With formulas read as text too, as rows above the area are read for their definitions then.
			Assert.Equal(
				["2: A2 | C1 | D2", "3: A2 | C1 | D3", "4: B4 | C1 | D4", "6: B6 | B6 | D6"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("B2:D6", formulas: ExcelFormulas.FormulaText)));
		});
	}

	[Fact]
	public async Task FillsFromHiddenCells()
	{
		// Column B is hidden, and rows 2 and 5: A2 fills down from a hidden row, B3 across from a hidden column, and A5
		// fills row 6, which the file lacks.
		var xml = WorksheetWith(
			"", "<col min=\"2\" max=\"2\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>",
			Row(1, "ABC") + Row(2, "ABC", " hidden=\"1\"") + Row(3, "ABC") + Row(4, "ABC") + Row(5, "ABC", " hidden=\"1\""),
			"A2:A4", "B3:C4", "A5:C6");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: A1 | C1", "3: A2 | B3", "4: A2 | B3", "6: A5 | A5"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged(rows: true, columns: true)));
			Assert.Equal(
				["1: A1 | B1 | C1", "3: A2 | B3 | B3", "4: A2 | B3 | B3", "6: A5 | A5 | A5"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged(rows: true)));
			Assert.Equal(
				["1: A1 | C1", "2: A2 | C2", "3: A2 | B3", "4: A2 | B3", "5: A5 | A5", "6: A5 | A5"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged(columns: true)));
			Assert.Equal(
				["1: A1 | B1 | C1", "2: A2 | B2 | C2", "3: A2 | B3 | B3", "4: A2 | B3 | B3", "5: A5 | A5 | A5", "6: A5 | A5 | A5"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));

			// In an area below the hidden row, and right of the hidden column.
			Assert.Equal(["3: B3", "4: B3", "6: A5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("C3:C6", rows: true, columns: true)));
		});
	}

	[Fact]
	public async Task FillsNothingFromATopLeftCellWithoutAValue()
	{
		// A1 is valueless, A3 isn't in the file, and A4 is an empty shared string: their ranges' cells read as nothing.
		var xml = Worksheet(
			$"<row r=\"1\"><c r=\"A1\" s=\"1\"/>{Cell("B1")}{Cell("C1")}</row>{Row(2, "ABC")}<row r=\"3\">{Cell("B3")}{Cell("C3")}</row>" +
			$"<row r=\"4\"><c r=\"A4\" t=\"s\"><v>0</v></c>{Cell("B4")}</row>",
			"A1:B2", "A3:B3", "A4:B5");
		var strings = Sheets.Strings("");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: () | () | C1", "2: () | () | C2", "3: () | () | C3"], await Sheets.ReadAsync(xml, mode, strings: strings, reading: CellReading.Merged()));
			Assert.Equal(
				["1: () | () | C1", "2: () | () | C2", "3: () | () | C3", "4: ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, strings: strings, reading: CellReading.Merged()));
		});
	}

	[Fact]
	public async Task FillsTheRowsTheFileLacks()
	{
		// A2 fills rows 3 to 5, which the file lacks; A9 fills rows 10 to 12, after the file's last row.
		var xml = Worksheet(Row(2, "AC") + Row(7, "A") + Row(9, "AB"), "A2:B5", "A9:A12");
		var filled = new[] { "2: A2 | A2 | C2", "3: A2 | A2 | ()", "4: A2 | A2 | ()", "5: A2 | A2 | ()", "7: A7 | () | ()", "9: A9 | B9 | ()", "10: A9 | () | ()", "11: A9 | () | ()", "12: A9 | () | ()" };

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(filled, await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));

			// With empty rows kept, the rows between are empty records, and none comes after the last filled row.
			Assert.Equal(
				["1: ()", .. filled.Take(4), "6: ()", filled[4], "8: ()", .. filled.Skip(5)],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Merged()));

			// Up to the area's last row.
			Assert.Equal(filled.Take(filled.Length - 1), await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("A1:C11")));
			Assert.Equal(["3: A2", "4: A2"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("B3:C4")));

			// With empty rows kept, nothing after the file's last row in the area but filled rows.
			Assert.Equal(["1: ()", .. filled.Take(4), "6: ()", filled[4]], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Merged("A1:C8")));
		});
	}

	[Fact]
	public async Task FillsNoRowThatTheSheetHides()
	{
		// The sheet hides the rows it lacks: filled or not, none of them is a record when hidden rows are skipped.
		var xml = WorksheetWith("zeroHeight=\"1\"", "", Row(2, "AC") + Row(4, "A") + Row(5, "A", " hidden=\"1\""), "A2:B3", "A4:A8");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var reading = CellReading.Merged(rows: true);
			Assert.Equal(["2: A2 | A2 | C2", "4: A4 | () | ()"], await Sheets.ReadAsync(xml, mode, reading: reading));
			Assert.Equal(["2: A2 | A2 | C2", "4: A4 | () | ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));

			// Read as Excel stores them, the missing rows are filled.
			Assert.Equal(
				["2: A2 | A2 | C2", "3: A2 | A2 | ()", "4: A4 | () | ()", "5: A4 | () | ()", "6: A4 | () | ()", "7: A4 | () | ()", "8: A4 | () | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));
		});
	}

	[Fact]
	public async Task EndsTheAreaAfterTheRowsItFills()
	{
		// A3 fills the rest of the area, and row 20, below it, ends the reading.
		var xml = Worksheet(Row(1, "A") + Row(3, "A") + Row(20, "A") + Row(21, "A"), "A3:A30");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: A1", "3: A3", "4: A3", "5: A3", "6: A3", "7: A3", "8: A3", "9: A3", "10: A3"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("A1:A10")));

			// With every row a fill of its own.
			var records = await Sheets.ReadRecordsAsync(xml, new SheetMode(mode.Scan, 1), reading: CellReading.Merged("A1:A10"));
			Assert.Equal([1L, 3, 4, 5, 6, 7, 8, 9, 10], records.Select(r => r.Row));
		});
	}

	[Fact]
	public async Task LeavesCellsThatNoRangeFills()
	{
		// A one-cell range; a range outside the area; and a range whose only columns in the area are hidden.
		var xml = WorksheetWith("", "<col min=\"3\" max=\"3\" width=\"0\"/>", Row(1, "ABCDE") + Row(2, "ABCDE"), "B1", "E1:E2", "C1:C2");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: A1 | B1 | D1", "2: A2 | B2 | D2"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("A1:D2", columns: true)));
			Assert.Equal(["1: A1 | B1 | C1 | D1", "2: A2 | B2 | C1 | D2"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("A1:D2")));
		});
	}

	[Fact]
	public async Task FillsWithTheTopLeftCellsKindAndFormula()
	{
		var xml = Worksheet(
			"<row r=\"1\"><c r=\"A1\"><v>1.5</v></c><c r=\"C1\" t=\"b\"><v>true</v></c><c r=\"E1\" t=\"e\"><v>#N/A</v></c><c r=\"G1\" t=\"s\"><v>0</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><f>SUM(B1:B2)</f><v>3</v></c></row>" +
			"<row r=\"4\"><c r=\"A4\"><v>1</v></c><c r=\"B4\"><f t=\"shared\" ref=\"B4:C5\" si=\"0\">A4*2</f><v>2</v></c></row>" +
			"<row r=\"5\"><c r=\"C5\"><f t=\"shared\" si=\"0\"/><v>4</v></c></row>",
			"A1:B1", "C1:D1", "E1:F1", "G1:H1", "A3:B3", "A4:B4");
		var strings = Sheets.Strings("text");

		// Rows are padded to the first record's 8 fields.
		const string Padding = " | () | () | () | () | () | ()";

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: number:1.5 | number:1.5 | bool:1 | bool:1 | error:#N/A | error:#N/A | text | text", "3: number:3 | number:3" + Padding, "4: number:1 | number:1" + Padding, "5: () | () | number:4 | () | () | () | () | ()"],
				await Sheets.ReadAsync(xml, mode, strings: strings, reading: CellReading.Merged()));

			// As text, the top-left cell's formula fills; B4, covered by A4's range, still defines the group that C5 reads.
			Assert.Equal(
				["1: number:1.5 | number:1.5 | bool:1 | bool:1 | error:#N/A | error:#N/A | text | text", "3: =SUM(B1:B2) | =SUM(B1:B2)" + Padding, "4: number:1 | number:1" + Padding, "5: () | () | =B5*2 | () | () | () | () | ()"],
				await Sheets.ReadAsync(xml, mode, strings: strings, reading: CellReading.Merged(formulas: ExcelFormulas.FormulaText)));
		});
	}

	[Fact]
	public async Task FillsWholeColumnsAndRows()
	{
		var rows = Row(1, "AB") + Row(3, "AB") + Row(6, "ABC");
		var columns = Worksheet(rows, "A:A");
		var wholeRows = Worksheet(rows, "6:6");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: A1 | B1", "2: A1 | ()", "3: A1 | B3", "4: A1 | ()"], await Sheets.ReadAsync(columns, mode, reading: CellReading.Merged("A1:B4")));
			Assert.Equal(["5: ()", "6: A6 | A6 | A6"], await Sheets.ReadAsync(wholeRows, mode, skipEmptyRows: false, reading: CellReading.Merged("B5:D7")));
		});
	}

	[Fact]
	public async Task FillsRangesAcrossTheWordsOfTheFieldSets()
	{
		// A range over 78 fields with a cell it covers in its second word, one starting at field 64, and one at the last two columns.
		var xml = Worksheet(
			$"<row r=\"1\">{Cell("A1")}{Cell("BX1")}{Cell("CA1")}</row><row r=\"2\">{Cell("BM2")}{Cell("XFC2")}</row><row r=\"3\">{Cell("BN3")}</row>",
			"A1:BZ1", "BM2:BN3", "XFC2:XFD3");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var records = await Sheets.ReadRecordsAsync(xml, mode, reading: CellReading.Merged());
			Assert.Equal(3, records.Count);
			Assert.Equal(Enumerable.Repeat("A1", 78).Append("CA1"), records[0].Fields.Select(f => f.Text));
			foreach (var record in records.Skip(1))
			{
				Assert.Equal(CellReference.MaxColumns, record.Fields.Count);
				Assert.Equal(["BM2", "BM2"], record.Fields.Skip(64).Take(2).Select(f => f.Text));
				Assert.Equal(["XFC2", "XFC2"], record.Fields.Skip(CellReference.MaxColumns - 2).Select(f => f.Text));
				Assert.All(record.Fields.Take(64).Concat(record.Fields.Skip(66).Take(CellReference.MaxColumns - 68)), f => Assert.Equal(SheetCellKind.Empty, f.Kind));
			}
		});
	}

	[Fact]
	public async Task FillsAfterAHeldRecord()
	{
		var xml = Worksheet(Row(1, "AB") + Row(2, "AB") + Row(3, "B"), "A2:A4");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var held = HeldRecord.Names(["x", "y"], 0, 2, 1);
			Assert.Equal(["1: x | y", "2: A2 | B2", "3: A2 | B3", "4: A2 | ()"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged("A2:B5", held: held)));
		});
	}

	[Fact]
	public async Task NamesTheCellsOfErrorsAfterFills()
	{
		var badIndex = Worksheet($"<row r=\"1\">{Cell("A1")}<c r=\"C1\" t=\"s\"><v>9</v></c></row>", "A1:B2");
		var outOfOrder = Worksheet($"<row r=\"1\">{Cell("A1")}</row><row r=\"2\">{Cell("B2")}{Cell("A2")}</row>", "A1:B2");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(badIndex, mode, reading: CellReading.Merged()));
			Assert.Equal("Data!C1", e.Location);

			// Cells a range covers are still checked for their order.
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(outOfOrder, mode, reading: CellReading.Merged()));
			Assert.Equal("Data!A2", e.Location);
			Assert.Contains("it is not after the cell before it, B2", e.Message);
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsFillsTowardTheUncompressedSize(bool async)
	{
		// Each fill but the top-left cell's own counts its bytes, and each filled missing row 1; the second read of the part counts too.
		var abcde = Worksheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>abcde</t></is></c></row>", "A1:D3");
		await AssertChargesAsync(abcde, (3 * 5) + (2 * (1 + (4 * 5))), ["1: abcde | abcde | abcde | abcde", "2: abcde | abcde | abcde | abcde", "3: abcde | abcde | abcde | abcde"], async);

		// An empty fill counts 1.
		var blank = Worksheet("<row r=\"1\"><c r=\"A1\" t=\"str\"><v>&#32;</v></c></row>", "A1:C1");
		await AssertChargesAsync(blank, 2, ["1: () | () | ()"], async);

		// A shared string counts its bytes at each fill; its own cell counts as it does without fills.
		var shared = Worksheet("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>", "A1:C1");
		await AssertChargesAsync(shared, 2 * 10, ["1: 0123456789 | 0123456789 | 0123456789"], async, sharedStrings: true);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsFillsAtTheUncompressedSize(bool async)
	{
		var xml = Worksheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>abcde</t></is></c></row>", "A1:D3");
		var part = Encoding.UTF8.GetByteCount(xml);
		var package = TestPackage.Workbook(new TestSheet("Data", xml: xml));
		var charges = (3 * 5) + (2 * (1 + (4 * 5)));

		var (records, total) = await Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, (2 * part) + charges), fillMergedCells: true);
		Assert.Equal(3, records.Count);
		Assert.Equal((2 * part) + charges, total);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, (2 * part) + charges - 1), fillMergedCells: true));
		Assert.Contains("ExcelReaderOptions.MaxUncompressedSize", e.Message);
	}

	/// <summary>
	/// Reads a one-sheet package with and without fills, and checks the fills' records and that they count exactly
	/// <paramref name="charges"/> more than the plain read, besides the second read of the part.
	/// </summary>
	/// <param name="xml">The sheet's part.</param>
	/// <param name="charges">The bytes the fills count.</param>
	/// <param name="expected">The records with fills.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="sharedStrings">Whether the package has the shared string <c>0123456789</c>, loaded first.</param>
	/// <returns>A task that completes when the checks pass.</returns>
	private static async Task AssertChargesAsync(string xml, long charges, string[] expected, bool async, bool sharedStrings = false)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: xml));
		if (sharedStrings)
		{
			package = package.SharedStrings("<t>0123456789</t>");
		}

		var limits = new PartLimits(1 << 20, 1 << 20);
		var (plain, plainTotal) = await Sheets.ReadPackageAsync(package, async, limits, sharedStrings: sharedStrings);
		var (filled, filledTotal) = await Sheets.ReadPackageAsync(package, async, limits, sharedStrings: sharedStrings, fillMergedCells: true);

		Assert.Equal(expected, filled.Select(r => r.ToString()));
		Assert.Single(plain);
		Assert.Equal(charges, filledTotal - plainTotal - Encoding.UTF8.GetByteCount(xml));
	}
}
