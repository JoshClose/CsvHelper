using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Which fields <see cref="SheetScanner"/> gives a style under <see cref="ExcelText.Formatted"/> (Phase 8's step 5): every
/// number, date, time and duration, and a text or a boolean whose format has a text section that changes it; not a
/// formula read as text, an error, an empty field, text in <c>@</c> alone or in a format without a text section. Merged
/// ranges' fills and a found header keep their cells' styles. In every scan mode, with the scanner's fast cells on and off
/// (Phase 9's step 3), so that a cell read again after a refill has its style once, and the fast path's styles are the
/// fallback's. A field's style is its cell format's number format's index (<see cref="StyleTable.NumberFormatIndex"/>): in
/// <see cref="FormattedTextTests.Styles"/>, and in the tables made here, each cell format's own. Workbooks whose formats
/// change no text (<see cref="CellFormatter.ChangesText"/> false, so that the scanner skips the lookup for text and
/// booleans), and workbooks at and past <see cref="CellFormatter.MaxCompiledFormats"/> distinct formats (past which the
/// formatter looks each style up as it comes), give the same styles.
/// </summary>
public class StyledScannerTests
{
	/// <summary>The styles of <see cref="FormattedTextTests.Formats"/>.</summary>
	private static readonly StyleTable Styles = FormattedTextTests.Book("").LoadStylesAsync(false).GetAwaiter().GetResult();

	/// <summary>The shared strings of <see cref="FormattedTextTests.Book"/>.</summary>
	private static readonly SharedStrings Strings = Sheets.Strings("pear", "xyz", "");

	/// <summary>
	/// Number formats none of which changes text: a number, a date, a number, an elapsed time, a percentage, a date, a text
	/// section of <c>@</c> alone, and a number with a literal, each a cell format.
	/// </summary>
	internal static readonly string[] PlainFormats = ["0", "14", "#,##0.00", "[h]:mm", "0.0%", "mmm-yy", "0;-0;0;@", "\"=\"0"];

	/// <summary>The styles of <see cref="PlainFormats"/>.</summary>
	internal static readonly StyleTable PlainStyles = StylesOf(PlainFormats);

	/// <summary>
	/// Styles of exactly <see cref="CellFormatter.MaxCompiledFormats"/> distinct number formats (General and the rest custom),
	/// some of whose text sections change text (<see cref="ManyFormats"/>).
	/// </summary>
	private static readonly Lazy<StyleTable> AtTheCap = new(() => StylesOf(ManyFormats(CellFormatter.MaxCompiledFormats - 1, true)));

	/// <summary>Styles of exactly <see cref="CellFormatter.MaxCompiledFormats"/> distinct number formats, none of which changes text.</summary>
	private static readonly Lazy<StyleTable> AtTheCapPlain = new(() => StylesOf(ManyFormats(CellFormatter.MaxCompiledFormats - 1, false)));

	/// <summary>Styles of General and <see cref="CellFormatter.MaxCompiledFormats"/> + 1 distinct custom formats, some of which change text.</summary>
	private static readonly Lazy<StyleTable> PastTheCap = new(() => StylesOf(ManyFormats(CellFormatter.MaxCompiledFormats + 1, true)));

	/// <summary>Styles of General and <see cref="CellFormatter.MaxCompiledFormats"/> + 1 distinct custom formats, none of which changes text.</summary>
	private static readonly Lazy<StyleTable> PastTheCapPlain = new(() => StylesOf(ManyFormats(CellFormatter.MaxCompiledFormats + 1, false)));

	/// <summary>The tables of many formats, by name: whether their formats change text, and how many custom formats they have.</summary>
	public static TheoryData<string> ManyFormatTables => new() { "at the cap", "at the cap, plain", "past the cap", "past the cap, plain" };

	/// <summary>
	/// A styles part of these number formats, each a cell format in order: a format that is a number is that built-in id,
	/// and each other one a custom code, of id 164 and its place.
	/// </summary>
	/// <param name="formats">The formats.</param>
	/// <returns>The part's XML.</returns>
	internal static string StylesPart(IReadOnlyList<string> formats)
	{
		var custom = new StringBuilder();
		var cells = new StringBuilder();
		var count = 0;
		for (var i = 0; i < formats.Count; i++)
		{
			var format = formats[i];
			if (!int.TryParse(format, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
			{
				id = 164 + i;
				custom.Append($"<numFmt numFmtId=\"{id}\" formatCode=\"{Sheets.Escape(format).Replace("\"", "&quot;")}\"/>");
				count++;
			}

			cells.Append($"<xf numFmtId=\"{id}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		}

		return TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\">" +
			$"<numFmts count=\"{count}\">{custom}</numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
			"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
			$"<cellXfs count=\"{formats.Count}\">{cells}</cellXfs></styleSheet>";
	}

	/// <summary>The styles of these number formats, as <see cref="StylesPart"/> writes them, loaded from a workbook's styles part.</summary>
	/// <param name="formats">The formats.</param>
	/// <returns>The styles.</returns>
	internal static StyleTable StylesOf(IReadOnlyList<string> formats) =>
		TestPackage.Workbook("Data").Set("xl/styles.xml", StylesPart(formats)).LoadStylesAsync(false).GetAwaiter().GetResult();

	/// <summary>
	/// General, then distinct custom formats: a number with a literal of its place; every 512th place, a format whose text
	/// section changes text (or, without text sections, the number with a literal); and every 512th place from 256, a
	/// format whose text section is <c>@</c> alone.
	/// </summary>
	/// <param name="custom">The number of custom formats.</param>
	/// <param name="textSections">Whether every 512th format has a text section that changes text.</param>
	/// <returns>The formats, General first.</returns>
	internal static List<string> ManyFormats(int custom, bool textSections)
	{
		var formats = new List<string> { "0" };
		for (var i = 1; i <= custom; i++)
		{
			formats.Add(i % 512 == 0 && textSections ? $"0;-0;0;\"t{i}: \"@" : i % 512 == 256 ? $"0\" a{i}\";-0;0;@" : $"#,##0\" u{i}\"");
		}

		return formats;
	}

	/// <summary>Whether the format at a place of <see cref="ManyFormats"/> changes text.</summary>
	/// <param name="place">The format's place: General is 0.</param>
	/// <param name="textSections">Whether the formats have text sections that change text.</param>
	/// <returns>Whether it does.</returns>
	private static bool ChangesTextAt(int place, bool textSections) => textSections && place > 0 && place % 512 == 0;

	/// <summary>A table of many formats, by its name in <see cref="ManyFormatTables"/>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The styles, the number of custom formats, and whether some of them change text.</returns>
	private static (StyleTable Styles, int Custom, bool TextSections) ManyFormatTable(string name) => name switch
	{
		"at the cap" => (AtTheCap.Value, CellFormatter.MaxCompiledFormats - 1, true),
		"at the cap, plain" => (AtTheCapPlain.Value, CellFormatter.MaxCompiledFormats - 1, false),
		"past the cap" => (PastTheCap.Value, CellFormatter.MaxCompiledFormats + 1, true),
		_ => (PastTheCapPlain.Value, CellFormatter.MaxCompiledFormats + 1, false),
	};

	/// <summary>Reads with the styles and a formatter in the invariant culture.</summary>
	/// <param name="formulas">What cells with a formula read as.</param>
	/// <param name="fillMergedCells">Whether merged ranges' cells read as their top-left cells.</param>
	/// <returns>The reading.</returns>
	private static CellReading Formatted(ExcelFormulas formulas = ExcelFormulas.CachedValue, bool fillMergedCells = false) =>
		new(Styles, formulas: formulas, fillMergedCells: fillMergedCells, formatter: new CellFormatter(Styles, DisplayCulture.Invariant, false));

	/// <summary>A cell of a number in a cell format.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="value">The number.</param>
	/// <param name="style">The cell format.</param>
	/// <returns>The cell's XML.</returns>
	private static string N(string reference, string value, int style) => $"<c r=\"{reference}\" s=\"{style}\"><v>{value}</v></c>";

	[Fact]
	public Task GivesStylesToTheFieldsWhoseTextTheirFormatCanChange() => Sheets.InEveryModeAsync(async mode =>
	{
		Assert.Equal(
			[
				"1: Name | number:1@11 | number:2@11 | Day | Took | Paid | Note | Part",
				"2: apple | number:1234.5@2 | number:0.125@5 | date:45306@3 | duration:1.5@6 | bool:1@12 | abc@4 | number:3.5@7",
				"3: pear | number:-1234.5@10 | number:0.3333333333333333@0 | date:-1@1 | number:0.5@13 | bool:0 | xyz@12 | error:#N/A",
			],
			await Sheets.ReadAsync(Sheets.Worksheet(FormattedTextTests.Rows), mode, strings: Strings, reading: Formatted(), styled: true));

		// Without a formatter, no field has a style.
		var unstyled = await Sheets.ReadRecordsAsync(Sheets.Worksheet(FormattedTextTests.Rows), mode, strings: Strings, reading: new CellReading(Styles));
		Assert.All(unstyled, r => Assert.All(r.Styles, s => Assert.Equal(-1, s)));
	});

	[Fact]
	public Task GivesIsoDatesAndFormulaStringsTheirStyles() => Sheets.InEveryModeAsync(async mode =>
	{
		// A t="d" date is a number in its style; one that isn't a date is text, styled through a text section. t="str" is text.
		var rows =
			"<row r=\"1\"><c r=\"A1\" t=\"d\" s=\"3\"><v>2024-01-15</v></c><c r=\"B1\" t=\"d\" s=\"4\"><v>abc</v></c><c r=\"C1\" t=\"d\"><v>abc</v></c>" +
			"<c r=\"D1\" t=\"str\" s=\"4\"><v>x</v></c><c r=\"E1\" t=\"str\" s=\"15\"><v>y</v></c><c r=\"F1\" t=\"b\" s=\"9\"><v>1</v></c></row>";
		Assert.Equal(
			["1: date:45306@3 | abc@4 | abc | x@4 | y | bool:1@9"],
			await Sheets.ReadAsync(Sheets.Worksheet(rows), mode, reading: Formatted(), styled: true));
	});

	[Fact]
	public Task LeavesFormulasReadAsTextUnstyled() => Sheets.InEveryModeAsync(async mode =>
	{
		var rows = "<row r=\"1\"><c r=\"A1\" s=\"2\"><f>1+1</f><v>2</v></c><c r=\"B1\" s=\"4\" t=\"str\"><f>\"a\"</f><v>a</v></c></row>";
		Assert.Equal(["1: =1+1 | =\"a\""], await Sheets.ReadAsync(Sheets.Worksheet(rows), mode, reading: Formatted(ExcelFormulas.FormulaText), styled: true));
		Assert.Equal(["1: number:2@2 | a@4"], await Sheets.ReadAsync(Sheets.Worksheet(rows), mode, reading: Formatted(), styled: true));
	});

	[Fact]
	public Task GivesMergedFillsTheirTopLeftCellsStyle() => Sheets.InEveryModeAsync(async mode =>
	{
		// A1:B1 a number, C1:C3 text through a text section over a row the file lacks, D1:D2 a number in General, E1:F1 a
		// shared string through a text section.
		var rows =
			$"<row r=\"1\">{N("A1", "1234.5", 2)}<c r=\"C1\" t=\"inlineStr\" s=\"4\"><is><t>abc</t></is></c>{N("D1", "7", 0)}" +
			"<c r=\"E1\" t=\"s\" s=\"12\"><v>1</v></c></row>" +
			$"<row r=\"3\">{N("A3", "1", 5)}</row>";
		Assert.Equal(
			[
				"1: number:1234.5@2 | number:1234.5@2 | abc@4 | number:7@0 | xyz@12 | xyz@12",
				"2: () | () | abc@4 | number:7@0 | () | ()",
				"3: number:1@5 | () | abc@4 | () | () | ()",
			],
			await Sheets.ReadAsync(
				MergedScannerTests.Worksheet(rows, "A1:B1", "C1:C3", "D1:D2", "E1:F1"), mode, strings: Strings, reading: Formatted(fillMergedCells: true), styled: true));
	});

	[Fact]
	public Task KeepsTheStylesOfAFoundHeader() => Sheets.InEveryModeAsync(async mode =>
	{
		var rows =
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Report</t></is></c></row>" +
			$"<row r=\"2\"><c r=\"B2\" t=\"inlineStr\"><is><t>Name</t></is></c>{N("C2", "1", 11)}<c r=\"D2\" t=\"b\" s=\"12\"><v>0</v></c>" +
			"<c r=\"E2\" t=\"s\" s=\"4\"><v>0</v></c></row>" +
			$"<row r=\"3\"><c r=\"B3\" t=\"inlineStr\"><is><t>apple</t></is></c>{N("C3", "5", 2)}</row>";
		Assert.Equal(
			["2: Name | number:1@11 | bool:0@12 | pear@4", "3: apple | number:5@2 | () | ()"],
			await Sheets.ReadAfterHeaderAsync(
				Sheets.Worksheet(rows), mode, ExcelHeaderRow.Find(["Name", "Q1", "t:FALSE", "Note: pear"]), strings: Strings, reading: Formatted(), styled: true));
	});

	[Fact]
	public Task KeepsTheStylesOfAHeaderWiderThanTheFirstRoom() => Sheets.InEveryModeAsync(async mode =>
	{
		// 40 columns, past the 16 fields a search's captured row has room for at first: names in odd columns, numbers shown
		// Q2, Q4, ... in even ones.
		var header = new System.Text.StringBuilder("<row r=\"2\">");
		var expected = new List<string>();
		for (var column = 1; column <= 40; column++)
		{
			var reference = CellReference.ColumnName(column - 1) + "2";
			header.Append(column % 2 == 1 ? $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>C{column}</t></is></c>" : N(reference, column.ToString(System.Globalization.CultureInfo.InvariantCulture), 11));
			expected.Add(column % 2 == 1 ? $"C{column}" : $"number:{column}@11");
		}

		var rows = "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Report</t></is></c></row>" + header + "</row>" + $"<row r=\"3\">{N("AN3", "5", 2)}</row>";
		var records = await Sheets.ReadAfterHeaderAsync(Sheets.Worksheet(rows), mode, ExcelHeaderRow.Find(["C1", "Q2", "C39", "Q40"]), reading: Formatted(), styled: true);
		Assert.Equal(["2: " + string.Join(" | ", expected), "3: " + string.Concat(Enumerable.Repeat("() | ", 39)) + "number:5@2"], records);

		// Raw names the numbers' columns 2, 4, ...
		records = await Sheets.ReadAfterHeaderAsync(Sheets.Worksheet(rows), mode, ExcelHeaderRow.Find(["C1", "2", "C39", "40"]), reading: new CellReading(Styles), styled: true);
		Assert.Equal(40, records[0].Split('|').Length);
	});

	[Fact]
	public void GivesAStyleOnlyWhereTheFormatCanChangeTheText()
	{
		var formatter = new CellFormatter(Styles, DisplayCulture.Invariant, false);

		// Numbers, dates and durations take their format's index, whatever the format.
		Assert.Equal(2, formatter.StyleOf(SheetCellKind.Number, 2));
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.Number, 0));
		Assert.Equal(3, formatter.StyleOf(SheetCellKind.DateNumber, 3));
		Assert.Equal(6, formatter.StyleOf(SheetCellKind.Duration, 6));
		Assert.Equal(13, formatter.StyleOf(SheetCellKind.Number, 13));

		// Text and booleans only through a text section that isn't @ alone: "Note: "@, "t:"@ and ;;;'s empty one.
		Assert.Equal(4, formatter.StyleOf(SheetCellKind.Text, 4));
		Assert.Equal(12, formatter.StyleOf(SheetCellKind.Bool, 12));
		Assert.Equal(9, formatter.StyleOf(SheetCellKind.Text, 9));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, 0));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Bool, 2));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, 15));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, 13));

		// Errors and empty fields never.
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Error, 2));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Empty, 2));

		// A style past the cell formats has the first one's.
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.Number, 1000));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, -5));
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.Number, -5));

		// Some of the formats change text.
		Assert.True(formatter.ChangesText);
	}

	[Fact]
	public void ChangesNoTextWithoutATextSectionThatChangesIt()
	{
		// No format changes text: text and booleans have no style, whatever their cell format; numbers have theirs.
		var formatter = new CellFormatter(PlainStyles, DisplayCulture.Invariant, false);
		Assert.False(formatter.ChangesText);
		for (var style = -2; style < PlainFormats.Length + 2; style++)
		{
			var index = style >= 0 && style < PlainFormats.Length ? style : 0;
			Assert.Equal(index, formatter.StyleOf(SheetCellKind.Number, style));
			Assert.Equal(index, formatter.StyleOf(SheetCellKind.DateNumber, style));
			Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, style));
			Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Bool, style));
			Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Error, style));
			Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Empty, style));
		}

		// Without a styles part, every cell is General, which changes no text.
		var general = new CellFormatter(StyleTable.Empty, DisplayCulture.Invariant, false);
		Assert.False(general.ChangesText);
		Assert.Equal(0, general.StyleOf(SheetCellKind.Number, 0));
		Assert.Equal(0, general.StyleOf(SheetCellKind.Number, 7));
		Assert.Equal(0, general.StyleOf(SheetCellKind.Duration, -1));
		Assert.Equal(-1, general.StyleOf(SheetCellKind.Text, 0));
		Assert.Equal(-1, general.StyleOf(SheetCellKind.Bool, 3));
	}

	[Fact]
	public Task GivesNoStyleToTextOrBooleansWhenNoFormatChangesText() => Sheets.InEveryModeAsync(async mode =>
	{
		// Text in @ alone, a boolean in a number format, a shared string in @ alone and a string in a format with a literal
		// keep their text; numbers and dates take their formats; an error has none.
		var rows =
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\" s=\"6\"><is><t>abc</t></is></c><c r=\"B1\" t=\"b\"><v>1</v></c>" +
			$"{N("C1", "1234.5", 2)}{N("D1", "45306", 1)}<c r=\"E1\" t=\"s\" s=\"6\"><v>0</v></c><c r=\"F1\" t=\"str\" s=\"7\"><v>y</v></c>" +
			$"<c r=\"G1\" t=\"e\" s=\"2\"><v>#N/A</v></c><c r=\"H1\" t=\"b\" s=\"3\"><v>0</v></c>{N("I1", "1.5", 3)}</row>";
		var reading = new CellReading(PlainStyles, formatter: new CellFormatter(PlainStyles, DisplayCulture.Invariant, false));
		Assert.Equal(
			["1: abc | bool:1 | number:1234.5@2 | date:45306@1 | pear | y | error:#N/A | bool:0 | duration:1.5@3"],
			await Sheets.ReadAsync(Sheets.Worksheet(rows), mode, strings: Strings, reading: reading, styled: true));
	});

	[Theory]
	[MemberData(nameof(ManyFormatTables))]
	public void GivesTheSameStylesAtAndPastTheCap(string table)
	{
		// At the cap, the formatter's tables; past it, a lookup per field: the same answers, by the rule, for every style.
		var (styles, custom, textSections) = ManyFormatTable(table);
		var formatter = new CellFormatter(styles, DisplayCulture.Invariant, false);
		Assert.Equal(custom + 1, styles.Count);
		Assert.Equal(custom + 1, styles.NumberFormatCount);

		// Past the cap, whether a format changes text isn't known before its field asks.
		Assert.Equal(textSections || custom >= CellFormatter.MaxCompiledFormats, formatter.ChangesText);
		for (var style = -1; style <= custom + 1; style++)
		{
			var place = style >= 0 && style <= custom ? style : 0;
			var shown = ChangesTextAt(place, textSections) ? place : -1;
			Assert.Equal(place, formatter.StyleOf(SheetCellKind.Number, style));
			Assert.Equal(place, formatter.StyleOf(SheetCellKind.Duration, style));
			Assert.Equal(shown, formatter.StyleOf(SheetCellKind.Text, style));
			Assert.Equal(shown, formatter.StyleOf(SheetCellKind.Bool, style));
			Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Error, style));
		}
	}

	[Theory]
	[MemberData(nameof(ManyFormatTables))]
	public async Task ReadsTheSameStylesAtAndPastTheCap(string table)
	{
		var (styles, custom, textSections) = ManyFormatTable(table);
		var rows =
			$"<row r=\"1\">{N("A1", "1", 0)}{N("B1", "2", 1)}<c r=\"C1\" t=\"inlineStr\" s=\"512\"><is><t>abc</t></is></c>" +
			"<c r=\"D1\" t=\"inlineStr\" s=\"768\"><is><t>def</t></is></c><c r=\"E1\" t=\"b\" s=\"1024\"><v>1</v></c><c r=\"F1\" t=\"b\" s=\"1025\"><v>0</v></c>" +
			$"{N("G1", "3", custom)}<c r=\"H1\" t=\"str\" s=\"{custom}\"><v>x</v></c><c r=\"I1\" t=\"s\" s=\"4096\"><v>0</v></c>{N("J1", "4", 99999)}</row>";

		// s="4096" is past the cell formats at the cap (the first one's, General), and a text section's past it.
		var pear = custom >= 4096 && ChangesTextAt(4096, textSections) ? "pear@4096" : "pear";
		var expected =
			$"1: number:1@0 | number:2@1 | {(ChangesTextAt(512, textSections) ? "abc@512" : "abc")} | def | " +
			$"{(ChangesTextAt(1024, textSections) ? "bool:1@1024" : "bool:1")} | bool:0 | number:3@{custom} | " +
			$"{(ChangesTextAt(custom, textSections) ? $"x@{custom}" : "x")} | {pear} | number:4@0";
		var reading = new CellReading(styles, formatter: new CellFormatter(styles, DisplayCulture.Invariant, false));

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal([expected], await Sheets.ReadAsync(Sheets.Worksheet(rows), mode, strings: Strings, reading: reading, styled: true));
		});
	}
}
