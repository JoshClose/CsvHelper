using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Finding a sheet's merged ranges (<see cref="MergedCells"/>): Excel's shape, after <c>sheetData</c>; the references
/// Excel opens and those it can't (checked through COM on 2026-11-04); sorting and the order of the ranges' ends;
/// overlaps; stopping after <c>&lt;/mergeCells&gt;</c>; the limit; and the XML's errors. Each test runs in every
/// <see cref="ScanMode"/>.
/// </summary>
public class MergedCellsTests
{
	/// <summary>The rows of the tests' sheets.</summary>
	private const string Rows = "<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"B1\"><v>2</v></c></row><row r=\"2\"><c r=\"A2\"><v>3</v></c></row>";

	/// <summary>References that Excel opens, and the ranges they are.</summary>
	public static TheoryData<string, string> Opened => new()
	{
		{ "A1:B2", "A1:B2" },
		{ "a1:b2", "A1:B2" },
		{ "$A$1:$B$2", "A1:B2" },
		{ "B2:A1", "A1:B2" },
		{ "A:B", "A:B" },
		{ "1:2", "1:2" },
		{ "B1", "B1" },
		{ "&#65;1:B2", "A1:B2" },
		{ "A01:B02", "A1:B2" },
		{ "A1:XFD1048576", "1:1048576" },
	};

	/// <summary>References that Excel can't open, as the error quotes them.</summary>
	public static TheoryData<string, string> Refused => new()
	{
		{ "", "" },
		{ " A1:B2 ", " A1:B2 " },
		{ "A1:B2&#9;", "A1:B2\t" },
		{ "S!A1:B2", "S!A1:B2" },
		{ "A1:B2 C3:D4", "A1:B2 C3:D4" },
		{ "A1:XFE2", "A1:XFE2" },
		{ "A0:B2", "A0:B2" },
		{ "A1:B2:C3", "A1:B2:C3" },
		{ "A1:", "A1:" },
	};

	[Fact]
	public async Task FindsTheRangesAsExcelWritesThem()
	{
		// After sheetData and autoFilter, before pageMargins.
		var xml = Sheets.Worksheet(Rows).Replace(
			"</sheetData>",
			"</sheetData><autoFilter ref=\"A1:B2\"/><mergeCells count=\"3\"><mergeCell ref=\"A1:B1\"/><mergeCell ref=\"C1:C4\"/><mergeCell ref=\"D2:E3\"/></mergeCells>");

		await InEveryModeAsync(mode =>
		{
			var cells = Find(xml, mode)!;
			Assert.Equal(["A1:B1", "C1:C4", "D2:E3"], cells.Ranges.Select(r => r.ToString()));

			// In the order of their last rows.
			Assert.Equal([0, 2, 1], cells.Ends);
		});
	}

	[Theory]
	[MemberData(nameof(Opened))]
	public async Task ReadsTheReferencesExcelOpens(string reference, string range)
	{
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", $"</sheetData><mergeCells count=\"1\"><mergeCell ref=\"{reference}\"/></mergeCells>");

