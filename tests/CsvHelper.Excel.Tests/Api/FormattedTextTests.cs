using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order read from formatted cells: string members get the text Excel shows, typed members the values.</summary>
[CsvRecord]
public partial class FormattedOrder
{
	/// <summary>A text cell without a format.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number in <c>#,##0.00</c>, read as its value; its header cell shows <c>Q1</c>.</summary>
	public decimal Q1 { get; set; }

	/// <summary>A number in <c>0.0%</c>, read as its text; its header cell shows <c>Q2</c>.</summary>
	public string Q2 { get; set; } = "";

	/// <summary>A date in <c>mmm-yy</c>, read as its value.</summary>
	public DateTime Day { get; set; }

	/// <summary>A duration in <c>[h]:mm</c>, read as its value.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A boolean in <c>"t:"@</c>, read as its value.</summary>
	public bool Paid { get; set; }

	/// <summary>A text in <c>"Note: "@</c>, read as its text.</summary>
	public string Note { get; set; } = "";

	/// <summary>A number in <c># ?/?</c>, read as its value.</summary>
	public double Part { get; set; }
}

/// <summary>A record whose integer member can't read a number with a fraction, for the import's errors.</summary>
[CsvRecord]
public partial class FormattedCount
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number in <c>#,##0.00</c> or <c>0.00;(0.00)</c>: 1234.5 isn't an integer.</summary>
	public int Q1 { get; set; }
}

/// <summary>
/// <see cref="ExcelReaderOptions.Text"/> end to end (Phase 8's step 5): under <see cref="ExcelText.Formatted"/> every path
/// that gives text gives the text Excel shows (strings, header names and the header search, merged fills, hidden columns,
/// <c>ParallelEnumerate</c>, dynamic records, <c>CsvDataReader</c>, the conversion and the import's errors), while typed
/// reads keep the values; error cells, formulas read as text and empty text keep their own text; and
/// <see cref="ExcelText.Raw"/> reads as before.
/// </summary>
public class FormattedTextTests
{
	/// <summary>
	/// The number formats, one a cell format, so that a format's index (a field's style) is its cell format's: built-in ids
	/// as numbers, the rest custom codes. 13 is a code the renderer can't read (a lower-case exponent, which Excel refuses).
	/// </summary>
	internal static readonly string[] Formats =
	[
		"0", "14", "#,##0.00", "mmm-yy", "\"Note: \"@", "0.0%", "[h]:mm", "# ?/?", "\"=\"0", ";;;", "0.00;(0.00)", "\"Q\"0", "\"t:\"@", "0.00e+00",
		"[$-407]mmmm", "49", "7", "mmmm yyyy",
	];

	/// <summary>
	/// The main sheet: a header whose B1 and C1 are numbers shown <c>Q1</c> and <c>Q2</c>; apple's cells in a format each;
	/// pear's in others: <c>@</c> alone, a negative's section, General, a negative date (which the 1900 date system can't
	/// show), a code that can't be read, a boolean without a format, a shared string through a text section, and an error
	/// in a number format.
	/// </summary>
	internal const string Rows =
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" s=\"11\"><v>1</v></c><c r=\"C1\" s=\"11\"><v>2</v></c>" +
		"<c r=\"D1\" t=\"inlineStr\"><is><t>Day</t></is></c><c r=\"E1\" t=\"inlineStr\"><is><t>Took</t></is></c><c r=\"F1\" t=\"inlineStr\"><is><t>Paid</t></is></c>" +
		"<c r=\"G1\" t=\"inlineStr\"><is><t>Note</t></is></c><c r=\"H1\" t=\"inlineStr\"><is><t>Part</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\" s=\"2\"><v>1234.5</v></c><c r=\"C2\" s=\"5\"><v>0.125</v></c>" +
		"<c r=\"D2\" s=\"3\"><v>45306</v></c><c r=\"E2\" s=\"6\"><v>1.5</v></c><c r=\"F2\" t=\"b\" s=\"12\"><v>1</v></c>" +
		"<c r=\"G2\" t=\"inlineStr\" s=\"4\"><is><t>abc</t></is></c><c r=\"H2\" s=\"7\"><v>3.5</v></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"s\" s=\"15\"><v>0</v></c><c r=\"B3\" s=\"10\"><v>-1234.5</v></c><c r=\"C3\"><v>0.3333333333333333</v></c>" +
		"<c r=\"D3\" s=\"1\"><v>-1</v></c><c r=\"E3\" s=\"13\"><v>0.5</v></c><c r=\"F3\" t=\"b\"><v>0</v></c>" +
		"<c r=\"G3\" t=\"s\" s=\"12\"><v>1</v></c><c r=\"H3\" t=\"e\" s=\"2\"><v>#N/A</v></c></row>";

