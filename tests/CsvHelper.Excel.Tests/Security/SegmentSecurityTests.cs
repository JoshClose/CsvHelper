using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Sheet;
#if NET
using Xunit.Abstractions;
#endif

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The guard's limits with segments (Phase 9's step 8, the plan's "The guard's limits"). The security suite's sheet bombs
/// (<see cref="SecurityTests"/>: gaps to <c>XFD</c>, missing rows, a repeated shared string, merged fills, which read
/// serially, and white space that inflates past the limit), read under
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> with segments forced, end in the sequential read's
/// exception and message. The records before a trip are a prefix of the sequential ones, as many within a window's and a
/// segment's bytes of the sequential read's: the pipeline inflates ahead uncounted, and the stitcher charges a segment
/// before it appends one, falling back to the serial read where the limit would trip, whose window then reads the same
/// bytes from another place. Where nothing but the order of reading decides, the records are the same. Rows longer than
/// the part limit read as they read sequentially; a corrupt deflate stream ends the read as it ends the sequential one;
/// and every inflated byte counts once, whoever inflated it, so a workbook's limit stops the same read of a sheet whether
/// its reads were sequential or in segments. Each read is in a workbook of its own, whose total starts at 0, unless it
/// counts reads.
/// </summary>
public class SegmentSecurityTests
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The degree the bombs are read at: more workers than the least, and than one block of the ring.</summary>
	private const int Degree = 4;

	/// <summary>The message of a read that takes the workbook past its limit, up to the limit's size.</summary>
	private const string PastTheLimit = "Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize";

	/// <summary>How long a bomb's read may take: the sequential read of some takes seconds.</summary>
	private static readonly TimeSpan BombLimit = TimeSpan.FromMinutes(5);

	/// <summary>A worksheet part as <see cref="Sheets.Worksheet(string)"/> writes it, up to its rows.</summary>
	private static readonly string SheetStart = Sheets.Worksheet("").Substring(0, Sheets.Worksheet("").IndexOf("</sheetData>", StringComparison.Ordinal));

	/// <summary>A worksheet part as <see cref="Sheets.Worksheet(string)"/> writes it, from after its rows.</summary>
	private static readonly string SheetEnd = Sheets.Worksheet("").Substring(SheetStart.Length);

	[Fact]
	public async Task StopsAGapBombAsTheSequentialReadDoes()
	{
		// A million rows of one cell in XFD (SecurityTests' gap amplification), under a 16 MB limit: each row's 16,383 empty
		// fields count a byte each. A segment's block passes its cap within a few dozen rows, so the stitcher falls back,
		// and the serial read trips within a few rows of where the sequential read trips.
		var zip = Workbooks.Data("").SetStreamed(Sheets.PartName, stream => WriteRows(stream, 1_000_000, n => $"<row r=\"{n}\"><c r=\"XFD{n}\"><v>1</v></c></row>")).ToArray();

		var reads = await AssertTripsAsSequentiallyAsync(zip, NoHeader(maxUncompressedSize: 16L * MiB), 36 + CellReference.MaxColumns - 1, "gaps to XFD");

		Assert.All(reads, counts => Assert.Equal(1L, counts.Starts));
		Assert.All(reads, counts => Assert.True(counts.Fallbacks >= 1, counts.ToString()));
	}

	[Theory]
	[InlineData("rows")]
	[InlineData("cells")]
	public async Task StopsARepeatedSharedStringAsTheSequentialReadDoes(string where)
	{
		// One shared string of a megabyte, read by a million rows or by a row of 16,384 cells, under a 16 MB limit: each cell
		// counts the string's bytes beyond its own XML. In rows, a block takes a megabyte a row and passes its cap at its
		// fourth; in one row, the first fill never ends, and no segment begins.
		var zip = Workbooks.Data("").SharedStrings($"<t>{new string('x', MiB)}</t>").SetStreamed(Sheets.PartName, stream =>
		{
			if (where == "rows")
			{
				WriteRows(stream, 1_000_000, n => $"<row r=\"{n}\"><c r=\"A{n}\" t=\"s\"><v>0</v></c></row>");
			}
			else
			{
				Write(stream, SheetStart + "<row r=\"1\">");
				WriteRows(stream, CellReference.MaxColumns, _ => "<c t=\"s\"><v>0</v></c>", false);
				Write(stream, "</row>" + SheetEnd);
			}
		}).ToArray();

		var reads = await AssertTripsAsSequentiallyAsync(zip, NoHeader(maxPartSize: 4L * MiB, maxUncompressedSize: 16L * MiB), MiB, $"a repeated shared string in {where}");

		Assert.All(reads, counts => Assert.Equal(where == "rows" ? 1L : 0L, counts.Starts));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsMissingRowsAsTheSequentialReadDoes(bool skipEmptyRows)
	{
		// 500 rows, then the grid's last: kept, the missing rows between count a byte each, written by the stitcher between
		// two segments, or by the serial read once it falls back, and a limit of 300,000 bytes stops them; skipped, they
		// count nothing, and every row reads.
		var rows = string.Concat(Enumerable.Range(1, 500).Select(n => $"<row r=\"{n}\"><c r=\"A{n}\"><v>{n}</v></c></row>")) +
			$"<row r=\"{CellReference.MaxRow}\"><c r=\"A{CellReference.MaxRow}\"><v>2</v></c></row>";
		var zip = Workbooks.Data(rows).ToArray();
		var options = NoHeader(maxUncompressedSize: 300_000);
		options = options with { Csv = options.Csv with { SkipEmptyLines = skipEmptyRows } };
		if (!skipEmptyRows)
		{
			await AssertTripsAsSequentiallyAsync(zip, options, 1, "missing rows kept");

			return;
		}

		var expected = ReadAlone(zip, options, false, 0);
		Assert.Null(expected.Error);
		Assert.Equal(501, expected.Records.Count);
		foreach (var size in ParallelReads.Sizes)
		{
			var actual = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, true, size), $"missing rows skipped, segments of {size}");

			Assert.Null(ParallelReads.Difference(expected, actual));
			Assert.Equal(1L, actual.Counts.Starts);
		}
	}

	[Theory]
	[InlineData("sheet")]
	[InlineData("column")]
	public async Task StopsAMergedRangeOfTheGridAsTheSequentialReadDoes(string read)
	{
		// SecurityTests' merged range of the whole grid around a megabyte's value, under a 16 MB limit: with merged cells
		// filled, the read is serial, so it trips where the sequential read trips, after the same records.
		var value = new string('x', MiB);
		var package = Workbooks.Data("").Set(Sheets.PartName, MergedScannerTests.Worksheet($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{value}</t></is></c></row>", "A1:XFD1048576"));
		var options = NoHeader(maxPartSize: 4L * MiB, maxUncompressedSize: 16L * MiB) with { FillMergedCells = true, Range = read == "sheet" ? null : "B:B" };
		var zip = package.ToArray();

		var expected = ReadAlone(zip, options, false, 0, Light);

		Assert.StartsWith(PastTheLimit, Assert.IsType<InvalidDataException>(expected.Error).Message);
		foreach (var size in ParallelReads.Sizes)
		{
			var actual = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, true, size, Light), $"a merged range, {read}, segments of {size}", BombLimit);

			Assert.Null(ParallelReads.Difference(expected, actual));
			Assert.Equal(0L, actual.Counts.Starts);
		}
	}

	[Fact]
	public async Task StopsWhiteSpaceThatInflatesPastTheLimitAfterTheRowsBeforeIt()
	{
		// 500 rows, then 64 MB of white space, which deflate makes about 64 KB, under a 16 MB limit and a 64 KB part limit. The
		// segments begin in the rows; in the white space, no row starts within a segment's cap (32 KB), so the cutting stops
		// there and the serial read trips in it, after every row. The inflater stopped about a quarter of the way into the
		// sheet's compressed data, though the pipeline read ahead of the serial read.
		var rows = string.Concat(Enumerable.Range(1, 500).Select(n => $"<row r=\"{n}\"><c r=\"A{n}\"><v>{n}</v></c></row>"));
		var zip = Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
		{
			Write(stream, SheetStart + rows);
			Fill(stream, 64L * MiB);
			Write(stream, "<row r=\"501\"><c r=\"A501\"><v>501</v></c></row>" + SheetEnd);
		}).ToArray();
		var options = NoHeader(maxPartSize: 64 * 1024, maxUncompressedSize: 16L * MiB);

		var expected = ReadAlone(zip, options, false, 0);

		Assert.Equal(500, expected.Records.Count);
		Assert.Equal($"{PastTheLimit} ({16L * MiB} bytes uncompressed).", Assert.IsType<InvalidDataException>(expected.Error).Message);
		foreach (var size in ParallelReads.Sizes)
		{
			var input = new ReadTrackingStream(zip);
			using var workbook = ExcelWorkbook.Open(input, options);
			input.Forget();
			ParallelReads.Force(workbook, size);

			var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, options, Degree), $"white space, segments of {size}", BombLimit);

			Assert.Null(ParallelReads.Difference(expected, actual));
			Assert.Equal(1L, actual.Counts.Starts);
			Assert.True(actual.Counts.Fallbacks >= 1, actual.Counts.ToString());
			AssertStoppedEarly(zip, input, 16L * MiB);
		}
	}

	[Fact]
	public async Task ReadsRowsLongerThanThePartLimit()
	{
		// Rows of 80 KB (4,000 cells) under a 64 KB part limit: a sequential read reads them, since only a cell must fit its
		// window, and so does a read in segments, which can't cut them (a segment is at most half the limit) and falls back.
		var rows = new StringBuilder();
		for (var n = 1; n <= 230; n++)
		{
			var cells = n is > 100 and <= 130 ? 4000 : 2;
			rows.Append($"<row r=\"{n}\">");
			for (var c = 0; c < cells; c++)
			{
				rows.Append($"<c><v>{n * 10_000 + c}</v></c>");
			}

			rows.Append("</row>");
		}

		var zip = Workbooks.Data(rows.ToString()).ToArray();
		var options = NoHeader(maxPartSize: 64 * 1024);

		var expected = ReadAlone(zip, options, false, 0);

		Assert.Null(expected.Error);
		Assert.Equal(230, expected.Records.Count);
		foreach (var size in ParallelReads.Sizes)
		{
			var actual = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, true, size), $"rows past the part limit, segments of {size}");

			Assert.Null(ParallelReads.Difference(expected, actual));
			Assert.Equal(1L, actual.Counts.Starts);
			Assert.True(actual.Counts.Fallbacks >= 1, actual.Counts.ToString());
		}
	}

	[Fact]
	public async Task EndsAtACorruptDeflateStreamAsTheSequentialReadDoes()
	{
		// 64 bytes in the middle of the sheet's deflate stream flipped: the inflater fails there, on the pipeline's thread or
		// on the serial read's, and the read ends in the sequential read's exception, after a prefix of its records, as many
		// within a read of the inflater's (whose bytes are lost with its error) and a segment.
		var zip = SegmentSheets.Numbers(20_000).ToArray();
		var (start, length) = ZipBytes.DataRange(zip, Sheets.PartName);
		for (var i = 0; i < 64; i++)
		{
			zip[start + (length / 2) + i] ^= 0xFF;
		}

		var options = ParallelReads.Small;
		var expected = ReadAlone(zip, options, false, 0, Line);

		Assert.True(expected.Error is not null && FuzzTests.IsExpected(expected.Error), ParallelReads.Show(expected.Error));
		foreach (var size in ParallelReads.Sizes)
		{
			var actual = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, true, size, Line), $"a corrupt deflate stream, segments of {size}");

			Assert.Null(ParallelReads.ErrorDifference(expected.Error, actual.Error));
			AssertPrefix(expected.Records, actual.Records, (256 * 1024 + size) / 90 + 2, $"a corrupt deflate stream, segments of {size}");
		}
	}

	[Fact]
	public async Task CountsEveryReadOfASheetOnce()
	{
		// A sheet of 2 MB read whole again and again, under a limit of the other parts and four and a half sheets: the fifth
		// read passes it, read sequentially or in segments. Every inflated byte counts once, whether the serial read, the
		// pipeline's thread or the stop inflated it: none twice, and none not at all.
		var package = SegmentSheets.Numbers(20_000);
		var sheet = Encoding.UTF8.GetByteCount(package.Text(Sheets.PartName));
		Assert.True(sheet > 3 * MiB / 2);
		var options = ParallelReads.Small with { MaxUncompressedSize = OtherParts(package) + (long)(4.5 * sheet) };
		foreach (var parallel in new[] { false, true })
		{
			using var workbook = ExcelWorkbook.Open(package.ToStream(), options);
			ParallelReads.Force(workbook, 4096);
			for (var read = 1; read <= 5; read++)
			{
				var reading = parallel
					? await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, null, options, Degree, Line), $"read {read}")
					: ParallelReads.ReadSequentially(workbook, null, options, Line);
				var what = $"{(parallel ? "in segments" : "sequentially")}, read {read}";
				if (read < 5)
				{
					Assert.True(reading.Error is null, $"{what}: {ParallelReads.Show(reading.Error)}");
					Assert.Equal(20_000, reading.Records.Count);
				}
				else
				{
					Assert.True(reading.Error is InvalidDataException e && e.Message.StartsWith(PastTheLimit, StringComparison.Ordinal), $"{what}: {ParallelReads.Show(reading.Error)}");
				}
			}
		}
	}

	/// <summary>
	/// Reads a sheet bomb sequentially and then in segments at <see cref="Degree"/>, cut at each of
	/// <see cref="ParallelReads.Sizes"/>, each in a workbook of its own; checks that the read in segments ends in the
	/// sequential read's exception and message, after a prefix of its records, as many within a window's and a segment's
	/// bytes of the sequential read's.
	/// </summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="options">The workbook's and the readers' options.</param>
	/// <param name="chargedPerRecord">The fewest bytes a record counts toward the limit, its XML's and the extras': how many records a window's and a segment's bytes can be.</param>
	/// <param name="what">The bomb, for failures.</param>
	/// <returns>What each read in segments added to the workbook's counters.</returns>
	private static async Task<List<SegmentCounts>> AssertTripsAsSequentiallyAsync(byte[] zip, ExcelReaderOptions options, long chargedPerRecord, string what)
	{
		var expected = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, false, 0, Light), what, BombLimit);

		Assert.StartsWith(PastTheLimit, Assert.IsType<InvalidDataException>(expected.Error).Message);
		var reads = new List<SegmentCounts>();
		foreach (var size in ParallelReads.Sizes)
		{
			var actual = await ParallelReads.WithinAsync(() => ReadAlone(zip, options, true, size, Light), $"{what}, segments of {size}", BombLimit);

			var difference = ParallelReads.ErrorDifference(expected.Error, actual.Error);
			Assert.True(difference is null, $"{what}, segments of {size}: {difference}");
			AssertPrefix(expected.Records, actual.Records, (int)(((long)XmlWindow.DefaultSize + size) / chargedPerRecord) + 2, $"{what}, segments of {size}");
			reads.Add(actual.Counts);
		}

		return reads;
	}

	/// <summary>Reads a workbook's first sheet in a workbook of its own: sequentially, or in segments cut at a size.</summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="options">The workbook's and the reader's options.</param>
	/// <param name="parallel">Whether to read under <c>ParallelEnumerate</c> at <see cref="Degree"/>, segments forced.</param>
	/// <param name="size">The segments' cut target, for a read in segments.</param>
	/// <param name="describe">Describes a record; <see langword="null"/> for <see cref="ParallelReads.Describe"/>.</param>
	/// <returns>The reading.</returns>
	private static SheetReading ReadAlone(byte[] zip, ExcelReaderOptions options, bool parallel, int size, Func<CsvReader, string>? describe = null)
	{
		using var workbook = ExcelWorkbook.Open(new MemoryStream(zip, false), options);
		if (!parallel)
		{
			return ParallelReads.ReadSequentially(workbook, null, options, describe);
		}

		ParallelReads.Force(workbook, size);

		return ParallelReads.ReadInParallel(workbook, null, options, Degree, describe);
	}

	/// <summary>
	/// Checks that two reads that tripped a limit agree on the records both read, and differ in how many by no more than a
	/// bound.
	/// </summary>
	/// <param name="expected">The sequential read's records.</param>
	/// <param name="actual">The read in segments' records.</param>
	/// <param name="tolerance">The most their counts may differ by.</param>
	/// <param name="what">The read, for failures.</param>
	private static void AssertPrefix(List<string> expected, List<string> actual, long tolerance, string what)
	{
		var common = Math.Min(expected.Count, actual.Count);
		Assert.True(expected.Take(common).SequenceEqual(actual.Take(common)), $"{what}: the first {common} records differ.");
		Assert.True(Math.Abs(expected.Count - actual.Count) <= tolerance, $"{what}: {actual.Count} records before the error, and {expected.Count} sequentially, more than {tolerance} apart.");
	}

	/// <summary>
	/// Checks that the reads stopped early in the sheet's compressed data: into it, but no further than twice the limit's
	/// share of it, plus 16 KB for the inflater's input buffer, as <see cref="SecurityTests"/> checks a sequential read.
	/// </summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="input">The stream the workbook read.</param>
	/// <param name="limit">The limit that stopped the read, in inflated bytes.</param>
	private static void AssertStoppedEarly(byte[] zip, ReadTrackingStream input, long limit)
	{
		var (start, length) = ZipBytes.DataRange(zip, Sheets.PartName);
		var bound = (long)(2.0 * length * limit / ZipBytes.UncompressedSize(zip, Sheets.PartName)) + 16 * 1024;
		Assert.InRange(bound, 1, length - 1);
		Assert.InRange(input.ReadInto(start, length), 1, bound);
	}

	/// <summary>A record of a bomb, described in a few bytes: its line, its fields, and its last field's length.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <returns>The description.</returns>
	private static string Light(CsvReader reader) => $"{reader.LineNumber}: {reader.FieldCount} fields, the last of {reader.GetSpan(reader.FieldCount - 1).Length} bytes";

	/// <summary>A record described by its line alone.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <returns>The line.</returns>
	private static string Line(CsvReader reader) => reader.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>The options of a reader with no header and a 256-byte buffer, and these limits.</summary>
	/// <param name="maxPartSize">The part limit.</param>
	/// <param name="maxUncompressedSize">The workbook's limit.</param>
	/// <returns>The options.</returns>
	private static ExcelReaderOptions NoHeader(long maxPartSize = 256L * MiB, long maxUncompressedSize = 4L << 30) => new()
	{
		MaxPartSize = maxPartSize,
		MaxUncompressedSize = maxUncompressedSize,
		Csv = new() { HasHeader = false, BufferSize = 256 },
	};

	/// <summary>The bytes of the parts a workbook reads besides its sheets: as <see cref="Workbooks.Data"/> makes them, without shared strings.</summary>
	/// <param name="package">The package.</param>
	/// <returns>The bytes.</returns>
	private static long OtherParts(TestPackage package) =>
		new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml" }.Sum(p => (long)Encoding.UTF8.GetByteCount(package.Text(p)));

	/// <summary>Writes rows of XML, each from its number, inside a worksheet part's sheetData, or alone.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="count">The number of rows.</param>
	/// <param name="row">Makes a row's XML from its number, counting from 1.</param>
	/// <param name="wrap">Whether to write the worksheet's start and end around the rows, rather than the rows alone.</param>
	private static void WriteRows(Stream stream, int count, Func<int, string> row, bool wrap = true)
	{
		if (wrap)
		{
			Write(stream, SheetStart);
		}

		var text = new StringBuilder();
		for (var n = 1; n <= count; n++)
		{
			text.Append(row(n));
			if (text.Length > 60_000)
			{
				Write(stream, text.ToString());
				text.Clear();
			}
		}

		Write(stream, text.ToString());
		if (wrap)
		{
			Write(stream, SheetEnd);
		}
	}

	/// <summary>Writes white space.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="count">How many bytes.</param>
	private static void Fill(Stream stream, long count)
	{
		var chunk = Encoding.ASCII.GetBytes(new string(' ', 64 * 1024));
		for (var left = count; left > 0; left -= chunk.Length)
		{
			stream.Write(chunk, 0, (int)Math.Min(chunk.Length, left));
		}
	}

	/// <summary>Writes text as UTF-8.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="text">The text.</param>
	private static void Write(Stream stream, string text)
	{
		var bytes = Encoding.UTF8.GetBytes(text);
		stream.Write(bytes, 0, bytes.Length);
	}
}

