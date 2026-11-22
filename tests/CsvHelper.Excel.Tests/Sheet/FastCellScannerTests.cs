using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// What the sheet scanner's fast path (Phase 9's step 3) takes and what it leaves to the fallback, counted by
/// <see cref="SheetScanner.FallbackCells"/> in whole reads into a 64 KB window (<c>ScanMode.All[0]</c>), where a sheet under
/// 64 KB is never cut: none of a sheet of canonical cells, of every form and type, between white space, markup and other
/// elements, in rows with and without <c>r</c>, prefixed or not; exactly one for each kind of cell it declines, placed among
/// canonical ones; the cells of the rows read only for shared formulas, a merged range's top-left cell, and a cell the
/// window cuts. With the fast path off, every cell the fallback reads, each time it reads it. Each sheet reads the same
/// records both ways; <see cref="FastCellFuzzTests"/> compares them in every mode.
/// </summary>
public class FastCellScannerTests
{
	/// <summary>The window's size limit of the white space tests: as <c>ExcelReaderOptions.MaxPartSize</c> at 64 KB makes it.</summary>
	private const int WindowLimit = 64 * 1024;

	/// <summary>The shared strings: a text, an empty one, and one with white space at its ends.</summary>
	private static readonly SharedStrings Strings = Sheets.Strings("pear", "", " padded ");

	/// <summary>The start tags of cells, with or without a prefix, which a cell's count counts.</summary>
	private static readonly Regex CellTags = new("<(?:[A-Za-z_][A-Za-z0-9_.-]*:)?c[ \t\r\n/>]");

	/// <summary>
	/// Rows of canonical cells only: every type (none, <c>n</c>, <c>s</c>, <c>b</c>, <c>e</c>, <c>str</c>,
	/// <c>inlineStr</c>) with and without <c>r</c>, <c>s</c> and <c>t</c>, as <c>/&gt;</c>, <c>&gt;&lt;/c&gt;</c>, a
	/// <c>v</c> and an <c>is</c>; empty and blank values, values with references, line ends, escapes and white space at
	/// their ends; a lowercase reference; white space, line ends, comments, processing instructions, text and other elements
	/// between the cells and rows; rows without <c>r</c>, an empty row, a row whose end tag has white space, and
	/// <c>xml:space</c> on a row.
	/// </summary>
	private static readonly string CanonicalRows =
		"<row r=\"1\"><c r=\"A1\"/><c r=\"B1\" s=\"1\"/><c r=\"C1\" t=\"s\"></c><c><v>4</v></c><c r=\"E1\" s=\"2\" t=\"n\"><v>1.5</v></c>" +
		"<c r=\"F1\" t=\"s\"><v>0</v></c><c t=\"s\"><v> 2 </v></c><c r=\"H1\" t=\"s\"><v>1</v></c><c r=\"I1\" t=\"s\"><v></v></c></row>\r\n" +
		"<row r=\"2\">\r\n\t<c r=\"A2\" t=\"b\"><v>1</v></c>\r\n\t<c r=\"B2\" t=\"b\"><v>true</v></c> <c r=\"C2\" t=\"b\"><v>TRUE</v></c>" +
		"<c r=\"D2\" t=\"b\"><v> 1 </v></c><c r=\"E2\" t=\"b\"><v>yes</v></c><c t=\"b\"><v>False</v></c>\n</row>\n" +
		"<row><c t=\"e\"><v>#N/A</v></c><c r=\"B3\" t=\"e\"><v> #REF! </v></c><c t=\"str\"><v>text</v></c><c t=\"str\"><v> padded </v></c>" +
		"<c t=\"str\"><v>a&amp;b&#10;c&#x41;</v></c><c t=\"str\"><v>cr\rcrlf\r\nend</v></c><c t=\"str\"><v>_x000D__x0041_</v></c></row>" +
		"<row r=\"5\"><c r=\"a5\" t=\"inlineStr\"><is><t>inline</t></is></c><!-- a note --><c r=\"B5\" t=\"inlineStr\"><v>as v</v></c><?pi between?>" +
		"<c r=\"C5\" t=\"inlineStr\"><is><t> padded </t></is></c>stray text<c r=\"D5\" s=\"1\" t=\"inlineStr\"><is><t></t></is></c><extLst/>" +
		"<c r=\"E5\" t=\"str\"><v></v></c><c r=\"F5\"><v>&#49;2</v></c><c r=\"G5\" t=\"e\"><v></v></c></row >" +
		"<row r=\"6\"/><row r=\"7\" xml:space=\"preserve\"><c r=\"A7\" t=\"str\"><v> kept </v></c><c t=\"inlineStr\"><is><t> kept </t></is></c>" +
		"<c t=\"e\"><v> #N/A </v></c><c><v> 7 </v></c></row><row><c r=\"C8\" s=\"123456789\"><v>8</v></c></row>";