	/// <summary>The header as Excel shows it.</summary>
	private const string HeaderShown = "Name|Q1|Q2|Day|Took|Paid|Note|Part";

	/// <summary>Apple's fields as Excel shows them.</summary>
	private const string AppleShown = "apple|1,234.50|12.5%|Jan-24|36:00|t:TRUE|Note: abc|3 1/2";

	/// <summary>Reads as <see cref="ExcelText.Formatted"/>.</summary>
	private static readonly ExcelReaderOptions Formatted = new() { Text = ExcelText.Formatted };

	/// <summary>The styles part: <see cref="Formats"/>, each a cell format.</summary>
	/// <returns>The part's XML.</returns>
	internal static string Styles()
	{
		var custom = new StringBuilder();
		var cells = new StringBuilder();
		var count = 0;
		for (var i = 0; i < Formats.Length; i++)
		{
			var format = Formats[i];
			if (!int.TryParse(format, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
			{
				id = 164 + i;
				custom.Append($"<numFmt numFmtId=\"{id}\" formatCode=\"{Sheets.Escape(format).Replace("\"", "&quot;")}\"/>");
				count++;
			}

			cells.Append($"<xf numFmtId=\"{id}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		}

		return TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\">" +
			$"<numFmts count=\"{count}\">{custom}</numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
			"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
			$"<cellXfs count=\"{Formats.Length}\">{cells}</cellXfs></styleSheet>";
	}

	/// <summary>A workbook of one sheet, Data, with <see cref="Styles"/> and the shared strings <c>pear</c>, <c>xyz</c> and empty text.</summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="xml">The whole sheet part, in place of one around <paramref name="rows"/>; or <see langword="null"/>.</param>
	/// <returns>The package.</returns>
	internal static TestPackage Book(string rows, bool date1904 = false, string? xml = null)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: xml ?? Sheets.Worksheet(rows)))
			.Set("xl/styles.xml", Styles())
			.SharedStrings("<t>pear</t>", "<t>xyz</t>", "<t/>");