		await InEveryModeAsync(mode => Assert.Equal([range], Find(xml, mode)!.Ranges.Select(r => r.ToString())));
	}

	[Theory]
	[MemberData(nameof(Refused))]
	public async Task RefusesTheReferencesExcelCantOpen(string reference, string quoted)
	{
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", $"</sheetData><mergeCells count=\"1\"><mergeCell ref=\"{reference}\"/></mergeCells>");
		var offset = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf("<mergeCell ", StringComparison.Ordinal)));

		await InEveryModeAsync(mode =>
		{
			var e = Assert.Throws<CsvFormatException>(() => Find(xml, mode));
			Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: the attribute ref=\"{quoted}\" of a mergeCell is not a block of cells, such as A1:B2.", e.Message);
			Assert.Null(e.Location);
		});
	}

	[Fact]
	public async Task RefusesAMergeCellWithoutARef()
	{
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", "</sheetData><mergeCells count=\"1\"><mergeCell/></mergeCells>");
		var offset = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf("<mergeCell/>", StringComparison.Ordinal)));

		await InEveryModeAsync(mode =>
			Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: a mergeCell has no ref.", Assert.Throws<CsvFormatException>(() => Find(xml, mode)).Message));
	}

	[Fact]
	public async Task SkipsWhatExcelIgnores()
	{
		// Content in a mergeCell, other elements in mergeCells, a wrong count, a prefix, and the strict namespace.
		var xml = Sheets.Worksheet(Rows).Replace(
			"</sheetData>",
			"</sheetData><mergeCells count=\"7\"><foo ref=\"C3:D4\"/><mergeCell ref=\"A1:B2\"><x><mergeCell ref=\"E5:F6\"/></x></mergeCell><!-- c --></mergeCells>");
		var prefixed = TestPackage.Declaration + $"<x:worksheet xmlns:x=\"{TestPackage.StrictMainNamespace}\"><x:sheetData/><x:mergeCells><x:mergeCell ref=\"B2:C3\"/></x:mergeCells></x:worksheet>";

		await InEveryModeAsync(mode =>
		{
			Assert.Equal(["A1:B2"], Find(xml, mode)!.Ranges.Select(r => r.ToString()));
			Assert.Equal(["B2:C3"], Find(prefixed, mode)!.Ranges.Select(r => r.ToString()));
		});
	}

	[Fact]
	public async Task FindsNoRangesWhereTheSheetHasNone()
	{
		await InEveryModeAsync(mode =>
		{
			Assert.Null(Find(Sheets.Worksheet(Rows), mode));
			Assert.Null(Find(Sheets.Worksheet(Rows).Replace("</sheetData>", "</sheetData><mergeCells/>"), mode));
			Assert.Null(Find(Sheets.Worksheet(Rows).Replace("</sheetData>", "</sheetData><mergeCells count=\"0\"></mergeCells>"), mode));

			// A root that isn't a worksheet: the scanner reports it.
			Assert.Null(Find($"<chartsheet xmlns=\"{TestPackage.MainNamespace}\"><mergeCells><mergeCell ref=\"A1:B2\"/></mergeCells></chartsheet>", mode));
		});
	}

	[Fact]
	public async Task SortsTheRanges()
	{
		var xml = Sheets.Worksheet(Rows).Replace(
			"</sheetData>",
			"</sheetData><mergeCells><mergeCell ref=\"C5:D9\"/><mergeCell ref=\"A5:B5\"/><mergeCell ref=\"E1:E2\"/><mergeCell ref=\"A1:B3\"/><mergeCell ref=\"F1:F12\"/></mergeCells>");

		await InEveryModeAsync(mode =>
		{
			var cells = Find(xml, mode)!;
			Assert.Equal(["A1:B3", "E1:E2", "F1:F12", "A5:B5", "C5:D9"], cells.Ranges.Select(r => r.ToString()));
			Assert.Equal(["E1:E2", "A1:B3", "A5:B5", "C5:D9", "F1:F12"], cells.Ends.Select(i => cells.Ranges[i].ToString()));
		});
	}

	[Theory]
	[InlineData("A1:B2", "A1:B2", "A1:B2", "A1:B2")]
	[InlineData("A1:B2", "B2", "A1:B2", "B2")]
	[InlineData("A1:B2", "B2:B2", "A1:B2", "B2")]
	[InlineData("A1:B10", "B5:C6", "A1:B10", "B5:C6")]
	[InlineData("C3:D4", "A1:C3", "A1:C3", "C3:D4")]
	[InlineData("B:B", "A7:C7", "B:B", "A7:C7")]
	[InlineData("5:5", "D1:D9", "D1:D9", "5:5")]
	public async Task RefusesRangesThatOverlap(string first, string second, string earlier, string later)
	{
		// Ranges before and after them, apart, so that the overlap is found among others.
		var xml = Sheets.Worksheet(Rows).Replace(
			"</sheetData>",
			$"</sheetData><mergeCells><mergeCell ref=\"X1:Y3\"/><mergeCell ref=\"{first}\"/><mergeCell ref=\"X20:Z20\"/><mergeCell ref=\"{second}\"/><mergeCell ref=\"X30:X40\"/></mergeCells>");
		var offset = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf("<mergeCells>", StringComparison.Ordinal)));

		await InEveryModeAsync(mode =>
		{
			var e = Assert.Throws<CsvFormatException>(() => Find(xml, mode));
			Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: its merged cells {earlier} and {later} overlap.", e.Message);
		});
	}

	[Fact]
	public async Task TakesRangesThatTouch()
	{
		var xml = Sheets.Worksheet(Rows).Replace(
			"</sheetData>",
			"</sheetData><mergeCells><mergeCell ref=\"A1:B2\"/><mergeCell ref=\"C1:D2\"/><mergeCell ref=\"A3:D3\"/><mergeCell ref=\"E1:E3\"/><mergeCell ref=\"A4\"/><mergeCell ref=\"A5:A6\"/></mergeCells>");

		await InEveryModeAsync(mode => Assert.Equal(6, Find(xml, mode)!.Ranges.Length));
	}

	[Fact]
	public void StopsAfterTheRanges()
	{
		// What follows the ranges isn't read: not even its broken XML.
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", "</sheetData><mergeCells><mergeCell ref=\"A1:B2\"/></mergeCells><pageSetup <<<");
		var bytes = Encoding.UTF8.GetBytes(xml);
		var end = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf("</mergeCells>", StringComparison.Ordinal) + "</mergeCells>".Length));

		var stream = new TrickleStream(bytes);
		using var scanner = new XmlScanner(new XmlWindow(stream, Sheets.PartName, 1 << 20, 16));
		var cells = SyncOrAsync.Result(MergedCells.ReadAsync(scanner, "Data", 1 << 20, false, CancellationToken.None));

		Assert.Single(cells!.Ranges);
		Assert.Equal(end, stream.Delivered);
	}

	[Fact]
	public async Task CountsTheRangesTowardTheLimit()
	{
		var count = 50;
		var refs = string.Concat(Enumerable.Range(1, count).Select(n => $"<mergeCell ref=\"A{n}:B{n}\"/>"));
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", $"</sheetData><mergeCells>{refs}</mergeCells>");
		var bytes = Encoding.UTF8.GetBytes(xml);

		await InEveryModeAsync(mode =>
		{
			// The window's limit is the part's size; the ranges' limit is what is checked.
			Assert.Equal(count, Read(bytes, mode, Math.Max(bytes.Length, count * MergedCells.BytesPerRange), count * MergedCells.BytesPerRange)!.Ranges.Length);
			var e = Assert.Throws<InvalidDataException>(() => Read(bytes, mode, bytes.Length, (count * MergedCells.BytesPerRange) - 1));
			Assert.Equal($"The merged cells of {Sheets.PartName} take more than ExcelReaderOptions.MaxPartSize ({(count * MergedCells.BytesPerRange) - 1} bytes).", e.Message);
		});
	}

	[Fact]
	public async Task ReportsTheXmlsErrorsForTheSheet()
	{
		// XML that ends in the rows, inside a tag, is found by the read that looks for the ranges.
		var whole = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row>");
		var xml = whole.Substring(0, whole.IndexOf("<row r=\"2\">", StringComparison.Ordinal) + 4);

		await InEveryModeAsync(mode =>
		{
			var e = Assert.Throws<CsvFormatException>(() => Find(xml, mode));
			Assert.StartsWith("Sheet Data can't be read at byte ", e.Message);
			Assert.Contains($" of {Sheets.PartName}: ", e.Message);
			Assert.IsType<InvalidDataException>(e.InnerException);
			Assert.Equal(0, e.LineNumber);
		});
	}

	/// <summary>Finds the merged ranges of a part.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns>The ranges, or <see langword="null"/>.</returns>
	private static MergedCells? Find(string xml, ScanMode mode) => Read(Encoding.UTF8.GetBytes(xml), mode, 1 << 20, 1 << 20);

	/// <summary>Finds the merged ranges of a part, under limits.</summary>
	/// <param name="bytes">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="windowSize">The window's size limit.</param>
	/// <param name="maxSize">The ranges' limit.</param>
	/// <returns>The ranges, or <see langword="null"/>.</returns>
	private static MergedCells? Read(byte[] bytes, ScanMode mode, long windowSize, long maxSize)
	{
		Stream stream = mode.Trickle ? new TrickleStream(bytes) : new MemoryStream(bytes);
		using var scanner = new XmlScanner(new XmlWindow(stream, Sheets.PartName, windowSize, mode.WindowSize));

		return mode.Async
			? MergedCells.ReadAsync(scanner, "Data", maxSize, true, CancellationToken.None).AsTask().GetAwaiter().GetResult()
			: SyncOrAsync.Result(MergedCells.ReadAsync(scanner, "Data", maxSize, false, CancellationToken.None));
	}

	/// <summary>Runs a check in every <see cref="ScanMode"/>, and says which mode failed.</summary>
	/// <param name="test">The check, for one mode.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	private static Task InEveryModeAsync(Action<ScanMode> test)
	{
		foreach (var mode in ScanMode.All)
		{
			try
			{
				test(mode);
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"In mode '{mode}': {e.Message}", e);
			}
		}

		return Task.CompletedTask;
	}
}
