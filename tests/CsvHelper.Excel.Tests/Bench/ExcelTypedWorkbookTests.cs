#if NET
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
/// The typed workbook writer of the Excel read benchmarks (<c>bench/CsvHelper.Benchmarks/ExcelTypedWorkbook.cs</c>),
/// checked against Excel: the first 1,000 orders written by it and entered in Excel (<c>excel/orders-1000.xlsx</c>, by
/// the corpus tool's <c>orders</c> command) have the same sheet, shared strings and formats, and read as the same orders
/// through CsvHelper.Excel and as the same values through ExcelDataReader.
/// </summary>
/// <remarks>
/// The numbers' text is the exception: Excel writes some numbers in 17 significant digits whose 15 digits read back,
/// by a rule its files don't show (see <see cref="ExcelTypedWorkbook.Number"/>). The sheets are compared with each
/// number as its value, and each number whose text differs is checked to be that case.
/// </remarks>
public class ExcelTypedWorkbookTests
{
	/// <summary>Excel's workbook of the first 1,000 orders.</summary>
	private const string ExcelFile = "excel/orders-1000.xlsx";

	/// <summary>The number of orders in Excel's workbook.</summary>
	private const int Orders = 1000;

	/// <summary>The sheet part.</summary>
	private const string Sheet = "xl/worksheets/sheet1.xml";

