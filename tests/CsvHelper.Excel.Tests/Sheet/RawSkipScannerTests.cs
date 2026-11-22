using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The raw skips of Phase 9's step 2 (<see cref="XmlScanner.RawSkips"/>): the rows a sheet scanner skips whole, above an
/// area or hidden, skipped by a raw search for their end tag, and the merged cells' pre-read skipping <c>sheetData</c> so.
/// That they read as the token skips do (rows with and without <c>r</c>, empty rows, prefixes, nested rows, markup that
/// holds end tags); the rows still read for what later cells need; what a raw skip no longer checks; the tripwires that
/// catch a skip stopped inside an attribute value; the errors that move from the pre-read to the main read; and the
/// probe's edge cases (<c>rless-probe.md</c>, section 3), written as XML. Each test runs in every <see cref="SheetMode"/>, with
/// raw skips on and off (each with fast cells as <see cref="SheetMode.All"/> pairs them); what only a raw skip leaves
/// unchecked is keyed on <see cref="ScanMode.RawSkips"/> alone.
/// </summary>
public class RawSkipScannerTests
{
	/// <summary>What is wrong with a <c>&lt;</c> in an attribute value.</summary>
	private const string LessThan = "an attribute value has '<', which XML doesn't allow";

	/// <summary>What is wrong with markup that starts with <c>&lt;!</c> and is neither a comment nor a CDATA section.</summary>
	private const string Bang = "'<!' starts neither a comment nor a CDATA section";

	/// <summary>The fake list of merged ranges that the probe's markup and attribute values hold, after an end tag of <c>sheetData</c>.</summary>
	private const string Fake = "</sheetData><mergeCells count=\"1\"><mergeCell ref=\"D9:E10\"/></mergeCells>";

	/// <summary>
	/// The probe's source sheet (its <c>merged</c> source): a title merged across <c>A1:F1</c>, a header, rows with gaps,
	/// and <c>B4:C6</c> merged, with every cell's <c>r</c>; its <c>mc:Ignorable</c> namespace <c>p</c> takes the foreign
	/// attribute of the <c>e-attr-*</c> cases, on <c>B4</c>.
	/// </summary>
	private static readonly string Source = BuildSource();

	/// <summary>The source's records, read without merged cells filled.</summary>
	private static readonly string[] SourceRecords =
	[
		"1: Probe title",
		"2: Id | Name | Date | Amount | Paid | Note",
		"3: number:1 | full | number:45306 | number:10.5 | bool:1 | all six",
		"4: number:2 | one gap | () | number:20.25 | bool:0 | C empty",
		"5: number:3 | () | () | () | bool:1 | B-D empty",
		"6: () | no id | number:45352 | number:40 | bool:0 | A empty",
		"7: number:5 | () | () | () | () | B-E empty",
		"8: number:6 | trailing",
		"9: () | alternate | () | number:70 | () | A C E empty",
		"10: number:8 | full again | number:45657.572916666664 | number:80.125 | bool:1 | last",
	];

	/// <summary>The source's records with merged cells filled: the title across row 1, and <c>B4</c> over <c>B4:C6</c>.</summary>
	private static readonly string[] FilledSourceRecords =
	[
		"1: Probe title | Probe title | Probe title | Probe title | Probe title | Probe title",
		"2: Id | Name | Date | Amount | Paid | Note",
		"3: number:1 | full | number:45306 | number:10.5 | bool:1 | all six",
		"4: number:2 | one gap | one gap | number:20.25 | bool:0 | C empty",
		"5: number:3 | one gap | one gap | () | bool:1 | B-D empty",
		"6: () | one gap | one gap | number:40 | bool:0 | A empty",
		"7: number:5 | () | () | () | () | B-E empty",
		"8: number:6 | trailing | () | () | () | ()",
		"9: () | alternate | () | number:70 | () | A C E empty",
		"10: number:8 | full again | number:45657.572916666664 | number:80.125 | bool:1 | last",
	];

	/// <summary>The probe's edge cases, by the names of its workbooks.</summary>
	public static TheoryData<string> Probes => new()
	{
		"e-attr-ok", "e-attr-lt", "e-attr-lt-end", "e-attr-lt-mc", "e-comment", "e-pi", "e-cdata", "e-cdata-merge", "e-prefix", "e-squote",
	};

