using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// The workbook's fast cells (<c>ExcelWorkbook.fastCells</c>, Phase 9's step 3) reaching both scanners a reader opens: the
/// header search's, over the rows the header can be in, and the sheet's, over the rows after it. The workbook counts the
/// cells the scanners' fallback began reading (<c>ExcelWorkbook.fallbackCells</c>), the search's when the search ends and
/// the sheet's when the reader is disposed: with the switch off, every cell each scanner read; with it on, only the cells
/// the fast path declined. The two scanners read different numbers of cells, so that a switch or a count that reaches one
/// of them alone fails.
/// </summary>
public class FastCellReadingTests
{
	/// <summary>
	/// A report: a title and a note above the header in row 3, then three orders in A and B, two with a note in C, outside
	/// the header's columns. Every cell is canonical, so the fast path takes all of them.
	/// </summary>
	private const string Rows =
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Report</t></is></c><c r=\"B1\"><v>1</v></c></row>" +
		"<row r=\"2\"><c r=\"A2\"><v>2</v></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B3\" t=\"inlineStr\"><is><t>Qty</t></is></c></row>" +
		"<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B4\"><v>3</v></c><c r=\"C4\" t=\"str\"><v>note</v></c></row>" +
		"<row r=\"5\"><c r=\"A5\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B5\"><v>4</v></c><c r=\"C5\" t=\"str\"><v>note</v></c></row>" +
		"<row r=\"6\"><c r=\"A6\" t=\"inlineStr\"><is><t>plum</t></is></c><c r=\"B6\"><v>5</v></c></row>";

	/// <summary>The cells the header search reads, rows 1 to 3, until it finds the header.</summary>
	private const long SearchCells = 5;

	/// <summary>The cells the sheet's scanner reads, rows 4 to 6, C4 and C5 among them though they are no fields.</summary>
	private const long SheetCells = 8;

	/// <summary>The orders, after the header.</summary>
	private static readonly string[] Orders = ["apple|3", "pear|4", "plum|5"];

	/// <summary>The options: the header found by its names, in the first 20 rows.</summary>
	private static readonly ExcelReaderOptions Found = new() { HeaderRow = ExcelHeaderRow.Find(["Name", "Qty"]) };

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsEveryCellBothScannersReadWithTheSwitchOff(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(Rows), async);
		workbook.fastCells = false;
		var before = workbook.fallbackCells;

		// The search's cells count when it ends, as the reader opens; the sheet's when the reader is disposed.
		using (var reader = await workbook.OpenReaderAsync(async, Found))
		{
			Assert.Equal(SearchCells, workbook.fallbackCells - before);
			Assert.Equal(Orders, await reader.ReadAllAsync(async));
			Assert.Equal(["Name", "Qty"], reader.Headers);
		}

		Assert.Equal(SearchCells + SheetCells, workbook.fallbackCells - before);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsNothingForCanonicalCellsWithTheSwitchOn(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(Rows), async);
		Assert.True(workbook.fastCells, "Fast cells are on by default.");
		var before = workbook.fallbackCells;

		using (var reader = await workbook.OpenReaderAsync(async, Found))
		{
			Assert.Equal(0, workbook.fallbackCells - before);
			Assert.Equal(Orders, await reader.ReadAllAsync(async));
		}

		Assert.Equal(0, workbook.fallbackCells - before);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsTheDeclinedCellsOfEachScannerWithTheSwitchOn(bool async)
	{
		// B1 has an attribute the fast path doesn't take, which only the search reads; B5 a formula, which only the sheet's
		// scanner reads. Each declines one cell, read by the fallback with the same records.
		var rows = Rows.Replace("<c r=\"B1\"><v>1</v></c>", "<c r=\"B1\" cm=\"1\"><v>1</v></c>").Replace("<c r=\"B5\"><v>4</v></c>", "<c r=\"B5\"><f>2+2</f><v>4</v></c>");
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(rows), async);
		var before = workbook.fallbackCells;

		using (var reader = await workbook.OpenReaderAsync(async, Found))
		{
			Assert.Equal(1, workbook.fallbackCells - before);
			Assert.Equal(Orders, await reader.ReadAllAsync(async));
		}

		Assert.Equal(2, workbook.fallbackCells - before);

		// Off, every cell again, for both readers of the workbook in turn.
		workbook.fastCells = false;
		before = workbook.fallbackCells;
		using (var reader = await workbook.OpenReaderAsync(async, Found))
		{
			Assert.Equal(Orders, await reader.ReadAllAsync(async));
		}

		Assert.Equal(SearchCells + SheetCells, workbook.fallbackCells - before);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsTheSheetsScannerWithoutAHeaderSearch(bool async)
	{
		// No header row: the sheet's scanner alone reads, every row; with the switch off, all 13 cells.
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(Rows), async);
		var options = new ExcelReaderOptions { Csv = new() { HasHeader = false } };

		workbook.fastCells = false;
		var before = workbook.fallbackCells;
		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(0, workbook.fallbackCells - before);
			Assert.Equal(6, (await reader.ReadAllAsync(async)).Count);
		}

		Assert.Equal(SearchCells + SheetCells, workbook.fallbackCells - before);

		workbook.fastCells = true;
		before = workbook.fallbackCells;
		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			Assert.Equal(6, (await reader.ReadAllAsync(async)).Count);
		}

		Assert.Equal(0, workbook.fallbackCells - before);
	}
}