	/// <summary>A number cell: one without a type, as Excel and the writer give it, its reference and its value's text.</summary>
	private static readonly Regex NumberCell = new("<c r=\"([A-Z]+[0-9]+)\"( s=\"[0-9]+\")?><v>([^<]*)</v></c>", RegexOptions.CultureInvariant);

	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static ExcelTypedWorkbookTests()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	[Fact]
	public void WritesTheSheetAndSharedStringsExcelWrites()
	{
		var excel = Entries(File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile)));
		var written = Entries(WriteOrders());

		// Excel's entries in its order, without the theme and document properties, which the reading doesn't need.
		Assert.Equal(excel.Keys.Where(n => !n.StartsWith("docProps/", StringComparison.Ordinal) && !n.StartsWith("xl/theme/", StringComparison.Ordinal)), written.Keys);

		// From the dimension on, the sheets are the same but for the numbers' digits.
		var excelSheet = FromDimension(excel[Sheet]);
		var writtenSheet = FromDimension(written[Sheet]);
		Assert.Equal(WithNumberValues(excelSheet), WithNumberValues(writtenSheet));

		// Each number whose text differs is one Excel wrote in 17 digits where 15 read back, and the writer wrote 15: 314 of
		// the 7,000, among the totals (162), the dates (51 and 58) and the weights (43). Ties of the 17th digit, which Excel
		// rounds away from zero, don't occur in these numbers.
		var differences = NumberCell.Matches(excelSheet).Zip(NumberCell.Matches(writtenSheet))
			.Where(pair => pair.First.Groups[3].Value != pair.Second.Groups[3].Value)
			.Select(pair => (Reference: pair.First.Groups[1].Value, Excel: pair.First.Groups[3].Value, Written: pair.Second.Groups[3].Value))
			.ToList();
		Assert.All(differences, d =>
		{
			Assert.True(SignificantDigits(d.Excel) > 15, $"{d.Reference}: Excel's {d.Excel}");
			Assert.True(SignificantDigits(d.Written) <= 15, $"{d.Reference}: {d.Written}");
			Assert.Equal(double.Parse(d.Excel, CultureInfo.InvariantCulture), double.Parse(d.Written, CultureInfo.InvariantCulture));
		});
		Assert.Equal(314, differences.Count);

		Assert.Equal(excel["xl/sharedStrings.xml"], written["xl/sharedStrings.xml"]);
		Assert.Equal(Between(excel["xl/styles.xml"], "<numFmts", "</numFmts>"), Between(written["xl/styles.xml"], "<numFmts", "</numFmts>"));
		Assert.Equal(Between(excel["xl/styles.xml"], "<cellXfs", "</cellXfs>"), Between(written["xl/styles.xml"], "<cellXfs", "</cellXfs>"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheOrdersAsExcelsWorkbookDoes(bool async)
	{
		var excel = File.ReadAllBytes(CorpusFiles.PathOf(ExcelFile));
		var written = WriteOrders();
		var expected = ExcelOrders.Generate(Orders).Select(Describe).ToList();

		Assert.Equal(expected, await ReadOrdersAsync(excel, async));
		Assert.Equal(expected, await ReadOrdersAsync(written, async));
		Assert.Equal(ReadWithExcelDataReader(excel), ReadWithExcelDataReader(written));
	}

	[Fact]
	public void WritesAPackageTheOpenXmlSdkValidates()
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(WriteOrders()), false);

		var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}");

		Assert.Empty(errors);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesEachTypeOfCell(bool async)
	{
		var rows = new List<object?[]>
		{
			new object?[] { "Text", "Number", "Bool", "Date" },
			new object?[] { "x", 1.5, true, 45000.25 },
			new object?[] { null, "", false, 2.0 },
		};
		var workbook = Write(rows, [null, null, null, ExcelOrders.DateFormat]);

		var sheet = Entries(workbook)[Sheet];
		Assert.Contains("<dimension ref=\"A1:D3\"/>", sheet);
		Assert.Contains(
			"<row r=\"2\" spans=\"1:4\" x14ac:dyDescent=\"0.35\"><c r=\"A2\" t=\"s\"><v>4</v></c><c r=\"B2\"><v>1.5</v></c>"
			+ "<c r=\"C2\" t=\"b\"><v>1</v></c><c r=\"D2\" s=\"1\"><v>45000.25</v></c></row>"
			+ "<row r=\"3\" spans=\"1:4\" x14ac:dyDescent=\"0.35\"><c r=\"C3\" t=\"b\"><v>0</v></c><c r=\"D3\" s=\"1\"><v>2</v></c></row>",
			sheet);

		// The header's cells have no format; the date column's cells below it read as dates.
		using var reader = async
			? await ExcelReader.CreateAsync(new MemoryStream(workbook))
			: ExcelReader.Create(new MemoryStream(workbook));
		Assert.Equal(["x|1.5|TRUE|2023-03-15T06:00:00", "||FALSE|1900-01-02"], await reader.ReadAllAsync(async));
	}

	[Theory]
	[InlineData(70d, "70")]
	[InlineData(3538515843d, "3538515843")]
	[InlineData(-17.5, "-17.5")]
	[InlineData(15220.15, "15220.15")]
	[InlineData(44775.348252719908, "44775.348252719908")]
	[InlineData(4.5056305148199343, "4.5056305148199343")]
	[InlineData(0.1, "0.1")]
	[InlineData(0.05, "0.05")]
	[InlineData(0.0123, "1.23E-2")]
	[InlineData(0.057447142925787315, "5.7447142925787315E-2")]
	[InlineData(0.041666666666666664, "4.1666666666666664E-2")]
	[InlineData(0.001, "1E-3")]
	[InlineData(1.1574074074074073E-05, "1.1574074074074073E-5")]
	[InlineData(1E-10, "1E-10")]
	[InlineData(12000000d, "12000000")]
	[InlineData(1002227378711789.6, "1002227378711789.6")]
	[InlineData(12345678901234568d, "1.2345678901234568E+16")]
	[InlineData(1.5E+20, "1.5E+20")]
	[InlineData(1E+21, "1E+21")]
	public void WritesNumbersAsExcelDoes(double value, string text)
	{
		// Each text is Excel's, from its corpus workbooks or from the numbers it wrote through COM at step 9.
		Assert.Equal(text, ExcelTypedWorkbook.Number(value));
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void RejectsNumbersThatArentFinite(double value)
	{
		Assert.Throws<NotSupportedException>(() => ExcelTypedWorkbook.Number(value));
	}

	/// <summary>Writes the first 1,000 orders, as the benchmark writes a million.</summary>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] WriteOrders() => Write(ExcelOrders.Rows(ExcelOrders.Generate(Orders)), ExcelOrders.Formats);

	/// <summary>Writes rows as a workbook of one sheet.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="formats">The columns' formats below row 1.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Write(IReadOnlyList<object?[]> rows, IReadOnlyList<string?> formats)
	{
		using var stream = new MemoryStream();
		ExcelTypedWorkbook.Write(stream, ExcelOrders.SheetName, rows, formats, CompressionLevel.Optimal);

		return stream.ToArray();
	}

	/// <summary>Reads a workbook's orders with CsvHelper.Excel, through the mapper generated for <see cref="OrderRecord"/>.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The orders, each described.</returns>
	private static async Task<List<string>> ReadOrdersAsync(byte[] workbook, bool async)
	{
		var orders = new List<string>();
		if (async)
		{
			await using var reader = await ExcelReader.CreateAsync(new MemoryStream(workbook));
			await foreach (var order in reader.GetRecordsAsync<OrderRecord>())
			{
				orders.Add(Describe(order));
			}
		}
		else
		{
			using var reader = ExcelReader.Create(new MemoryStream(workbook));
			orders.AddRange(reader.GetRecords<OrderRecord>().Select(Describe));
		}

		return orders;
	}

	/// <summary>Reads a workbook's values with ExcelDataReader, each as invariant text.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The rows, each as its values joined with <c>|</c>.</returns>
	private static List<string> ReadWithExcelDataReader(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var rows = new List<string>();
		while (reader.Read())
		{
			var values = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i) switch
			{
				null => "",
				DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
				double number => number.ToString("R", CultureInfo.InvariantCulture),
				var value => Convert.ToString(value, CultureInfo.InvariantCulture),
			});
			rows.Add(string.Join("|", values));
		}

		return rows;
	}

	/// <summary>Describes an order: every member, the dates by their ticks and offset, and the total to the cent.</summary>
	/// <param name="order">The order.</param>
	/// <returns>The description.</returns>
	private static string Describe(OrderRecord order) => string.Join(
		"|",
		order.Id.ToString(CultureInfo.InvariantCulture),
		order.CustomerId.ToString(),
		order.Customer,
		order.OrderDate.Ticks.ToString(CultureInfo.InvariantCulture),
		order.ShippedAt.Ticks.ToString(CultureInfo.InvariantCulture),
		order.ShippedAt.Offset.ToString(),
		order.Total.ToString("0.00", CultureInfo.InvariantCulture),
		order.Weight.ToString("R", CultureInfo.InvariantCulture),
		order.Quantity.ToString(CultureInfo.InvariantCulture),
		order.Paid.ToString(),
		order.Status,
		order.Sku.ToString(CultureInfo.InvariantCulture),
		order.Notes);

	/// <summary>A sheet with each number cell's text replaced by its value's shortest text, to compare values.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The XML with the numbers' values.</returns>
	private static string WithNumberValues(string sheet) => NumberCell.Replace(
		sheet,
		m => $"<c r=\"{m.Groups[1].Value}\"{m.Groups[2].Value}><v>{double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)}</v></c>");

	/// <summary>The number of significant digits of a number's text.</summary>
	/// <param name="text">The text, such as <c>38676.379999999997</c> or <c>5.7E-2</c>.</param>
	/// <returns>The digits from the first that isn't 0 to the last that isn't 0.</returns>
	private static int SignificantDigits(string text) =>
		text.Split('E')[0].Replace("-", "").Replace(".", "").Trim('0').Length;

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
#endif
