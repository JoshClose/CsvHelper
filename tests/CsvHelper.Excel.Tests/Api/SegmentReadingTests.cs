using System.Text;
using CsvHelper.Excel.Tests.Security;
using CsvHelper.Excel.Tests.Sheet;
using CsvHelper.Linq;
using Xunit.Abstractions;
#if NET
using System.IO.Compression;
using CsvHelper.Benchmarks;
#endif

namespace CsvHelper.Excel.Tests.Api;

/// <summary>A row of <see cref="SegmentSheets.Numbers"/>, for the parallel query.</summary>
[CsvRecord]
public partial class SegmentNumber
{
	/// <summary>A number cell.</summary>
	public int Id { get; set; }

	/// <summary>An inline string cell.</summary>
	public string Text { get; set; } = "";
}

/// <summary>
/// Reading a sheet in segments (Phase 9's step 8) through the public API. Under
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>, with segments forced through the workbook's
/// switches (cut at every row, or at 4 KB, from the first fill on), a sheet reads as it reads sequentially: rows with
/// references, without them, numbered by their first cell's, and mixed; missing rows across cuts, kept and skipped; areas
/// ending in a segment; hidden rows and columns; prefixed sheets; shown text; rows larger than a segment. The serial cases
/// (degree 1, segment scans off, sheets under the first megabyte, formulas as text, merged cells filled, UTF-16 parts, and
/// rows without references in an area that bounds rows) start no segments. Errors come after every record before the
/// failing row, as the sequential read's (type, message, location, line), at every degree with segments on and off; markup
/// falls back to the serial read; and an enumeration stopped early (a <c>break</c>, a selector's exception, an enumerator
/// disposed, the reader disposed) returns every buffer it rented, and the reader reads on in order. The workbook's counters
/// (<c>segmentStarts</c>, <c>segmentsStitched</c>, <c>segmentFallbacks</c>) say which path each read took.
/// </summary>
public class SegmentReadingTests
{
	/// <summary>The degrees most of these tests read at: the least that starts segments, a few, and more than CI's cores.</summary>
	private static readonly int[] Degrees = [2, 4, 16];

	/// <summary>The faults of <see cref="ThrowsTheSequentialErrorAfterTheRecordsBeforeIt"/>, each at row element 300.</summary>
	public static TheoryData<string> Faults => new()
	{
		"malformed row", "rows out of order", "an anchor after floating rows", "a cell past XFD", "a bad <! between cells", "a < in an attribute value",
	};

	/// <summary>The markup of <see cref="FallsBackAtMarkup"/>, each at row 150.</summary>
	public static TheoryData<string> Markups => new()
	{
		"a comment between rows", "a processing instruction between rows", "CDATA in a cell", "a comment in a cell", "a comment in a hidden row, skipped",
		"a comment in a hidden row, read",
	};

