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
/// The workbook writer of the Excel read benchmarks (<c>bench/CsvHelper.Benchmarks/ExcelTextWorkbook.cs</c>), checked
/// against Excel: NCsvPerf's rows written by it and saved by Excel (<c>excel/packageassets-1695.xlsx</c>) have the same
/// sheet and shared strings, and read as the same values through CsvHelper.Excel and ExcelDataReader.
/// </summary>
public class ExcelTextWorkbookTests
{
	/// <summary>Excel's workbook of NCsvPerf's rows, every column as text.</summary>
	private const string ExcelFile = "excel/packageassets-1695.xlsx";

	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static ExcelTextWorkbookTests()
	{
#if NET
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
	}

	/// <summary>Both strings variants, sync and async, as theory data.</summary>
	public static IEnumerable<object[]> StringsModes =>
		new[] { ExcelStrings.Shared, ExcelStrings.Inline }.SelectMany(s => new[] { new object[] { s, false }, new object[] { s, true } });

	/// <summary>NCsvPerf's lines, copied next to the tests from the benchmarks' <c>Data</c> folder.</summary>
	private static string[] Lines => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Bench", "PackageAssets.csv"));

	/// <summary>The rows as fields, as the generator gives them to the writer: the lines have no quoted fields.</summary>
	private static List<string[]> Rows => [.. Lines.Select(line => line.Split(','))];

	/// <summary>The rows as each reader's records are compared: the fields joined with <c>|</c>.</summary>
	private static List<string> Expected => [.. Lines.Select(line => line.Replace(',', '|'))];

	[Fact]
	public void WritesTheSheetAndSharedStringsExcelWrites()
	{
		var excel = Entries(File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile)));
		var written = Entries(Write(Rows, ExcelStrings.Shared));

		// Excel's entries in its order, without the theme and document properties, which the reading doesn't need.
		Assert.Equal(excel.Keys.Where(n => !n.StartsWith("docProps/", StringComparison.Ordinal) && !n.StartsWith("xl/theme/", StringComparison.Ordinal)), written.Keys);

		// Excel's root element declares namespaces of its own, for revision ids; from the dimension on, the sheets are
		// the same: the dimension, rows with their spans and row heights, and cells with the text format's style.
		Assert.Equal(FromDimension(excel["xl/worksheets/sheet1.xml"]), FromDimension(written["xl/worksheets/sheet1.xml"]));
		Assert.Equal(excel["xl/sharedStrings.xml"], written["xl/sharedStrings.xml"]);
		Assert.Equal(Between(excel["xl/styles.xml"], "<cellXfs", "</cellXfs>"), Between(written["xl/styles.xml"], "<cellXfs", "</cellXfs>"));
	}

	[Theory]
	[MemberData(nameof(StringsModes))]
	public async Task ReadsAsExcelsWorkbookDoes(ExcelStrings strings, bool async)
	{
		var excel = File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile));
		var written = Write(Rows, strings);

		Assert.Equal(Expected, await ReadAsync(excel, async));
		Assert.Equal(Expected, await ReadAsync(written, async));
		Assert.Equal(Expected, ReadWithExcelDataReader(excel));
		Assert.Equal(Expected, ReadWithExcelDataReader(written));
	}

	[Theory]
	[InlineData(ExcelStrings.Shared)]
	[InlineData(ExcelStrings.Inline)]
	public void WritesAPackageTheOpenXmlSdkValidates(ExcelStrings strings)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(Write(Rows, strings)), false);

		var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}");

		Assert.Empty(errors);
	}

	[Theory]
	[MemberData(nameof(StringsModes))]
	public async Task KeepsSpacesAndEscapesEntities(ExcelStrings strings, bool async)
	{
		var rows = new List<string[]>
		{
			new[] { " a", "b ", " c d " },
			new[] { "", "x&y", "<b>" },
			new[] { "", "", "" },
			new[] { "_x41_", "", "a_x0041" },
		};

		var records = await ReadAsync(Write(rows, strings), async);

		// The row without a value has no row element, so it is skipped as an empty line. Neither _x41_ nor a text ending
		// in _x0041 is an escape: an escape has four hexadecimal digits and ends with an underscore.
		Assert.Equal([" a|b | c d ", "|x&y|<b>", "_x41_||a_x0041"], records);
	}

	[Fact]
	public void WritesTheSpansOfEachBlockOf16Rows()
	{
		// A1:B16, C17, A18 and E40: the spans Excel 16.0 build 20527 wrote for these cells (checked through COM on
		// 2026-11-03), the columns of each block of 16 rows, on each row of the block that has a value.
		var rows = Enumerable.Range(1, 40).Select(r => r switch
		{
			<= 16 => new[] { "a", "b", "", "", "" },
			17 => new[] { "", "", "c", "", "" },
			18 => new[] { "a", "", "", "", "" },
			40 => new[] { "", "", "", "", "e" },
			_ => new[] { "", "", "", "", "" },
		}).ToList();

		var sheet = Entries(Write(rows, ExcelStrings.Shared))["xl/worksheets/sheet1.xml"];
		var spans = Regex.Matches(sheet, "<row r=\"(\\d+)\" spans=\"([^\"]*)\"").Cast<Match>()
			.Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}");

		Assert.Equal([.. Enumerable.Range(1, 16).Select(r => $"{r} 1:2"), "17 1:3", "18 1:3", "40 5:5"], spans);
		Assert.Contains("<dimension ref=\"A1:E40\"/>", sheet);
	}

	[Theory]
	[InlineData("tab\there")]
	[InlineData("line\nbreak")]
	[InlineData("_x0041_")]
	[InlineData("a_x00e9_b")]
	public void RejectsTextExcelWouldEscape(string value)
	{
		foreach (var strings in new[] { ExcelStrings.Shared, ExcelStrings.Inline })
		{
			Assert.Throws<NotSupportedException>(() => Write([new[] { value }], strings));
		}
	}

	[Theory]
	[InlineData(0, "A")]
	[InlineData(25, "Z")]
	[InlineData(26, "AA")]
	[InlineData(701, "ZZ")]
	[InlineData(702, "AAA")]
	[InlineData(16383, "XFD")]
	public void NamesColumns(int index, string name)
	{
		Assert.Equal(name, ExcelTextWorkbook.ColumnName(index));
	}

	/// <summary>Writes rows as a workbook of one sheet.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="strings">Where the strings go.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Write(IReadOnlyList<string[]> rows, ExcelStrings strings)
	{
		using var stream = new MemoryStream();
		ExcelTextWorkbook.Write(stream, "PackageAssets", rows, strings, CompressionLevel.Optimal);

		return stream.ToArray();
	}

	/// <summary>Reads a workbook's rows with CsvHelper.Excel, every row a record.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The records, each as its fields joined with <c>|</c>.</returns>
	private static async Task<List<string>> ReadAsync(byte[] workbook, bool async)
	{
		var options = new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } };
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
		var records = new List<string>();
		while (reader.Read())
		{
			records.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetString(i) ?? "")));
		}

		return records;
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

	/// <summary>The text from the start of one marker to the end of another.</summary>
	/// <param name="text">The text.</param>
	/// <param name="start">The first marker.</param>
	/// <param name="end">The second marker, after the first.</param>
	/// <returns>The text between them, with the markers.</returns>
	private static string Between(string text, string start, string end)
	{
		var from = text.IndexOf(start, StringComparison.Ordinal);
		var to = text.IndexOf(end, from, StringComparison.Ordinal);
		Assert.True(from >= 0 && to >= 0, $"The text has no {start}...{end}.");

		return text.Substring(from, to + end.Length - from);
	}
}
