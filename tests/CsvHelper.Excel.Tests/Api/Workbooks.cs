using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order, read from a sheet with a generated mapper.</summary>
[CsvRecord]
public partial class SheetOrder
{
	/// <summary>A shared string cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>An inline string cell, or an empty one.</summary>
	public string? Note { get; set; }
}

/// <summary>A name and a quantity that must be a number.</summary>
[CsvRecord]
public partial class SheetQuantity
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }
}

/// <summary>A record with a column the orders sheet doesn't have.</summary>
[CsvRecord]
public partial class SheetOrderWithCustomer
{
	/// <summary>A shared string cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A column no sheet has.</summary>
	public string Customer { get; set; } = "";
}

/// <summary>Workbooks for the public API's tests, and ways to open and read them synchronously or asynchronously.</summary>
internal static class Workbooks
{
	/// <summary>The rows of the Orders sheet, after its header, as <see cref="ReadAllAsync"/> reads them.</summary>
	public static readonly string[] OrderRows = ["apple|3|1.5|TRUE|first", "pear|10|0.25|FALSE|", "plum|7|2|TRUE|third"];

	/// <summary>
	/// A workbook as Excel writes one: a chartsheet first, a hidden worksheet, then "Orders" (a header and three orders,
	/// with shared strings, numbers, booleans and inline strings) and "Q1 Orders" (one row).
	/// </summary>
	/// <returns>The package.</returns>
	public static TestPackage Orders() => TestPackage.Workbook(
			new TestSheet("Chart", type: "chartsheet"),
			new TestSheet("Hidden", state: "hidden", xml: Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>hidden</t></is></c></row>")),
			new TestSheet("Orders", xml: Sheets.Worksheet(
				"<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"s\"><v>2</v></c><c r=\"D1\" t=\"s\"><v>3</v></c><c r=\"E1\" t=\"s\"><v>4</v></c></row>" +
				"<row r=\"2\"><c r=\"A2\" t=\"s\"><v>5</v></c><c r=\"B2\"><v>3</v></c><c r=\"C2\" s=\"1\"><v>1.5</v></c><c r=\"D2\" t=\"b\"><v>1</v></c><c r=\"E2\" t=\"inlineStr\"><is><t>first</t></is></c></row>" +
				"<row r=\"3\"><c r=\"A3\" t=\"s\"><v>6</v></c><c r=\"B3\"><v>10</v></c><c r=\"C3\" s=\"1\"><v>0.25</v></c><c r=\"D3\" t=\"b\"><v>0</v></c></row>" +
				"<row r=\"5\"><c r=\"A5\" t=\"s\"><v>7</v></c><c r=\"B5\"><v>7</v></c><c r=\"C5\" s=\"1\"><v>2</v></c><c r=\"D5\" t=\"b\"><v>1</v></c><c r=\"E5\" t=\"inlineStr\"><is><t>third</t></is></c></row>")),
			new TestSheet("Q1 Orders", xml: Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>q1</t></is></c></row>")))
		.SharedStrings("<t>Name</t>", "<t>Quantity</t>", "<t>Price</t>", "<t>Paid</t>", "<t>Note</t>", "<t>apple</t>", "<t>pear</t>", "<t>plum</t>");

	/// <summary>A workbook of one sheet, "Data", with these rows.</summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <returns>The package.</returns>
	public static TestPackage Data(string rows) => TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet(rows)));

	/// <summary>A sheet of numbers: a header <c>Id</c>, then the rows 1 to <paramref name="count"/>.</summary>
	/// <param name="count">The number of rows after the header.</param>
	/// <returns>The rows.</returns>
	public static string Numbers(int count) =>
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Id</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Text</t></is></c></row>" +
		string.Concat(Enumerable.Range(1, count).Select(i => $"<row r=\"{i + 1}\"><c r=\"A{i + 1}\"><v>{i}</v></c><c r=\"B{i + 1}\" t=\"inlineStr\"><is><t>row {i}</t></is></c></row>"));

	/// <summary>Opens a package's workbook from a seekable stream.</summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="options">The workbook's options, or <see langword="null"/>.</param>
	/// <returns>The workbook.</returns>
	public static async Task<ExcelWorkbook> OpenAsync(TestPackage package, bool async, ExcelReaderOptions? options = null) =>
		async ? await ExcelWorkbook.OpenAsync(package.ToStream(), options) : ExcelWorkbook.Open(package.ToStream(), options);

	/// <summary>Opens a reader of the sheet the options choose, or the first worksheet.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="options">The reader's options, or <see langword="null"/>.</param>
	/// <returns>The reader.</returns>
	public static async Task<CsvReader> OpenReaderAsync(this ExcelWorkbook workbook, bool async, ExcelReaderOptions? options = null) =>
		async ? await workbook.OpenReaderAsync(options) : workbook.OpenReader(options);

	/// <summary>Opens a reader of the sheet with a name.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="options">The reader's options, or <see langword="null"/>.</param>
	/// <returns>The reader.</returns>
	public static async Task<CsvReader> OpenReaderAsync(this ExcelWorkbook workbook, bool async, string sheetName, ExcelReaderOptions? options = null) =>
		async ? await workbook.OpenReaderAsync(sheetName, options) : workbook.OpenReader(sheetName, options);

	/// <summary>Opens a reader of the sheet at an index.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="sheetIndex">The sheet's index.</param>
	/// <param name="options">The reader's options, or <see langword="null"/>.</param>
	/// <returns>The reader.</returns>
	public static async Task<CsvReader> OpenReaderAsync(this ExcelWorkbook workbook, bool async, int sheetIndex, ExcelReaderOptions? options = null) =>
		async ? await workbook.OpenReaderAsync(sheetIndex, options) : workbook.OpenReader(sheetIndex, options);

	/// <summary>Opens a reader of a sheet.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="options">The reader's options, or <see langword="null"/>.</param>
	/// <returns>The reader.</returns>
	public static async Task<CsvReader> OpenReaderAsync(this ExcelWorkbook workbook, bool async, ExcelSheet sheet, ExcelReaderOptions? options = null) =>
		async ? await workbook.OpenReaderAsync(sheet, options) : workbook.OpenReader(sheet, options);

	/// <summary>Reads the rest of a reader's records, each as its fields joined with <c>|</c>.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>The records.</returns>
	public static async Task<List<string>> ReadAllAsync(this CsvReader reader, bool async)
	{
		var records = new List<string>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			records.Add(Fields(reader));
		}

		return records;
	}

	/// <summary>The fields of a reader's current record, joined with <c>|</c>.</summary>
	/// <param name="reader">The reader.</param>
	/// <returns>The fields.</returns>
	public static string Fields(CsvReader reader) => string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(reader.GetString));

	/// <summary>Writes a package to a new temporary file.</summary>
	/// <param name="package">The package.</param>
	/// <returns>The file's path, for the caller to delete.</returns>
	public static string ToFile(TestPackage package)
	{
		var path = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}.xlsx");
		File.WriteAllBytes(path, package.ToArray());

		return path;
	}

	/// <summary>Whether a file can be opened with no sharing: no handle to it is left open.</summary>
	/// <param name="path">The file's path.</param>
	/// <returns>Whether it is closed.</returns>
	public static bool IsClosed(string path)
	{
		try
		{
			new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose();

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}
}