	[Fact]
	public async Task ReadsTheSequentialRecordsInSegments()
	{
		var reads = await AssertReadsAsSequentiallyAsync(SegmentSheets.Numbers(2000), ParallelReads.Small, Degrees, "2,000 numbers");

		// One pipeline a read, stitched to the end: a sheet of plain rows never falls back.
		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.True(read.Counts.Stitched >= (read.Size == 1 ? 1000 : 10), $"segments of {read.Size}, degree {read.Degree}: {read.Counts}"));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Fact]
	public async Task ReadsSeriallyAtDegreeOne()
	{
		var reads = await AssertReadsAsSequentiallyAsync(SegmentSheets.Numbers(2000), ParallelReads.Small, [1], "2,000 numbers");

		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Starts));
	}

	[Fact]
	public async Task ReadsSeriallyWithSegmentScansOff()
	{
		using var workbook = ExcelWorkbook.Open(SegmentSheets.Numbers(2000).ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		ParallelReads.Force(workbook, 1);
		workbook.segmentScans = false;

		var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, ParallelReads.Small, 4), "segment scans off");

		Assert.Null(ParallelReads.Difference(expected, actual));
		Assert.Equal(0L, actual.Counts.Starts);
	}

	[Fact]
	public async Task ReadsASheetUnderAMegabyteSeriallyByDefault()
	{
		// The workbook's switches as a reader has them: segments only past the part's first megabyte.
		var package = SegmentSheets.Numbers(6000);
		Assert.InRange(package.Text(Sheets.PartName).Length, 256 * 1024, 1024 * 1024);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);

		var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, ParallelReads.Small, 4), "6,000 numbers");

		Assert.Null(ParallelReads.Difference(expected, actual));
		Assert.Equal(0L, actual.Counts.Starts);
	}

	[Fact]
	public async Task StartsSegmentsPastTheFirstMegabyteByDefault()
	{
		var package = SegmentSheets.Numbers(30000);
		Assert.True(package.Text(Sheets.PartName).Length > 2 * 1024 * 1024);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var options = new ExcelReaderOptions();
		var expected = ParallelReads.ReadSequentially(workbook, null, options);

		var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, options, 4), "30,000 numbers");

		Assert.Null(ParallelReads.Difference(expected, actual));
		Assert.Equal(1L, actual.Counts.Starts);
		Assert.True(actual.Counts.Stitched >= 2, actual.Counts.ToString());
	}

	[Theory]
	[InlineData("formulas as text")]
	[InlineData("merged cells filled")]
	public async Task ReadsFormulaTextAndMergedCellsSerially(string reading)
	{
		// Shared formulas and merged fills carry state from row to row, which a segment can't start from.
		var rows = new StringBuilder(Workbooks.Numbers(1000));
		rows.Replace("<c r=\"A500\"><v>499</v></c>", "<c r=\"A500\"><f>A499+1</f><v>499</v></c>");
		var package = TestPackage.Workbook(new TestSheet("Data", xml: MergedScannerTests.Worksheet(rows.ToString(), "B3:B5")));
		var options = reading == "formulas as text"
			? ParallelReads.Small with { Formulas = ExcelFormulas.FormulaText }
			: ParallelReads.Small with { FillMergedCells = true };

		var reads = await AssertReadsAsSequentiallyAsync(package, options, Degrees, reading);

		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Starts));
	}

	[Fact]
	public async Task ReadsAUtf16PartSerially()
	{
		// The window reads a UTF-16 part through its transcoding stream, so the inflated bytes aren't the text segments scan.
		var package = SegmentSheets.Numbers(1000);
		var xml = package.Text(Sheets.PartName).Replace("encoding=\"UTF-8\"", "encoding=\"UTF-16\"");
		package.Set(Sheets.PartName, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(xml)]);

		var reads = await AssertReadsAsSequentiallyAsync(package, ParallelReads.Small, Degrees, "a UTF-16 part");

		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Starts));
	}

	[Fact]
	public async Task NumbersRowsWithoutReferencesAcrossCuts()
	{
		// Each segment's rows float, numbered from 1, and the stitcher rebases them after the rows before.
		var package = SegmentSheets.Numbers(2000, references: false);
		using (var workbook = ExcelWorkbook.Open(package.ToStream()))
		{
			var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
			Assert.Equal(Enumerable.Range(2, 2000).Select(n => $"{n}:"), expected.Records.Select(r => r.Substring(0, r.IndexOf(':') + 1)));
		}

		var reads = await AssertReadsAsSequentiallyAsync(package, ParallelReads.Small, Degrees, "2,000 numbers without references");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Fact]
	public async Task ReadsRowsNumberedByTheirFirstCell()
	{
		// No row has an r, but each cell has: a segment's first row is anchored by its first cell.
		var rows = new StringBuilder("<row><c r=\"A1\" t=\"inlineStr\"><is><t>Id</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Text</t></is></c></row>");
		for (var n = 2; n <= 2001; n++)
		{
			rows.Append($"<row><c r=\"A{n}\"><v>{n - 1}</v></c><c r=\"C{n}\" t=\"inlineStr\"><is><t>row {n - 1}</t></is></c></row>");
		}

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), ParallelReads.Small, Degrees, "rows numbered by their cells");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
	}

	[Fact]
	public async Task ReadsAnchorsAfterFloatingRows()
	{
		// Blocks of 20 rows with r and 20 without, in order. Cut at every row, an anchored segment follows a floating one,
		// which the stitcher checks; cut at 4 KB, a segment's floating rows come before an anchored one, which marks it.
		var rows = new StringBuilder();
		for (var n = 1; n <= 800; n++)
		{
			var reference = n / 20 % 2 == 0 ? $" r=\"{n}\"" : "";
			rows.Append($"<row{reference}><c><v>{n}</v></c><c t=\"inlineStr\"><is><t>row {n}</t></is></c></row>");
		}

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), ParallelReads.NoHeader, Degrees, "anchors after floating rows");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads.Where(r => r.Size == 1), read => Assert.Equal(0L, read.Counts.Fallbacks));
		Assert.All(reads.Where(r => r.Size > 1), read => Assert.True(read.Counts.Fallbacks >= 1, $"degree {read.Degree}: {read.Counts}"));
	}

	[Fact]
	public async Task NeverStartsSegmentsForRowsWithoutReferencesInAnArea()
	{
		// A floating row's place in an area that bounds rows isn't known in a segment.
		var package = SegmentSheets.Numbers(2000, references: false);

		var reads = await AssertReadsAsSequentiallyAsync(package, ParallelReads.Small with { Range = "A5:B1500" }, Degrees, "rows without references in A5:B1500");

		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Starts));
	}

	[Fact]
	public async Task FallsBackAtAFloatingRowInAnArea()
	{
		// Rows with r first, so that segments start, then rows without: the first floating segment falls back, and the
		// area ends in the rows read serially.
		var rows = new StringBuilder();
		for (var n = 1; n <= 1000; n++)
		{
			var reference = n <= 500 ? $" r=\"{n}\"" : "";
			rows.Append($"<row{reference}><c><v>{n}</v></c><c t=\"inlineStr\"><is><t>row {n}</t></is></c></row>");
		}

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), ParallelReads.NoHeader with { Range = "A2:B800" }, Degrees, "A2:B800");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.True(read.Counts.Fallbacks >= 1, $"segments of {read.Size}, degree {read.Degree}: {read.Counts}"));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task WritesMissingRowsAcrossCuts(bool skipEmptyRows)
	{
		// Rows 1 to 3, then gaps of up to 40 rows: kept, the missing rows between two segments are the stitcher's, and those
		// inside a segment its worker's.
		var random = new Random(8);
		var rows = new StringBuilder();
		var row = 0;
		for (var i = 0; i < 600; i++)
		{
			row += i < 3 ? 1 : 1 + random.Next(40);
			rows.Append($"<row r=\"{row}\"><c r=\"A{row}\"><v>{row}</v></c><c r=\"B{row}\" t=\"inlineStr\"><is><t>row {row}</t></is></c></row>");
		}

		var options = new ExcelReaderOptions { Csv = new() { HasHeader = false, SkipEmptyLines = skipEmptyRows, BufferSize = 256 } };

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), options, Degrees, $"missing rows {(skipEmptyRows ? "skipped" : "kept")}");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Fact]
	public async Task EndsAnAreaInASegment()
	{
		// The area's last row, 1234, is inside a segment: the segment ends the rows there, and none after it is read.
		var reads = await AssertReadsAsSequentiallyAsync(SegmentSheets.Numbers(3000), ParallelReads.Small with { Range = "A100:B1234" }, Degrees, "A100:B1234");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task SkipsHiddenRowsAndColumnsAcrossCuts(bool rows, bool columns)
	{
		// Every seventh row hidden, and column B.
		var xml = new StringBuilder();
		for (var n = 1; n <= 1500; n++)
		{
			var hidden = n % 7 == 0 ? " hidden=\"1\"" : "";
			xml.Append($"<row r=\"{n}\"{hidden}><c r=\"A{n}\"><v>{n}</v></c><c r=\"B{n}\"><v>{-n}</v></c><c r=\"C{n}\" t=\"inlineStr\"><is><t>row {n}</t></is></c></row>");
		}

		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet("", "<col min=\"2\" max=\"2\" hidden=\"1\"/>", xml.ToString())));
		var options = ParallelReads.NoHeader with { SkipHiddenRows = rows, SkipHiddenColumns = columns };

		var reads = await AssertReadsAsSequentiallyAsync(package, options, Degrees, $"hidden rows {(rows ? "skipped" : "read")}, columns {(columns ? "skipped" : "read")}");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
	}

	[Fact]
	public async Task ReadsAPrefixedSheet()
	{
		// The splitter cuts at <x:row, the prefix sheetData has.
		var rows = new StringBuilder();
		for (var n = 1; n <= 1500; n++)
		{
			rows.Append($"<x:row r=\"{n}\"><x:c r=\"A{n}\"><x:v>{n}</x:v></x:c><x:c r=\"B{n}\" t=\"inlineStr\"><x:is><x:t>row {n}</x:t></x:is></x:c></x:row>");
		}

		var part = TestPackage.Declaration + $"<x:worksheet xmlns:x=\"{TestPackage.MainNamespace}\"><x:sheetData>{rows}</x:sheetData></x:worksheet>";

		var reads = await AssertReadsAsSequentiallyAsync(TestPackage.Workbook(new TestSheet("Data", xml: part)), ParallelReads.NoHeader, Degrees, "a prefixed sheet");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Fact]
	public async Task CarriesStylesThroughSegments()
	{
		// FormattedTextTests' sheet of CarriesStylesThroughParallelEnumerate: numbers in #,##0.00 and 0.0%, and a header of
		// numbers shown Q1.
		var rows = new StringBuilder("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>N</t></is></c><c r=\"B1\" s=\"11\"><v>1</v></c></row>");
		for (var row = 2; row <= 3001; row++)
		{
			rows.Append($"<row r=\"{row}\"><c r=\"A{row}\" s=\"0\"><v>{row}</v></c><c r=\"B{row}\" s=\"{(row % 2 == 0 ? 2 : 5)}\"><v>{row}.5</v></c></row>");
		}

		var options = ParallelReads.Small with { Text = ExcelText.Formatted };

		var reads = await AssertReadsAsSequentiallyAsync(FormattedTextTests.Book(rows.ToString()), options, Degrees, "formatted");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
	}

	[Fact]
	public async Task ReadsRowsLargerThanASegment()
	{
		// Rows of 10 KB: cut at 4 KB, each segment is one row larger than the cut target.
		var rows = new StringBuilder();
		for (var n = 1; n <= 300; n++)
		{
			rows.Append($"<row r=\"{n}\"><c r=\"A{n}\"><v>{n}</v></c><c r=\"B{n}\" t=\"inlineStr\"><is><t>{new string((char)('a' + n % 26), 10_000)}</t></is></c></row>");
		}

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), ParallelReads.NoHeader, Degrees, "rows of 10 KB");

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.Equal(0L, read.Counts.Fallbacks));
	}

	[Fact]
	public async Task ReadsWithMoreScanThreadsThanProcessors()
	{
		var package = SegmentSheets.Numbers(2000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			ParallelReads.Force(workbook, size, threads: 2 * Environment.ProcessorCount + 1);

			var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, ParallelReads.Small, 16), $"segments of {size}");

			Assert.Null(ParallelReads.Difference(expected, actual));
			Assert.Equal(1L, actual.Counts.Starts);
		}
	}

	[Fact]
	public async Task ReadsAReaderOpenedAsynchronously()
	{
		// ParallelEnumerate is synchronous: a reader opened asynchronously enumerates in segments all the same.
		using var workbook = await ExcelWorkbook.OpenAsync(SegmentSheets.Numbers(2000).ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		ParallelReads.Force(workbook, 1);
		var before = SegmentCounts.Of(workbook);
		var reader = await workbook.OpenReaderAsync(ParallelReads.Small);

		var records = await ParallelReads.WithinAsync(
			() =>
			{
				using (reader)
				{
					return reader.ParallelEnumerate(r => ParallelReads.Describe(r), 4).ToList();
				}
			},
			"an asynchronous open");

		Assert.Equal(expected.Records, records);
		Assert.Equal(1L, (SegmentCounts.Of(workbook) - before).Starts);
	}

	[Theory]
	[MemberData(nameof(Faults))]
	public async Task ThrowsTheSequentialErrorAfterTheRecordsBeforeIt(string fault)
	{
		var package = Workbooks.Data(FaultyRows(fault));
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.NoHeader);

		// The rows before the fault read; a malformed row (a cell left open) reads as a record before the XML fails after it.
		Assert.Equal(fault == "malformed row" ? 300 : 299, expected.Records.Count);
		var error = Assert.IsType<CsvFormatException>(expected.Error);
		if (fault == "rows out of order")
		{
			Assert.StartsWith("Row Data!299:299 can't be read at byte", error.Message);
		}

		// At every degree, with segments forced or off: the failing fill's whole records before its error, then the error.
		foreach (var size in ParallelReads.Sizes)
		{
			foreach (var segments in new[] { true, false })
			{
				for (var degree = 1; degree <= 16; degree++)
				{
					ParallelReads.Force(workbook, size);
					workbook.segmentScans = segments;
					var at = degree;

					var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, ParallelReads.NoHeader, at), $"{fault}, degree {at}");

					var difference = ParallelReads.Difference(expected, actual);
					Assert.True(difference is null, $"{fault}, segments {(segments ? $"of {size} bytes" : "off")}, degree {at}: {difference}");
				}
			}
		}
	}

	[Theory]
	[MemberData(nameof(Markups))]
	public async Task FallsBackAtMarkup(string markup)
	{
		// A worker searches its segment for <! and <? before it scans: the stitcher reads the sheet serially from there.
		var rows = new StringBuilder();
		for (var n = 1; n <= 300; n++)
		{
			var cell = $"<c r=\"A{n}\"><v>{n}</v></c>";
			var text = $"<c r=\"B{n}\" t=\"inlineStr\"><is><t>row {n}</t></is></c>";
			var start = $"<row r=\"{n}\">";
			var after = "";
			if (n == 150)
			{
				switch (markup)
				{
					case "a comment between rows":
						after = "<!-- a note -->";

						break;
					case "a processing instruction between rows":
						after = "<?producer note?>";

						break;
					case "CDATA in a cell":
						text = $"<c r=\"B{n}\" t=\"inlineStr\"><is><t><![CDATA[a<b & c]]></t></is></c>";

						break;
					case "a comment in a cell":
						cell = $"<c r=\"A{n}\"><!-- a note --><v>{n}</v></c>";

						break;
					default:
						start = $"<row r=\"{n}\" hidden=\"1\">";
						text += "<!-- a note -->";

						break;
				}
			}

			rows.Append(start).Append(cell).Append(text).Append("</row>").Append(after);
		}

		var options = ParallelReads.NoHeader with { SkipHiddenRows = markup == "a comment in a hidden row, skipped" };

		var reads = await AssertReadsAsSequentiallyAsync(Workbooks.Data(rows.ToString()), options, Degrees, markup);

		Assert.All(reads, read => Assert.Equal(1L, read.Counts.Starts));
		Assert.All(reads, read => Assert.True(read.Counts.Fallbacks >= 1, $"segments of {read.Size}, degree {read.Degree}: {read.Counts}"));
	}

	[Fact]
	public async Task FallsBackAtACutInsideAComment()
	{
		// The comment holds what the splitter takes for a row start, so a segment starts inside it, after one that holds
		// its start: that one falls back first, and the serial read reads the comment as a comment.
		var rows = new StringBuilder();
		for (var n = 1; n <= 300; n++)
		{
			rows.Append($"<row r=\"{n}\"><c r=\"A{n}\"><v>{n}</v></c></row>");
			if (n == 150)
			{
				rows.Append("<!-- <row r=\"151\"><c r=\"A151\"><v>999</v></c></row> -->");
			}
		}

		var package = Workbooks.Data(rows.ToString());
		using (var workbook = ExcelWorkbook.Open(package.ToStream()))
		{
			var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.NoHeader);
			Assert.Equal(300, expected.Records.Count);
			Assert.DoesNotContain(expected.Records, r => r.Contains("999"));
		}

		var reads = await AssertReadsAsSequentiallyAsync(package, ParallelReads.NoHeader, Degrees, "a cut inside a comment");

		Assert.All(reads, read => Assert.True(read.Counts.Fallbacks >= 1, $"segments of {read.Size}, degree {read.Degree}: {read.Counts}"));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(500)]
	[InlineData(2999)]
	public async Task StopsAtABreak(int count)
	{
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);
			var before = SegmentCounts.Of(workbook);

			var seen = await ParallelReads.WithinAsync(
				() =>
				{
					using var reader = workbook.OpenReader(ParallelReads.Small);

					return TakeInParallel(reader, count);
				},
				$"a break after {count}");

			Assert.Equal(expected.Records.Take(count), seen);
			pool.AssertReturned($"a break after {count}, segments of {size}");
			if (count >= 500)
			{
				// The pipeline started, and took its buffers from the workbook's pool.
				Assert.Equal(1L, (SegmentCounts.Of(workbook) - before).Starts);
				Assert.True(pool.Rents > 0, $"a break after {count}, segments of {size}: no array rented");
			}
		}
	}

	[Fact]
	public async Task ThrowsTheSelectorsExceptionAfterTheRecordsBeforeIt()
	{
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);

			var (records, error) = await ParallelReads.WithinAsync(
				() =>
				{
					using var reader = workbook.OpenReader(ParallelReads.Small);
					var seen = new List<string>();
					var e = Record.Exception(() =>
					{
						foreach (var record in reader.ParallelEnumerate(r => r.LineNumber == 1500 ? throw new InvalidOperationException("The selector failed at line 1500.") : ParallelReads.Describe(r), 4))
						{
							seen.Add(record);
						}
					});

					return (seen, e);
				},
				"a selector's exception");

			// Lines 2 to 1499: the records before the one the selector failed on.
			Assert.Equal(expected.Records.Take(1498), records);
			Assert.Equal("The selector failed at line 1500.", Assert.IsType<InvalidOperationException>(error).Message);
			pool.AssertReturned($"a selector's exception, segments of {size}");
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public async Task DisposesAnEnumeratorBeforeItEnds(int moves)
	{
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);
			var before = SegmentCounts.Of(workbook);

			var (taken, rest) = await ParallelReads.WithinAsync(
				() =>
				{
					using var reader = workbook.OpenReader(ParallelReads.Small);
					var records = new List<string>();
					using (var enumerator = reader.ParallelEnumerate(r => ParallelReads.Describe(r), 4).GetEnumerator())
					{
						for (var i = 0; i < moves && enumerator.MoveNext(); i++)
						{
							records.Add(enumerator.Current);
						}
					}

					return (records, ReadAll(reader));
				},
				$"an enumerator disposed after {moves} records");

			Assert.Equal(expected.Records.Take(moves), taken);
			pool.AssertReturned($"an enumerator disposed after {moves} records, segments of {size}");
			if (moves == 0)
			{
				// An enumeration that never began never began segments: the reader reads every record.
				Assert.Equal(expected.Records, rest);
				Assert.Equal(0L, (SegmentCounts.Of(workbook) - before).Starts);
			}
			else
			{
				ParallelReads.AssertReadsOn(expected.Records, moves, rest, $"after {moves} records, segments of {size}");
			}
		}
	}

	[Fact]
	public async Task DisposesTheReaderAfterAnEarlyStop()
	{
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);
			var before = SegmentCounts.Of(workbook);

			var seen = await ParallelReads.WithinAsync(
				() =>
				{
					var reader = workbook.OpenReader(ParallelReads.Small);
					var records = TakeInParallel(reader, 100);
					reader.Dispose();

					return records;
				},
				"the reader disposed after an early stop");

			Assert.Equal(expected.Records.Take(100), seen);
			Assert.Equal(1L, (SegmentCounts.Of(workbook) - before).Starts);
			pool.AssertReturned($"the reader disposed after an early stop, segments of {size}");

			// The workbook reads on: nothing of the stopped read is left open.
			Assert.Null(ParallelReads.Difference(expected, ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small)));
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(2000)]
	public async Task ReadsOnAfterAnEarlyStop(int count)
	{
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);

			var (taken, rest) = await ParallelReads.WithinAsync(
				() =>
				{
					using var reader = workbook.OpenReader(ParallelReads.Small);
					var records = TakeInParallel(reader, count);

					return (records, ReadAll(reader));
				},
				$"Read after {count} records");

			Assert.Equal(expected.Records.Take(count), taken);
			ParallelReads.AssertReadsOn(expected.Records, count, rest, $"Read after {count} records, segments of {size}");
			pool.AssertReturned($"Read after {count} records, segments of {size}");
		}
	}

	[Fact]
	public async Task EnumeratesAgainAfterAnEarlyStop()
	{
		// The second enumeration reads on serially: a source starts segments once.
		var package = SegmentSheets.Numbers(3000);
		using var workbook = ExcelWorkbook.Open(package.ToStream());
		var expected = ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small);
		foreach (var size in ParallelReads.Sizes)
		{
			var pool = new CheckingSegmentPool();
			ParallelReads.Force(workbook, size, pool);
			var before = SegmentCounts.Of(workbook);

			var (taken, rest) = await ParallelReads.WithinAsync(
				() =>
				{
					using var reader = workbook.OpenReader(ParallelReads.Small);
					var records = TakeInParallel(reader, 100);

					return (records, reader.ParallelEnumerate(r => ParallelReads.Describe(r), 4).ToList());
				},
				"a second enumeration");

			Assert.Equal(expected.Records.Take(100), taken);
			ParallelReads.AssertReadsOn(expected.Records, 100, rest, $"a second enumeration, segments of {size}");
			Assert.Equal(1L, (SegmentCounts.Of(workbook) - before).Starts);
			pool.AssertReturned($"a second enumeration, segments of {size}");
		}
	}

	[Fact]
	public async Task QueriesASheetInParallelWithTake()
	{
		// CsvHelper.Linq's parallel query filters and maps on ParallelEnumerate's workers; Take stops the enumeration early,
		// and the reader reads on after it.
		using var workbook = ExcelWorkbook.Open(SegmentSheets.Numbers(3000).ToStream());
		ParallelReads.Force(workbook, 1);
		var before = SegmentCounts.Of(workbook);

		var (ids, next) = await ParallelReads.WithinAsync(
			() =>
			{
				using var reader = workbook.OpenReader(ParallelReads.Small);
				var query = reader.Query<SegmentNumber>(new CsvQueryOptions { MaxDegreeOfParallelism = 4 }).Where(n => n.Id % 7 == 0).Select(n => n.Id).Take(50).ToList();

				return (query, reader.Read() ? reader.Parse<int>(0) : -1);
			},
			"a parallel query");

		Assert.Equal(Enumerable.Range(1, 3000).Where(i => i % 7 == 0).Take(50), ids);
		Assert.InRange(next, 351, 3000);
		Assert.Equal(1L, (SegmentCounts.Of(workbook) - before).Starts);
	}

	/// <summary>
	/// Reads a workbook sequentially, then under <c>ParallelEnumerate</c> with segments forced at each of
	/// <see cref="ParallelReads.Sizes"/> and each degree, and checks that each parallel read gives the sequential records,
	/// header and error.
	/// </summary>
	/// <param name="package">The workbook.</param>
	/// <param name="options">The options of the workbook and of every reader.</param>
	/// <param name="degrees">The degrees to read at.</param>
	/// <param name="what">What is read, for failures.</param>
	/// <returns>Each parallel read's segment size, degree and the counters it added, in order.</returns>
	private static async Task<List<(int Size, int Degree, SegmentCounts Counts)>> AssertReadsAsSequentiallyAsync(
		TestPackage package, ExcelReaderOptions options, int[] degrees, string what)
	{
		using var workbook = ExcelWorkbook.Open(package.ToStream(), options);
		var expected = ParallelReads.ReadSequentially(workbook, null, options);
		var reads = new List<(int Size, int Degree, SegmentCounts Counts)>();
		foreach (var size in ParallelReads.Sizes)
		{
			foreach (var degree in degrees)
			{
				ParallelReads.Force(workbook, size);

				var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, options, degree), $"{what}, degree {degree}, segments of {size}");

				var difference = ParallelReads.Difference(expected, actual);
				Assert.True(difference is null, $"{what}, degree {degree}, segments of {size} bytes: {difference}");
				reads.Add((size, degree, actual.Counts));
			}
		}

		return reads;
	}

	/// <summary>
	/// A sheet of 600 numbers, without a header, with a fault at row element 300: the rows before it read, and it throws
	/// the sheet's error there.
	/// </summary>
	/// <param name="fault">The fault, one of <see cref="Faults"/>.</param>
	/// <returns>The content of <c>sheetData</c>.</returns>
	private static string FaultyRows(string fault)
	{
		var rows = new StringBuilder();
		for (var n = 1; n <= 600; n++)
		{
			var cells = $"<c r=\"A{n}\"><v>{n}</v></c><c r=\"B{n}\" t=\"inlineStr\"><is><t>row {n}</t></is></c>";
			var start = $"<row r=\"{n}\">";
			if (fault == "an anchor after floating rows" && n > 200 && n < 300)
			{
				// Rows 201 to 299 without r, after which row element 300 says it is row 250.
				cells = $"<c><v>{n}</v></c><c t=\"inlineStr\"><is><t>row {n}</t></is></c>";
				start = "<row>";
			}

			if (n == 300)
			{
				switch (fault)
				{
					case "malformed row":
						cells = $"<c r=\"A{n}\"><v>{n}</v><c r=\"B{n}\"><v>1</v></c>";

						break;
					case "rows out of order":
						start = "<row r=\"299\">";
						cells = "<c r=\"A299\"><v>1</v></c>";

						break;
					case "an anchor after floating rows":
						start = "<row r=\"250\">";
						cells = "<c r=\"A250\"><v>1</v></c>";

						break;
					case "a cell past XFD":
						cells = $"<c r=\"A{n}\"><v>{n}</v></c><c r=\"XFE{n}\"><v>1</v></c>";

						break;
					case "a bad <! between cells":
						// Markup to the worker's search, which falls back; an error to the serial read.
						cells = $"<c r=\"A{n}\"><v>{n}</v></c><!x><c r=\"B{n}\"><v>1</v></c>";

						break;
					default:
						// The splitter cuts at the <row in the attribute's value, inside the tag.
						cells = $"<c r=\"A{n}\" t=\"str\" note=\"a<row r=&quot;1&quot;>\"><v>{n}</v></c>";

						break;
				}
			}

			rows.Append(start).Append(cells).Append("</row>");
		}

		return rows.ToString();
	}

	/// <summary>Enumerates a reader's records in parallel at degree 4, and stops after some.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="count">The records to take.</param>
	/// <returns>The records taken.</returns>
	private static List<string> TakeInParallel(CsvReader reader, int count)
	{
		var records = new List<string>();
		foreach (var record in reader.ParallelEnumerate(r => ParallelReads.Describe(r), 4))
		{
			records.Add(record);
			if (records.Count == count)
			{
				break;
			}
		}

		return records;
	}

	/// <summary>Reads the rest of a reader's records with <see cref="CsvReader.Read"/>, each described.</summary>
	/// <param name="reader">The reader.</param>
	/// <returns>The records.</returns>
	private static List<string> ReadAll(CsvReader reader)
	{
		var records = new List<string>();
		while (reader.Read())
		{
			records.Add(ParallelReads.Describe(reader));
		}

		return records;
	}
}