	[Fact]
	public async Task SkipsHiddenRowsAsTheTokenSkipDoes()
	{
		// Hidden rows with r and cells; empty, with and without an end tag; numbered by their first cell's r; and numbered by
		// none. A row without r is skipped from its first cell, which gave its number.
		var rows =
			$"<row r=\"1\">{N("A1", "1")}</row>" +
			$"<row r=\"2\" hidden=\"1\">{N("A2", "2")}{T("B2", "two")}</row>" +
			"<row r=\"3\" hidden=\"1\"/>" +
			$"<row r=\"4\">{N("A4", "4")}</row>" +
			$"<row hidden=\"1\">{N("A6", "6")}{T("B6", "six")}</row>" +
			"<row hidden=\"1\"><c><v>7</v></c><c t=\"inlineStr\"><is><t>seven</t></is></c></row>" +
			$"<row>{N("A8", "8")}</row>" +
			"<row r=\"9\" hidden=\"1\"></row>" +
			$"<row r=\"10\">{N("A10", "10")}{T("B10", "ten")}</row>";
		var xml = Sheets.Worksheet(rows);
		var merged = MergedScannerTests.Worksheet(rows, "A10:B11");
		var reading = CellReading.Hidden(rows: true, columns: false);

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1", "4: number:4", "8: number:8", "10: number:10 | ten"], await Sheets.ReadAsync(xml, mode, reading: reading));
			Assert.Equal(["1: number:1", "4: number:4", "5: ()", "8: number:8", "10: number:10 | ten"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));

			// With merged cells filled, and in an area.
			Assert.Equal(
				["1: number:1", "4: number:4", "8: number:8", "10: number:10 | number:10", "11: number:10 | number:10"],
				await Sheets.ReadAsync(merged, mode, reading: CellReading.Merged(rows: true)));
			Assert.Equal(["8: number:8", "10: number:10 | ten"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(true, false, "A5:B10")));

			// Read whole, every row with a value is a record.
			Assert.Equal(
				["1: number:1", "2: number:2 | two", "4: number:4", "6: number:6 | six", "7: number:7 | seven", "8: number:8", "10: number:10 | ten"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task SkipsRowsAboveTheAreaAsTheTokenSkipDoes()
	{
		// Rows above the area with r, numbered by their first cell's r, numbered by none, and empty.
		var xml = Sheets.Worksheet(
			$"<row r=\"1\">{N("A1", "1")}{N("B1", "11")}</row>" +
			$"<row>{N("A2", "2")}{T("B2", "two")}</row>" +
			"<row><c><v>3</v></c><c><v>33</v></c></row>" +
			"<row r=\"4\"/>" +
			$"<row r=\"5\">{N("A5", "5")}{N("B5", "55")}</row>" +
			$"<row>{N("B6", "66")}</row>" +
			$"<row r=\"8\">{N("A8", "8")}</row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["5: number:5 | number:55", "6: () | number:66"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A4:B7")));
			Assert.Equal(["4: ()", "5: number:5 | number:55", "6: () | number:66"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Area("A4:B7")));
			Assert.Equal(["3: number:33", "5: number:55", "6: number:66"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("B3:B6")));
		});
	}

	[Fact]
	public async Task SkipsTheRowsOfPrefixedSheets()
	{
		// Every element prefixed, as the Open XML SDK, ClosedXML and MiniExcel write them: the rows' name is x:row.
		var xml = TestPackage.Declaration + $"<x:worksheet xmlns:x=\"{TestPackage.MainNamespace}\"><x:sheetData>" +
			"<x:row r=\"1\"><x:c r=\"A1\"><x:v>1</x:v></x:c></x:row>" +
			"<x:row r=\"2\" hidden=\"1\"><x:c r=\"A2\"><x:v>2</x:v></x:c><x:c r=\"B2\" t=\"inlineStr\"><x:is><x:t>two</x:t></x:is></x:c></x:row>" +
			"<x:row hidden=\"1\"><x:c r=\"A3\"><x:v>3</x:v></x:c></x:row>" +
			"<x:row r=\"4\" hidden=\"1\"/>" +
			"<x:row r=\"5\"><x:c r=\"A5\"><x:v>5</x:v></x:c></x:row>" +
			"</x:sheetData><x:mergeCells count=\"1\"><x:mergeCell ref=\"A5:B5\"/></x:mergeCells></x:worksheet>";

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1", "5: number:5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
			Assert.Equal(["3: number:3", "5: number:5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:B5")));
			Assert.Equal(["1: number:1", "5: number:5 | number:5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Merged(rows: true)));
		});
	}

	[Fact]
	public async Task SkipsRowsOfAnotherPrefixTokenByToken()
	{
		// sheetData without a prefix, and a row of the same namespace with one: a raw skip searches for sheetData's prefix and
		// row, so the x:row is skipped token by token, with the same records.
		var rows =
			$"<row r=\"1\">{N("A1", "1")}</row>" +
			$"<x:row r=\"2\" hidden=\"1\" xmlns:x=\"{TestPackage.MainNamespace}\"><x:c r=\"A2\"><x:v>2</x:v></x:c></x:row>" +
			$"<row r=\"3\" hidden=\"1\">{N("A3", "3")}</row>" +
			$"<row r=\"4\">{N("A4", "4")}</row>";
		var xml = Sheets.Worksheet(rows);

		// Markup that can't be read in each hidden row: found in the x:row by either skip; in the row, only token by token.
		var inPrefixed = Sheets.Worksheet(rows.Replace("<x:v>2</x:v>", "<x:v>2</x:v><!x>"));
		var inUnprefixed = Sheets.Worksheet(rows.Replace(N("A3", "3"), N("A3", "3") + "<!x>"));
		var reading = CellReading.Hidden(rows: true, columns: false);

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1", "4: number:4"], await Sheets.ReadAsync(xml, mode, reading: reading));
			Assert.Equal(["3: number:3", "4: number:4"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:A4")));
			Assert.Equal(["1: number:1", "2: number:2", "3: number:3", "4: number:4"], await Sheets.ReadAsync(xml, mode));

			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(inPrefixed, mode, reading: reading));
			Assert.Equal($"Sheet Data can't be read at byte {At(inPrefixed, "<!x>")} of {Sheets.PartName}: {Bang}.", e.Message);
			if (mode.Scan.RawSkips)
			{
				Assert.Equal(["1: number:1", "4: number:4"], await Sheets.ReadAsync(inUnprefixed, mode, reading: reading));
			}
			else
			{
				e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(inUnprefixed, mode, reading: reading));
				Assert.Equal($"Sheet Data can't be read at byte {At(inUnprefixed, "<!x>")} of {Sheets.PartName}: {Bang}.", e.Message);
			}
		});
	}

	[Fact]
	public async Task PassesOverNestedRowsAndMarkupInSkippedRows()
	{
		// In rows skipped whole: rows nested in an element of the row and directly in it, empty or not, which a raw skip
		// counts; and a comment, a CDATA section and a processing instruction that hold a row's start tag and the end tags of a
		// row and of sheetData, which it passes over.
		var xml = Sheets.Worksheet(
			$"<row r=\"1\">{N("A1", "1")}</row>" +
			$"<row r=\"2\" hidden=\"1\">{N("A2", "2")}<extLst><row r=\"90\">{N("A90", "90")}</row><row/></extLst><row><c><v>91</v></c></row>{N("B2", "22")}</row>" +
			$"<row r=\"3\" hidden=\"1\">{N("A3", "3")}<!-- <row> </row></sheetData> --><c r=\"B3\" t=\"str\"><v><![CDATA[</row></sheetData>]]></v></c><?probe <row> </row></sheetData>?></row>" +
			"<row r=\"4\" hidden=\"1\"><c r=\"A4\" t=\"inlineStr\"><is><t><![CDATA[<row></row>]]></t></is></c></row>" +
			$"<row r=\"5\">{N("A5", "5")}</row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1", "5: number:5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
			Assert.Equal(["5: number:5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A5:B5")));

			// Read, the nested rows are elements a row passes over, and the CDATA sections are values.
			Assert.Equal(
				["1: number:1", "2: number:2 | number:22", "3: number:3 | </row></sheetData>", "4: <row></row>", "5: number:5"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task ReadsTheRowsThatLaterCellsNeed()
	{
		// Rows above the area and hidden rows are read, not skipped, when formulas read as text, for the shared formulas they
		// define, and when merged cells are filled, for the top-left cells of the ranges that start in them: the same records
		// both ways, and their XML checked both ways.
		var rows =
			"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A3\" si=\"0\">B1*2</f><v>2</v></c><c r=\"B1\"><v>1</v></c></row>" +
			"<row r=\"2\" hidden=\"1\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/><v>4</v></c><c r=\"B2\"><f t=\"shared\" ref=\"B2:B3\" si=\"1\">A2+1</f><v>5</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\"/><v>6</v></c><c r=\"B3\"><f t=\"shared\" si=\"1\"/><v>7</v></c></row>";
		var faultyRows = rows.Replace("<v>5</v></c></row>", "<v>5</v></c><!x></row>");
		var xml = Sheets.Worksheet(rows);
		var merged = MergedScannerTests.Worksheet(rows, "B2:B3");
		var faulty = Sheets.Worksheet(faultyRows);
		var faultyMerged = MergedScannerTests.Worksheet(faultyRows, "B2:B3");
		var formulaText = CellReading.Hidden(true, false, formulas: ExcelFormulas.FormulaText);

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["3: =B3*2 | =A3+1"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:B3", ExcelFormulas.FormulaText)));
			Assert.Equal(["1: =B1*2 | number:1", "3: =B3*2 | =A3+1"], await Sheets.ReadAsync(xml, mode, reading: formulaText));
			Assert.Equal(["3: number:6 | number:7"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A3:B3")));
			Assert.Equal(["1: number:2 | number:1", "3: number:6 | number:5"], await Sheets.ReadAsync(merged, mode, reading: CellReading.Merged(rows: true)));
			Assert.Equal(["3: number:6 | number:5"], await Sheets.ReadAsync(merged, mode, reading: CellReading.Merged("A3:B3")));

			// Markup that can't be read in row 2, after B2: found at the row when the row is read; when it is skipped, only
			// token by token, for the sheet; with merged cells filled, token by token, by the pre-read.
			var atRow = $"Row Data!2:2 can't be read at byte {At(faulty, "<!x>")} of {Sheets.PartName}: {Bang}.";
			var atSheet = $"Sheet Data can't be read at byte {At(faulty, "<!x>")} of {Sheets.PartName}: {Bang}.";
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(faulty, mode, reading: CellReading.Area("A3:B3", ExcelFormulas.FormulaText)));
			Assert.Equal(atRow, e.Message);
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(faulty, mode, reading: formulaText));
			Assert.Equal(atRow, e.Message);
			if (mode.Scan.RawSkips)
			{
				Assert.Equal(["3: number:6 | number:7"], await Sheets.ReadAsync(faulty, mode, reading: CellReading.Area("A3:B3")));
			}
			else
			{
				e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(faulty, mode, reading: CellReading.Area("A3:B3")));
				Assert.Equal(atSheet, e.Message);
			}

			var subject = mode.Scan.RawSkips ? "Row Data!2:2" : "Sheet Data";
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(faultyMerged, mode, reading: CellReading.Merged("A3:B3")));
			Assert.Equal($"{subject} can't be read at byte {At(faultyMerged, "<!x>")} of {Sheets.PartName}: {Bang}.", e.Message);
		});
	}

	[Fact]
	public async Task NoLongerChecksTheContentOfSkippedRows()
	{
		// XML that can't be read in a row skipped whole: token by token, the skip stops at it, for the sheet; raw, nothing of
		// the row is read. Where the row is read, the error names its row or its cell.
		var cases = new (string Content, string At, int From, string Detail, string Where)[]
		{
			("<c r=\"A2\" p=\"a<b\"/>", "a<b", 1, LessThan, "Row Data!2:2"),
			($"{N("A2", "2")}<!x>", "<!x>", 0, Bang, "Row Data!2:2"),
			("<c r=\"A2\"><v>2</v></c x>", "</c x>", 0, "an end tag has more than a name", "Cell Data!A2"),
		};

		foreach (var (content, at, from, detail, where) in cases)
		{
			var xml = Sheets.Worksheet($"<row r=\"1\">{N("A1", "1")}</row><row r=\"2\" hidden=\"1\">{content}</row><row r=\"3\">{N("A3", "3")}</row>");
			var offset = At(xml, at) + from;
			var readings = new (CellReading Reading, string[] Records)[]
			{
				(CellReading.Hidden(rows: true, columns: false), new[] { "1: number:1", "3: number:3" }),
				(CellReading.Area("A3:A3"), new[] { "3: number:3" }),
			};

			await Sheets.InEveryModeAsync(async mode =>
			{
				foreach (var (reading, records) in readings)
				{
					if (mode.Scan.RawSkips)
					{
						Assert.Equal(records, await Sheets.ReadAsync(xml, mode, reading: reading));
					}
					else
					{
						var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: reading));
						Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
					}
				}

				var read = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
				Assert.Equal($"{where} can't be read at byte {offset} of {Sheets.PartName}: {detail}.", read.Message);
			});
		}
	}

	[Fact]
	public async Task NoLongerChecksRowsWithoutRAfterTheirFirstCell()
	{
		// A row without r is numbered at its first cell, whose tag is read before the row is known to be skipped; what can't
		// be read comes after it. Its name was noted at its start, so it is skipped raw from there.
		var cases = new (string Row, string At, int From, string Detail)[]
		{
			("<row hidden=\"1\"><c r=\"A2\"><v>2</v></c><c r=\"B2\" p=\"a<b\"/></row>", "a<b", 1, LessThan),
			("<row hidden=\"1\"><c r=\"A2\"><v>2</v></c><!x></row>", "<!x>", 0, Bang),
			("<row hidden=\"1\"><c><v>2</v></c><c p=\"a<b\"/></row>", "a<b", 1, LessThan),
			("<row hidden=\"1\"><c><v>2</v></c><!x></row>", "<!x>", 0, Bang),
		};

		foreach (var (row, at, from, detail) in cases)
		{
			var xml = Sheets.Worksheet($"<row r=\"1\">{N("A1", "1")}</row>{row}<row r=\"3\">{N("A3", "3")}</row>");
			await AssertOnlyTokenSkipsCheckRow2Async(xml, At(xml, at) + from, detail);
		}
	}

	[Fact]
	public async Task NoLongerChecksSkippedRowsOfPrefixedSheets()
	{
		// Every element prefixed: the rows' name is x:row, so skipped rows, with r or without, are skipped raw.
		var cases = new (string Row, string At, int From, string Detail)[]
		{
			("<x:row r=\"2\" hidden=\"1\"><x:c r=\"A2\"><x:v>2</x:v></x:c><!x></x:row>", "<!x>", 0, Bang),
			("<x:row r=\"2\" hidden=\"1\"><x:c r=\"A2\"><x:v>2</x:v></x:c><x:c r=\"B2\" p=\"a<b\"/></x:row>", "a<b", 1, LessThan),
			("<x:row hidden=\"1\"><x:c r=\"A2\"><x:v>2</x:v></x:c><!x></x:row>", "<!x>", 0, Bang),
		};

		foreach (var (row, at, from, detail) in cases)
		{
			var xml = TestPackage.Declaration + $"<x:worksheet xmlns:x=\"{TestPackage.MainNamespace}\"><x:sheetData>" +
				$"<x:row r=\"1\"><x:c r=\"A1\"><x:v>1</x:v></x:c></x:row>{row}<x:row r=\"3\"><x:c r=\"A3\"><x:v>3</x:v></x:c></x:row>" +
				"</x:sheetData></x:worksheet>";
			await AssertOnlyTokenSkipsCheckRow2Async(xml, At(xml, at) + from, detail);
		}
	}

	[Fact]
	public async Task FailsAtTheNextEndTagWhenASkipStopsInAValue()
	{
		// A hidden row whose cell has </row> in an attribute value, which XML doesn't allow: token by token, the skip stops at
		// its '<'; raw, the skip ends at it, the rest of the cell is read as sheetData's content, and the cell's end tag then
		// ends the rows, which it doesn't close.
		var xml = Sheets.Worksheet($"<row r=\"1\">{N("A1", "1")}</row><row r=\"2\" hidden=\"1\"><c r=\"A2\" note=\"</row>\"><v>2</v></c></row><row r=\"3\">{N("A3", "3")}</row>");
		var lessThan = At(xml, "</row>\"");
		var cellEnd = At(xml, "</c>", xml.IndexOf("note=", StringComparison.Ordinal));
		var tripwire = $"Sheet Data can't be read at byte {cellEnd} of {Sheets.PartName}: the end tag </c> doesn't close sheetData.";
		var tokens = $"Sheet Data can't be read at byte {lessThan} of {Sheets.PartName}: {LessThan}.";

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var reading in new[] { CellReading.Hidden(rows: true, columns: false), CellReading.Area("A3:A3") })
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: reading));
				Assert.Equal(mode.Scan.RawSkips ? tripwire : tokens, e.Message);
				Assert.Null(e.Location);
			}

			// Read, the row's cell has the '<'.
			var read = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
			Assert.Equal($"Row Data!2:2 can't be read at byte {lessThan} of {Sheets.PartName}: {LessThan}.", read.Message);
		});
	}

	[Fact]
	public async Task RefusesAnEndTagThatDoesntCloseSheetData()
	{
		// The rows end at an end tag that isn't sheetData's, read, skipped hidden or skipped above an area; and a root without
		// sheetData ends at one that isn't the worksheet's.
		var xml = Sheets.Worksheet($"<row r=\"1\">{N("A1", "1")}</row><row r=\"2\" hidden=\"1\">{N("A2", "2")}</row><row r=\"3\">{N("A3", "3")}</row>").Replace("</sheetData>", "</sheetdata>");
		var at = At(xml, "</sheetdata>");
		var message = $"Sheet Data can't be read at byte {at} of {Sheets.PartName}: the end tag </sheetdata> doesn't close sheetData.";
		var noRows = $"<worksheet xmlns=\"{TestPackage.MainNamespace}\"><sheetPr/><dimension ref=\"A1\"/></worksheets>";

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var reading in new[] { CellReading.Default, CellReading.Hidden(rows: true, columns: false), CellReading.Area("A2:A5") })
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: reading));
				Assert.Equal(message, e.Message);
				Assert.Null(e.Location);
			}

			// With merged cells filled, a raw pre-read searches for </sheetData> to the end of the part; token by token, it
			// takes the misspelled end tag for sheetData's, and the main read finds it.
			var merged = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Merged()));
			Assert.Equal(
				mode.Scan.RawSkips ? $"Sheet Data can't be read at byte {Encoding.UTF8.GetByteCount(xml)} of {Sheets.PartName}: it ends inside an element." : message,
				merged.Message);

			var root = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(noRows, mode));
			Assert.StartsWith($"Sheet Data can't be read at byte {At(noRows, "</worksheets>")} of {Sheets.PartName}: the end tag </worksheets> doesn't close ", root.Message);
		});
	}

	[Fact]
	public async Task MovesTheRowsErrorsFromThePreReadToTheMainRead()
	{
		// With merged cells filled, XML that can't be read in row 5: token by token, the pre-read finds it, for the sheet; raw,
		// the pre-read doesn't read the rows, and the main read finds it after the rows before it, at its row (markup between
		// cells) or its cell (a '<' in an attribute of its v), as a read without merged cells does.
		var rows = string.Concat(Enumerable.Range(1, 4).Select(r => $"<row r=\"{r}\">{N($"A{r}", $"{r}")}</row>"));
		var markup = MergedScannerTests.Worksheet(rows + $"<row r=\"5\">{N("A5", "5")}<!x>{N("B5", "55")}</row>", "A1:B1");
		var value = MergedScannerTests.Worksheet(rows + $"<row r=\"5\">{N("A5", "5")}<c r=\"B5\"><v note=\"a<b\">55</v></c></row>", "A1:B1");
		var markupAt = At(markup, "<!x>");
		var valueAt = At(value, "a<b") + 1;

		await Sheets.InEveryModeAsync(async mode =>
		{
			var raw = mode.Scan.RawSkips;
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(markup, mode, reading: CellReading.Merged()));
			Assert.Equal($"{(raw ? "Row Data!5:5" : "Sheet Data")} can't be read at byte {markupAt} of {Sheets.PartName}: {Bang}.", e.Message);
			Assert.Equal(raw ? "Data!5:5" : null, e.Location);
			Assert.Equal(raw ? 5 : 0, e.LineNumber);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(value, mode, reading: CellReading.Merged()));
			Assert.Equal($"{(raw ? "Cell Data!B5" : "Sheet Data")} can't be read at byte {valueAt} of {Sheets.PartName}: {LessThan}.", e.Message);
			Assert.Equal(raw ? "Data!B5" : null, e.Location);

			// Without merged cells, the main read finds them there in both modes.
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(markup, mode));
			Assert.Equal($"Row Data!5:5 can't be read at byte {markupAt} of {Sheets.PartName}: {Bang}.", e.Message);
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(value, mode));
			Assert.Equal($"Cell Data!B5 can't be read at byte {valueAt} of {Sheets.PartName}: {LessThan}.", e.Message);

			if (raw)
			{
				// The rows before it were read.
				using var sheet = Sheets.Open(markup, mode.Scan, reading: CellReading.Merged());
				var captured = new Captured(mode.RecordsPerFill);
				await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.FillAllAsync(sheet, captured, mode.Scan.Async));
				Assert.Equal(["1: number:1 | number:1", "2: number:2 | ()", "3: number:3 | ()", "4: number:4 | ()"], captured.Records.Select(r => r.ToString()));
			}
		});
	}

	[Theory]
	[MemberData(nameof(Probes))]
	public async Task ReadsTheProbesEdgeCasesAsTheProbeFound(string probe)
	{
		// Each read with and without merged cells filled, of the whole sheet and of A1:F3, above B4's row; and the ranges the
		// pre-read finds. A '<' in an attribute value throws, at B4's row in the main read, or in the pre-read token by
		// token; markup never makes the fake D9:E10 a range; and the prefixes and single quotes read as the source does.
		var xml = Probe(probe);
		var bytes = Encoding.UTF8.GetBytes(xml);

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var fills in new[] { false, true })
			{
				foreach (var area in new[] { false, true })
				{
					var reading = fills ? CellReading.Merged(area ? "A1:F3" : null) : area ? CellReading.Area("A1:F3") : CellReading.Default;
					var expected = Expected(probe, xml, fills, area, mode.Scan.RawSkips);
					var actual = await OutcomeAsync(() => Sheets.ReadAsync(xml, mode, reading: reading));
					Assert.True(expected == actual, $"{(fills ? "Filled" : "Not filled")}, {(area ? "A1:F3" : "the whole sheet")}: expected\n{expected}\nread\n{actual}");
				}
			}

			var ranges = ExpectedRanges(probe, xml, mode.Scan.RawSkips);
			var found = await OutcomeAsync(() => Task.FromResult(Ranges(bytes, mode.Scan)));
			Assert.True(ranges == found, $"The pre-read: expected\n{ranges}\nfound\n{found}");
		});
	}

	/// <summary>A number cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="number">The number's text.</param>
	/// <returns>The cell's XML.</returns>
	private static string N(string reference, string number) => $"<c r=\"{reference}\"><v>{number}</v></c>";

	/// <summary>An inline string cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string T(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";

	/// <summary>A boolean cell.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="value"><c>1</c> or <c>0</c>.</param>
	/// <returns>The cell's XML.</returns>
	private static string B(string reference, string value) => $"<c r=\"{reference}\" t=\"b\"><v>{value}</v></c>";

	/// <summary>
	/// Checks a sheet of shown rows 1 and 3 whose row 2 has XML that can't be read where a skip of the row passes over it,
	/// in every <see cref="SheetMode"/>. Skipped hidden, or above the area <c>A3:A3</c>: raw, the shown rows read; token by
	/// token, the skip stops at it, for the sheet. Read, it throws at row 2.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="offset">The byte offset of what can't be read.</param>
	/// <param name="detail">What is wrong with it.</param>
	/// <returns>A task that completes when the checks pass.</returns>
	private static async Task AssertOnlyTokenSkipsCheckRow2Async(string xml, long offset, string detail)
	{
		var readings = new (CellReading Reading, string[] Records)[]
		{
			(CellReading.Hidden(rows: true, columns: false), new[] { "1: number:1", "3: number:3" }),
			(CellReading.Area("A3:A3"), new[] { "3: number:3" }),
		};

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var (reading, records) in readings)
			{
				if (mode.Scan.RawSkips)
				{
					Assert.Equal(records, await Sheets.ReadAsync(xml, mode, reading: reading));
				}
				else
				{
					var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: reading));
					Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
				}
			}

			var read = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
			Assert.Equal($"Row Data!2:2 can't be read at byte {offset} of {Sheets.PartName}: {detail}.", read.Message);
		});
	}

	/// <summary>The byte offset in a part of the first occurrence of a text, from a character on.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="text">The text.</param>
	/// <param name="from">The character to search from.</param>
	/// <returns>The offset.</returns>
	private static long At(string xml, string text, int from = 0) => Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf(text, from, StringComparison.Ordinal)));

	/// <summary>Builds <see cref="Source"/>.</summary>
	/// <returns>The part's XML.</returns>
	private static string BuildSource()
	{
		var rows =
			$"<row r=\"1\">{T("A1", "Probe title")}</row>" +
			$"<row r=\"2\">{T("A2", "Id")}{T("B2", "Name")}{T("C2", "Date")}{T("D2", "Amount")}{T("E2", "Paid")}{T("F2", "Note")}</row>" +
			$"<row r=\"3\">{N("A3", "1")}{T("B3", "full")}{N("C3", "45306")}{N("D3", "10.5")}{B("E3", "1")}{T("F3", "all six")}</row>" +
			$"<row r=\"4\">{N("A4", "2")}{T("B4", "one gap")}{N("D4", "20.25")}{B("E4", "0")}{T("F4", "C empty")}</row>" +
			$"<row r=\"5\">{N("A5", "3")}{B("E5", "1")}{T("F5", "B-D empty")}</row>" +
			$"<row r=\"6\">{T("B6", "no id")}{N("C6", "45352")}{N("D6", "40")}{B("E6", "0")}{T("F6", "A empty")}</row>" +
			$"<row r=\"7\">{N("A7", "5")}{T("F7", "B-E empty")}</row>" +
			$"<row r=\"8\">{N("A8", "6")}{T("B8", "trailing")}</row>" +
			$"<row r=\"9\">{T("B9", "alternate")}{N("D9", "70")}{T("F9", "A C E empty")}</row>" +
			$"<row r=\"10\">{N("A10", "8")}{T("B10", "full again")}{N("C10", "45657.572916666664")}{N("D10", "80.125")}{B("E10", "1")}{T("F10", "last")}</row>";

		return TestPackage.Declaration +
			"<worksheet xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:p=\"urn:probe\" mc:Ignorable=\"p\" " +
			$"xmlns=\"{TestPackage.MainNamespace}\" xmlns:r=\"{TestPackage.RelationshipsNamespace}\">" +
			"<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/></sheetView></sheetViews>" +
			"<cols><col min=\"1\" max=\"6\" width=\"12\" customWidth=\"1\"/></cols>" +
			$"<sheetData>{rows}</sheetData>" +
			"<mergeCells count=\"2\"><mergeCell ref=\"A1:F1\"/><mergeCell ref=\"B4:C6\"/></mergeCells>" +
			"<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>";
	}

	/// <summary>A probe's sheet, made from <see cref="Source"/> as the probe made its workbook.</summary>
	/// <param name="name">The probe's name, such as <c>e-attr-lt</c>.</param>
	/// <returns>The part's XML.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The probe has no such case.</exception>
	private static string Probe(string name) => name switch
	{
		"e-attr-ok" => Noted("a&lt;/sheetData&gt;b > c"),
		"e-attr-lt" => Noted("a<b"),
		"e-attr-lt-end" => Noted("</sheetData>"),
		"e-attr-lt-mc" => Noted("</sheetData><mergeCells count='1'><mergeCell ref='D9:E10'/></mergeCells>"),
		"e-comment" => Marked("<!-- ", " -->"),
		"e-pi" => Marked("<?probe ", "?>"),
		"e-cdata" => Source.Replace("<t>no id</t>", $"<t><![CDATA[x{Fake}y]]></t>").Replace("<t>alternate</t>", "<t><![CDATA[</mergeCells>]]></t>"),
		"e-cdata-merge" => Source.Replace("<mergeCell ref=\"B4:C6\"/>", "<![CDATA[</mergeCells>]]><mergeCell ref=\"B4:C6\"/>"),
		"e-prefix" => Prefixed(Source),
		"e-squote" => TestPackage.Declaration + Source.Substring(TestPackage.Declaration.Length).Replace('"', '\''),
		_ => throw new ArgumentOutOfRangeException(nameof(name), name, "The probe has no such case."),
	};

	/// <summary>The source with the foreign attribute <c>p:note</c> on <c>B4</c>.</summary>
	/// <param name="value">The attribute's value, as XML.</param>
	/// <returns>The part's XML.</returns>
	private static string Noted(string value) =>
		Source.Replace("<c r=\"B4\" t=\"inlineStr\">", $"<c r=\"B4\" p:note=\"{value}\" t=\"inlineStr\">");

	/// <summary>
	/// The source with markup between B3 and C3 that holds <c>&lt;/sheetData&gt;</c>, between rows 3 and 4 that holds
	/// <see cref="Fake"/>, and in <c>mergeCells</c> that holds <c>&lt;/mergeCells&gt;</c>.
	/// </summary>
	/// <param name="open">What starts the markup, such as <c>&lt;!-- </c>.</param>
	/// <param name="close">What ends it, such as <c> --&gt;</c>.</param>
	/// <returns>The part's XML.</returns>
	private static string Marked(string open, string close) => Source
		.Replace("<c r=\"C3\"", $"{open}</sheetData>{close}<c r=\"C3\"")
		.Replace("<row r=\"4\">", $"{open}{Fake}{close}<row r=\"4\">")
		.Replace("<mergeCell ref=\"B4:C6\"/>", $"{open}</mergeCells>{close}<mergeCell ref=\"B4:C6\"/>");

	/// <summary>A part with every element prefixed <c>x:</c>, and no default namespace.</summary>
	/// <param name="xml">The part.</param>
	/// <returns>The part's XML.</returns>
	private static string Prefixed(string xml)
	{
		var body = Regex.Replace(xml.Substring(TestPackage.Declaration.Length), "<(/?)(?=[A-Za-z])", "<$1x:");

		return TestPackage.Declaration + body.Replace(" xmlns=\"", " xmlns:x=\"");
	}

	/// <summary>A probe's records without merged cells filled: the source's, with <c>e-cdata</c>'s CDATA text verbatim.</summary>
	/// <param name="probe">The probe.</param>
	/// <returns>The records.</returns>
	private static IEnumerable<string> PlainRecords(string probe) => probe == "e-cdata"
		? SourceRecords.Select(r => r.Replace(" | no id | ", $" | x{Fake}y | ").Replace(" | alternate | ", " | </mergeCells> | "))
		: SourceRecords;

	/// <summary>A probe's records with merged cells filled: B6, whose CDATA <c>e-cdata</c> changes, reads as B4.</summary>
	/// <param name="probe">The probe.</param>
	/// <returns>The records.</returns>
	private static IEnumerable<string> FilledRecords(string probe) => probe == "e-cdata"
		? FilledSourceRecords.Select(r => r.Replace(" | alternate | ", " | </mergeCells> | "))
		: FilledSourceRecords;

	/// <summary>The byte offset of the first <c>&lt;</c> in the value of <c>B4</c>'s <c>p:note</c>.</summary>
	/// <param name="xml">The probe's part.</param>
	/// <returns>The offset.</returns>
	private static long NoteLessThan(string xml) => At(xml, "<", xml.IndexOf("p:note=\"", StringComparison.Ordinal) + "p:note=\"".Length);

	/// <summary>
	/// What the pre-read throws when its raw skip ends at a <c>&lt;/sheetData&gt;</c> in <c>B4</c>'s value: the cell's end tag
	/// then ends the worksheet's children, and isn't the worksheet's.
	/// </summary>
	/// <param name="xml">The probe's part.</param>
	/// <returns>The outcome.</returns>
	private static string PreReadTripwire(string xml) =>
		Threw($"Sheet Data can't be read at byte {At(xml, "</c>", xml.IndexOf("p:note=", StringComparison.Ordinal))} of {Sheets.PartName}: the end tag </c> doesn't close worksheet.");

	/// <summary>What a read of a probe gives, as the probe found and the spec of step 2 says.</summary>
	/// <param name="probe">The probe.</param>
	/// <param name="xml">Its part.</param>
	/// <param name="fills">Whether merged cells are filled.</param>
	/// <param name="area">Whether the read is of <c>A1:F3</c>, rather than the whole sheet.</param>
	/// <param name="raw">Whether the readers skip by a raw search.</param>
	/// <returns>The outcome: the records, or what the read throws (<see cref="OutcomeAsync"/>).</returns>
	private static string Expected(string probe, string xml, bool fills, bool area, bool raw)
	{
		var records = fills ? FilledRecords(probe) : PlainRecords(probe);
		var read = string.Join("\n", area ? records.Take(3) : records);
		if (!probe.StartsWith("e-attr-lt", StringComparison.Ordinal))
		{
			return read;
		}

		var atRow = Threw($"Row Data!4:4 can't be read at byte {NoteLessThan(xml)} of {Sheets.PartName}: {LessThan}.");
		if (!fills)
		{
			// The main read meets the '<' at B4's row, which a read of A1:F3 stops at, before its cells.
			return area ? read : atRow;
		}

		if (!raw)
		{
			// The pre-read reads the rows token by token, and meets it first.
			return Threw($"Sheet Data can't be read at byte {NoteLessThan(xml)} of {Sheets.PartName}: {LessThan}.");
		}

		return probe switch
		{
			"e-attr-lt-end" => PreReadTripwire(xml),

			// The pre-read finds the list in the value (a crafted file's residue), whose range is below A1:F3.
			"e-attr-lt-mc" => area ? string.Join("\n", PlainRecords(probe).Take(3)) : atRow,
			_ => area ? read : atRow,
		};
	}

	/// <summary>What the pre-read of a probe finds: the ranges, or what it throws.</summary>
	/// <param name="probe">The probe.</param>
	/// <param name="xml">Its part.</param>
	/// <param name="raw">Whether it skips <c>sheetData</c> by a raw search.</param>
	/// <returns>The outcome: the ranges, one a line, or what the read throws (<see cref="OutcomeAsync"/>).</returns>
	private static string ExpectedRanges(string probe, string xml, bool raw)
	{
		const string Real = "A1:F1\nB4:C6";
		if (!probe.StartsWith("e-attr-lt", StringComparison.Ordinal))
		{
			return Real;
		}

		if (!raw)
		{
			return Threw($"Sheet Data can't be read at byte {NoteLessThan(xml)} of {Sheets.PartName}: {LessThan}.");
		}

		return probe switch
		{
			"e-attr-lt-end" => PreReadTripwire(xml),
			"e-attr-lt-mc" => "D9:E10",
			_ => Real,
		};
	}

	/// <summary>The merged ranges the pre-read finds in a part, as <see cref="Sheets.FindMergedCells"/> reads it.</summary>
	/// <param name="bytes">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns>The ranges, none when the part has none.</returns>
	private static List<string> Ranges(byte[] bytes, ScanMode mode)
	{
		var cells = Sheets.FindMergedCells(bytes, mode);

		return cells is null ? [] : [.. cells.Ranges.Select(r => r.ToString())];
	}

	/// <summary>The outcome of a read: its records, one a line, or the message of the <see cref="CsvFormatException"/> it throws.</summary>
	/// <param name="read">The read.</param>
	/// <returns>A task whose result is the outcome.</returns>
	private static async Task<string> OutcomeAsync(Func<Task<List<string>>> read)
	{
		try
		{
			return string.Join("\n", await read());
		}
		catch (CsvFormatException e)
		{
			return Threw(e.Message);
		}
	}

	/// <summary>The outcome of a read that throws <see cref="CsvFormatException"/>.</summary>
	/// <param name="message">The exception's message.</param>
	/// <returns>The outcome.</returns>
	private static string Threw(string message) => "CsvFormatException: " + message;
}
