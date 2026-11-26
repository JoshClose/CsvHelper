using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Segment mode (<see cref="SheetScanner.CreateSegmentScanner"/>, <see cref="SheetSegmentScanner"/>): a worker's scan of a
/// segment, its rows floating or anchored, how each segment ends and each mark; when the segments can begin
/// (<see cref="SheetScanner.CanStartSegments"/>) and how the sheet's scanner reads on after them
/// (<see cref="SheetScanner.ResumeAt"/>); and reads through segments (<see cref="SegmentRun"/>) that cut across missing,
/// hidden and out-of-order rows, areas and prefixes, which read as the serial read does. Each worker test runs with the fast
/// paths on and off.
/// </summary>
public class SegmentScannerTests
{
	/// <summary>A row that sets the width to two fields, and lets segments begin after it.</summary>
	private const string Head = "<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"B1\"><v>2</v></c></row>";

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void FloatsASegmentWhoseFirstRowHasNoReference(bool fast)
	{
		using var begun = Begun.Open(Head, fast);
		var (result, records) = begun.Scan("<row><c><v>3</v></c></row>\n<row><c><v>4</v></c><c><v>5</v></c></row><row/><row><c/></row>\n");

		Assert.Equal(SegmentEnd.Done, result.End);
		Assert.Equal(0, result.FirstRow);
		Assert.Equal(4, result.LastRow);
		Assert.Equal(["1: number:3 | ()", "2: number:4 | number:5"], records);

		// Kept, the rows without a value are records of one empty field, numbered from the segment's start too.
		using var kept = Begun.Open(Head, fast, skipEmptyRows: false);
		Assert.Equal(["1: number:3 | ()", "2: number:4 | number:5", "3: ()", "4: ()"], kept.Scan("<row><c><v>3</v></c></row><row><c><v>4</v></c><c><v>5</v></c></row><row/><row><c/></row>").Records);

		// Rows without cells, numbered at their end tags, float too: first, between rows and last.
		var withoutCells = kept.Scan("<row></row><row><c><v>2</v></c></row><row><extLst/></row><row spans=\"1:2\"></row>");
		Assert.Equal(SegmentEnd.Done, withoutCells.Result.End);
		Assert.Equal((0L, 4L), (withoutCells.Result.FirstRow, withoutCells.Result.LastRow));
		Assert.Equal(["1: ()", "2: number:2 | ()", "3: ()", "4: ()"], withoutCells.Records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AnchorsASegmentAtItsFirstRowsReference(bool fast)
	{
		const string Rows = "<row r=\"7\"><c r=\"A7\"><v>3</v></c></row><row><c><v>4</v></c></row><row r=\"11\"><c><v>5</v></c></row>";
		using var begun = Begun.Open(Head, fast, skipEmptyRows: false);
		var (result, records) = begun.Scan(Rows);

		// No rows missing before the first are written (the stitcher writes them); those between its rows are the worker's.
		Assert.Equal(SegmentEnd.Done, result.End);
		Assert.Equal(7, result.FirstRow);
		Assert.Equal(11, result.LastRow);
		Assert.Equal(["7: number:3 | ()", "8: number:4 | ()", "9: ()", "10: ()", "11: number:5 | ()"], records);

		// A row without r anchors the segment by its first cell's r: the tokenizer's path's cells and the fast path's alike.
		foreach (var first in new[] { "<row><c r=\"B9\"><v>3</v></c></row>", "<row><c t=\"str\" r=\"B9\"><v>x</v></c></row>", "<row spans=\"1:2\"><c r=\"B9\" s=\"0\"><v>3</v></c></row>" })
		{
			var anchored = begun.Scan(first + "<row><c><v>4</v></c></row>");
			Assert.Equal(SegmentEnd.Done, anchored.Result.End);
			Assert.Equal(9, anchored.Result.FirstRow);
			Assert.Equal(10, anchored.Result.LastRow);
		}

		// With empty rows skipped, the missing rows are no records.
		using var skipped = Begun.Open(Head, fast);
		Assert.Equal(["7: number:3 | ()", "8: number:4 | ()", "11: number:5 | ()"], skipped.Scan(Rows).Records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void WritesTheMissingRowsInTheAreaAfterAFirstRowAboveIt(bool fast)
	{
		// A held header sets the width, so segments can begin before any row of the area, and one can start above it.
		var reading = new CellReading(layout: SheetLayout.Of(ExcelRange.Parse("A5:B10")), held: HeldRecord.Names(["a", "b"], 0, 2, 0));
		using var begun = Begun.Open("<row r=\"1\"/>", fast, skipEmptyRows: false, reading: reading);
		var (result, records) = begun.Scan("<row r=\"2\"><c><v>1</v></c></row><row r=\"7\"><c><v>2</v></c></row><row r=\"9\"/>");

		Assert.Equal((2L, 9L), (result.FirstRow, result.LastRow));
		Assert.Equal(["5: ()", "6: ()", "7: number:2 | ()", "8: ()", "9: ()"], records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void KeepsTheWhiteSpaceThatSheetDatasSpaceKeeps(bool fast)
	{
		const string Rows = "<row r=\"1\"><c r=\"A1\" t=\"str\"><v> a </v></c></row><row r=\"2\"/>";
		using var preserved = Begun.Open(Rows, fast, xml: Sheets.Worksheet(Rows).Replace("<sheetData>", "<sheetData xml:space=\"preserve\">"));
		Assert.Equal(["3:  b "], preserved.Scan("<row r=\"3\"><c t=\"str\"><v> b </v></c></row>").Records);
		using var root = Begun.Open(Rows, fast, xml: Sheets.Worksheet(Rows).Replace("<worksheet ", "<worksheet xml:space=\"preserve\" "));
		Assert.Equal(["3:  b "], root.Scan("<row r=\"3\"><c t=\"str\"><v> b </v></c></row>").Records);
		using var trimmed = Begun.Open(Rows, fast);
		Assert.Equal(["3: b"], trimmed.Scan("<row r=\"3\"><c t=\"str\"><v> b </v></c></row>").Records);
	}

	[Fact]
	public void ReadsAnEmptySegment()
	{
		// The part's last bytes can be none, when nothing was carried and the stream ended or failed: the rows didn't end.
		using var begun = Begun.Open(Head, true);
		Assert.Equal(SegmentMark.Unfinished, begun.Scan("", final: true).Result.Mark);
		var (result, records) = begun.Scan("");
		Assert.Equal(SegmentEnd.Done, result.End);
		Assert.Equal((-1L, 0L), (result.FirstRow, result.LastRow));
		Assert.Empty(records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void MarksARowWithAReferenceAfterFloatingRows(bool fast)
	{
		using var begun = Begun.Open(Head, fast);

		// Even one whose number is the row after the segment's last: its order against the rows before isn't known here.
		var anchors = new[]
		{
			"<row r=\"5\"><c><v>2</v></c></row>",
			"<row r=\"2\"><c><v>2</v></c></row>",
			"<row><c r=\"A2\"><v>2</v></c></row>",
			"<row><c t=\"str\" r=\"A2\"><v>x</v></c></row>",
			"<row r=\"2\"/>",
		};
		foreach (var anchor in anchors)
		{
			var (result, _) = begun.Scan("<row><c><v>1</v></c></row>" + anchor);
			Assert.Equal(SegmentEnd.Marked, result.End);
			Assert.Equal(SegmentMark.Anchor, result.Mark);
		}

		// Rows without r after an anchored first row are the sheet's: the row before's plus one.
		var after = begun.Scan("<row r=\"5\"><c><v>1</v></c></row><row><c><v>2</v></c></row><row/><row><c><v>3</v></c></row>");
		Assert.Equal(["5: number:1 | ()", "6: number:2 | ()", "8: number:3 | ()"], after.Records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void MarksAFloatingRowUnderAnAreaThatBoundsRows(bool fast)
	{
		using var area = Begun.Open("<row r=\"2\"><c r=\"A2\"><v>1</v></c></row><row r=\"3\"/>", fast, reading: CellReading.Area("A2:C100"));
		var (result, _) = area.Scan("<row><c><v>1</v></c></row>");
		Assert.Equal(SegmentMark.FloatingArea, result.Mark);
		Assert.Equal(SegmentEnd.Done, area.Scan("<row r=\"9\"><c><v>1</v></c></row><row><c><v>2</v></c></row>").Result.End);

		// The whole sheet's layout, which skipping hidden rows makes, bounds none.
		using var hidden = Begun.Open(Head, fast, reading: CellReading.Hidden(rows: true, columns: false));
		var floating = hidden.Scan("<row><c><v>1</v></c></row><row hidden=\"1\"><c><v>2</v></c></row><row><c><v>3</v></c></row>");
		Assert.Equal(SegmentEnd.Done, floating.Result.End);
		Assert.Equal(["1: number:1 | ()", "3: number:3 | ()"], floating.Records);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void EndsTheRowsAtTheEndOfSheetDataOrBelowTheArea(bool fast)
	{
		using var begun = Begun.Open(Head, fast);
		var ended = begun.Scan("<row r=\"5\"><c><v>1</v></c></row></sheetData><pageMargins left=\"0.7\"/></worksheet>", final: true);
		Assert.Equal(SegmentEnd.Ended, ended.Result.End);
		Assert.Equal((5L, 5L), (ended.Result.FirstRow, ended.Result.LastRow));
		Assert.Single(ended.Records);

		var empty = begun.Scan("\r\n</sheetData></worksheet>", final: true);
		Assert.Equal(SegmentEnd.Ended, empty.Result.End);
		Assert.Equal((-1L, 0L), (empty.Result.FirstRow, empty.Result.LastRow));

		// Neither the row below the area nor anything after it is read; nor is it the segment's last row.
		using var area = Begun.Open("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\"/>", fast, reading: CellReading.Area("A1:C6"));
		var below = area.Scan("<row r=\"5\"><c><v>1</v></c></row><row r=\"7\"><c><v>2</v></c></row><row r=\"x\"/>");
		Assert.Equal(SegmentEnd.Ended, below.Result.End);
		Assert.Equal((5L, 5L), (below.Result.FirstRow, below.Result.LastRow));
		Assert.Equal(["5: number:1"], below.Records);

		var first = area.Scan("<row r=\"9\"><c><v>1</v></c></row>");
		Assert.Equal(SegmentEnd.Ended, first.Result.End);
		Assert.Equal((9L, 0L), (first.Result.FirstRow, first.Result.LastRow));
		Assert.Empty(first.Records);
	}

	[Theory]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row><!-- note --><row r=\"6\"/>")]
	[InlineData("<row r=\"5\"><c><v>1<!-- in a value --></v></c></row>")]
	[InlineData("<row r=\"5\"><c t=\"str\"><v><![CDATA[x]]></v></c></row>")]
	[InlineData("<?pi?><row r=\"5\"><c><v>1</v></c></row>")]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row><!DOCTYPE x>")]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row><!")]
	[InlineData("<row r=\"5\" hidden=\"1\"><c><v>1</v></c><!-- skipped raw --></row>")]
	public void MarksMarkupAnywhereBeforeScanning(string segment)
	{
		foreach (var fast in new[] { true, false })
		{
			using var begun = Begun.Open(Head, fast, reading: CellReading.Hidden(rows: true, columns: false));
			var (result, records) = begun.Scan(segment);
			Assert.Equal(SegmentEnd.Marked, result.End);
			Assert.Equal(SegmentMark.Markup, result.Mark);
			Assert.Empty(records);
		}
	}

	[Fact]
	public void ReadsExclamationAndQuestionMarksInText()
	{
		using var begun = Begun.Open(Head, true);
		var (result, records) = begun.Scan("<row r=\"5\"><c t=\"str\"><v>Why? Now!</v></c><c t=\"e\"><v>#DIV/0!</v></c></row>!?");
		Assert.Equal(SegmentEnd.Done, result.End);
		Assert.Equal(["5: Why? Now! | error:#DIV/0!"], records);
	}

	[Theory]
	[InlineData("<row r=\"5\"><c><v>1</v></c>", false)]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row><row r=\"6\"><c r=\"A", false)]
	[InlineData("<row r=\"5\"><c><v>1</v></c><extLst><ext>", false)]
	[InlineData("<row r=\"5\"><c><v>1</v><extLst/></c></row><row r=\"6\" hidden=\"1\"><c><v>1</v></c>", false)]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row><row r=\"6\"><c><v>1</v></c></row></sheetDa", false)]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row>", true)]
	[InlineData("<row r=\"5\"><c><v>1</v></c></row>  ", true)]
	public void MarksASegmentThatEndsInsideARowATagOrASkip(string segment, bool final)
	{
		foreach (var fast in new[] { true, false })
		{
			using var begun = Begun.Open(Head, fast, reading: CellReading.Hidden(rows: true, columns: false));
			var (result, _) = begun.Scan(segment, final: final);
			Assert.Equal(SegmentEnd.Marked, result.End);
			Assert.Equal(SegmentMark.Unfinished, result.Mark);
		}
	}

	[Theory]
	[InlineData("<row r=\"x\"/>", "Sheet Data can't be read at byte 1008 of xl/worksheets/sheet1.xml: the attribute r=\"x\" is not an integer.")]
	[InlineData("<row r=\"5\"><c r=\"C5\"><v>1</v></c><c r=\"B5\"><v>2</v></c></row>", "Cell Data!B5 can't be read at byte 1033 of xl/worksheets/sheet1.xml: it is not after the cell before it, C5: a row's cells must be in column order.")]
	[InlineData("<row r=\"5\"><c r=\"A5\" t=\"s\"><v>7</v></c></row>", "Cell Data!A5 can't be read at byte 1011 of xl/worksheets/sheet1.xml: it is shared string 7, and the workbook has no shared strings.")]
	[InlineData("<row r=\"5\"><c r=\"A5\" a=\"<\"><v>1</v></c></row>", "Row Data!5:5 can't be read at byte 1024 of xl/worksheets/sheet1.xml: an attribute value has '<', which XML doesn't allow.")]
	[InlineData("<row r=\"5\"/><row r=\"5\"/>", "Row Data!5:5 can't be read at byte 1012 of xl/worksheets/sheet1.xml: it is not after the row before it, 5: rows must be in order.")]
	public void MarksAnError(string segment, string message)
	{
		foreach (var fast in new[] { true, false })
		{
			using var begun = Begun.Open(Head, fast);
			var (result, _) = begun.Scan(segment);
			Assert.Equal(SegmentEnd.Marked, result.End);
			Assert.Equal(SegmentMark.Error, result.Mark);
			var e = Assert.IsType<CsvFormatException>(result.Error);
			Assert.Equal(message, e.Message);
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void MarksABlockPastItsCap(bool fast)
	{
		using var begun = Begun.Open(Head, fast);

		// 15 fields of 1 byte, each with the byte after it and its four: 90. A row that takes it to 91 is past a cap of 90.
		const string Row = "<row><c><v>1</v></c><c><v>2</v></c><c><v>3</v></c><c><v>4</v></c><c><v>5</v></c></row>";
		Assert.Equal(SegmentEnd.Done, begun.Scan(Row + Row + Row, blockCap: 90).Result.End);
		var (result, _) = begun.Scan(Row + Row + Row + "<row><c><v>6</v></c></row>", blockCap: 90);
		Assert.Equal(SegmentMark.BlockCap, result.Mark);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void CountsExtrasAndMarksThemPastTheLimitLeft(bool fast)
	{
		var strings = Sheets.Strings("short", new string('x', 100));

		// A gap to XFD, a shared string past its cell's XML, the padding to the width, and the rows missing between rows.
		const string Gap = "<row r=\"3\"><c r=\"XFD3\"><v>1</v></c></row>";
		const string Shared = "<row r=\"4\"><c r=\"A4\" t=\"s\"><v>1</v></c></row>";
		var beyond = 100 - "<c r=\"A4\" t=\"s\"><v>1</v>".Length;
		using (var begun = Begun.Open(Head, fast, strings: strings))
		{
			var (result, _) = begun.Scan(Gap + Shared);
			Assert.Equal(SegmentEnd.Done, result.End);
			Assert.Equal(CellReference.MaxColumns - 1 + beyond + 1, result.Extras);
		}

		using (var kept = Begun.Open(Head, fast, skipEmptyRows: false, strings: strings))
		{
			// Row 5's padding, rows 6 to 8, and row 9's padding: none before row 5, which are the stitcher's.
			Assert.Equal(1 + 3 + 1, kept.Scan("<row r=\"5\"><c><v>1</v></c></row><row r=\"9\"><c><v>1</v></c></row>").Result.Extras);
		}

		using (var limited = Begun.Open(Head, fast, strings: strings, extrasLimit: CellReference.MaxColumns - 1))
		{
			Assert.Equal(SegmentEnd.Done, limited.Scan(Gap).Result.End);
			Assert.Equal(SegmentMark.ExtrasCap, limited.Scan(Gap + Shared).Result.Mark);

			// Each segment counts from 0.
			Assert.Equal(CellReference.MaxColumns - 1, limited.Scan(Gap).Result.Extras);
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void StartsEverySegmentAfresh(bool fast)
	{
		using var begun = Begun.Open(Head, fast, skipEmptyRows: false);

		// A segment left inside a cell, in a skip, in a floating segment, after an error and after the rows' end.
		var leftovers = new[]
		{
			"<row r=\"5\"><c><v>1</v></c><c><v>",
			"<row r=\"5\"><extLst><ext>",
			"<row><c><v>1</v></c></row>",
			"<row r=\"5\"/><row r=\"3\"/>",
			"</sheetData>",
			"<row r=\"900\"/>",
		};
		foreach (var leftover in leftovers)
		{
			begun.Scan(leftover);
			var (result, records) = begun.Scan("<row r=\"3\"><c><v>3</v></c></row><row r=\"5\"/>");
			Assert.Equal(SegmentEnd.Done, result.End);
			Assert.Equal((3L, 5L), (result.FirstRow, result.LastRow));
			Assert.Equal(["3: number:3 | ()", "4: ()", "5: ()"], records);
			Assert.Equal(2, result.Extras);
		}

		// The cells the tokenizer's path read are the segment's own.
		var cells = begun.Scan("<row r=\"3\"><c><v>3</v></c><c r=\"C3\"><v>3</v></c></row>").Result.FallbackCells;
		Assert.Equal(fast ? 0 : 2, cells);
		Assert.Equal(cells, begun.Scan("<row r=\"3\"><c><v>3</v></c><c r=\"C3\"><v>3</v></c></row>").Result.FallbackCells);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ReadsPrefixedSheets(bool fast)
	{
		var ns = TestPackage.MainNamespace;
		var xml = $"<x:worksheet xmlns:x=\"{ns}\"><x:sheetData><x:row r=\"1\"><x:c r=\"A1\"><x:v>1</x:v></x:c><x:c r=\"B1\"><x:v>2</x:v></x:c></x:row><x:row r=\"2\"/></x:sheetData></x:worksheet>";
		using var begun = Begun.Open(Head, fast, xml: xml);
		Assert.Equal("<x:row"u8.ToArray(), begun.Sheet.RowStart);

		var anchored = begun.Scan("<x:row><x:c r=\"A5\"><x:v>1</x:v></x:c></x:row><x:row><x:c><x:v>2</x:v></x:c></x:row>");
		Assert.Equal((5L, 6L), (anchored.Result.FirstRow, anchored.Result.LastRow));
		var floating = begun.Scan("<x:row><x:c><x:v>1</x:v></x:c></x:row><x:row><x:c r=\"B2\"><x:v>2</x:v></x:c></x:row>");
		Assert.Equal(SegmentMark.Anchor, floating.Result.Mark);
	}

	[Fact]
	public void CanStartSegmentsBetweenRowsOnceTheWidthIsSet()
	{
		using var archive = SegmentReads.OpenBudget();
		var xml = Sheets.Worksheet("<row r=\"1\"/><row r=\"2\"><c r=\"A2\"><v>1</v></c></row><row r=\"5\"><c r=\"A5\"><v>2</v></c></row><row r=\"6\"/>");
		using var sheet = Sheets.Open(xml, ScanMode.All[0], skipEmptyRows: false, budget: SegmentReads.Budget(archive));
		var captured = new Captured(1);
		var target = new CapturingTarget(captured);
		Assert.False(sheet.CanStartSegments);

		// Row 1, without a value, sets no width; row 2 does; missing rows 3 and 4, each a fill of its own, leave gaps to
		// write, and then row 5 waiting after them.
		var can = new List<bool>();
		for (var fill = 0; fill < 6; fill++)
		{
			captured.StartFill();
			Assert.True(sheet.Fill(target));
			can.Add(sheet.CanStartSegments);
		}

		Assert.Equal([false, true, false, false, true, true], can);
		Assert.Equal(["1: ()", "2: number:1", "3: ()", "4: ()", "5: number:2", "6: ()"], captured.Records.Select(r => r.ToString()));
		Assert.Equal(6, sheet.LastRow);
		Assert.True(sheet.KeepsMissingRows);
		Assert.NotNull(sheet.Budget);
		Assert.Equal("<row"u8.ToArray(), sheet.RowStart);
		Assert.Equal(xml.IndexOf("</sheetData>"), sheet.Position);
		Assert.Equal(Encoding.UTF8.GetBytes(xml.Substring(xml.IndexOf("</sheetData>"))), sheet.Unread.ToArray());

		// After the rows, never.
		captured.StartFill();
		Assert.False(sheet.Fill(target));
		Assert.False(sheet.CanStartSegments);
	}

	[Fact]
	public void CantStartSegmentsWhereTheRowsStateGoesOnOrWithoutABudget()
	{
		var rows = "<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row><row r=\"3\"/>";
		var xml = Sheets.Worksheet(rows);
		Assert.True(AfterFirstFill(xml, CellReading.Default));
		Assert.False(AfterFirstFill(xml, CellReading.FormulaText));
		Assert.True(AfterFirstFill(xml, CellReading.Merged()));
		Assert.False(AfterFirstFill(xml.Replace("<pageMargins", "<mergeCells count=\"1\"><mergeCell ref=\"A2:B2\"/></mergeCells><pageMargins"), CellReading.Merged()));
		Assert.False(AfterFirstFill(xml, CellReading.Default, budget: false));

		// A UTF-16 part, which the window reads through its transcoding stream.
		Assert.False(AfterFirstFill(xml, CellReading.Default, utf16: true));

		// Under an area that bounds rows, only when the next row has an r: not without one, nor when the window cuts it.
		var area = CellReading.Area("A1:C10");
		Assert.True(AfterFirstFill(xml, area));
		Assert.False(AfterFirstFill(Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row><c><v>2</v></c></row>"), area));
		Assert.False(AfterFirstFill(Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>  <!-- --><row r=\"2\"/>"), area));
		Assert.True(AfterFirstFill(Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>\r\n<row spans=\"1:3\" x:r = '2'/>"), area));
		Assert.False(AfterFirstFill(Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row xmlns:r=\"urn:r\"/>"), area));
		// A 16-byte window grows only for the tokens that need it, the root's among them: not to this row's tag.
		var longTag = Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row spans=\"1:3\" x=\"{new string('x', 2000)}\" r=\"2\"/>");
		Assert.True(AfterFirstFill(longTag, area));
		Assert.False(AfterFirstFill(longTag, area, windowSize: 16));
		Assert.True(AfterFirstFill(Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row><c><v>2</v></c></row>"), CellReading.Hidden(rows: true, columns: false)));
	}

	[Fact]
	public void CreatesWorkersOnlyWhereTheStateCanBeCopied()
	{
		using var archive = SegmentReads.OpenBudget();
		using var sheet = Sheets.Open(Sheets.Worksheet(Head), ScanMode.All[0], budget: SegmentReads.Budget(archive));
		Assert.Throws<InvalidOperationException>(() => sheet.CreateSegmentScanner(0));
		var captured = new Captured(1);
		captured.StartFill();
		Assert.True(sheet.Fill(new CapturingTarget(captured)));
		Assert.Throws<ArgumentOutOfRangeException>(() => sheet.CreateSegmentScanner(-1));
		Assert.IsType<SheetSegmentScanner>(sheet.CreateSegmentScanner(0));

		using var formulas = Sheets.Open(Sheets.Worksheet(Head), ScanMode.All[0], reading: CellReading.FormulaText, budget: SegmentReads.Budget(archive));
		captured.StartFill();
		Assert.True(formulas.Fill(new CapturingTarget(captured)));
		Assert.Throws<InvalidOperationException>(() => formulas.CreateSegmentScanner(0));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ResumesSeriallyFromASegmentsStart(bool fast)
	{
		var rows = Head + "<row r=\"3\"><c r=\"A3\"><v>3</v></c></row><row><c><v>4</v></c></row><row r=\"9\"><c><v>9</v></c></row>";
		var xml = Sheets.Worksheet(rows);
		var bytes = Encoding.UTF8.GetBytes(xml);
		var at = xml.IndexOf("<row><c><v>4");
		using var archive = SegmentReads.OpenBudget();
		using var sheet = Sheets.Open(xml, ScanMode.All[0].WithFastCells(fast), skipEmptyRows: false, budget: SegmentReads.Budget(archive));
		Assert.Throws<InvalidOperationException>(() => sheet.ResumeAt(new MemoryStream(), 0, 0, 1));
		var first = new Captured(1);
		first.StartFill();
		Assert.True(sheet.Fill(new CapturingTarget(first)));
		Assert.True(sheet.CanStartSegments);

		// As if segments had delivered rows up to 3, with missing rows kept: row 4 is the next to write.
		Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ResumeAt(new MemoryStream(), at, 0, 4));
		Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ResumeAt(new MemoryStream(), at, 3, 3));
		Assert.Throws<ArgumentNullException>(() => sheet.ResumeAt(null!, at, 3, 4));
		sheet.ResumeAt(new MemoryStream(bytes, at, bytes.Length - at), at, 3, 4);
		var captured = new Captured(int.MaxValue);
		captured.StartFill();
		Assert.False(sheet.Fill(new CapturingTarget(captured)));
		Assert.Equal(["4: number:4 | ()", "5: ()", "6: ()", "7: ()", "8: ()", "9: number:9 | ()"], captured.Records.Select(r => r.ToString()));

		// From inside the rows missing before a row: the rest of them first, then the row.
		using var again = Sheets.Open(xml, ScanMode.All[0].WithFastCells(fast), skipEmptyRows: false, budget: SegmentReads.Budget(archive));
		var head = new Captured(1);
		head.StartFill();
		Assert.True(again.Fill(new CapturingTarget(head)));
		var nine = xml.IndexOf("<row r=\"9\"");
		again.ResumeAt(new MemoryStream(bytes, nine, bytes.Length - nine), nine, 4, 7);
		var rest = new Captured(int.MaxValue);
		rest.StartFill();
		Assert.False(again.Fill(new CapturingTarget(rest)));
		Assert.Equal(["7: ()", "8: ()", "9: number:9 | ()"], rest.Records.Select(r => r.ToString()));

		// An error after it is the serial read's, at its offset in the part.
		var broken = Encoding.UTF8.GetBytes("<row r=\"2\"/></sheetData>");
		using var third = Sheets.Open(xml, ScanMode.All[0].WithFastCells(fast), budget: SegmentReads.Budget(archive));
		var one = new Captured(1);
		one.StartFill();
		Assert.True(third.Fill(new CapturingTarget(one)));
		third.ResumeAt(new MemoryStream(broken), 500, 3, 4);
		var e = Assert.Throws<CsvFormatException>(() => third.Fill(new CapturingTarget(new Captured(int.MaxValue))));
		Assert.Equal("Row Data!2:2 can't be read at byte 500 of xl/worksheets/sheet1.xml: it is not after the row before it, 3: rows must be in order.", e.Message);
	}

	/// <summary>Sheets whose segments cut across what goes from row to row, each read as the serial read reads it in every way of cutting.</summary>
	/// <returns>Each sheet's name, whether empty rows are skipped, and its rows.</returns>
	public static TheoryData<string, bool, string> AcrossCuts => new()
	{
		{ "hidden rows", false, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\" hidden=\"1\"><c><v>2</v></c></row><row r=\"5\" hidden=\"1\"/><row hidden=\"true\"><c><v>6</v></c></row><row><c><v>7</v></c></row>" },
		{ "missing rows, kept", false, "<row r=\"2\"><c r=\"A2\"><v>2</v></c></row><row r=\"6\"><c><v>6</v></c></row><row r=\"7\"/><row r=\"12\"><c r=\"B12\"><v>12</v></c></row>" },
		{ "missing rows, skipped", true, "<row r=\"2\"><c r=\"A2\"><v>2</v></c></row><row r=\"6\"><c><v>6</v></c></row><row r=\"7\"/><row r=\"12\"><c r=\"B12\"><v>12</v></c></row>" },
		{ "rows without r", false, "<row><c><v>1</v></c></row><row><c><v>2</v></c><c><v>2</v></c></row><row/><row><c/></row><row><c><v>5</v></c></row><row><c><v>6</v></c></row>" },
		{ "mixed", false, "<row><c><v>1</v></c></row><row r=\"4\"><c><v>4</v></c></row><row><c><v>5</v></c></row><row><c r=\"A9\"><v>9</v></c></row><row><c><v>10</v></c></row>" },
		{ "out of order across a cut", true, "<row r=\"2\"><c><v>2</v></c></row><row r=\"6\"><c><v>6</v></c></row><row r=\"4\"><c><v>4</v></c></row><row r=\"9\"><c><v>9</v></c></row>" },
		{ "an anchor after floating rows", true, "<row><c><v>1</v></c></row><row><c><v>2</v></c></row><row><c><v>3</v></c></row><row r=\"2\"><c><v>2</v></c></row>" },
		{ "past the last row", true, "<row r=\"1048575\"><c><v>1</v></c></row><row><c><v>2</v></c></row><row><c><v>3</v></c></row>" },
		{ "the last row", true, "<row r=\"1048574\"><c><v>1</v></c></row><row><c><v>2</v></c></row><row><c><v>3</v></c></row>" },
		{ "a cell past XFD", true, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\"><c r=\"XFD2\"><v>1</v></c><c><v>2</v></c></row>" },
		{ "a malformed row", true, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\"><c><v>2</v></c></row><row r=\"3\" a=\"<\"/><row r=\"4\"/>" },
		{ "markup between rows", true, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\"><c><v>2</v></c></row><!-- <row r=\"3\"> --><row r=\"4\"><c><v>4</v></c></row>" },
		{ "a comment across cuts", true, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\"><c><v>2<!-- <row r=\"5\"><c><v>5</v></c></row> --></v></c></row><row r=\"6\"><c><v>6</v></c></row>" },
		{ "a row in an element skipped", true, "<row r=\"1\"><c><v>1</v></c></row><extLst><row r=\"5\"><c><v>5</v></c></row></extLst><row r=\"2\"><c><v>2</v></c></row>" },
		{ "a nested row", true, "<row r=\"1\"><c><v>1</v></c></row><row r=\"2\"><extLst><row r=\"9\"/></extLst><c><v>2</v></c></row><row r=\"3\"><c><v>3</v></c></row>" },
	};

	[Theory]
	[MemberData(nameof(AcrossCuts))]
	public async Task ReadsAsTheSerialReadAcrossCuts(string name, bool skipEmptyRows, string rows)
	{
		var xml = Sheets.Worksheet(rows);
		var readings = new[] { CellReading.Default, CellReading.Hidden(rows: true, columns: true), CellReading.Area("A1:B1048576"), CellReading.Area("A1:B10") };
		foreach (var reading in readings)
		{
			var expected = await OutcomeAsync(() => Sheets.ReadAsync(xml, SheetMode.All[0], skipEmptyRows, reading: reading));
			foreach (var target in new[] { 1, 16, 40, 1000 })
			{
				foreach (var fast in new[] { true, false })
				{
					var actual = Outcome(() =>
					{
						using var archive = SegmentReads.OpenBudget();
						using var sheet = Sheets.Open(xml, ScanMode.All[0].WithFastCells(fast).WithRawSkips(fast), skipEmptyRows, reading: reading, budget: SegmentReads.Budget(archive));
						var read = new SegmentRun(sheet, Encoding.UTF8.GetBytes(xml), _ => target, minimum: 0).ReadAll(1);

						return [.. read.Records.Select(r => r.ToString())];
					});
					Assert.True(expected.SequenceEqual(actual), $"{name}, {target}-byte targets, fast paths {(fast ? "on" : "off")}:\nexpected {string.Join("\n", expected)}\nactual {string.Join("\n", actual)}");
				}
			}
		}
	}

	[Fact]
	public void JoinsSegmentsAndFallsBackWhereTheyCantBeJoined()
	{
		var rows = string.Concat(Enumerable.Range(1, 40).Select(r => $"<row r=\"{r}\"><c r=\"A{r}\"><v>{r}</v></c></row>"));
		var xml = Sheets.Worksheet(rows.Replace("<row r=\"30\">", "<!-- --><row r=\"30\">"));
		using var archive = SegmentReads.OpenBudget();
		using var sheet = Sheets.Open(xml, ScanMode.All[0], budget: SegmentReads.Budget(archive));
		var run = new SegmentRun(sheet, Encoding.UTF8.GetBytes(xml), _ => 1);
		var read = run.ReadAll(1);

		Assert.Equal(40, read.Records.Count);
		Assert.True(run.Started);
		Assert.Equal(SegmentMark.Markup, run.FellBackAt!.Result.Mark);

		// Rows 2 to 28, a segment each; row 29's has the comment.
		Assert.Equal(27, run.Stitched);
		Assert.Equal(Enumerable.Range(1, 40).Select(r => (long)r), read.Records.Select(r => r.Row));
	}

	/// <summary>Whether segments can begin after a sheet's first fill, of one record.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="reading">How its cells are read.</param>
	/// <param name="budget">Whether the scanner has a budget.</param>
	/// <param name="utf16">Whether the part is read as UTF-16.</param>
	/// <param name="windowSize">The window's starting size.</param>
	/// <returns>Whether they can.</returns>
	private static bool AfterFirstFill(string xml, CellReading reading, bool budget = true, bool utf16 = false, int windowSize = XmlWindow.DefaultSize)
	{
		using var archive = SegmentReads.OpenBudget();
		var bytes = utf16 ? [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(xml)] : Encoding.UTF8.GetBytes(xml);
		Stream stream = new MemoryStream(bytes);
		var limited = budget ? SegmentReads.Budget(archive)(stream) : null;
		var merged = reading.FillMergedCells ? Sheets.FindMergedCells(Encoding.UTF8.GetBytes(xml), ScanMode.All[0]) : null;
		using var sheet = new SheetScanner(
			new XmlScanner(new XmlWindow(limited ?? stream, Sheets.PartName, 1 << 20, windowSize)), "Data", SharedStrings.Empty, StyleTable.Empty, true, limited,
			formulas: reading.Formulas, layout: reading.Layout, skipHiddenRows: reading.SkipHiddenRows, skipHiddenColumns: reading.SkipHiddenColumns, mergedCells: merged);
		var captured = new Captured(1);
		captured.StartFill();
		Assert.True(sheet.Fill(new CapturingTarget(captured)));

		return sheet.CanStartSegments;
	}

	/// <summary>The records a read gives, or the exception it throws, described.</summary>
	/// <param name="read">The read.</param>
	/// <returns>The records, or the exception's type, message, location and line.</returns>
	private static List<string> Outcome(Func<List<string>> read)
	{
		try
		{
			return read();
		}
		catch (Exception e) when (e is CsvFormatException or InvalidDataException)
		{
			return [Describe(e)];
		}
	}

	/// <summary>The records an asynchronous read gives, or the exception it throws, described.</summary>
	/// <param name="read">The read.</param>
	/// <returns>The records, or the exception's type, message, location and line.</returns>
	private static async Task<List<string>> OutcomeAsync(Func<Task<List<string>>> read)
	{
		try
		{
			return await read();
		}
		catch (Exception e) when (e is CsvFormatException or InvalidDataException)
		{
			return [Describe(e)];
		}
	}

	/// <summary>Describes an exception as the tests compare it.</summary>
	/// <param name="e">The exception.</param>
	/// <returns>Its type, message, location and line.</returns>
	private static string Describe(Exception e) =>
		e is CsvFormatException format ? $"{e.GetType().Name}: {e.Message} at {format.Location} line {format.LineNumber}" : $"{e.GetType().Name}: {e.Message}";

	/// <summary>A sheet's scanner read up to where segments can begin, and a worker of it, which scans segments the tests give.</summary>
	private sealed class Begun : IDisposable
	{
		private readonly PackageArchive archive;
		private readonly ISegmentScanner worker;
		private readonly SegmentBlock reused = new();

		/// <summary>Creates the context.</summary>
		/// <param name="archive">The package whose guard the budget counts toward.</param>
		/// <param name="sheet">The sheet's scanner, where segments can begin.</param>
		/// <param name="worker">A worker of it.</param>
		private Begun(PackageArchive archive, SheetScanner sheet, ISegmentScanner worker)
		{
			this.archive = archive;
			Sheet = sheet;
			this.worker = worker;
		}

		/// <summary>The sheet's scanner.</summary>
		public SheetScanner Sheet { get; }

		/// <summary>Reads a sheet of <paramref name="head"/> serially, a record a fill, until segments can begin, and makes a worker.</summary>
		/// <param name="head">The rows before the segments.</param>
		/// <param name="fast">Whether the fast paths (raw skips and fast cells) are on.</param>
		/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
		/// <param name="reading">How the cells are read.</param>
		/// <param name="strings">The shared strings.</param>
		/// <param name="extrasLimit">The worker's limit of extras; <see langword="null"/> for the budget's.</param>
		/// <param name="xml">The part, for one that isn't <see cref="Sheets.Worksheet(string)"/> of the head.</param>
		/// <returns>The context.</returns>
		public static Begun Open(string head, bool fast, bool skipEmptyRows = true, CellReading? reading = null, SharedStrings? strings = null, long? extrasLimit = null, string? xml = null)
		{
			var archive = SegmentReads.OpenBudget();
			var sheet = Sheets.Open(xml ?? Sheets.Worksheet(head), ScanMode.All[0].WithFastCells(fast).WithRawSkips(fast), skipEmptyRows, strings, reading: reading, budget: SegmentReads.Budget(archive));
			var captured = new Captured(1);
			while (!sheet.CanStartSegments)
			{
				captured.StartFill();
				Assert.True(sheet.Fill(new CapturingTarget(captured)), "The sheet ended before segments could begin.");
			}

			return new Begun(archive, sheet, sheet.CreateSegmentScanner(extrasLimit ?? sheet.Budget!.Remaining));
		}

		/// <summary>
		/// Scans a segment, and describes its records. A block of the usual limit is the same one each time, as a slot's is:
		/// the scan clears it.
		/// </summary>
		/// <param name="segment">The segment's XML.</param>
		/// <param name="offset">Its offset in the part.</param>
		/// <param name="final">Whether the part ends with it.</param>
		/// <param name="blockCap">The block's limit.</param>
		/// <returns>The scan, and the records (<see cref="CapturedRecord.ToString"/>), their lines as the block has them.</returns>
		public (SegmentResult Result, List<string> Records) Scan(string segment, long offset = 1000, bool final = false, long blockCap = SegmentSizes.MaxBlock)
		{
			var bytes = Encoding.UTF8.GetBytes(segment);
			var block = blockCap == SegmentSizes.MaxBlock ? reused : new SegmentBlock(blockCap);
			var result = worker.Scan(bytes, bytes.Length, offset, final, block);
			var captured = new Captured(int.MaxValue);
			captured.StartFill();
			captured.AppendBlock(block, 0);

			return (result, [.. captured.Records.Select(r => r.ToString())]);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Sheet.Dispose();
			archive.Dispose();
		}
	}
}