/// <summary>
/// The differential over generated sheets and the orders (the plan's "Generated sheets", "the gate's 'the serial records in
/// order at degrees 1 to 16'"): each sheet read under <c>ParallelEnumerate</c> at every degree from 1 to 16, with segments
/// forced at a byte and at 4 KB, gives each record's line, each field's stored bytes, text, shown text and typed reads, and
/// the error, as the sequential read with the same options gives them; and a stress loop of early stops with random delays,
/// over a checking pool. They run alone (<see cref="TimedCollection"/>), so that their time limits measure their own
/// reads. <c>CSVHELPER_EXCEL_SEGMENT_INPUTS</c> sets the number of generated sheets and <c>CSVHELPER_EXCEL_SEGMENT_SEED</c>
/// the first seed, so that a failure can be replayed alone; <c>CSVHELPER_EXCEL_SEGMENT_STRESS</c> sets the stress loop's
/// rounds.
/// </summary>
/// <param name="output">Shows what the reads covered.</param>
[Collection(TimedCollection.Name)]
public class SegmentDifferentialTests(ITestOutputHelper output)
{
	/// <summary>The generated sheets a run reads, unless <c>CSVHELPER_EXCEL_SEGMENT_INPUTS</c> says otherwise.</summary>
	private const int DefaultInputs = 40;