#if NET
/// <summary>
/// The memory a sheet bomb takes in segments (the plan's "Memory"): a million rows of one cell in <c>XFD</c>, under a 64 MB
/// limit, read in segments of 256 KB at degree 4, allocates far less than one segment's block would take without its cap
/// (about 330 MB, for the 4,000 rows the limit's extras let a worker write), since each block stops at 4 MB and marks its
/// segment, and the stitcher falls back. It measures the process's allocations, which the pipeline's threads make, so it
/// runs alone (<see cref="TimedCollection"/>).
/// </summary>
/// <param name="output">Shows what the read allocated.</param>
[Collection(TimedCollection.Name)]
public class SegmentMemoryTests(ITestOutputHelper output)
{
	/// <summary>
	/// The most the read may allocate: the ring's eight slots' blocks, each at most 4 MB of bytes and fields and the
	/// arrays it grew through, their segments' buffers, and the serial read's batches of 16,384 fields a record.
	/// </summary>
	private const long Budget = 320L * 1024 * 1024;

	/// <summary>The bomb, zipped once.</summary>
	private static readonly Lazy<byte[]> Bomb = new(() => Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
	{
		var start = Sheets.Worksheet("");
		var end = start.IndexOf("</sheetData>", StringComparison.Ordinal);
		var text = new StringBuilder(start.Substring(0, end));
		for (var n = 1; n <= 1_000_000; n++)
		{
			text.Append($"<row r=\"{n}\"><c r=\"XFD{n}\"><v>1</v></c></row>");
			if (text.Length > 60_000 || n == 1_000_000)
			{
				if (n == 1_000_000)
				{
					text.Append(start.Substring(end));
				}

				var bytes = Encoding.UTF8.GetBytes(text.ToString());
				stream.Write(bytes, 0, bytes.Length);
				text.Clear();
			}
		}
	}).ToArray());

	[Fact]
	public async Task HoldsNoBlockPastItsCap()
	{
		var options = new ExcelReaderOptions { MaxUncompressedSize = 64L * 1024 * 1024, Csv = new() { HasHeader = false, BufferSize = 256 } };
		using var workbook = ExcelWorkbook.Open(new MemoryStream(Bomb.Value, false), options);
		ParallelReads.Force(workbook, SegmentSizes.DefaultSize);
		var before = GC.GetTotalAllocatedBytes(true);

		var reading = await ParallelReads.WithinAsync(
			() => ParallelReads.ReadInParallel(workbook, null, options, 4, r => r.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
			"gaps to XFD in segments of 256 KB",
			TimeSpan.FromMinutes(5));

		var allocated = GC.GetTotalAllocatedBytes(true) - before;
		output.WriteLine($"{reading.Records.Count:N0} records before the limit, {allocated:N0} bytes allocated; {reading.Counts}.");
		Assert.IsType<InvalidDataException>(reading.Error);
		Assert.True(reading.Counts.Fallbacks >= 1, reading.Counts.ToString());
		Assert.True(allocated < Budget, $"{allocated:N0} bytes allocated");
	}
}
#endif
