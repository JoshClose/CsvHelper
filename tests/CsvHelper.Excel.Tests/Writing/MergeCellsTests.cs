using System.Globalization;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Merged cells on a sheet's writer (<see cref="ExcelWriterExtensions.MergeCells"/>, Phase 5's step 4), synchronously and
/// asynchronously: ranges merged before, while and after the rows are written, after the title's; the ranges Excel would
/// refuse (an overlap, row 1 under a title, a table's cells); a CSV writer and a closed sheet; auto-fit leaving out cells
/// merged across columns; and each new sheet of <see cref="ExcelRowLimit.NewSheet"/> starting without them.
/// </summary>
public class MergeCellsTests
{
	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MergesRangesBeforeWhileAndAfterTheRows(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Title = "Report" });
			writer.MergeCells("$B$9:$A$8");
			await WriteRows(writer, a, ["Name", "Amount", "Code"], ["apple", "1", "A1"], ["pear", "2", "P2"]);
			writer.MergeCells("D2:E3");
			writer.MergeCells("12:13");
			await Written.DisposeAsync(writer, a);
			var columns = book.AddSheet("Columns");
			columns.MergeCells("B:C");
			await Written.DisposeAsync(columns, a);
		}, async);

		// The title's first; then the ranges in the order merged, as their corners.
		Assert.Contains(
			"</sheetData><mergeCells count=\"4\"><mergeCell ref=\"A1:C1\"/><mergeCell ref=\"A8:B9\"/><mergeCell ref=\"D2:E3\"/><mergeCell ref=\"A12:XFD13\"/></mergeCells><pageMargins ",
			Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains("<mergeCell ref=\"B1:C1048576\"/>", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			// ClosedXML names whole rows as rows.
			Assert.Equal(["12:13", "A1:C1", "A8:B9", "D2:E3"], book.Worksheet(1).MergedRanges.Select(r => r.RangeAddress.ToString()).OrderBy(r => r, StringComparer.Ordinal));
		}

		Assert.Equal(["Name|Amount|Code", "apple|1|A1", "pear|2|P2"], Written.Read(workbook)[0].Rows.Skip(1));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData("B2:C3", "The range B2:C3 overlaps A1:B2, merged before in the sheet Data: Excel opens no sheet whose merged cells overlap.")]
	[InlineData("A:A", "The range A1:A1048576 overlaps A1:B2, merged before in the sheet Data: Excel opens no sheet whose merged cells overlap.")]
	[InlineData("B2", "\"B2\" is one cell, and a merged range has two or more.")]
	[InlineData("$B$2:$B$2", "\"$B$2:$B$2\" is one cell, and a merged range has two or more.")]
	[InlineData("A1:XFE2", "\"A1:XFE2\" is not a range of cells, such as A1:C3.")]
	[InlineData("Data!A3:B4", "\"Data!A3:B4\" is not a range of cells, such as A1:C3.")]
	[InlineData("", "\"\" is not a range of cells, such as A1:C3.")]
	public void RefusesRangesExcelWouldRefuse(string range, string message)
	{
		var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			using var writer = book.AddSheet("Data");
			writer.MergeCells("A1:B2");

			var e = Assert.Throws<ArgumentException>(() => writer.MergeCells(range));

			// .NET Framework puts the parameter's name on a line of its own, .NET in parentheses after the message.
			Assert.StartsWith(message, e.Message);
			Assert.Equal("range", e.ParamName);

			// A range refused isn't kept.
			writer.MergeCells("C3:D4");
		}

		var workbook = stream.ToArray();
		Assert.Contains("<mergeCells count=\"2\"><mergeCell ref=\"A1:B2\"/><mergeCell ref=\"C3:D4\"/></mergeCells>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public void RefusesRowOneUnderATitle()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());
		using var writer = book.AddSheet("Data", new ExcelSheetOptions { Title = "Report" });

		var e = Assert.Throws<ArgumentException>(() => writer.MergeCells("D1:E1"));

		Assert.StartsWith("The range D1:E1 is in row 1, which holds the title of the sheet Data, merged across the header's columns.", e.Message);
		Assert.Equal("range", e.ParamName);
		writer.MergeCells("D2:E2");
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RefusesRangesInATablesColumnsAtOrBelowItsHeader(bool async)
	{
		// After the header, the range throws; before it, the header does, and is discarded, as a record that fails.
		ArgumentException? after = null;
		InvalidOperationException? before = null;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Table = new(), Title = "Report" });
			writer.MergeCells("E2:F3");
			await WriteRows(writer, a, ["Name", "Amount", "Code"], ["apple", "1", "A1"]);
			after = Assert.Throws<ArgumentException>(() => writer.MergeCells("C9:D9"));
			writer.MergeCells("D9:E9");
			await Written.DisposeAsync(writer, a);

			var early = book.AddSheet("Early", new ExcelSheetOptions { Table = new() });
			early.MergeCells("B5:C5");
			early.WriteField("Name");
			early.WriteField("Amount");
			before = async
				? await Assert.ThrowsAsync<InvalidOperationException>(() => early.EndRecordAsync().AsTask())
				: Assert.Throws<InvalidOperationException>(early.EndRecord);
			await Written.DisposeAsync(early, a);
		}, async);

		Assert.StartsWith("The range C9:D9 is merged in the columns of the table of the sheet Data (A to C), at or below its header in row 2: Excel opens no table with merged cells.", after!.Message);
		Assert.Equal("range", after.ParamName);
		Assert.Equal("The range B5:C5 is merged in the columns of the table of the sheet Early (A to B), at or below its header in row 1: Excel opens no table with merged cells.", before!.Message);
		Assert.Contains("<mergeCell ref=\"E2:F3\"/><mergeCell ref=\"D9:E9\"/>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));

		// The header discarded, the sheet has no table, and its merged range.
		var early = Written.Entry(workbook, "xl/worksheets/sheet2.xml");
		Assert.Contains("<sheetData></sheetData><mergeCells count=\"1\"><mergeCell ref=\"B5:C5\"/></mergeCells>", early);
		Assert.DoesNotContain("tableParts", early);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public void RefusesACsvWriterAndAClosedSheet()
	{
		using var csv = CsvWriter.Create(new StringWriter());
		var e = Assert.Throws<InvalidOperationException>(() => csv.MergeCells("A1:B2"));
		Assert.Equal("MergeCells merges a sheet's cells, and this CsvWriter writes CSV: use a writer from ExcelWorkbookWriter.AddSheet.", e.Message);

		using var book = ExcelWorkbookWriter.Create(new MemoryStream());
		var writer = book.AddSheet("Data");
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(() => writer.MergeCells("A1:B2"));
		Assert.Throws<ArgumentNullException>(() => ((CsvWriter)null!).MergeCells("A1:B2"));
		Assert.Throws<ArgumentNullException>(() => book.AddSheet("Other").MergeCells(null!));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LeavesCellsMergedAcrossColumnsOutOfAutoFit(bool async)
	{
		// As Excel's AutoFit (Csv\_bench\results\phase5\step4, 13-merges.xlsx): the text over A4:C4 counts nothing, the
		// texts merged down D2:D3 and E2:E3 count, the one below E2:E3's first row too.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { BoldHeader = false });
			writer.MergeCells("D2:D3");
			writer.MergeCells("E2:E3");
			await WriteRows(writer, a, ["N", "A", "C", "D", "E"], ["a", "1", "c", "a long text merged down", "x"], ["b", "2", "c", "", "a long text under the first row"], ["a long text across three columns", "", "", "d", "e"]);
			writer.MergeCells("A4:C4");
			await Written.DisposeAsync(writer, a);
		}, async);

		var widths = Widths(workbook);
		Assert.Equal(ColumnWidths.Fit(new[] { "N", "a", "b" }.Max(t => ColumnWidths.TextPixels(t.AsSpan()))), widths[0]);
		Assert.Equal(ColumnWidths.Fit(ColumnWidths.TextPixels("a long text merged down".AsSpan())), widths[3]);
		Assert.Equal(ColumnWidths.Fit(ColumnWidths.TextPixels("a long text under the first row".AsSpan())), widths[4]);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StartsEachNewSheetWithoutMergedRanges(bool async)
	{
		var options = new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet };
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Title = "Report" });
			writer.MergeCells("D2:E2");
			await WriteRows(writer, a, ["Name", "Code"], ["a", "1"]);

			// The next record starts the next sheet: a range merged before it is still the first sheet's.
			writer.MergeCells("D3:E3");
			await WriteRows(writer, a, ["b", "2"]);
			writer.MergeCells("D2:E2");
			await Written.DisposeAsync(writer, a);
		}, async, options);

		Assert.Contains("<mergeCells count=\"3\"><mergeCell ref=\"A1:B1\"/><mergeCell ref=\"D2:E2\"/><mergeCell ref=\"D3:E3\"/></mergeCells>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains("<mergeCells count=\"2\"><mergeCell ref=\"A1:B1\"/><mergeCell ref=\"D2:E2\"/></mergeCells>", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	/// <summary>Writes rows of fields by hand, a field per <see cref="CsvWriter.WriteField(string?)"/>.</summary>
	/// <param name="writer">The sheet's writer.</param>
	/// <param name="async">Whether to end the records asynchronously.</param>
	/// <param name="rows">The rows.</param>
	/// <returns>A task that completes when they are written.</returns>
	private static async Task WriteRows(CsvWriter writer, bool async, params string[][] rows)
	{
		foreach (var row in rows)
		{
			foreach (var field in row)
			{
				writer.WriteField(field);
			}

			await Written.EndRecordAsync(writer, async);
		}
	}

	/// <summary>The widths of a workbook's first sheet's columns, as written.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The widths by column, from 0.</returns>
	private static double[] Widths(byte[] workbook)
	{
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		var matches = System.Text.RegularExpressions.Regex.Matches(sheet, "<col min=\"(\\d+)\" max=\"\\d+\" width=\"([0-9.]+)\"");
		var widths = new double[matches.Cast<System.Text.RegularExpressions.Match>().Max(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))];
		foreach (System.Text.RegularExpressions.Match match in matches)
		{
			widths[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1] = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
		}

		return widths;
	}
}