	/// <summary>The stress loop's rounds, unless <c>CSVHELPER_EXCEL_SEGMENT_STRESS</c> says otherwise.</summary>
	private const int DefaultRounds = 200;

	/// <summary>The ways the stress loop ends an enumeration.</summary>
	private static readonly string[] Stops = ["break", "selector", "unread", "disposed", "read on", "again"];

#if NET
	/// <summary>The orders at 100,000, as the typed benchmark writes them: shared strings, numbers, dates and booleans, made once.</summary>
	private static readonly Lazy<byte[]> Orders = new(() =>
	{
		using var stream = new MemoryStream();
		ExcelTypedWorkbook.Write(stream, ExcelOrders.SheetName, ExcelOrders.Rows(ExcelOrders.Generate(100_000)), ExcelOrders.Formats, CompressionLevel.Fastest);

		return stream.ToArray();
	});
#endif

	[Fact]
	public async Task ReadsGeneratedSheetsAtEveryDegree()
	{
		var inputs = Setting("CSVHELPER_EXCEL_SEGMENT_INPUTS", DefaultInputs);
		var first = Setting("CSVHELPER_EXCEL_SEGMENT_SEED", 1);
		var failures = new List<string>();
		var counts = default(SegmentCounts);
		var errors = 0;
		for (var seed = first; seed < first + inputs && failures.Count < 5; seed++)
		{
			var sheet = SegmentSheets.Generate(seed);
			using var workbook = ExcelWorkbook.Open(sheet.Package.ToStream(), sheet.Options);
			var expected = ParallelReads.ReadSequentially(workbook, null, sheet.Options);
			errors += expected.Error is null ? 0 : 1;
			foreach (var size in ParallelReads.Sizes)
			{
				foreach (var degree in Enumerable.Range(1, 16))
				{
					ParallelReads.Force(workbook, size);

					var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, sheet.Options, degree), $"{sheet.Description}; degree {degree}");

					counts += actual.Counts;
					if (ParallelReads.Difference(expected, actual) is { } difference)
					{
						failures.Add($"{sheet.Description}; degree {degree}, segments of {size} bytes: {difference}");

						break;
					}
				}
			}
		}

		output.WriteLine($"Seeds {first} to {first + inputs - 1}: {errors} sheets end in an error; the parallel reads: {counts}.");
		Assert.True(failures.Count == 0, string.Join("\n\n", failures) + "\n\nReplay one with CSVHELPER_EXCEL_SEGMENT_SEED=<seed> and CSVHELPER_EXCEL_SEGMENT_INPUTS=1.");

		// The sheets reach every path: segments started, stitched, and fallen back from.
		Assert.True(counts.Starts > 0 && counts.Stitched > 0 && counts.Fallbacks > 0, counts.ToString());
	}

