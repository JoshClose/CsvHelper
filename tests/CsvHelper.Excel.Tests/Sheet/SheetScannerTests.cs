using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The sheet scanner: cell types and values, placement by reference, padding, empty rows, structure, errors and the gap
/// fields' count. Each test of a sheet's XML runs in every <see cref="SheetMode"/>: whole and one-byte streams, 64 KB and
/// 16-byte windows, sync and async, with fills that take every record and fills that take one.
/// </summary>
public class SheetScannerTests
{
	[Fact]
	public async Task ReadsExcelsShape()
	{
		var strings = Sheets.Strings("Name", "Amount", "Paid", "apple");
		var xml = Sheets.Worksheet(
			"<row r=\"1\" spans=\"1:3\" x14ac:dyDescent=\"0.3\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"s\"><v>2</v></c></row>" +
			"<row r=\"2\" spans=\"1:3\" x14ac:dyDescent=\"0.3\"><c r=\"A2\" t=\"s\"><v>3</v></c><c r=\"B2\" s=\"1\"><v>1.5</v></c><c r=\"C2\" t=\"b\"><v>1</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: Name | Amount | Paid", "2: apple | number:1.5 | bool:1"], await Sheets.ReadAsync(xml, mode, strings: strings));
		});
	}

	[Fact]
	public async Task ReadsEveryCellType()
	{
		var strings = Sheets.Strings("shared");
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><v>42</v></c><c r=\"B1\" t=\"n\"><v>-1.5E-05</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"s\"><v>0</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\" t=\"str\"><v>text</v></c><c r=\"B3\" t=\"str\"><f>A1&amp;\"\"</f><v>formula</v></c></row>" +
			"<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>inline</t></is></c><c r=\"B4\" t=\"inlineStr\"><v>as v</v></c></row>" +
			"<row r=\"5\"><c r=\"A5\" t=\"b\"><v>1</v></c><c r=\"B5\" t=\"b\"><v>0</v></c><c r=\"C5\" t=\"b\"><v>true</v></c><c r=\"D5\" t=\"b\"><v>FALSE</v></c><c r=\"E5\" t=\"b\"><v> 1 </v></c><c r=\"F5\" t=\"b\"><v>yes</v></c></row>" +
			"<row r=\"6\"><c r=\"A6\" t=\"e\"><v>#N/A</v></c><c r=\"B6\" t=\"e\"><f>1/0</f><v>#DIV/0!</v></c></row>" +
			"<row r=\"7\"><c r=\"A7\" t=\"d\"><v>2024-01-02T03:04:05</v></c><c r=\"B7\" t=\"x\"><v>other</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				[
					"1: number:42 | number:-1.5E-05",
					"2: shared | ()",
					"3: text | formula",
					"4: inline | as v",
					"5: bool:1 | bool:0 | bool:1 | bool:0 | bool:1 | yes",
					"6: error:#N/A | error:#DIV/0!",
					"7: 2024-01-02T03:04:05 | other",
				],
				await Sheets.ReadAsync(xml, mode, strings: strings));
		});
	}

	[Fact]
	public async Task TrimsValuesUnlessTheirSpaceIsPreserved()
	{
		// As Excel 365 reads them (checked through COM on 2026-11-02): text and errors are trimmed unless xml:space="preserve"
		// applies, on the v or t, or inherited from the is, r, c, row, sheetData or root. Excel won't open a workbook whose
		// number, boolean, index or error keeps spaces that way; numbers, booleans and indexes are trimmed anyway, since they
		// must parse, and errors keep them, as text does.
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><v> 42 </v></c><c r=\"B1\"><v>&#9;7&#10;</v></c><c r=\"C1\" t=\"str\"><v> text </v></c><c r=\"D1\" t=\"e\"><v> #N/A </v></c>" +
			"<c r=\"E1\" t=\"str\"><v xml:space=\"preserve\"> kept </v></c><c r=\"F1\" t=\"inlineStr\"><is><t>  inline  </t></is></c>" +
			"<c r=\"G1\" t=\"inlineStr\"><is><r><t>bold </t></r><r><t> plain</t></r></is></c><c r=\"H1\" t=\"str\" xml:space=\"preserve\"><v> on c </v></c>" +
			"<c r=\"I1\" t=\"str\"><v>&#32;references&#32;</v></c><c r=\"J1\" t=\"str\"><v>_x0020_escapes_x0020_</v></c><c r=\"K1\" t=\"str\"><v>a  b</v></c></row>" +
			"<row r=\"2\" xml:space=\"preserve\"><c r=\"A2\" t=\"str\"><v> on row </v></c><c r=\"B2\" t=\"inlineStr\"><is><r><t xml:space=\"default\"> default </t></r></is></c></row>" +
			"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is xml:space=\"preserve\"><r><t> on is </t></r></is></c><c r=\"B3\" t=\"str\"><v>   </v></c><c r=\"C3\" t=\"b\"><v>&#32;</v></c></row>" +
			"<row r=\"4\"><c r=\"A4\" t=\"str\"><v> \r\n</v></c><c r=\"B4\" t=\"inlineStr\"><is><r><t> </t></r><r><t>\t</t></r></is></c></row>" +
			"<row r=\"5\"><c r=\"A5\" t=\"str\"><v>&#32;</v></c></row>");
		var root = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"str\"><v> under the root </v></c></row>").Replace("<worksheet ", "<worksheet xml:space=\"preserve\" ");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// Blank text is valueless, so row 4, whose texts are all blank, is dropped; a value that only references make
			// blank is an empty field, not a valueless cell, so row 5 is kept.
			Assert.Equal(
				[
					"1: number:42 | number:7 | text | error:#N/A |  kept  | inline | boldplain |  on c  | references |  escapes  | a  b",
					"2:  on row  | default | () | () | () | () | () | () | () | () | ()",
					"3:  on is  | () | () | () | () | () | () | () | () | () | ()",
					"5: () | () | () | () | () | () | () | () | () | () | ()",
				],
				await Sheets.ReadAsync(xml, mode));
			Assert.Equal(["1:  under the root "], await Sheets.ReadAsync(root, mode));
		});
	}

	[Fact]
	public async Task DecodesValues()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\">" +
			"<c r=\"A1\" t=\"str\"><v>&amp;&lt;&#233;&#x1F600;</v></c>" +
			"<c r=\"B1\" t=\"str\"><v><![CDATA[<b>&amp;]]></v></c>" +
			"<c r=\"C1\" t=\"str\"><v>a\r\nb</v></c>" +
			"<c r=\"D1\" t=\"str\"><v>_x0041__x000D_</v></c>" +
			"<c r=\"E1\" t=\"inlineStr\"><is><r><rPr><b/></rPr><t>Bold</t></r><r><t xml:space=\"preserve\"> _x0041_</t></r><rPh sb=\"0\" eb=\"1\"><t>no</t></rPh><phoneticPr fontId=\"1\"/></is></c>" +
			"<c r=\"F1\" t=\"inlineStr\"><is><r><t>_x00</t></r><r><t>41_</t></r></is></c>" +
			"<c r=\"G1\"><v>1<!-- c -->2</v></c>" +
			"<c r=\"H1\" t=\"str\"><v>_x00<![CDATA[41_]]></v></c>" +
			"</row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				[$"1: &<é{char.ConvertFromUtf32(0x1F600)} | <b>&amp; | a\nb | A\r | Bold A | _x0041_ | number:12 | A"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task LeavesValuelessCellsOut()
	{
		var strings = Sheets.Strings("", "x");
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" s=\"2\"/><c r=\"B1\"><v>1</v></c><c r=\"C1\" s=\"1\"></c><c r=\"D1\"><f>B1+1</f></c><c r=\"E1\" t=\"str\"><v></v></c><c r=\"F1\"><v>2</v></c>" +
			"<c r=\"G1\" t=\"inlineStr\"><is><t/></is></c><c r=\"H1\" t=\"s\"><v>0</v></c><c r=\"I1\"><v>   </v></c><c r=\"J1\" t=\"s\"><v></v></c><c r=\"K1\" t=\"str\"><v/></c><c r=\"L1\"/></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"s\"><v>1</v></c><c r=\"B2\" t=\"inlineStr\"><is/></c><c r=\"C2\" t=\"inlineStr\"></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: () | number:1 | () | () | () | number:2", "2: x | () | () | () | () | ()"], await Sheets.ReadAsync(xml, mode, strings: strings));
		});
	}

	[Fact]
	public async Task PlacesCellsByTheirReferences()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"3\"><c r=\"C3\"><v>1</v></c><c r=\"E3\"><v>2</v></c></row>" +
			"<row r=\"4\"><c r=\"b4\"><v>3</v></c></row>" +
			"<row r=\"7\" spans=\"1:2\"><c r=\"A7\"><v>4</v></c><c r=\"B9\"><v>5</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// A cell's row digits aren't checked against its row.
			Assert.Equal(["3: () | () | number:1 | () | number:2", "4: () | number:3 | () | () | ()", "7: number:4 | number:5 | () | () | ()"], await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task NumbersRowsAndCellsWithoutReferences()
	{
		var xml = Sheets.Worksheet(
			"<row><c><v>1</v></c><c><v>2</v></c></row>" +
			"<row><c r=\"C5\"><v>3</v></c><c><v>4</v></c></row>" +
			"<row><c><v>5</v></c></row>" +
			"<row r=\"9\"><c><v>6</v></c><c r=\"D9\"><v>7</v></c><c/><c><v>8</v></c></row>" +
			"<row><c r=\"A11\" s=\"1\"/><c><v>9</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: number:1 | number:2", "5: () | () | number:3 | number:4", "6: number:5 | ()", "9: number:6 | () | () | number:7 | () | number:8", "11: () | number:9"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task ReadsTheLastColumn()
	{
		var xml = Sheets.Worksheet("<row r=\"1\"><c r=\"XFD1\"><v>1</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var records = await Sheets.ReadRecordsAsync(xml, mode);

			Assert.Equal(CellReference.MaxColumns, records[0].Fields.Count);
			Assert.All(records[0].Fields.Take(CellReference.MaxColumns - 1), f => Assert.Equal(("", SheetCellKind.Empty), f));
			Assert.Equal(("1", SheetCellKind.Number), records[0].Fields[CellReference.MaxColumns - 1]);
			Assert.Equal(CellReference.MaxColumns, records[1].Fields.Count);
		});
	}

	[Fact]
	public async Task PadsRowsToTheFirstRecordWithAValue()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" s=\"1\"/></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"str\"><v>a</v></c><c r=\"B2\" t=\"str\"><v>b</v></c><c r=\"C2\" t=\"str\"><v>c</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><v>1</v></c></row>" +
			"<row r=\"4\"><c r=\"A4\"><v>1</v></c><c r=\"D4\"><v>4</v></c></row>" +
			"<row r=\"5\"><c r=\"B5\"><v>2</v></c><c r=\"C5\" s=\"1\"/><c r=\"D5\" s=\"1\"/></row>" +
			"<row r=\"6\"/>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			string[] rows = ["2: a | b | c", "3: number:1 | () | ()", "4: number:1 | () | () | number:4", "5: () | number:2 | ()"];
			Assert.Equal(rows, await Sheets.ReadAsync(xml, mode));

			// Rows without a value are one empty field, and aren't padded.
			Assert.Equal(["1: ()", .. rows, "6: ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false));
		});
	}

	[Fact]
	public async Task KeepsEveryRowWhenEmptyRowsAreKept()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"2\"><c r=\"A2\"><v>1</v></c></row><row r=\"5\"><c r=\"A5\" s=\"1\"/></row><row r=\"6\"/><row><c r=\"B9\"><v>2</v></c></row><row/>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["2: number:1", "9: () | number:2"], await Sheets.ReadAsync(xml, mode));
			Assert.Equal(
				["1: ()", "2: number:1", "3: ()", "4: ()", "5: ()", "6: ()", "7: ()", "8: ()", "9: () | number:2", "10: ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false));
		});
	}

	[Fact]
	public async Task ReadsNoRecordsWithoutRows()
	{
		var ns = TestPackage.MainNamespace;
		var sheets = new[]
		{
			$"<worksheet xmlns=\"{ns}\"><sheetPr/><dimension ref=\"A1\"/></worksheet>",
			$"<worksheet xmlns=\"{ns}\"/>",
			Sheets.Worksheet(""),
			Sheets.Worksheet("").Replace("<sheetData></sheetData>", "<sheetData/>"),
			Sheets.Worksheet("<row r=\"3\"/><row r=\"4\"><c r=\"A4\" s=\"1\"/></row>"),
		};

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var xml in sheets.Take(4))
			{
				Assert.Empty(await Sheets.ReadAsync(xml, mode));
				Assert.Empty(await Sheets.ReadAsync(xml, mode, skipEmptyRows: false));
			}

			Assert.Empty(await Sheets.ReadAsync(sheets[4], mode));
		});
	}

	[Fact]
	public async Task ReadsNothingAfterSheetData()
	{
		// Markup the tokenizer rejects as soon as it reaches it.
		var xml = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>").Replace("<pageMargins", "<!not XML><pageMargins");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1"], await Sheets.ReadAsync(xml, mode));
		});

		// And it isn't inflated: a stream of one byte per read is read no further than the window's first refill after it.
		var tail = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>").Replace("<pageMargins", string.Concat(Enumerable.Repeat("<extLst/>", 1000)) + "<pageMargins");
		var bytes = System.Text.Encoding.UTF8.GetBytes(tail);
		var stream = new TrickleStream(bytes);
		using (var sheet = new SheetScanner(new XmlScanner(new XmlWindow(stream, Sheets.PartName, 1 << 20, 16)), "Data", SharedStrings.Empty, StyleTable.Empty, true, null))
		{
			var captured = new Captured(int.MaxValue);
			await Sheets.FillAllAsync(sheet, captured, false);
			Assert.Single(captured.Records);
		}

		Assert.InRange(stream.Delivered, 0, tail.IndexOf("</sheetData>") + "</sheetData>".Length + 16);
	}

	[Fact]
	public async Task SkipsWhatIsNotARowOrACell()
	{
		var xml = Sheets.Worksheet(
			"\r\n  <row r=\"1\">\r\n    <c r=\"A1\"><v>1</v></c>\r\n    <extLst><ext uri=\"x\"><c r=\"B1\"><v>no</v></c></ext></extLst>\r\n    <c r=\"C1\"><v>3</v></c>\r\n  </row>\r\n" +
			"<unknown><row r=\"5\"><c r=\"A5\"><v>no</v></c></row></unknown>" +
			"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" ref=\"A2:A3\" si=\"0\">A1+1</f><v>2</v><extLst><ext><v>no</v></ext></extLst></c><c r=\"B2\"><unknown><v>no</v></unknown><v>4</v></c><c r=\"C2\">text<v>5</v>text</c></row>" +
			"<row r=\"3\"><c r=\"A3\" t=\"s\"><is><t>no</t></is></c><c r=\"B3\" t=\"str\"><is><t>no</t></is></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: number:1 | () | number:3", "2: number:2 | number:4 | number:5"], await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task MatchesLocalNamesInEitherNamespace()
	{
		var prefixed =
			$"<x:worksheet xmlns:x=\"{TestPackage.MainNamespace}\"><x:sheetData><x:row r=\"1\"><x:c r=\"A1\" t=\"inlineStr\"><x:is><x:r><x:t>a</x:t></x:r></x:is></x:c>" +
			"<x:c r=\"B1\"><x:v>1</x:v></x:c></x:row></x:sheetData></x:worksheet>";
		var strict = $"<worksheet xmlns=\"{TestPackage.StrictMainNamespace}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"str\"><v>a</v></c><c r=\"B1\"><v>1</v></c></row></sheetData></worksheet>";

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: a | number:1"], await Sheets.ReadAsync(prefixed, mode));
			Assert.Equal(["1: a | number:1"], await Sheets.ReadAsync(strict, mode));
		});
	}

	[Fact]
	public async Task ThrowsForRowsOutOfOrder()
	{
		var cases = new (string Rows, string At, string Location, string Detail)[]
		{
			("<row r=\"5\"><c r=\"A5\"><v>1</v></c></row><row r=\"3\"><c r=\"A3\"><v>2</v></c></row>", "<row r=\"3\"", "Data!3:3", "it is not after the row before it, 5: rows must be in order"),
			("<row r=\"5\"><c r=\"A5\"><v>1</v></c></row><row r=\"5\"/>", "<row r=\"5\"/>", "Data!5:5", "it is not after the row before it, 5: rows must be in order"),
			("<row r=\"5\"/><row><c r=\"A4\"><v>2</v></c></row>", "<c r=\"A4\"", "Data!4:4", "it is not after the row before it, 5: rows must be in order"),
		};

		foreach (var (rows, at, location, detail) in cases)
		{
			var xml = Sheets.Worksheet(rows);
			await Sheets.InEveryModeAsync(async mode =>
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
				Assert.Equal($"Row {location} can't be read at byte {xml.IndexOf(at)} of xl/worksheets/sheet1.xml: {detail}.", e.Message);
				Assert.Equal(location, e.Location);
				Assert.Equal(long.Parse(location.Substring(location.IndexOf(':') + 1)), e.LineNumber);
			});
		}
	}

	[Fact]
	public async Task ThrowsForCellsOutOfOrder()
	{
		var cases = new (string Rows, string At, string Location, string Before)[]
		{
			("<row r=\"1\"><c r=\"C1\"><v>1</v></c><c r=\"B1\"><v>2</v></c></row>", "<c r=\"B1\"", "Data!B1", "C1"),
			("<row r=\"2\"><c r=\"C2\"/><c r=\"C2\"><v>2</v></c></row>", "<c r=\"C2\"><v>", "Data!C2", "C2"),
			("<row r=\"3\"><c><v>1</v></c><c><v>2</v></c><c r=\"A3\"><v>3</v></c></row>", "<c r=\"A3\"", "Data!A3", "B3"),
		};

		foreach (var (rows, at, location, before) in cases)
		{
			var xml = Sheets.Worksheet(rows);
			await Sheets.InEveryModeAsync(async mode =>
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
				Assert.Equal($"Cell {location} can't be read at byte {xml.IndexOf(at)} of xl/worksheets/sheet1.xml: it is not after the cell before it, {before}: a row's cells must be in column order.", e.Message);
				Assert.Equal(location, e.Location);
				Assert.Equal(long.Parse(location.Substring(6)), e.LineNumber);
			});
		}
	}

	[Fact]
	public async Task ThrowsForReferencesOutsideTheSheet()
	{
		var cells = new[] { "A0", "XFE1", "1A", "", "A1048577", "AAAA1", "$A$1", "A1:B2" };
		foreach (var reference in cells)
		{
			var xml = Sheets.Worksheet($"<row r=\"7\"><c r=\"{reference}\"><v>1</v></c></row>");
			await Sheets.InEveryModeAsync(async mode =>
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
				Assert.Equal($"Row Data!7:7 can't be read at byte {xml.IndexOf("<c r=")} of xl/worksheets/sheet1.xml: the attribute r=\"{reference}\" is not a cell reference (A1 to XFD1048576).", e.Message);
				Assert.Equal("Data!7:7", e.Location);
			});
		}

		var rows = new (string Rows, string At, string Detail)[]
		{
			("<row r=\"0\"/>", "<row r=", "its row number, 0, is not one of a sheet's (1 to 1048576)"),
			("<row r=\"1048577\"><c><v>1</v></c></row>", "<row r=", "its row number, 1048577, is not one of a sheet's (1 to 1048576)"),
			("<row r=\"1048576\"><c><v>1</v></c></row><row><c><v>2</v></c></row>", "<c><v>2", "its row number, 1048577, is not one of a sheet's (1 to 1048576)"),
			("<row r=\"x\"/>", "x\"/>", "the attribute r=\"x\" is not an integer"),
		};

		foreach (var (sheetRows, at, detail) in rows)
		{
			var xml = Sheets.Worksheet(sheetRows);
			await Sheets.InEveryModeAsync(async mode =>
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
				Assert.Equal($"Sheet Data can't be read at byte {xml.IndexOf(at)} of xl/worksheets/sheet1.xml: {detail}.", e.Message);
				Assert.Null(e.Location);
				Assert.Equal(0, e.LineNumber);
			});
		}

		var past = Sheets.Worksheet("<row r=\"1\"><c r=\"XFD1\"><v>1</v></c><c><v>2</v></c></row>");
		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(past, mode));
			Assert.Equal($"Row Data!1:1 can't be read at byte {past.IndexOf("<c><v>2")} of xl/worksheets/sheet1.xml: it is past the last column, XFD.", e.Message);
		});
	}

	[Fact]
	public async Task ThrowsForSharedStringsThatAreNotThere()
	{
		var cases = new (string Value, SharedStrings Strings, string Detail)[]
		{
			("1", Sheets.Strings("a"), "it is shared string 1, past the last of test.xml, 0"),
			("2", Sheets.Strings(), "it is shared string 2, and test.xml has no strings"),
			("0", SharedStrings.Empty, "it is shared string 0, and the workbook has no shared strings"),
			("x", Sheets.Strings("a"), "its value, \"x\", is not a shared string index"),
			("-1", Sheets.Strings("a"), "its value, \"-1\", is not a shared string index"),
			("1.0", Sheets.Strings("a", "b"), "its value, \"1.0\", is not a shared string index"),
		};

		foreach (var (value, strings, detail) in cases)
		{
			var xml = Sheets.Worksheet($"<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"B2\" t=\"s\"><v>{value}</v></c></row>");
			await Sheets.InEveryModeAsync(async mode =>
			{
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, strings: strings));
				Assert.Equal($"Cell Data!B2 can't be read at byte {xml.IndexOf("<c r=\"B2\"")} of xl/worksheets/sheet1.xml: {detail}.", e.Message);
				Assert.Equal("Data!B2", e.Location);
				Assert.Equal(2, e.LineNumber);
			});
		}

		var trimmed = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"s\"><v> 1 </v></c></row>");
		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: b"], await Sheets.ReadAsync(trimmed, mode, strings: Sheets.Strings("a", "b")));
		});
	}

	[Fact]
	public async Task ThrowsForXmlThatCantBeReadAtTheCellOrRow()
	{
		var reference = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"str\"><v>1&nbsp;</v></c></row>");
		var truncated = Sheets.Worksheet("<row r=\"2\"><c r=\"B2\"><v>1</v></c></row>");
		truncated = truncated.Substring(0, truncated.IndexOf("</v>") + 2);
		var between = Sheets.Worksheet("<row r=\"3\"><c r=\"A3\"><v>1</v></c><!-- no end");
		between = between.Substring(0, between.IndexOf("<!-- no end") + 11);
		var root = $"<chartsheet xmlns=\"{TestPackage.MainNamespace}\"><sheetPr/></chartsheet>";
		var doctype = TestPackage.Declaration + "<!DOCTYPE worksheet>" + Sheets.Worksheet("").Substring(TestPackage.Declaration.Length);

		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(reference, mode));
			Assert.Equal($"Cell Data!A1 can't be read at byte {reference.IndexOf('&')} of xl/worksheets/sheet1.xml: '&nbsp;' is neither a predefined entity nor a valid character reference.", e.Message);
			Assert.Equal("Data!A1", e.Location);
			Assert.Equal(1, e.LineNumber);
			Assert.IsType<InvalidDataException>(e.InnerException);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(truncated, mode));
			Assert.StartsWith($"Cell Data!B2 can't be read at byte {truncated.Length - 2} of xl/worksheets/sheet1.xml: it ends inside", e.Message);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(between, mode));
			Assert.Equal($"Row Data!3:3 can't be read at byte {between.IndexOf("<!--")} of xl/worksheets/sheet1.xml: it ends inside a comment.", e.Message);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(root, mode));
			Assert.Equal("Sheet Data can't be read at byte 0 of xl/worksheets/sheet1.xml: its root element is not worksheet.", e.Message);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(doctype, mode));
			Assert.Equal($"Sheet Data can't be read at byte {TestPackage.Declaration.Length} of xl/worksheets/sheet1.xml: it has a DOCTYPE, which Office Open XML parts must not have.", e.Message);
		});
	}

	[Fact]
	public async Task QuotesTheSheetNameInLocations()
	{
		var xml = Sheets.Worksheet("<row r=\"1\"><c r=\"C1\"><v>1</v></c><c r=\"B1\"><v>2</v></c></row>");

		foreach (var (name, quoted) in new[] { ("Q1 Orders", "'Q1 Orders'"), ("Bob's", "'Bob''s'"), ("A1", "'A1'"), ("Data_2", "Data_2") })
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, SheetMode.All[0], sheetName: name));
			Assert.Equal($"{quoted}!B1", e.Location);
			Assert.StartsWith($"Cell {quoted}!B1 can't be read", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowsTheSameErrorAgain(bool async)
	{
		var cases = new[]
		{
			Sheets.Worksheet("<row r=\"2\"><c r=\"A2\"><v>1</v></c></row><row r=\"1\"/>"),
			Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\" t=\"str\"><v>{new string('x', 1000)}</v></c></row>"),
		};

		foreach (var xml in cases)
		{
			using var sheet = Sheets.Open(xml, ScanMode.All[async ? 1 : 0], maxSize: 256);
			var target = new CapturingTarget(new Captured(int.MaxValue));
			var first = await Record.ExceptionAsync(async () => _ = async ? await sheet.FillAsync(target, CancellationToken.None) : sheet.Fill(target));
			var second = await Record.ExceptionAsync(async () => _ = async ? await sheet.FillAsync(target, CancellationToken.None) : sheet.Fill(target));

			Assert.True(first is CsvFormatException or InvalidDataException, first?.ToString());
			Assert.Same(first, second);
		}
	}

	[Fact]
	public async Task KeepsLimitsAsInvalidDataException()
	{
		var xml = Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\" t=\"str\"><v>{new string('x', 1000)}</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => Sheets.ReadAsync(xml, mode, maxSize: 256));
			Assert.EndsWith("the token or cell there is larger than ExcelReaderOptions.MaxPartSize (256 bytes).", e.Message);

			// A cell as large as the window can grow to still reads.
			Assert.Single(await Sheets.ReadAsync(xml, mode, maxSize: 2048));
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsGapFieldsTowardTheUncompressedSize(bool async)
	{
		var rows = string.Concat(Enumerable.Range(1, 20).Select(n => $"<row r=\"{n}\"><c r=\"XFD{n}\"><v>1</v></c></row>"));
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet(rows)));
		var size = package.Text(Sheets.PartName).Length;
		var gaps = 20L * (CellReference.MaxColumns - 1);

		var (records, total) = await Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, 1L << 30));
		Assert.Equal(20, records.Count);
		Assert.Equal(size + gaps, total);

		await Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, size + gaps));
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, size + gaps - 1)));
		Assert.Equal($"Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({size + gaps - 1} bytes uncompressed).", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsPaddingAndMissingRowsTowardTheUncompressedSize(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"E1\"><v>5</v></c></row><row r=\"10\"><c r=\"A10\"><v>1</v></c></row>")));
		var size = package.Text(Sheets.PartName).Length;

		// 3 gap fields in row 1, 8 missing rows, and 4 fields of padding in row 10.
		var (records, total) = await Sheets.ReadPackageAsync(package, async, new PartLimits(1 << 20, 1L << 30), skipEmptyRows: false);
		Assert.Equal(10, records.Count);
		Assert.Equal(size + 15, total);

		// A million missing rows stop at the limit, not after they are all written.
		var far = TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet("<row r=\"1048576\"><c r=\"A1048576\"><v>1</v></c></row>")));
		var farSize = far.Text(Sheets.PartName).Length;
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Sheets.ReadPackageAsync(far, async, new PartLimits(1 << 20, farSize + 1000), skipEmptyRows: false));
		Assert.StartsWith("Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
		Assert.Single((await Sheets.ReadPackageAsync(far, async, new PartLimits(1 << 20, farSize + 1000))).Records);
	}
}
