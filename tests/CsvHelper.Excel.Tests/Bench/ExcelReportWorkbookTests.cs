using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Benchmarks;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Corpus;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ExcelDataReader;

namespace CsvHelper.Excel.Tests.Bench;

/// <summary>
/// The report writer of the Excel read benchmarks (<c>bench/CsvHelper.Benchmarks/ExcelReportWorkbook.cs</c>), checked
/// against Excel: NCsvPerf's first 250 lines laid out by Excel as the report (<c>excel/packageassets-report-250.xlsx</c>,
/// by the corpus tool's <c>report</c> command) have the same sheet, shared strings, styles and table, and read the same
/// with each of the report benchmark's options.
/// </summary>
public class ExcelReportWorkbookTests
{
	/// <summary>Excel's report of NCsvPerf's first 250 lines.</summary>
	private const string ExcelFile = "excel/packageassets-report-250.xlsx";

	/// <summary>The number of data rows in Excel's report.</summary>
	private const int Rows = 250;

	/// <summary>The sheet part.</summary>
	private const string Sheet = "xl/worksheets/sheet1.xml";

	/// <summary>The table part.</summary>
	private const string Table = "xl/tables/table1.xml";

	/// <summary>The styles part.</summary>
	private const string Styles = "xl/styles.xml";

	/// <summary>
	/// The revision ids Excel makes at random (<c>xr:uid</c>, <c>xr3:uid</c>), and the namespaces it declares for them,
	/// each with the space before it.
	/// </summary>
	private static readonly Regex RevisionIds = new(" (xr[0-9]*:uid=\"[{][0-9A-F-]+[}]\"|xmlns:(mc|xr[0-9]*)=\"[^\"]*\"|mc:Ignorable=\"[^\"]*\")", RegexOptions.CultureInvariant);

	/// <summary>The content types of the parts the writer leaves out: Excel's theme and document properties.</summary>
	private static readonly Regex LeftOutTypes = new("<Override PartName=\"/(xl/theme|docProps)/[^\"]*\" ContentType=\"[^\"]*\"/>", RegexOptions.CultureInvariant);

	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static ExcelReportWorkbookTests()
	{
#if NET
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
	}

	/// <summary>Each of the report benchmark's reads, sync and async, as theory data.</summary>
	public static IEnumerable<object[]> Reads =>
		new[] { "Range", "RangeFirstRows", "Table", "HeaderSearch", "SkipHiddenRows", "SkipHiddenColumns", "FillMergedCells" }
			.SelectMany(read => new[] { new object[] { read, false }, new object[] { read, true } });

	/// <summary>The data's header names, in order.</summary>
	private static string[] Names => [.. ExcelReport.Fields.Select(f => f.Name)];