#if NET
	[Fact]
	public async Task ReadsTheOrdersAtEveryDegree()
	{
		// A large shared-strings table loaded before the first segment, and 100,000 rows of 12 typed cells.
		var options = new ExcelReaderOptions();
		SheetReading expected;
		using (var workbook = ExcelWorkbook.Open(new MemoryStream(Orders.Value, false)))
		{
			expected = ParallelReads.ReadSequentially(workbook, null, options);
		}

		Assert.Equal(100_000, expected.Records.Count);
		foreach (var size in ParallelReads.Sizes)
		{
			foreach (var degree in Enumerable.Range(1, 16))
			{
				// A workbook a read, whose total of inflated bytes starts at 0.
				using var workbook = ExcelWorkbook.Open(new MemoryStream(Orders.Value, false));
				ParallelReads.Force(workbook, size);

				var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, options, degree), $"the orders, degree {degree}", TimeSpan.FromMinutes(5));

				var difference = ParallelReads.Difference(expected, actual);
				Assert.True(difference is null, $"the orders, degree {degree}, segments of {size} bytes: {difference}");
				Assert.Equal(degree == 1 ? 0L : 1L, actual.Counts.Starts);
			}
		}
	}
#endif

	[Fact]
	public async Task StopsEnumerationsEarlyUnderStress()
	{
		// Rounds of each early stop, at random degrees, segment sizes and scan threads, with a selector that spins for a
		// time each line sets: each round's records are the sequential ones it should be, and its pool gets back every array.
		var rounds = Setting("CSVHELPER_EXCEL_SEGMENT_STRESS", DefaultRounds);
		var random = new Random(20261125);
		var packages = new[] { SegmentSheets.Numbers(2000), SegmentSheets.Numbers(2000, references: false) };
		var expected = packages.Select(p =>
		{
			using var workbook = ExcelWorkbook.Open(p.ToStream());

			return ParallelReads.ReadSequentially(workbook, null, ParallelReads.Small).Records;
		}).ToArray();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var round = 0; round < rounds; round++)
		{
			var sheet = random.Next(packages.Length);
			var stop = Stops[random.Next(Stops.Length)];
			var degree = 2 + random.Next(15);
			var size = new[] { 1, 64, 4096 }[random.Next(3)];
			var threads = random.Next(2) == 0 ? 0 : degree;
			var count = 1 + random.Next(expected[sheet].Count - 1);
			var what = $"round {round}: {stop} after {count}, degree {degree}, segments of {size}, {threads} scan threads, sheet {sheet}";
			var pool = new CheckingSegmentPool();
			using var workbook = ExcelWorkbook.Open(packages[sheet].ToStream());
			ParallelReads.Force(workbook, size, pool, threads);

			await ParallelReads.WithinAsync(() => StopEarly(workbook, stop, degree, count, expected[sheet], what), what);

			pool.AssertReturned(what);
			tally[stop] = tally.TryGetValue(stop, out var n) ? n + 1 : 1;
		}

		output.WriteLine($"{rounds} rounds: {string.Join(", ", tally.Select(p => $"{p.Key} {p.Value}"))}.");
	}

	/// <summary>A selector that spins for a time its record's line sets, so that the workers finish out of order.</summary>
	/// <param name="reader">The worker's reader.</param>
	/// <returns>The record, described.</returns>
	private static string Slow(CsvReader reader)
	{
		var line = reader.LineNumber;
		Thread.SpinWait((int)(line * 7919 % 4000));
		if (line % 97 == 0)
		{
			Thread.Yield();
		}

		return ParallelReads.Describe(reader);
	}

	/// <summary>Ends an enumeration of a workbook's first sheet early, one of <see cref="Stops"/>, and checks what it read.</summary>
	/// <param name="workbook">The workbook, its switches set.</param>
	/// <param name="stop">How the enumeration ends.</param>
	/// <param name="degree">The enumeration's degree.</param>
	/// <param name="count">The records after which it ends.</param>
	/// <param name="expected">The sequential records.</param>
	/// <param name="what">The round, for failures.</param>
	/// <returns>The records read.</returns>
	private static int StopEarly(ExcelWorkbook workbook, string stop, int degree, int count, List<string> expected, string what)
	{
		var reader = workbook.OpenReader(ParallelReads.Small);
		var records = new List<string>();
		try
		{
			switch (stop)
			{
				case "selector":
					var e = Record.Exception(() =>
					{
						foreach (var record in reader.ParallelEnumerate(r => r.LineNumber == count + 1 ? throw new InvalidOperationException("stop") : Slow(r), degree))
						{
							records.Add(record);
						}
					});
					Assert.True(e is InvalidOperationException { Message: "stop" }, $"{what}: {ParallelReads.Show(e)}");
					Assert.True(expected.Take(count - 1).SequenceEqual(records), $"{what}: {records.Count} records before the selector's exception.");

					break;
				case "unread":
					using (var enumerator = reader.ParallelEnumerate(r => Slow(r), degree).GetEnumerator())
					{
						for (var i = 0; i < count % 3 && enumerator.MoveNext(); i++)
						{
							records.Add(enumerator.Current);
						}
					}

					Assert.True(expected.Take(records.Count).SequenceEqual(records), $"{what}: the records before the enumerator was disposed.");

					break;
				default:
					foreach (var record in reader.ParallelEnumerate(r => Slow(r), degree))
					{
						records.Add(record);
						if (records.Count == count)
						{
							break;
						}
					}

					Assert.True(expected.Take(count).SequenceEqual(records), $"{what}: the records before the break.");
					if (stop == "read on")
					{
						var rest = new List<string>();
						while (reader.Read())
						{
							rest.Add(ParallelReads.Describe(reader));
						}

						ParallelReads.AssertReadsOn(expected, count, rest, what);
					}
					else if (stop == "again")
					{
						ParallelReads.AssertReadsOn(expected, count, reader.ParallelEnumerate(r => Slow(r), degree).ToList(), what);
					}

					break;
			}
		}
		finally
		{
			// "disposed" ends here too: the reader disposed after its early stop, as every round's is.
			reader.Dispose();
		}

		return records.Count;
	}

	/// <summary>The value of an integer environment variable.</summary>
	/// <param name="name">The variable.</param>
	/// <param name="fallback">The value when it isn't set.</param>
	/// <returns>The value.</returns>
	private static int Setting(string name, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;
}

