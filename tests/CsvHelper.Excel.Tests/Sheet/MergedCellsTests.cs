using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Finding a sheet's merged ranges (<see cref="MergedCells"/>): Excel's shape, after <c>sheetData</c>; the references
/// Excel opens and those it can't (checked through COM on 2026-11-04); sorting and the order of the ranges' ends;
/// overlaps; stopping after <c>&lt;/mergeCells&gt;</c>; the limit; the XML's errors; and <c>sheetData</c> skipped by a
/// raw search (Phase 9's step 2): its rows unread, markup in them that holds the ranges' tags, and the end tag that must
/// close the worksheet. Each test runs in every <see cref="ScanMode"/>, with raw skips on and off.
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

		// Prefixed rows, which a raw skip passes over to </x:sheetData>.
		var prefixedRows = prefixed.Replace("<x:sheetData/>", "<x:sheetData><x:row r=\"1\"><x:c r=\"A1\"><x:v>1</x:v></x:c></x:row></x:sheetData>");

		await InEveryModeAsync(mode =>
		{
			Assert.Equal(["A1:B2"], Find(xml, mode)!.Ranges.Select(r => r.ToString()));
			Assert.Equal(["B2:C3"], Find(prefixed, mode)!.Ranges.Select(r => r.ToString()));
			Assert.Equal(["B2:C3"], Find(prefixedRows, mode)!.Ranges.Select(r => r.ToString()));
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
		// What follows the ranges isn't read: not even its broken XML. The rows are skipped raw or token by token.
		var xml = Sheets.Worksheet(Rows).Replace("</sheetData>", "</sheetData><mergeCells><mergeCell ref=\"A1:B2\"/></mergeCells><pageSetup <<<");
		var bytes = Encoding.UTF8.GetBytes(xml);
		var end = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf("</mergeCells>", StringComparison.Ordinal) + "</mergeCells>".Length));

		foreach (var raw in new[] { true, false })
		{
			var stream = new TrickleStream(bytes);
			using var scanner = new XmlScanner(new XmlWindow(stream, Sheets.PartName, 1 << 20, 16)) { RawSkips = raw };
			var cells = SyncOrAsync.Result(MergedCells.ReadAsync(scanner, "Data", 1 << 20, false, CancellationToken.None));

			Assert.Single(cells!.Ranges);
			Assert.Equal(end, stream.Delivered);
		}
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
		// XML that ends in the rows, inside a tag, is found by the read that looks for the ranges: token by token, at the
		// tag; raw, the rows aren't read, and the part ends inside sheetData.
		var whole = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\"><c r=\"A2\"><v>2</v></c></row>");
		var xml = whole.Substring(0, whole.IndexOf("<row r=\"2\">", StringComparison.Ordinal) + 4);
		var tag = Encoding.UTF8.GetByteCount(xml) - 4;
		var end = Encoding.UTF8.GetByteCount(xml);

		// A part that ends inside a comment in the rows: the same error both ways, at the comment.
		var comment = whole.Substring(0, whole.IndexOf("<row r=\"2\">", StringComparison.Ordinal)) + "<!-- no end";
		var commentAt = Encoding.UTF8.GetByteCount(comment) - "<!-- no end".Length;

		await InEveryModeAsync(mode =>
		{
			var e = Assert.Throws<CsvFormatException>(() => Find(xml, mode));
			Assert.Equal(
				mode.RawSkips
					? $"Sheet Data can't be read at byte {end} of {Sheets.PartName}: it ends inside an element."
					: $"Sheet Data can't be read at byte {tag} of {Sheets.PartName}: it ends inside a tag.",
				e.Message);
			Assert.IsType<InvalidDataException>(e.InnerException);
			Assert.Equal(0, e.LineNumber);

			e = Assert.Throws<CsvFormatException>(() => Find(comment, mode));
			Assert.Equal($"Sheet Data can't be read at byte {commentAt} of {Sheets.PartName}: it ends inside a comment.", e.Message);
		});
	}

	[Fact]
	public async Task LeavesTheRowsUnreadWithRawSkips()
	{
		// Rows whose XML can't be read: token by token, the read that looks for the ranges stops at them; raw, it finds the
		// ranges after them, and leaves the rows to the sheet's reader, which reports them at their cells.
		var cases = new (string Rows, string At, int From, string Detail)[]
		{
			("<row r=\"1\"><c r=\"A1\" note=\"a<b\"><v>1</v></c></row>", "a<b", 1, "an attribute value has '<', which XML doesn't allow"),
			("<row r=\"1\"><c r=\"A1\"><v>1</v></c><!x></row>", "<!x>", 0, "'<!' starts neither a comment nor a CDATA section"),
			("<row r=\"1\"><c r=\"A1\"><v>1</v></c x></row>", "</c x>", 0, "an end tag has more than a name"),
		};

		foreach (var (rows, at, from, detail) in cases)
		{
			var xml = Sheets.Worksheet(rows).Replace("</sheetData>", "</sheetData><mergeCells count=\"1\"><mergeCell ref=\"A1:B2\"/></mergeCells>");
			var offset = Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf(at, StringComparison.Ordinal) + from));

			await InEveryModeAsync(mode =>
			{
				if (mode.RawSkips)
				{
					Assert.Equal(["A1:B2"], Find(xml, mode)!.Ranges.Select(r => r.ToString()));
				}
				else
				{
					var e = Assert.Throws<CsvFormatException>(() => Find(xml, mode));
					Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
				}
			});
		}
	}

	[Fact]
	public async Task FindsTheRangesPastMarkupThatHoldsTheirTags()
	{
		// A comment, a processing instruction and a CDATA section in the rows that hold </sheetData> and a list of other
		// ranges, and markup in the list that holds </mergeCells>: neither read ends sheetData or the list there, and the
		// other ranges are never found (as EPPlus 4.5, which cuts the XML at the text </sheetData>, would).
		var fake = "</sheetData><mergeCells count=\"1\"><mergeCell ref=\"D9:E10\"/></mergeCells>";
		var rows = $"<row r=\"1\"><c r=\"A1\"><v>1</v></c><!-- {fake} --></row><?probe {fake}?><row r=\"2\"><c r=\"A2\" t=\"str\"><v><![CDATA[{fake}]]></v></c></row>";
		var merges = "<mergeCells count=\"2\"><mergeCell ref=\"A1:B1\"/><!-- </mergeCells> --><![CDATA[</mergeCells>]]><?probe </mergeCells>?><mergeCell ref=\"A2:B2\"/></mergeCells>";
		var xml = Sheets.Worksheet("").Replace("<sheetData></sheetData>", $"<sheetData>{rows}</sheetData>{merges}");

		await InEveryModeAsync(mode => Assert.Equal(["A1:B1", "A2:B2"], Find(xml, mode)!.Ranges.Select(r => r.ToString())));
	}

	[Fact]
	public async Task RefusesAnEndTagThatDoesntCloseTheWorksheet()
	{
		// The worksheet's children end at an end tag that isn't the worksheet's: the root's, misspelled; and, raw, the cell's
		// own end tag after a skip that stopped at a </sheetData> in the cell's attribute value, which token by token is the
		// '<' that XML doesn't allow there.
		var misspelled = Sheets.Worksheet(Rows).Replace("</worksheet>", "</worksheets>");
		var inValue = Sheets.Worksheet("").Replace("<sheetData></sheetData>", "<sheetData><row r=\"1\"><c r=\"A1\" note=\"</sheetData>\"><v>1</v></c></row></sheetData>");
		var misspelledAt = Encoding.UTF8.GetByteCount(misspelled.Substring(0, misspelled.IndexOf("</worksheets>", StringComparison.Ordinal)));
		var lessThan = Encoding.UTF8.GetByteCount(inValue.Substring(0, inValue.IndexOf("</sheetData>\"", StringComparison.Ordinal)));
		var cellEnd = Encoding.UTF8.GetByteCount(inValue.Substring(0, inValue.IndexOf("</c>", StringComparison.Ordinal)));

		await InEveryModeAsync(mode =>
		{
			var e = Assert.Throws<CsvFormatException>(() => Find(misspelled, mode));
			Assert.Equal($"Sheet Data can't be read at byte {misspelledAt} of {Sheets.PartName}: the end tag </worksheets> doesn't close worksheet.", e.Message);

			e = Assert.Throws<CsvFormatException>(() => Find(inValue, mode));
			Assert.Equal(
				mode.RawSkips
					? $"Sheet Data can't be read at byte {cellEnd} of {Sheets.PartName}: the end tag </c> doesn't close worksheet."
					: $"Sheet Data can't be read at byte {lessThan} of {Sheets.PartName}: an attribute value has '<', which XML doesn't allow.",
				e.Message);
			Assert.Null(e.Location);
		});
	}

	/// <summary>Finds the merged ranges of a part.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns>The ranges, or <see langword="null"/>.</returns>
	private static MergedCells? Find(string xml, ScanMode mode) => Read(Encoding.UTF8.GetBytes(xml), mode, 1 << 20, 1 << 20);

	/// <summary>Finds the merged ranges of a part, under limits, skipping <c>sheetData</c> raw or token by token as the mode says.</summary>
	/// <param name="bytes">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="windowSize">The window's size limit.</param>
	/// <param name="maxSize">The ranges' limit.</param>
	/// <returns>The ranges, or <see langword="null"/>.</returns>
	private static MergedCells? Read(byte[] bytes, ScanMode mode, long windowSize, long maxSize)
	{
		Stream stream = mode.Trickle ? new TrickleStream(bytes) : new MemoryStream(bytes);
		using var scanner = new XmlScanner(new XmlWindow(stream, Sheets.PartName, windowSize, mode.WindowSize)) { RawSkips = mode.RawSkips };

		return mode.Async
			? MergedCells.ReadAsync(scanner, "Data", maxSize, true, CancellationToken.None).AsTask().GetAwaiter().GetResult()
			: SyncOrAsync.Result(MergedCells.ReadAsync(scanner, "Data", maxSize, false, CancellationToken.None));
	}

	/// <summary>Runs a check in every <see cref="ScanMode"/>, with raw skips on and off, and says which mode failed.</summary>
	/// <param name="test">The check, for one mode.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	private static Task InEveryModeAsync(Action<ScanMode> test)
	{
		foreach (var mode in ScanMode.All.SelectMany(m => new[] { m, m.WithRawSkips(false) }))
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