	[Fact]
	public void WritesTheWorkbookExcelWrites()
	{
		var excel = Entries(File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile)));
		var written = Entries(Write(Records(Rows)));

		// Excel's entries in its order, without the theme and document properties, which the reading doesn't need.
		Assert.Equal(excel.Keys.Where(n => !n.StartsWith("docProps/", StringComparison.Ordinal) && !n.StartsWith("xl/theme/", StringComparison.Ordinal)), written.Keys);
		Assert.Equal(LeftOutTypes.Replace(excel["[Content_Types].xml"], ""), written["[Content_Types].xml"]);

		// Excel's root element declares namespaces of its own, for revision ids; from the dimension on, the sheets are the
		// same: the columns, the rows with their spans and hidden rows, the cells with their formats, the merged ranges,
		// the page margins and the table part.
		Assert.Equal(FromDimension(excel[Sheet]), FromDimension(written[Sheet]));
		Assert.Equal(excel["xl/sharedStrings.xml"], written["xl/sharedStrings.xml"]);
		Assert.Equal(excel["xl/worksheets/_rels/sheet1.xml.rels"], written["xl/worksheets/_rels/sheet1.xml.rels"]);

		// The cell formats, the table's differential formats and the default table style.
		Assert.Equal(Between(excel[Styles], "<cellXfs", "</cellXfs>"), Between(written[Styles], "<cellXfs", "</cellXfs>"));
		Assert.Equal(Between(excel[Styles], "<dxfs", "</dxfs>"), Between(written[Styles], "<dxfs", "</dxfs>"));
		Assert.Equal(Between(excel[Styles], "<tableStyles", "/>"), Between(written[Styles], "<tableStyles", "/>"));

		// The table is Excel's but for its random revision ids.
		Assert.Contains("xr:uid=", excel[Table]);
		Assert.Equal(RevisionIds.Replace(excel[Table], ""), written[Table]);
	}

	[Theory]
	[MemberData(nameof(Reads))]
	public async Task ReadsAsExcelsWorkbookDoes(string read, bool async)
	{
		var (options, headers, records) = Expected(read);

		foreach (var workbook in new[] { File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile)), Write(Records(Rows)) })
		{
			using var reader = async
				? await ExcelReader.CreateAsync(new MemoryStream(workbook), options)
				: ExcelReader.Create(new MemoryStream(workbook), options);

			Assert.Equal(records, await reader.ReadAllAsync(async));
			Assert.Equal(headers, reader.Headers);
		}
	}

	[Fact]
	public void ReadsAsExcelsWorkbookDoesThroughExcelDataReader()
	{
		var excel = ReadWithExcelDataReader(File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile)));
		var written = ReadWithExcelDataReader(Write(Records(Rows)));

		// ExcelDataReader reads every row from 1, the empty row 3 among them, from column A.
		Assert.Equal(Rows + 4, excel.Count);
		Assert.Equal("|NuGet package assets" + new string('|', ExcelReport.Columns - 1), excel[0]);
		Assert.Equal(excel, written);
	}

	[Theory]
	[InlineData(Rows)]
	[InlineData(1000)]
	public void WritesAPackageTheOpenXmlSdkValidates(int rows)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(Write(Records(rows))), false);

		var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}");

		Assert.Empty(errors);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LaysOutTheBenchmarksRowsAsExcelsWorkbookDoes(bool async)
	{
		// The benchmark's million rows are 10,000 regions; 1,000 rows are 10, the last ending at the last row.
		var workbook = Write(Records(1000));
		var sheet = Entries(workbook)[Sheet];

		Assert.Contains("<dimension ref=\"B1:AB1004\"/>", sheet);
		Assert.Equal(["B1:AB1", .. Enumerable.Range(0, 10).Select(r => $"B{5 + r * 100}:B{104 + r * 100}")], Regex.Matches(sheet, "<mergeCell ref=\"([^\"]*)\"/>").Cast<Match>().Select(m => m.Groups[1].Value));
		Assert.Equal(Enumerable.Range(1, 20).Select(n => (4 + n * 50).ToString(CultureInfo.InvariantCulture)), Regex.Matches(sheet, "<row r=\"([0-9]+)\" spans=\"2:28\" hidden=\"1\"").Cast<Match>().Select(m => m.Groups[1].Value));
		Assert.Contains("ref=\"C4:AB1004\"", Entries(workbook)[Table]);

		var area = ExcelReport.Area(1000);
		Assert.Equal(1000, (await ReadAsync(workbook, new ExcelReaderOptions { Range = area }, async)).Count);
		Assert.Equal(980, (await ReadAsync(workbook, new ExcelReaderOptions { Range = area, SkipHiddenRows = true }, async)).Count);

		var filled = await ReadAsync(workbook, new ExcelReaderOptions { Range = area, FillMergedCells = true }, async);
		Assert.Equal(["North", "East", "South", "West", "North", "East", "South", "West", "North", "East"], Enumerable.Range(0, 10).Select(r => filled[r * 100 + 99].Split('|')[0]));
	}

	[Fact]
	public void RejectsRecordsExcelsShapeIsUnknownFor()
	{
		var records = Records(Rows);

		// Another header, whose widths in Excel aren't known.
		Assert.Throws<NotSupportedException>(() => Write([[.. Enumerable.Reverse(Names)], .. records.Skip(1)]));

		// No data rows, for a table needs one.
		Assert.Throws<NotSupportedException>(() => Write(records.Take(1).ToList()));

		// A row of another width.
		Assert.Throws<NotSupportedException>(() => Write([.. records.Take(10), records[10].Take(24).ToArray(), .. records.Skip(11)]));

		// A last region of one row, which Excel doesn't merge.
		Assert.Throws<NotSupportedException>(() => Write(Records(101)));
		Write(Records(102));
	}

	/// <summary>What a read of Excel's report gives: its options, the header, and the records' fields joined with <c>|</c>.</summary>
	/// <param name="read">The read, named as the benchmark's method.</param>
	/// <returns>The options, the header and the records.</returns>
	private static (ExcelReaderOptions Options, string[] Headers, List<string> Records) Expected(string read)
	{
		var lines = Records(Rows).Skip(1).ToList();
		var all = Enumerable.Range(0, Rows);
		var area = ExcelReport.Area(Rows);
		string[] withKeys = [ExcelReport.RegionHeader, ExcelReport.KeyHeader, .. Names];

		return read switch
		{
			"Range" => (new ExcelReaderOptions { Range = area }, withKeys, Rendered(all, lines, filled: false)),
			"RangeFirstRows" => (new ExcelReaderOptions { Range = ExcelReport.Area(100) }, withKeys, Rendered(all.Take(100), lines, filled: false)),
			"Table" => (new ExcelReaderOptions { Table = ExcelReport.TableName }, [ExcelReport.KeyHeader, .. Names], [.. all.Select(i => Key(i) + "|" + string.Join("|", lines[i]))]),
			"HeaderSearch" => (new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find(Names) }, withKeys, Rendered(all, lines, filled: false)),
			"SkipHiddenRows" => (new ExcelReaderOptions { Range = area, SkipHiddenRows = true }, withKeys, Rendered(all.Where(i => !ExcelReport.IsHidden(i)), lines, filled: false)),
			"SkipHiddenColumns" => (new ExcelReaderOptions { Range = area, SkipHiddenColumns = true }, [ExcelReport.RegionHeader, .. Names], [.. all.Select(i => (ExcelReport.RegionOf(i) ?? "") + "|" + string.Join("|", lines[i]))]),
			"FillMergedCells" => (new ExcelReaderOptions { Range = area, FillMergedCells = true }, withKeys, Rendered(all, lines, filled: true)),
			_ => throw new ArgumentOutOfRangeException(nameof(read), read, "No such read."),
		};
	}

	/// <summary>Data rows as a read of the report's columns gives them: the region, the key, then the fields.</summary>
	/// <param name="indexes">The data rows' indexes, from 0.</param>
	/// <param name="lines">The data rows' fields.</param>
	/// <param name="filled">Whether merged cells are filled, so that every row of a region has its name.</param>
	/// <returns>The records, each as its fields joined with <c>|</c>.</returns>
	private static List<string> Rendered(IEnumerable<int> indexes, List<string[]> lines, bool filled) =>
		[.. indexes.Select(i => (filled ? ExcelReport.ShownRegionOf(i) : ExcelReport.RegionOf(i) ?? "") + "|" + Key(i) + "|" + string.Join("|", lines[i]))];

	/// <summary>A data row's key as the reader gives it: its number from 1.</summary>
	/// <param name="index">The data row's index, from 0.</param>
	/// <returns>The key's text.</returns>
	private static string Key(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);

	/// <summary>The report's records: the data's header, then NCsvPerf's lines, repeated to a number of rows.</summary>
	/// <param name="rows">The number of data rows.</param>
	/// <returns>The header and the data rows' fields.</returns>
	private static List<string[]> Records(int rows)
	{
		var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Bench", "PackageAssets.csv"));
		var records = new List<string[]>(rows + 1) { Names };
		for (var i = 0; i < rows; i++)
		{
			records.Add(lines[i % lines.Length].Split(','));
		}

		return records;
	}

	/// <summary>Writes records as the report.</summary>
	/// <param name="records">The header, then the data rows.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Write(IReadOnlyList<string[]> records)
	{
		using var stream = new MemoryStream();
		ExcelReportWorkbook.Write(stream, records, CompressionLevel.Optimal);

		return stream.ToArray();
	}

	/// <summary>Reads a workbook's records with CsvHelper.Excel.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="options">The options.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The records, each as its fields joined with <c>|</c>.</returns>
	private static async Task<List<string>> ReadAsync(byte[] workbook, ExcelReaderOptions options, bool async)
	{
		using var reader = async
			? await ExcelReader.CreateAsync(new MemoryStream(workbook), options)
			: ExcelReader.Create(new MemoryStream(workbook), options);

		return await reader.ReadAllAsync(async);
	}

	/// <summary>Reads a workbook's rows with ExcelDataReader, an empty cell as an empty string.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The rows, each as its values joined with <c>|</c>.</returns>
	private static List<string> ReadWithExcelDataReader(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var rows = new List<string>();
		while (reader.Read())
		{
			rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "")));
		}

		return rows;
	}

	/// <summary>A package's entries as text, by name, in the zip's order.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <returns>The entries.</returns>
	private static Dictionary<string, string> Entries(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		var entries = new Dictionary<string, string>();
		foreach (var entry in archive.Entries)
		{
			using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
			entries.Add(entry.FullName, reader.ReadToEnd());
		}

		return entries;
	}

	/// <summary>A sheet's XML from its <c>dimension</c> element on.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The XML from the dimension on.</returns>
	private static string FromDimension(string sheet)
	{
		var start = sheet.IndexOf("<dimension ", StringComparison.Ordinal);
		Assert.True(start >= 0, "The sheet has no dimension.");

		return sheet.Substring(start);
	}

	/// <summary>The text from the start of one marker to the end of the first of another after it.</summary>
	/// <param name="text">The text.</param>
	/// <param name="start">The first marker.</param>
	/// <param name="end">The second marker.</param>
	/// <returns>The text between them, with the markers.</returns>
	private static string Between(string text, string start, string end)
	{
		var from = text.IndexOf(start, StringComparison.Ordinal);
		var to = from < 0 ? -1 : text.IndexOf(end, from, StringComparison.Ordinal);
		Assert.True(from >= 0 && to >= 0, $"The text has no {start}...{end}.");

		return text.Substring(from, to + end.Length - from);
	}
}