#if NET
/// <summary>
/// What reading in segments allocates (the plan's "Allocation"): a parallel read of a sheet's records as spans, as
/// <c>Rows</c> reads them, cut at 4 KB, allocates within a fixed budget, and 100,000 rows no more than 10,000 do beyond a
/// little slack, though they are ten times the segments: nothing a segment. It measures the process's allocations, which
/// the pipeline's threads make, so it runs alone (<see cref="TimedCollection"/>), and only optimized
/// (<see cref="OptimizedFactAttribute"/>).
/// </summary>
/// <param name="output">Shows what the reads allocated.</param>
[Collection(TimedCollection.Name)]
public class SegmentAllocationTests(ITestOutputHelper output)
{
	/// <summary>The rows of the smaller sheet; the larger has ten times as many.</summary>
	private const int Rows = 10_000;

	/// <summary>The most the larger sheet's read may allocate beyond the smaller's: far less than a hundred bytes a segment more.</summary>
	private const long Slack = 64 * 1024;

	/// <summary>The most either read may allocate: the pipeline's threads, slots and blocks, and core's batches, once each.</summary>
	private const long Budget = 4L * 1024 * 1024;

	/// <summary>The smaller sheet, made once.</summary>
	private static readonly Lazy<byte[]> Few = new(() => SegmentSheets.Numbers(Rows).ToArray());