	/// <summary>Each kind of cell the fast path declines, by name, as cell <c>B2</c> (written with or without its <c>r</c>).</summary>
	public static TheoryData<string, string> Declined => new()
	{
		{ "a formula", "<c r=\"B2\"><f>1+1</f><v>2</v></c>" },
		{ "t=\"d\"", "<c r=\"B2\" t=\"d\"><v>2024-01-02</v></c>" },
		{ "a type Excel doesn't have", "<c r=\"B2\" t=\"x\"><v>other</v></c>" },
		{ "a type with white space", "<c r=\"B2\" t=\" s\"><v>0</v></c>" },
		{ "a type with a reference", "<c r=\"B2\" t=\"&#115;\"><v>0</v></c>" },
		{ "a reference with white space", "<c r=\" B2\"><v>2</v></c>" },
		{ "a reference with a reference", "<c r=\"&#66;2\"><v>2</v></c>" },
		{ "a style of 10 digits", "<c r=\"B2\" s=\"1234567890\"><v>2</v></c>" },
		{ "a style with white space", "<c r=\"B2\" s=\" 1\"><v>2</v></c>" },
		{ "xml:space on the cell", "<c r=\"B2\" t=\"str\" xml:space=\"preserve\"><v> kept </v></c>" },
		{ "cm", "<c r=\"B2\" cm=\"1\"><v>2</v></c>" },
		{ "vm", "<c r=\"B2\" vm=\"1\"><v>2</v></c>" },
		{ "ph", "<c r=\"B2\" ph=\"1\"><v>2</v></c>" },
		{ "a namespace declaration", "<c r=\"B2\" xmlns:z=\"urn:z\"><v>2</v></c>" },
		{ "another attribute first", "<c note=\"1\" r=\"B2\"><v>2</v></c>" },
		{ "another attribute last", "<c r=\"B2\" s=\"1\" t=\"n\" note=\"1\"><v>2</v></c>" },
		{ "t before s", "<c r=\"B2\" t=\"n\" s=\"1\"><v>2</v></c>" },
		{ "s before r", "<c s=\"1\" r=\"B2\"><v>2</v></c>" },
		{ "t before r", "<c t=\"n\" r=\"B2\"><v>2</v></c>" },
		{ "single quotes", "<c r='B2'><v>2</v></c>" },
		{ "white space around =", "<c r = \"B2\"><v>2</v></c>" },
		{ "white space before >", "<c r=\"B2\" ><v>2</v></c>" },
		{ "white space before />", "<c r=\"B2\" s=\"1\" />" },
		{ "two spaces before an attribute", "<c  r=\"B2\"><v>2</v></c>" },
		{ "a tab between attributes", "<c r=\"B2\"\ts=\"1\"><v>2</v></c>" },
		{ "a line feed between attributes", "<c r=\"B2\"\ns=\"1\"><v>2</v></c>" },
		{ "an empty v", "<c r=\"B2\" t=\"str\"><v/></c>" },
		{ "xml:space on v", "<c r=\"B2\" t=\"str\"><v xml:space=\"preserve\"> kept </v></c>" },
		{ "another attribute on v", "<c r=\"B2\"><v note=\"1\">2</v></c>" },
		{ "CDATA in v", "<c r=\"B2\" t=\"str\"><v><![CDATA[a<b]]></v></c>" },
		{ "a comment before v", "<c r=\"B2\"><!-- note --><v>2</v></c>" },
		{ "a comment in v", "<c r=\"B2\"><v>2<!-- note --></v></c>" },
		{ "a processing instruction in the cell", "<c r=\"B2\"><?pi x?><v>2</v></c>" },
		{ "an element after v", "<c r=\"B2\"><v>2</v><extLst/></c>" },
		{ "white space after v", "<c r=\"B2\"><v>2</v> </c>" },
		{ "white space in an empty cell", "<c r=\"B2\" s=\"1\"> </c>" },
		{ "runs", "<c r=\"B2\" t=\"inlineStr\"><is><r><t>a</t></r><r><t>b</t></r></is></c>" },
		{ "two t", "<c r=\"B2\" t=\"inlineStr\"><is><t>a</t><t>b</t></is></c>" },
		{ "a phonetic run", "<c r=\"B2\" t=\"inlineStr\"><is><t>a</t><rPh sb=\"0\" eb=\"1\"><t>x</t></rPh></is></c>" },
		{ "an empty is", "<c r=\"B2\" t=\"inlineStr\"><is/></c>" },
		{ "an empty t", "<c r=\"B2\" t=\"inlineStr\"><is><t/></is></c>" },
		{ "xml:space on t", "<c r=\"B2\" t=\"inlineStr\"><is><t xml:space=\"preserve\"> a </t></is></c>" },
		{ "is for another type", "<c r=\"B2\" t=\"str\"><is><t>x</t></is></c>" },
		{ "is without a type", "<c r=\"B2\"><is><t>x</t></is></c>" },
		{ "an end tag with white space", "<c r=\"B2\"><v>2</v ></c>" },
		{ "the cell's end tag with white space", "<c r=\"B2\"><v>2</v></c >" },
		{ "another prefix", "<y:c r=\"B2\"><y:v>2</y:v></y:c>" },
		{ "another prefix for v", "<c r=\"B2\"><y:v>2</y:v></c>" },
	};

