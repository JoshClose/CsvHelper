using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The scanner's typed cells: number cells by their style's format, <c>t="d"</c> ISO 8601 dates as serial numbers, and
/// error cells by their policy. Each test runs in every <see cref="SheetMode"/>.
/// </summary>
public class TypedCellScannerTests
{
	/// <summary>
	/// The test package's styles with an elapsed-time format added: 0 is General, 1 a built-in date (14), 2 a custom date
	/// (164) and 3 <c>[h]:mm:ss</c> (165).
	/// </summary>
	/// <returns>The styles.</returns>
	private static Task<StyleTable> StylesAsync() => TestPackage.Workbook("Data")
		.Replace("xl/styles.xml", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/>", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"[h]:mm:ss\"/>")
		.Replace("xl/styles.xml", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/>", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/>")
		.LoadStylesAsync(false);

	[Fact]
	public async Task ReadsNumbersByTheirStylesFormat()
	{
		var styles = await StylesAsync();
		var xml = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>45293</v></c><c r=\"B1\" s=\"1\"><v>45293</v></c><c r=\"C1\" s=\"2\"><v>45293.5</v></c><c r=\"D1\" s=\"3\"><v>1.5</v></c><c r=\"E1\" s=\"9\"><v>2</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// A style past the cell formats has the first one's: General.
			Assert.Equal(["1: number:45293 | date:45293 | date:45293.5 | duration:1.5 | number:2"], await Sheets.ReadAsync(xml, mode, reading: new CellReading(styles)));
		});
	}

	[Fact]
	public async Task ReadsIsoDatesAsSerials()
	{
		var styles = await StylesAsync();
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" t=\"d\" s=\"2\"><v>2024-01-02</v></c><c r=\"B1\" t=\"d\" s=\"1\"><v>2024-01-02T03:04:05.00000006519257800</v></c>" +
			"<c r=\"C1\" t=\"d\" s=\"1\"><v>03:04:05.00000000000072700</v></c><c r=\"D1\" t=\"d\" s=\"3\"><v>12:00</v></c>" +
			"<c r=\"E1\" t=\"d\"><v>2024-01-02</v></c><c r=\"F1\" t=\"d\"><v>PT36H0M0.000S</v></c><c r=\"G1\" t=\"d\" s=\"1\"><v>PT36H</v></c>" +
			"<c r=\"H1\" t=\"d\" s=\"3\"><v>PT36H0M0.000S</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"d\"><v>2024-02-30</v></c><c r=\"B2\" t=\"d\" s=\"1\"><v xml:space=\"preserve\"> 2024-01-02 </v></c>" +
			"<c r=\"C2\" t=\"d\" s=\"1\"><v>&#x32;024-01-02</v></c><c r=\"D2\" t=\"d\"><v></v></c><c r=\"E2\" t=\"d\"/></row>" +
			$"<row r=\"3\"><c r=\"A3\" t=\"d\" s=\"1\"><v>03:04:05.1{new string('9', 10_000)}</v></c><c r=\"B3\" t=\"d\"><v>{new string('2', 10_000)}-01-02</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// The style decides, as for a number and as Excel reads it: a date in a date style, a duration in an elapsed-time
			// style, a number in General, whether the text is a date or a duration (as Excel's Strict files write an
			// elapsed-time cell). Text that isn't an ISO 8601 date or duration stays text; an empty one is valueless.
			Assert.Equal(
				[
					"1: date:45293 | date:45293.12783564815 | date:0.12783564814814816 | duration:0.5 | number:45293 | number:1.5 | date:1.5 | duration:1.5",
					"2: 2024-02-30 | date:45293 | date:45293 | () | () | () | () | ()",
					$"3: date:0.12783796296296296 | {new string('2', 10_000)}-01-02 | () | () | () | () | () | ()",
				],
				await Sheets.ReadAsync(xml, mode, reading: new CellReading(styles)));

			// The 1904 date system counts from 1904-01-01; a time alone and a duration are the same in both.
			var date1904 = await Sheets.ReadAsync(xml, mode, reading: new CellReading(styles, date1904: true));
			Assert.Equal("1: date:43831 | date:43831.12783564815 | date:0.12783564814814816 | duration:0.5 | number:43831 | number:1.5 | date:1.5 | duration:1.5", date1904[0]);
		});
	}

	[Fact]
	public async Task ReadsErrorCellsAsTheirPolicySays()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>a</t></is></c><c r=\"B1\" t=\"e\"><v>#N/A</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"e\"><f>1/0</f><v>#DIV/0!</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\" t=\"e\"><v>#REF!</v></c><c r=\"B3\"><v>3</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var errors = new[] { "1: a | error:#N/A", "2: error:#DIV/0! | ()", "3: error:#REF! | number:3" };
			Assert.Equal(errors, await Sheets.ReadAsync(xml, mode));
			Assert.Equal(errors, await Sheets.ReadAsync(xml, mode, reading: new CellReading(errorCells: ExcelErrorCells.Error)));

			// As null, an error is valueless, as an empty cell is: a row of errors alone is an empty row.
			var nulls = new CellReading(errorCells: ExcelErrorCells.Null);
			Assert.Equal(["1: a", "3: () | number:3"], await Sheets.ReadAsync(xml, mode, reading: nulls));
			Assert.Equal(["1: a", "2: ()", "3: () | number:3"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: nulls));
		});
	}
}