	/// <summary>The larger sheet, made once.</summary>
	private static readonly Lazy<byte[]> Many = new(() => SegmentSheets.Numbers(10 * Rows).ToArray());

	[OptimizedFact]
	public void ReadsInSegmentsWithoutAllocatingPerSegment()
	{
		var (few, fewSegments) = Measure(Few.Value);
		var (many, manySegments) = Measure(Many.Value);
		output.WriteLine($"{Rows:N0} rows in {fewSegments:N0} segments allocated {few:N0} bytes; {10 * Rows:N0} rows in {manySegments:N0} segments, {many:N0}.");

		Assert.True(manySegments >= 5 * fewSegments, $"{fewSegments} and {manySegments} segments");
		Assert.True(few < Budget && many < Budget, $"{few:N0} and {many:N0} bytes");
		Assert.True(many - few < Slack, $"{Rows:N0} rows allocated {few:N0} bytes and {10 * Rows:N0} rows {many:N0}.");
	}

	/// <summary>Measures a read: once first, then three times, keeping the smallest, as the other allocation tests do.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The bytes the smallest read allocated, and the segments it stitched.</returns>
	private static (long Bytes, long Segments) Measure(byte[] workbook)
	{
		Allocated(workbook);
		var reads = new[] { Allocated(workbook), Allocated(workbook), Allocated(workbook) };

		return reads.OrderBy(r => r.Bytes).First();
	}