	[Fact]
	public async Task TakesEveryCanonicalCell()
	{
		var xml = Sheet(CanonicalRows);
		var prefixed = Prefixed(xml);
		foreach (var sheet in new[] { xml, prefixed, Spaced(xml, "root"), Spaced(xml, "sheetData") })
		{
			Assert.Equal(0, await FallbackCellsAsync(sheet, true));
			Assert.Equal(CountCells(sheet), await FallbackCellsAsync(sheet, false));
			await AssertSameRecordsAsync(sheet);
		}

		// Under readings that change which cells are fields, and how they read: an area, hidden rows and columns skipped,
		// error cells as null, formulas as text.
		foreach (var reading in new[]
		{
			CellReading.Area("B2:D7"),
			CellReading.Hidden(rows: true, columns: true),
			new CellReading(errorCells: ExcelErrorCells.Null),
			CellReading.FormulaText,
		})
		{
			Assert.Equal(0, await FallbackCellsAsync(xml, true, reading));
			await AssertSameRecordsAsync(xml, reading);
		}
	}

	[Theory]
	[MemberData(nameof(Declined))]
	public async Task DeclinesOneCell(string kind, string cell)
	{
		// The cell among canonical ones, in a row with r and in one without (whose first cell numbers it), and prefixed.
		var withR = Sheet($"<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"B1\" t=\"s\"><v>0</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c>{cell}<c r=\"C2\" t=\"b\"><v>1</v></c></row><row r=\"3\"><c r=\"A3\"><v>3</v></c></row>");
		var withoutR = withR.Replace("<row r=\"2\">", "<row>");
		foreach (var xml in new[] { withR, withoutR })
		{
			var declined = await FallbackCellsAsync(xml, true);
			Assert.True(declined == 1, $"{kind}: the fast path declined {declined} cells.");
			Assert.Equal(CountCells(xml), await FallbackCellsAsync(xml, false));
			await AssertSameRecordsAsync(xml);
		}

		// Every element prefixed x: but those of y:, which stay another prefix than sheetData's.
		var prefixed = Prefixed(withR);
		var prefixedDeclined = await FallbackCellsAsync(prefixed, true);
		Assert.True(prefixedDeclined == 1, $"{kind}, prefixed: the fast path declined {prefixedDeclined} cells.");
		await AssertSameRecordsAsync(prefixed);
	}

