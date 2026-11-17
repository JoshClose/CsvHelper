using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Which fields <see cref="SheetScanner"/> gives a style under <see cref="ExcelText.Formatted"/> (Phase 8's step 5): every
/// number, date, time and duration, and a text or a boolean whose format has a text section that changes it; not a
/// formula read as text, an error, an empty field, text in <c>@</c> alone or in a format without a text section. Merged
/// ranges' fills and a found header keep their cells' styles. In every scan mode, so that a cell read again after a refill
/// has its style once. A field's style is its cell format's number format's index (<see cref="StyleTable.NumberFormatIndex"/>):
/// in <see cref="FormattedTextTests.Styles"/>, each cell format's own.
/// </summary>
public class StyledScannerTests
{
	/// <summary>The styles of <see cref="FormattedTextTests.Formats"/>.</summary>
	private static readonly StyleTable Styles = FormattedTextTests.Book("").LoadStylesAsync(false).GetAwaiter().GetResult();

	/// <summary>The shared strings of <see cref="FormattedTextTests.Book"/>.</summary>
	private static readonly SharedStrings Strings = Sheets.Strings("pear", "xyz", "");

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
	}
}