	/// <summary>
	/// Reads every field of a sheet as a span under <c>ParallelEnumerate</c> at degree 4, segments cut at 4 KB, and measures
	/// what the enumeration allocated on every thread; the workbook's open and the reader's are outside the measure.
	/// </summary>
	/// <param name="bytes">The workbook's bytes.</param>
	/// <returns>The bytes allocated, and the segments stitched.</returns>
	private static (long Bytes, long Segments) Allocated(byte[] bytes)
	{
		using var workbook = ExcelWorkbook.Open(new MemoryStream(bytes, false));
		ParallelReads.Force(workbook, 4096);
		var before = SegmentCounts.Of(workbook);
		long allocated;
		using (var reader = workbook.OpenReader(ParallelReads.Small))
		{
			var start = GC.GetTotalAllocatedBytes(true);
			var length = reader.ParallelEnumerate(Spans, 4).Sum();
			allocated = GC.GetTotalAllocatedBytes(true) - start;
			Assert.True(length > 0);
		}

		return (allocated, (SegmentCounts.Of(workbook) - before).Stitched);
	}

	/// <summary>Reads every field of a record as a span.</summary>
	/// <param name="reader">The worker's reader.</param>
	/// <returns>The fields' total length.</returns>
	private static long Spans(CsvReader reader)
	{
		var length = 0L;
		for (var i = 0; i < reader.FieldCount; i++)
		{
			length += reader.GetSpan(i).Length;
		}

		return length;
	}
}
#endif
