using CsvHelper.Excel.Tests.Api;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The corpus through the public API: every worksheet of every workbook read to its end, the formats CsvHelper.Excel
/// doesn't read, each producer's sheets as it writes them, and Excel's basic workbook read as records.
/// </summary>
public class CorpusTests
{
	/// <summary>The sheets each producer writes from the basic workbook: <c>name visibility kind</c>, in tab order.</summary>
	private static readonly Dictionary<string, string[]> SheetsByFile = new()
	{
		["excel/basic.xlsx"] = ExcelSheets,
		["excel/date1904.xlsx"] = ExcelSheets,
		["excel/strict.xlsx"] = ExcelSheets,
		["excel/macro.xlsm"] = ExcelSheets,
		["excel/template.xltx"] = ExcelSheets,
		["excel/packageassets-1695.xlsx"] = ["PackageAssets Visible Worksheet"],

		// Apache POI's test files from Google Sheets and Excel for Mac (see downloads.txt), each with sheets of its own.
		["excel-mac/github-321.xlsx"] = ["Sheet1 Visible Worksheet"],
		["excel-mac/test_conditional_formatting.xlsx"] = ["Sheet1 Visible Worksheet"],
		["google/64450.xlsx"] = ["Hoja 1 Visible Worksheet", "Hoja 2 Visible Worksheet"],
		["google/66365.xlsx"] = ["Sheet1 Visible Worksheet"],

		// LibreOffice has no very hidden sheets, and keeps a chartsheet as a worksheet with a chart.
		["libreoffice/basic.xlsx"] = LibreOfficeSheets,
		["libreoffice/basic-ooxml.xlsx"] = LibreOfficeSheets,
		["openpyxl/basic.xlsx"] = ExcelSheets,
		["xlsxwriter/basic.xlsx"] = ExcelSheets,
		["xlsxwriter/constant_memory.xlsx"] = ExcelSheets,

		// pandas writes a data frame per sheet, visible; it writes no chartsheet.
		["pandas/openpyxl.xlsx"] = VisibleWorksheets,
		["pandas/xlsxwriter.xlsx"] = VisibleWorksheets,

		// POI can't create a chartsheet.
		["poi/sxssf.xlsx"] = Worksheets,
		["poi/xssf.xlsx"] = Worksheets,

		// The .NET producers but EPPlus write no chartsheet; MiniExcel writes no sheet states.
		["closedxml/basic.xlsx"] = Worksheets,
		["openxmlsdk/basic.xlsx"] = Worksheets,
		["epplus/basic.xlsx"] = ExcelSheets,
		["miniexcel/basic.xlsx"] = VisibleWorksheets,
		["spreadcheetah/basic.xlsx"] = Worksheets,
		["largexlsx/basic.xlsx"] = Worksheets,
	};

	/// <summary>The basic workbook's sheets, as Excel writes them.</summary>
	private static string[] ExcelSheets => ["Data Visible Worksheet", "Chart Visible Chartsheet", "Hidden Hidden Worksheet", "VeryHidden VeryHidden Worksheet", "Q1 Orders Visible Worksheet", "Bob's Visible Worksheet", "日本語 Visible Worksheet"];

	/// <summary>The basic workbook's sheets without the chartsheet.</summary>
	private static string[] Worksheets => [.. ExcelSheets.Where(s => !s.EndsWith("Chartsheet", StringComparison.Ordinal))];

	/// <summary>The basic workbook's worksheets, every one visible.</summary>
	private static string[] VisibleWorksheets => [.. Worksheets.Select(s => s.Replace(" VeryHidden Worksheet", " Visible Worksheet").Replace(" Hidden Worksheet", " Visible Worksheet"))];

	/// <summary>The basic workbook's sheets as LibreOffice writes them.</summary>
	private static string[] LibreOfficeSheets => [.. ExcelSheets.Select(s => s.Replace("Chart Visible Chartsheet", "Chart Visible Worksheet").Replace("VeryHidden VeryHidden", "VeryHidden Hidden"))];

	/// <summary>Every workbook, sync and async, as theory data.</summary>
	public static IEnumerable<object[]> WorkbookModes => CorpusFiles.Workbooks.SelectMany(w => new[] { new object[] { w, false }, new object[] { w, true } });

	[Fact]
	public void ListsEveryCorpusWorkbook()
	{
		Assert.Equal(SheetsByFile.Keys.OrderBy(k => k, StringComparer.Ordinal), CorpusFiles.Workbooks);
	}

