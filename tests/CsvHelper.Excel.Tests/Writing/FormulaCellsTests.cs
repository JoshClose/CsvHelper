using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record with formulas: read back on .NET as an <c>ISpanParsable</c>, and on .NET Framework through the converter.</summary>
[CsvRecord]
public partial class FormulaRecord
{
	/// <summary>A text cell.</summary>
	public string Item { get; set; } = "";

	/// <summary>A number cell.</summary>
	public double Price { get; set; }

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A formula cell, or no cell by default.</summary>
#if !NET
	[CsvConverter(typeof(ExcelFormulaConverter))]
#endif
	public ExcelFormula Total { get; set; }

	/// <summary>A formula cell, or no cell when null.</summary>
#if !NET
	[CsvConverter(typeof(ExcelFormulaConverter))]
#endif
	public ExcelFormula? Size { get; set; }
}

/// <summary>
/// Formula cells end to end, synchronously and asynchronously: through the record writer, <see cref="ExcelWriterExtensions.WriteFormula"/>
/// and <see cref="CsvWriter.WriteField(object)"/>, read back with <see cref="ExcelFormulas.FormulaText"/> and
/// <see cref="ExcelFormulas.CachedValue"/>, by ClosedXML and ExcelDataReader, with their XML.
/// </summary>
public class FormulaCellsTests
{
	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>Reads formulas as their text.</summary>
	private static readonly ExcelReaderOptions FormulaText = new() { Formulas = ExcelFormulas.FormulaText };

	/// <summary>Three records: formulas of each member, a null one and a default one.</summary>
	private static readonly FormulaRecord[] Records =
	[
		new() { Item = "apple", Price = 1.5, Quantity = 4, Total = new ExcelFormula("B2*C2"), Size = new ExcelFormula("IF(D2>5,\"big\",\"small\")") },
		new() { Item = "pear", Price = 0.25, Quantity = 10, Total = new ExcelFormula("=B3*C3") },
		new() { Item = "total", Total = new ExcelFormula("SUM(D2:D3)"), Size = default(ExcelFormula) },
	];

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesFormulaCellsWithoutCachedValues(bool async)
	{
		var workbook = await Write(Records, async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"D2\"><f>B2*C2</f></c><c r=\"E2\"><f>IF(D2&gt;5,\"big\",\"small\")</f></c></row>", sheet);
		Assert.Contains("<c r=\"D3\"><f>B3*C3</f></c></row>", sheet);
		Assert.Contains("<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>total</t></is></c><c r=\"B4\"><v>0</v></c><c r=\"C4\"><v>0</v></c><c r=\"D4\"><f>SUM(D2:D3)</f></c></row>", sheet);
		Assert.DoesNotContain("<cols>", sheet);
		Assert.DoesNotContain("ignoredErrors", sheet);
		Assert.Empty(Written.ValidationErrors(workbook));

		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using (var reader = book.OpenReader(FormulaText))
		{
			var read = reader.GetRecords<FormulaRecord>().ToList();
			Assert.Equal(Records.Select(r => (r.Item, r.Total)), read.Select(r => (r.Item, r.Total)));

			// A null formula and a default one are both empty cells, which a nullable member reads as null.
			Assert.Equal([Records[0].Size, null, null], read.Select(r => r.Size));
		}

		// Without computing them, the cells are empty.
		using (var reader = book.OpenReader())
		{
			var read = reader.GetRecords<FormulaRecord>().ToList();
			Assert.All(read, r => Assert.Equal(default, r.Total));
			Assert.All(read, r => Assert.Null(r.Size));
		}

		Assert.Equal(
			["Item|Price|Quantity|Total|Size", "apple|1.5|4|=B2*C2|=IF(D2>5,\"big\",\"small\")", "pear|0.25|10|=B3*C3|", "total|0|0|=SUM(D2:D3)|"],
			ReadRows(workbook, FormulaText));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesFormulasByHand(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Sums");
			writer.WriteField(2);
			writer.WriteField(3);
			writer.WriteFormula("A1+B1");
			writer.WriteField((object)new ExcelFormula("=A1*B1"));
			writer.WriteField((object?)null);
			writer.WriteField(default(ExcelFormula));
			writer.WriteField(new ExcelFormula("1+1"));
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, Written.Phase4());

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<row r=\"1\"><c r=\"A1\"><v>2</v></c><c r=\"B1\"><v>3</v></c><c r=\"C1\"><f>A1+B1</f></c><c r=\"D1\"><f>A1*B1</f></c><c r=\"G1\"><f>1+1</f></c></row>", sheet);