		return date1904 ? package.Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ") : package;
	}

	/// <summary>A cell of a number in a cell format.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="value">The number.</param>
	/// <param name="style">The cell format.</param>
	/// <returns>The cell's XML.</returns>
	private static string N(string reference, string value, int style) => $"<c r=\"{reference}\" s=\"{style}\"><v>{value}</v></c>";

	/// <summary>A cell of inline text.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string T(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";

	/// <summary>Reads a sheet's header, when the options have one, and its records, each joined with <c>|</c>.</summary>
	/// <param name="package">The workbook.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="options">The options.</param>
	/// <returns>The lines.</returns>
	private static async Task<List<string>> ReadAsync(TestPackage package, bool async, ExcelReaderOptions options)
	{
		using var workbook = await Workbooks.OpenAsync(package, async);
		using var reader = await workbook.OpenReaderAsync(async, options);
		var lines = new List<string>();
		if (options.Csv.HasHeader)
		{
			if (async)
			{
				await reader.ReadHeaderAsync();
			}

			lines.Add(string.Join("|", reader.Headers));
		}

		while (async ? await reader.ReadAsync() : reader.Read())
		{
			lines.Add(Workbooks.Fields(reader));
		}

		return lines;
	}

	/// <summary>Converts a workbook's first sheet to CSV through the stream overloads.</summary>
	/// <param name="package">The workbook.</param>
	/// <param name="async">Whether to convert asynchronously.</param>
	/// <param name="options">The options.</param>
	/// <returns>The CSV.</returns>
	private static async Task<string> ConvertAsync(TestPackage package, bool async, ExcelToCsvOptions options)
	{
		using var excel = package.ToStream();
		using var csv = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToCsvAsync(excel, csv, options);
		}
		else
		{
			ExcelConvert.ToCsv(excel, csv, options);
		}

		return Encoding.UTF8.GetString(csv.ToArray());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheShownTextThroughEveryTextAccessor(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Book(Rows), async);
		using var reader = await workbook.OpenReaderAsync(async, Formatted);
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		Assert.Equal(HeaderShown, string.Join("|", reader.Headers));
		Assert.Equal(AppleShown, Workbooks.Fields(reader));
		Assert.Equal("1,234.50", reader.GetString("Q1"));
		Assert.Equal(1, reader.GetFieldIndex("Q1"));
		Assert.True(reader.TryGetFieldIndex("Q2", out var q2));
		Assert.Equal(2, q2);

		// GetChars and FieldEquals give the shown text; GetSpan the stored value.
		var chars = new char[32];
		var length = reader.GetChars(2, chars);
		Assert.Equal("12.5%", new string(chars, 0, length));
		Assert.True(reader.FieldEquals(1, "1,234.50"u8));
		Assert.False(reader.FieldEquals(1, "1234.5"u8));
		Assert.True(reader.FieldEquals(5, "t:TRUE"u8));
		Assert.Equal("1234.5"u8.ToArray(), reader.GetSpan(1).ToArray());

		// Typed reads keep the value.
		Assert.Equal(1234.5m, reader.Parse<decimal>("Q1"));
		Assert.Equal(0.125, reader.Parse<double>("Q2"));
		Assert.Equal(new DateTime(2024, 1, 15), reader.Parse<DateTime>("Day"));
		Assert.Equal(TimeSpan.FromHours(36), reader.Parse<TimeSpan>("Took"));
		Assert.True(reader.Parse<bool>("Paid"));
		Assert.Equal(3.5, reader.Parse<double>("Part"));

		// Pear: @ alone, a negative's section, General, a boolean without a text section, a shared string through one, an
		// error in a number format; the date Excel can't show and the code that can't be read are as Raw reads them.
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		var raw = await ReadAsync(Book(Rows), async, ExcelReaderOptions.Default);
		var rawPear = raw[2].Split('|');
		Assert.Equal($"pear|(1234.50)|0.333333333|{rawPear[3]}|{rawPear[4]}|FALSE|t:xyz|#N/A", Workbooks.Fields(reader));
		Assert.Equal("0.5", rawPear[4]);
		Assert.Equal(-1234.5m, reader.Parse<decimal>("Q1"));
		Assert.Equal(1.0 / 3, reader.Parse<double>("Q2"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsRawAsBefore(bool async)
	{
		var lines = await ReadAsync(Book(Rows), async, ExcelReaderOptions.Default);
		Assert.Equal("Name|1|2|Day|Took|Paid|Note|Part", lines[0]);
		Assert.Equal("apple|1234.5|0.125|2024-01-15|36:00:00|TRUE|abc|3.5", lines[1]);
		Assert.StartsWith("pear|-1234.5|0.3333333333333333|", lines[2], StringComparison.Ordinal);
		Assert.EndsWith("|0.5|FALSE|xyz|#N/A", lines[2], StringComparison.Ordinal);
		Assert.Equal(lines, await ReadAsync(Book(Rows), async, new ExcelReaderOptions { Text = ExcelText.Raw }));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MapsStringMembersToTheShownTextAndTypedMembersToTheValues(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Book(Rows), async);
		using var reader = await workbook.OpenReaderAsync(async, Formatted);
		FormattedOrder apple;
		if (async)
		{
			await using var records = reader.GetRecordsAsync<FormattedOrder>().GetAsyncEnumerator();
			Assert.True(await records.MoveNextAsync());
			apple = records.Current;
		}
		else
		{
			apple = reader.GetRecords<FormattedOrder>().First();
		}

		Assert.Equal("apple", apple.Name);
		Assert.Equal(1234.5m, apple.Q1);
		Assert.Equal("12.5%", apple.Q2);
		Assert.Equal(new DateTime(2024, 1, 15), apple.Day);
		Assert.Equal(TimeSpan.FromHours(36), apple.Took);
		Assert.True(apple.Paid);
		Assert.Equal("Note: abc", apple.Note);
		Assert.Equal(3.5, apple.Part);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesDynamicRecordsAndDataReadersTheShownText(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Book(Rows), async);
		using (var reader = await workbook.OpenReaderAsync(async, Formatted))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal("1,234.50", records[0]["Q1"]);
			Assert.Equal("t:TRUE", records[0]["Paid"]);
			Assert.Equal("t:xyz", records[1]["Note"]);
			Assert.Equal(1234.5m, records[0].Parse<decimal>("Q1"));
			Assert.Equal(new DateTime(2024, 1, 15), records[0].Parse<DateTime>("Day"));
			dynamic apple = records[0];
			Assert.Equal("12.5%", (string)apple.Q2);
		}

		using (var reader = await workbook.OpenReaderAsync(async, Formatted))
		using (var data = new CsvDataReader(reader))
		{
			Assert.True(data.Read());
			Assert.Equal("Q1", data.GetName(1));
			Assert.Equal("1,234.50", data.GetString(1));
			Assert.Equal(1234.5m, data.GetDecimal(1));
			Assert.Equal("Jan-24", data.GetValue(3));
			Assert.Equal(new DateTime(2024, 1, 15), data.GetDateTime(3));
			var table = new DataTable();
			table.Load(data);
			Assert.Equal("(1234.50)", table.Rows[0]["Q1"]);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CarriesStylesThroughParallelEnumerate(bool async)
	{
		var rows = new StringBuilder("<row r=\"1\">" + T("A1", "N") + N("B1", "1", 11) + "</row>");
		for (var row = 2; row <= 3001; row++)
		{
			rows.Append($"<row r=\"{row}\">{N($"A{row}", row.ToString(CultureInfo.InvariantCulture), 0)}{N($"B{row}", $"{row}.5", row % 2 == 0 ? 2 : 5)}</row>");
		}

		using var workbook = await Workbooks.OpenAsync(Book(rows.ToString()), async);
		using var reader = await workbook.OpenReaderAsync(async, Formatted);
		var texts = reader.ParallelEnumerate(r => (r.GetString(0), r.GetString("Q1"), r.Parse<double>(1)), 4).ToList();
		Assert.Equal(3000, texts.Count);
		for (var i = 0; i < texts.Count; i++)
		{
			var row = i + 2;
			var value = row + 0.5;
			var shown = row % 2 == 0 ? value.ToString("#,##0.00", CultureInfo.InvariantCulture) : (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
			Assert.Equal((row.ToString(CultureInfo.InvariantCulture), shown, value), texts[i]);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheHeaderAndFindsItByTheShownText(bool async)
	{
		var rows =
			$"<row r=\"1\">{T("A1", "Quarterly report")}</row>" +
			$"<row r=\"3\">{T("A3", "Name")}{N("B3", "1", 11)}{N("C3", "2", 11)}</row>" +
			$"<row r=\"4\">{T("A4", "apple")}{N("B4", "5", 2)}{N("C4", "6", 0)}</row>";
		var find = ExcelHeaderRow.Find(["Name", "Q1", "Q2"]);
		Assert.Equal(
			["Name|Q1|Q2", "apple|5.00|6"],
			await ReadAsync(Book(rows), async, Formatted with { HeaderRow = find }));

		// Raw names the columns 1 and 2, so no row has Q1.
		await Assert.ThrowsAsync<KeyNotFoundException>(() => ReadAsync(Book(rows), async, new ExcelReaderOptions { HeaderRow = find }));

		// A row given by its number is named the same.
		Assert.Equal(
			["Name|Q1|Q2", "apple|5.00|6"],
			await ReadAsync(Book(rows), async, Formatted with { HeaderRow = ExcelHeaderRow.At(3) }));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FillsMergedCellsAndSkipsHiddenColumnsWithTheirStyles(bool async)
	{
		// B2:C3 is merged, its top-left cell 1234.5 in #,##0.00; D is hidden; E2's percent and the merged header B1:C1 keep theirs.
		var rows =
			$"<row r=\"1\">{T("A1", "Name")}{N("B1", "1", 11)}{N("D1", "2", 11)}{N("E1", "3", 11)}</row>" +
			$"<row r=\"2\">{T("A2", "apple")}{N("B2", "1234.5", 2)}{N("D2", "7", 0)}{N("E2", "0.125", 5)}</row>" +
			$"<row r=\"3\">{T("A3", "pear")}{N("D3", "8", 0)}{N("E3", "45306", 3)}</row>";
		var xml = MergedScannerTests.Worksheet(rows, "B1:C1", "B2:C3").Replace("<sheetData>", "<cols><col min=\"4\" max=\"4\" hidden=\"1\"/></cols><sheetData>");
		var options = Formatted with { FillMergedCells = true, SkipHiddenColumns = true, Csv = new CsvReaderOptions { HasHeader = false } };
		Assert.Equal(
			["Name|Q1|Q1|Q3", "apple|1,234.50|1,234.50|12.5%", "pear|1,234.50|1,234.50|Jan-24"],
			await ReadAsync(Book("", xml: xml), async, options));

		// With the header found, as the search's scanner fills and skips them too.
		Assert.Equal(
			["Name|Q1|Q1|Q3", "apple|1,234.50|1,234.50|12.5%", "pear|1,234.50|1,234.50|Jan-24"],
			await ReadAsync(Book("", xml: xml), async, options with { Csv = CsvReaderOptions.Default, HeaderRow = ExcelHeaderRow.Find(["Name", "Q3"]) }));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepsErrorCellsUnderEachPolicy(bool async)
	{
		var rows = $"<row r=\"1\">{T("A1", "Name")}{T("B1", "Q1")}</row><row r=\"2\">{T("A2", "pear")}<c r=\"B2\" t=\"e\" s=\"9\"><v>#DIV/0!</v></c></row>";
		Assert.Equal(["Name|Q1", "pear|#DIV/0!"], await ReadAsync(Book(rows), async, Formatted));
		Assert.Equal(["Name|Q1", "pear|"], await ReadAsync(Book(rows), async, Formatted with { ErrorCells = ExcelErrorCells.Null }));

		using var workbook = await Workbooks.OpenAsync(Book(rows), async);
		using var reader = await workbook.OpenReaderAsync(async, Formatted with { ErrorCells = ExcelErrorCells.Error });
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		Assert.Equal("Data!B2 holds the error #DIV/0!.", Assert.Throws<FormatException>(() => reader.GetString(1)).Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeavesFormulasReadAsTextAndEmptyTextUnformatted(bool async)
	{
		var rows =
			$"<row r=\"1\">{T("A1", "Name")}{T("B1", "Q1")}{T("C1", "Note")}{T("D1", "Hidden")}</row>" +
			$"<row r=\"2\">{T("A2", "apple")}<c r=\"B2\" s=\"2\"><f>SUM(1000,234.5)</f><v>1234.5</v></c><c r=\"C2\" t=\"s\" s=\"4\"><v>2</v></c>" +
			$"{N("D2", "5", 9)}</row>";
		Assert.Equal(["Name|Q1|Note|Hidden", "apple|=SUM(1000,234.5)||"], await ReadAsync(Book(rows), async, Formatted with { Formulas = ExcelFormulas.FormulaText }));
		Assert.Equal(["Name|Q1|Note|Hidden", "apple|1,234.50||"], await ReadAsync(Book(rows), async, Formatted));
		Assert.Equal(["Name|Q1|Note|Hidden", "apple|1234.5||5"], await ReadAsync(Book(rows), async, ExcelReaderOptions.Default));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ShowsDatesInTheWorkbooksDateSystem(bool async)
	{
		// In the 1904 date system, 45306 is 2028-01-16, and -1 shows a minus sign and 1904-01-02.
		var rows = $"<row r=\"1\">{N("A1", "45306", 3)}{N("B1", "45306", 1)}{N("C1", "-1", 1)}{N("D1", "1.5", 6)}</row>";
		var options = Formatted with { Csv = new CsvReaderOptions { HasHeader = false } };
		Assert.Equal(["Jan-28|1/16/2028|-1/2/1904|36:00"], await ReadAsync(Book(rows, date1904: true), async, options));
		Assert.StartsWith("Jan-24|1/15/2024|", (await ReadAsync(Book(rows), async, options))[0], StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ShowsTheTextInTheReadersCulture(bool async)
	{
		var rows =
			$"<row r=\"1\">{N("A1", "1234.5", 2)}{N("B1", "0.125", 5)}{N("C1", "45306", 1)}{N("D1", "1234.5", 16)}{N("E1", "45306", 17)}" +
			$"{N("F1", "45306", 14)}{N("G1", "0.3333333333333333", 0)}{N("H1", "1.5", 6)}</row>";
		var options = Formatted with { Csv = new CsvReaderOptions { HasHeader = false } };

		// The invariant culture is Excel en-US's; [$-407] names the month in German whatever the culture.
		Assert.Equal(["1,234.50|12.5%|1/15/2024|$1,234.50 |January 2024|Januar|0.333333333|36:00"], await ReadAsync(Book(rows), async, options));

		// German: its separators, short date, currency and month names; : as written.
		var german = options with { Csv = options.Csv with { FormatProvider = CultureInfo.GetCultureInfo("de-DE") } };
		Assert.Equal(["1.234,50|12,5%|15.01.2024|1.234,50 €|Januar 2024|Januar|0,333333333|36:00"], await ReadAsync(Book(rows), async, german));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ConvertsToCsvWithTheShownTextAsText(bool async)
	{
		var options = new ExcelToCsvOptions { Excel = Formatted };
		var raw = await ConvertAsync(Book(Rows), async, new ExcelToCsvOptions());
		var rawPear = raw.Split(["\r\n"], StringSplitOptions.None)[2].Split(',');
		Assert.Equal(
			$"Name,Q1,Q2,Day,Took,Paid,Note,Part\r\napple,\"1,234.50\",12.5%,Jan-24,36:00,t:TRUE,Note: abc,3 1/2\r\npear,(1234.50),0.333333333,{rawPear[3]},0.5,FALSE,t:xyz,#N/A\r\n",
			await ConvertAsync(Book(Rows), async, options));

		// Written as text, every field is checked for injection: a code can show a number as =5, and General shows -5.
		var rows = $"<row r=\"1\">{N("A1", "5", 8)}{N("B1", "-5", 0)}{N("C1", "5", 0)}</row>";
		var escape = new CsvWriterOptions { Injection = CsvInjection.Escape };
		var noHeader = new CsvReaderOptions { HasHeader = false };
		Assert.Equal(
			"\"'=5\",\"'-5\",5\r\n",
			await ConvertAsync(Book(rows), async, new ExcelToCsvOptions { Excel = Formatted with { Csv = noHeader }, Csv = escape }));
		Assert.Equal("5,-5,5\r\n", await ConvertAsync(Book(rows), async, new ExcelToCsvOptions { Excel = new ExcelReaderOptions { Csv = noHeader }, Csv = escape }));

		// A reader opened by the caller converts the same.
		using var workbook = await Workbooks.OpenAsync(Book(Rows), async);
		using var reader = await workbook.OpenReaderAsync(async, Formatted);
		using var text = new StringWriter();
		using (var csv = CsvWriter.Create(text, new CsvWriterOptions { ValidateFieldCount = false }, leaveOpen: true))
		{
			if (async)
			{
				await ExcelConvert.ToCsvAsync(reader, csv);
			}
			else
			{
				ExcelConvert.ToCsv(reader, csv);
			}
		}

		Assert.StartsWith("Name,Q1,Q2,Day,Took,Paid,Note,Part\r\napple,\"1,234.50\",12.5%,", text.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesImportErrorsTheShownText(bool async)
	{
		// Q1's cells hold 1234.5 and -1234.5, which an integer can't read.
		var rows =
			$"<row r=\"1\">{T("A1", "Name")}{T("B1", "Q1")}</row><row r=\"2\">{T("A2", "apple")}{N("B2", "1234.5", 2)}</row>" +
			$"<row r=\"3\">{T("A3", "pear")}{N("B3", "-1234.5", 10)}</row>";
		var bytes = Book(rows).ToArray();
		foreach (var (options, values) in new[] { (Formatted, new[] { "1,234.50", "(1234.50)" }), (ExcelReaderOptions.Default, new[] { "1234.5", "-1234.5" }) })
		{
			using var stream = new MemoryStream(bytes);
			var result = async ? await ExcelReader.ImportAsync<FormattedCount>(stream, options) : ExcelReader.Import<FormattedCount>(stream, options);
			Assert.Equal(["Data!B2", "Data!B3"], result.Errors.Select(e => e.Location));
			Assert.Equal(["Q1", "Q1"], result.Errors.Select(e => e.Column));
			Assert.Equal(values, result.Errors.Select(e => e.Value));
		}

		// A header that isn't found: the closest row, row 1, is named as shown, so it lacks only Name; Raw names it 1, and
		// so it lacks Q1 too.
		var lacking = Book($"<row r=\"1\">{T("A1", "Title")}{N("B1", "1", 11)}</row><row r=\"2\">{T("A2", "x")}</row>").ToArray();
		foreach (var (options, lacks) in new[] { (Formatted, 1), (ExcelReaderOptions.Default, 2) })
		{
			using var stream = new MemoryStream(lacking);
			var find = options with { HeaderRow = ExcelHeaderRow.Find<FormattedCount>() };
			var result = async ? await ExcelReader.ImportAsync<FormattedCount>(stream, find) : ExcelReader.Import<FormattedCount>(stream, find);
			Assert.Equal(lacks, result.Errors.Count);
			Assert.All(result.Errors, e => Assert.Equal("Data!1:1", e.Location));
		}

		// A header cell shown Q1 names the column the error is in.
		var shownHeader = Book(rows.Replace(T("B1", "Q1"), N("B1", "1", 11))).ToArray();
		using (var stream = new MemoryStream(shownHeader))
		{
			var result = async ? await ExcelReader.ImportAsync<FormattedCount>(stream, Formatted) : ExcelReader.Import<FormattedCount>(stream, Formatted);
			Assert.Equal(["Q1", "Q1"], result.Errors.Select(e => e.Column));
			Assert.Equal(["1,234.50", "(1234.50)"], result.Errors.Select(e => e.Value));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsLongTextAndStoredValuesThatAreNoNumbers(bool async)
	{
		// 200 euro signs through "Note: "@ are 606 bytes, past the reader's 512 on the stack and the conversion's 64; 1E+100 in
		// 0 is 101 digits; a number cell whose value isn't a number reads as stored.
		var euros = new string('€', 200);
		var rows =
			$"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\" s=\"4\"><is><t>{euros}</t></is></c>{N("B1", "1E+100", 0)}{N("C1", "1E+100", 11)}" +
			$"{N("D1", "abc", 2)}</row>";
		var hundred = "1" + new string('0', 100);
		var options = Formatted with { Csv = new CsvReaderOptions { HasHeader = false } };
		Assert.Equal([$"Note: {euros}|1E+100|Q{hundred}|abc"], await ReadAsync(Book(rows), async, options));
		Assert.Equal(
			$"Note: {euros},1E+100,Q{hundred},abc\r\n",
			await ConvertAsync(Book(rows), async, new ExcelToCsvOptions { Excel = options }));

		using var workbook = await Workbooks.OpenAsync(Book(rows), async);
		using var reader = await workbook.OpenReaderAsync(async, options);
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		Assert.True(reader.FieldEquals(0, Encoding.UTF8.GetBytes($"Note: {euros}")));
		var chars = new char[300];
		Assert.Equal($"Note: {euros}", new string(chars, 0, reader.GetChars(0, chars)));
		Assert.Equal(1E+100, reader.Parse<double>(2));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheOption(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Book(Rows), async);
		var e = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Text = (ExcelText)2 }));
		Assert.Equal("options", e.ParamName);
		Assert.StartsWith("ExcelReaderOptions.Text is not an ExcelText value.", e.Message, StringComparison.Ordinal);
		Assert.Equal(ExcelText.Raw, ExcelReaderOptions.Default.Text);
	}
}