	[Theory]
	[MemberData(nameof(WorkbookModes))]
	public async Task ReadsEveryWorksheetThroughThePublicApi(string file, bool async)
	{
		var expected = CorpusFiles.Read(file, out var date1904);
		using var workbook = async ? await ExcelWorkbook.OpenAsync(CorpusFiles.PathOf(file)) : ExcelWorkbook.Open(CorpusFiles.PathOf(file));

		Assert.Equal(SheetsByFile[file], workbook.Sheets.Select(s => $"{s.Name} {s.Visibility} {s.Kind}"));
		Assert.Equal(date1904, workbook.Date1904);
		Assert.Equal(file == "excel/date1904.xlsx", workbook.Date1904);
		var options = new ExcelReaderOptions { Csv = new() { HasHeader = false, SkipEmptyLines = false } };
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
		{
			// Record n is row n, and field c is column c: each field is the cell's text, or empty.
			using var reader = await workbook.OpenReaderAsync(async, sheet, options);
			var cells = expected[sheet.Name];
			var rows = 0L;
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				rows++;
				Assert.Equal(rows, reader.LineNumber);
				for (var column = 0; column < reader.FieldCount; column++)
				{
					var text = cells.TryGetValue((rows, column), out var value) ? value.Text : "";
					Assert.True(text == reader.GetString(column), $"{new SheetLocation(sheet.Name).Cell(column, rows)}: {CellValue.Show(text)} read as {CellValue.Show(reader.GetString(column))}");
				}
			}

			// Every row with a value is read; a producer can end a sheet with valueless rows, which are records too.
			Assert.InRange(rows, cells.Keys.Select(k => k.Row).DefaultIfEmpty(0).Max(), long.MaxValue);
		}
	}

	[Theory]
	[InlineData("excel/binary.xlsb", "The workbook is an Excel binary workbook (.xlsb), which CsvHelper.Excel doesn't read.")]
	[InlineData("excel/encrypted.xlsx", "The input is a compound file, as password-protected workbooks and Excel 97-2003 workbooks (.xls) are.")]
	[InlineData("excel/legacy.xls", "The input is a compound file, as password-protected workbooks and Excel 97-2003 workbooks (.xls) are.")]
	public void RejectsTheFormatsItDoesntRead(string file, string message)
	{
		var e = Assert.Throws<NotSupportedException>(() => ExcelWorkbook.Open(CorpusFiles.PathOf(file)));
		Assert.StartsWith(message, e.Message);
	}

	[Fact]
	public void ReadsExcelsBasicWorkbookAsRecords()
	{
		using var reader = ExcelReader.FromFile(CorpusFiles.PathOf("excel/basic.xlsx"));
		var records = new List<string>();
		while (reader.Read())
		{
			records.Add($"{reader.LineNumber}: " + Workbooks.Fields(reader));
		}

		var long32767 = new string('x', 32767);
		Assert.Equal(["Kind", "Value", "More"], reader.Headers);
		Assert.Equal(
			[
				"2: text|plain text|x",
				"3: integer|42|",
				"4: decimal|3.25|",
				"5: negative|-17.5|",
				"6: small|1.0000000000000001E-5|",
				"7: precise|0.30000000000000004|",
				"8: boolean true|1|",
				"9: boolean false|0|",
				"10: not available|#N/A|",
				"11: divide by zero|#DIV/0!|",
				"12: gap|a||d",
				"13: short||",
				"15: ||block|1|2",
				$"16: emoji|{char.ConvertFromUtf32(0x1F600)}{char.ConvertFromUtf32(0x1F44D)}{char.ConvertFromUtf32(0x1F3FD)}|",
				"17: spaces|  leading and trailing  |",
				"18: line break|line one\nline two|",
				"19: control|a\u0001b|",
				"20: escape|_x0041_|",
				"21: markup|&<>\"'|",
				$"22: long|{long32767}|",
				"23: rich|bold and plain|",
				"24: phonetic|東京|",
				"25: formula number|84|",
				"26: formula text|text!|",
				"27: formula error|#VALUE!|",
				"28: shared formula|280|",
				"29: shared formula|290|",
				"30: shared formula|300|",
				"31: date|45293|",
				"32: date and time|45293.127835648149|",
				"33: time|0.12783564814814816|",
				"34: custom date|45657|",
			],
			records);
	}
}