		// A formula that starts like a number is no text, which Excel would flag.
		Assert.DoesNotContain("ignoredErrors", sheet);
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(["2|3|=A1+B1|=A1*B1|||=1+1"], ReadRows(workbook, FormulaText));
		Assert.Equal(["2|3"], ReadRows(workbook, Written.AllRows));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task EscapesFormulasAsText(bool async)
	{
		// Excel decodes _xHHHH_ in a formula as in text, and keeps a string's CR and controls written as escapes.
		var control = (char)1;
		var formulas = new[]
		{
			"\"a_x0041_b\"&\"<&>\"",
			"\"a\r\nb\"&\r\n\"c\"",
			"\"a" + control + "b\"&\"" + (char)0xFFFE + "\"",
			"LEN(\"" + char.ConvertFromUtf32(0x1F600) + "\")",
		};

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Escapes");
			foreach (var formula in formulas)
			{
				writer.WriteFormula(formula);
			}

			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, Written.Phase4());

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"A1\"><f>\"a_x005F_x0041_b\"&amp;\"&lt;&amp;&gt;\"</f></c>", sheet);
		Assert.Contains("<c r=\"B1\"><f>\"a_x000D_\nb\"&amp;\n\"c\"</f></c>", sheet);
		Assert.Contains("<c r=\"C1\"><f>\"a_x0001_b\"&amp;\"_xFFFE_\"</f></c>", sheet);
		Assert.Empty(Written.ValidationErrors(workbook));

		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(new ExcelReaderOptions { Formulas = ExcelFormulas.FormulaText, Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.True(reader.Read());
		Assert.Equal(formulas.Select(f => new ExcelFormula(f).ToString()), Enumerable.Range(0, 4).Select(reader.GetString));
		Assert.Equal(new ExcelFormula("\"a\r\nb\"&\n\"c\""), ExcelFormula.Parse(reader.GetString(1)));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsFormulasOutOfSharedStrings(bool async)
	{
		var workbook = await Write(Records, async, new ExcelWriterOptions { Strings = ExcelStrings.Shared });

		Assert.Contains("<c r=\"D2\"><f>B2*C2</f></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.DoesNotContain("B2*C2", Written.Entry(workbook, "xl/sharedStrings.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal("pear|0.25|10|=B3*C3|", ReadRows(workbook, FormulaText)[2]);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task IsReadByTheOracles(bool async)
	{
		var workbook = await Write(Records, async);

		// ClosedXML reads each formula, without its =, and computes it.
		using (var book = new XLWorkbook(new MemoryStream(workbook)))
		{
			var sheet = book.Worksheets.First();
			Assert.Equal(["D2=B2*C2", "E2=IF(D2>5,\"big\",\"small\")", "D3=B3*C3", "D4=SUM(D2:D3)"], sheet.CellsUsed(c => c.HasFormula).Select(c => $"{c.Address}={c.FormulaA1}"));
			Assert.Equal(6, sheet.Cell("D2").Value.GetNumber());
			Assert.Equal("big", sheet.Cell("E2").Value.GetText());
			Assert.Equal(8.5, sheet.Cell("D4").Value.GetNumber());
		}

		// ExcelDataReader reads the cached values, which there are none of.
		Assert.Equal(
			[
				"String:Item|String:Price|String:Quantity|String:Total|String:Size",
				"String:apple|Double:1.5|Double:4||",
				"String:pear|Double:0.25|Double:10||",
				"String:total|Double:0|Double:0||",
			],
			Written.ExcelDataReaderValues(workbook));
	}

	[Fact]
	public void MapsTheFormulaTypeToFormulaCells()
	{
		Assert.Equal(CellKind.Formula, CellKinds.Of(typeof(ExcelFormula)));
		Assert.Equal(CellKind.Formula, CellKinds.Of(typeof(ExcelFormula?)));
	}

	/// <summary>Writes records into a sheet named Orders.</summary>
	/// <typeparam name="T">The records' type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<T>(IEnumerable<T> records, bool async, ExcelWriterOptions? options = null) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Orders", records);
			}
			else
			{
				book.WriteSheet("Orders", records);
			}
		}, async, Written.Phase4(options));

	/// <summary>Reads the first sheet's rows, each as its fields joined with <c>|</c>, the header too.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="options">The reader's options; its CSV options are made to read every row.</param>
	/// <returns>The rows.</returns>
	private static List<string> ReadRows(byte[] workbook, ExcelReaderOptions options)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(options with { Csv = Written.AllRows.Csv });
		var rows = new List<string>();
		while (reader.Read())
		{
			rows.Add(Api.Workbooks.Fields(reader));
		}

		return rows;
	}
}