	[Fact]
	public async Task DeclinesTheCellsOfRowsReadForSharedFormulas()
	{
		// Rows above the area, and hidden rows, are read for the shared formulas they may define when formulas read as text:
		// their cells are left to the fallback, whatever they are; the area's and the shown rows' are taken.
		var xml = Sheet(
			"<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"B1\" t=\"s\"><v>0</v></c></row>" +
			"<row r=\"2\" hidden=\"1\"><c r=\"A2\"><v>2</v></c><c r=\"B2\" t=\"str\"><v>two</v></c><c r=\"C2\" t=\"b\"><v>1</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><v>3</v></c><c r=\"B3\" t=\"inlineStr\"><is><t>three</t></is></c></row>");
		var above = CellReading.Area("A2:C3", ExcelFormulas.FormulaText);
		var hidden = CellReading.Hidden(rows: true, columns: false, formulas: ExcelFormulas.FormulaText);

		Assert.Equal(2, await FallbackCellsAsync(xml, true, above));
		Assert.Equal(3, await FallbackCellsAsync(xml, true, hidden));
		Assert.Equal(7, await FallbackCellsAsync(xml, false, above));
		Assert.Equal(7, await FallbackCellsAsync(xml, false, hidden));

		// As values, the rows above and the hidden rows are skipped whole, and their cells never read.
		Assert.Equal(0, await FallbackCellsAsync(xml, true, CellReading.Area("A2:C3")));
		Assert.Equal(5, await FallbackCellsAsync(xml, false, CellReading.Area("A2:C3")));
		Assert.Equal(0, await FallbackCellsAsync(xml, true, CellReading.Hidden(rows: true, columns: false)));
		Assert.Equal(4, await FallbackCellsAsync(xml, false, CellReading.Hidden(rows: true, columns: false)));
		foreach (var reading in new[] { above, hidden, CellReading.Area("A2:C3"), CellReading.Hidden(rows: true, columns: false) })
		{
			await AssertSameRecordsAsync(xml, reading);
		}
	}

	[Fact]
	public async Task DeclinesTheTopLeftCellsOfMergedRanges()
	{
		// With merged cells filled, a range's top-left cell is read into its store by the fallback; the range's other cells,
		// passed over, and the cells around them are taken. Without filling, the top-left cell is taken too.
		var rows =
			"<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><v>9</v></c><c r=\"C1\"><v>3</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><v>2</v></c><c r=\"B2\" t=\"str\"><v>merged</v></c><c r=\"C2\"><v>4</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><v>5</v></c><c r=\"B3\"><v>6</v></c><c r=\"C3\"><v>7</v></c></row>";
		var xml = MergedScannerTests.Worksheet(rows, "A1:B1", "B2:C3");

		Assert.Equal(2, await FallbackCellsAsync(xml, true, CellReading.Merged()));
		Assert.Equal(9, await FallbackCellsAsync(xml, false, CellReading.Merged()));
		Assert.Equal(0, await FallbackCellsAsync(xml, true));
		await AssertSameRecordsAsync(xml, CellReading.Merged());
		await AssertSameRecordsAsync(xml, CellReading.Merged("B1:C3"));
	}

	[Fact]
	public async Task DeclinesACellTheWindowCuts()
	{
		// A cell whose value crosses the first 64 KB of the part, the window's end before its first refill: the fast path
		// declines it, the fallback begins it, refills, and the fast path takes it after the refill. With the fast path off,
		// the fallback reads it twice, and counts it each time.
		var value = "123456789";
		var empty = Sheet($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t></t></is></c></row><row r=\"2\"><c r=\"A2\"><v>{value}</v></c></row>");
		var filler = XmlWindow.DefaultSize - 4 - empty.IndexOf(value, StringComparison.Ordinal);
		var xml = empty.Replace("<t></t>", $"<t>{new string('x', filler)}</t>");
		Assert.Equal(XmlWindow.DefaultSize - 4, xml.IndexOf(value, StringComparison.Ordinal));

		Assert.Equal(1, await FallbackCellsAsync(xml, true));
		Assert.Equal(3, await FallbackCellsAsync(xml, false));
		await AssertSameRecordsAsync(xml);
	}

	[Fact]
	public async Task PassesOverWhiteSpaceInARowWithoutGrowingTheWindow()
	{
		// A megabyte of white space and line ends between two cells, and before the row's end tag, under a window that can't
		// grow past 64 KB: the fast path passes over it up to each window's end, and the scanner skips the rest as it comes,
		// as without the fast path.
		var spaces = string.Concat(Enumerable.Repeat(" \r\n\t", 256 * 1024));
		var xml = Sheet($"<row r=\"1\"><c r=\"A1\"><v>1</v></c>{spaces}<c r=\"B1\" t=\"s\"><v>0</v></c>{spaces}</row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row>");
		foreach (var scan in new[] { ScanMode.All[0], ScanMode.All[2], ScanMode.All[3] })
		{
			foreach (var fast in new[] { true, false })
			{
				var mode = new SheetMode(scan.WithFastCells(fast), int.MaxValue);
				Assert.Equal(["1: number:1 | pear", "2: number:2 | ()"], await Sheets.ReadAsync(xml, mode, strings: Strings, maxSize: WindowLimit));
			}
		}
	}

	/// <summary>A worksheet part around these rows, whose root also declares the prefix <c>y</c> of the main namespace.</summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <returns>The part's XML.</returns>
	private static string Sheet(string rows) => Sheets.Worksheet(rows).Replace("<worksheet ", $"<worksheet xmlns:y=\"{TestPackage.MainNamespace}\" ");

	/// <summary>A part with every element prefixed <c>x:</c> (but those already prefixed), and no default namespace.</summary>
	/// <param name="xml">The part.</param>
	/// <returns>The part's XML.</returns>
	internal static string Prefixed(string xml)
	{
		var body = Regex.Replace(xml.Substring(TestPackage.Declaration.Length), "<(/?)(?=[A-Za-z][A-Za-z0-9]*[ \t\r\n/>])", "<$1x:");

		return TestPackage.Declaration + body.Replace(" xmlns=\"", " xmlns:x=\"");
	}

	/// <summary>A part with <c>xml:space="preserve"</c> on its root or on its <c>sheetData</c>.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="on">Where: <c>root</c> or <c>sheetData</c>.</param>
	/// <returns>The part's XML.</returns>
	internal static string Spaced(string xml, string on) =>
		on == "root" ? xml.Replace("<worksheet ", "<worksheet xml:space=\"preserve\" ") : xml.Replace("<sheetData>", "<sheetData xml:space=\"preserve\">");

	/// <summary>The cells of a part's <c>sheetData</c>: their start tags, with or without a prefix.</summary>
	/// <param name="xml">The part.</param>
	/// <returns>The number of cells.</returns>
	private static long CountCells(string xml)
	{
		var start = xml.IndexOf("sheetData", StringComparison.Ordinal);
		var end = xml.LastIndexOf("sheetData", StringComparison.Ordinal);

		return CellTags.Matches(xml.Substring(start, end - start)).Count;
	}

	/// <summary>Reads a sheet whole, into a 64 KB window, and gives the cells the fallback began reading.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="fast">Whether the scanner reads the cells it can through its fast path.</param>
	/// <param name="reading">The reading, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>A task whose result is <see cref="SheetScanner.FallbackCells"/> after the read.</returns>
	private static async Task<long> FallbackCellsAsync(string xml, bool fast, CellReading? reading = null)
	{
		using var sheet = Sheets.Open(xml, ScanMode.All[0].WithFastCells(fast), strings: Strings, reading: reading);
		await Sheets.FillAllAsync(sheet, new Captured(int.MaxValue), false);

		return sheet.FallbackCells;
	}

	/// <summary>Checks that a sheet reads the same records with fast cells as with the fallback, whole and a record a fill.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="reading">The reading, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>A task that completes when the check passes.</returns>
	private static async Task AssertSameRecordsAsync(string xml, CellReading? reading = null)
	{
		foreach (var perFill in new[] { int.MaxValue, 1 })
		{
			foreach (var skipEmptyRows in new[] { true, false })
			{
				var fast = await Sheets.ReadAsync(xml, new SheetMode(ScanMode.All[0], perFill), skipEmptyRows, Strings, reading: reading, styled: true);
				var fallback = await Sheets.ReadAsync(xml, new SheetMode(ScanMode.All[0].WithFastCells(false), perFill), skipEmptyRows, Strings, reading: reading, styled: true);
				Assert.Equal(fallback, fast);
			}
		}
	}
}
