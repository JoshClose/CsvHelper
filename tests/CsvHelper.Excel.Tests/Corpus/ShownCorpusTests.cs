using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CsvHelper.Excel.Corpus;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Excel's answers already committed, read through <see cref="ExcelText.Formatted"/> (Phase 8's step 7): the text Excel
/// showed of each code of <c>excel-formats.json</c>, of each corpus cell (<c>excel-oracle</c>) and of each cell of the
/// written set (<c>excel-written</c>). Each is <c>Range.Text</c>, or Excel's own CSV export (<c>excel-csv</c>) where the
/// column's width changed it (decision 17); and where Excel can't show a value, <see cref="ExcelText.Raw"/>'s text (decision
/// 5). They agree but in the cells listed, each with its reason.
/// </summary>
public class ShownCorpusTests(ITestOutputHelper output)
{
	/// <summary>Reads as <see cref="ExcelText.Formatted"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Formatted = new() { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } };

	/// <summary>Reads as <see cref="ExcelText.Raw"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Raw = Formatted with { Text = ExcelText.Raw };

	/// <summary>A code whose elapsed time follows a date part, which the renderer can't read (step 4's decision 1).</summary>
	private const string ElapsedAfterDate = "elapsed time after a date part can't be read (step 4's decision 1), so the cell shows its raw text; Excel shows yyyy-mm-dd [h]:mm of a noon as 2024-01-15 12:00";

	/// <summary>The written set's formulas, which have no cached values.</summary>
	private const string NoCachedValue = "the writer's formulas have no cached values, which the reader reads (empty), where Excel calculates them";

	/// <summary>A text past what Excel shows and exports.</summary>
	private const string LongText = "Excel's Range.Text stops at 8,221 characters, and its CSV export writes nothing of a text of 32,767";

	/// <summary>The corpus cells whose text differs from Excel's, and why. A cell of <c>Sheet!</c> alone stands for every cell of the sheet.</summary>
	internal static readonly (string File, string Cell, string Reason)[] CorpusDifferences =
	[
		.. new[]
		{
			"closedxml/typed.xlsx", "excel/typed.xlsx", "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx",
			"poi/typed.xlsx", "xlsxwriter/typed.xlsx",
		}.Select(f => (f, "Dates!B68", ElapsedAfterDate)),
		.. new[] { "excel/typed.xlsx", "excel/typed-1904.xlsx", "excel/typed-strict.xlsx" }.Select(f => (f, "Numbers!B45", ExcelAnswersTests.NewerErrors)),
		.. new[] { "closedxml/typed.xlsx", "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx", "xlsxwriter/typed.xlsx" }.Select(f => (f, "Numbers!B21", ExcelAnswersTests.Overflow)),
		("closedxml/typed.xlsx", "Numbers!B38", ExcelAnswersTests.ClosedXmlNa),
		.. new[] { "Dates!B91", "Dates!B92", "Dates!B93", "Dates!B102", "Dates!B106" }.Select(c => ("excel/typed-strict.xlsx", c, ExcelAnswersTests.DateCompatibility)),
		("google/64450.xlsx", "'Hoja 2'!", ExcelAnswersTests.PivotTable),
		("google/66365.xlsx", "Sheet1!B2", ExcelAnswersTests.ArrayValues),
		.. new[] { "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx" }.SelectMany(f => new[] { (f, "Formulas!G15", ExcelAnswersTests.EmptyArrayCells), (f, "Formulas!G16", ExcelAnswersTests.EmptyArrayCells) }),
	];

	/// <summary>The written set's cells whose text differs from Excel's, and why.</summary>
	internal static readonly (string File, string Cell, string Reason)[] WrittenDifferences =
	[
		("formulas.xlsx", "Calc!", NoCachedValue),
		("formulas.xlsx", "Records!C2", NoCachedValue),
		("formulas.xlsx", "Records!C3", NoCachedValue),
		("autofit.xlsx", "Formulas!C2", NoCachedValue),
		("autofit.xlsx", "Formulas!C3", NoCachedValue),
		("newsheet.xlsx", "abcdefghijklmnopqrstuvwxyz12345!B2", NoCachedValue),
		("newsheet.xlsx", "abcdefghijklmnopqrstuvwxyz12345!B3", NoCachedValue),
		("newsheet.xlsx", "'abcdefghijklmnopqrstuvwxyz1 (2)'!B2", NoCachedValue),
		("text.xlsx", "Text!B32", LongText),
		("text-shared.xlsx", "Text!B32", LongText),
		.. new[] { "Custom!I2", "Custom!I3", "Custom!I4" }.Select(c => ("cultures.xlsx", c, WrittenAnswersTests.JapaneseMonths)),
		("cultures.xlsx", "Custom!A3", WrittenAnswersTests.GermanMarch),
	];

	/// <summary>
	/// The codes of <c>excel-formats.json</c> whose text differs from Excel's, and why: what Phase 8's decision 8 leaves out,
	/// and codes the renderer can't read (step 4's decision 1), whose cells show their raw text.
	/// </summary>
	private static readonly (string Reason, Func<string, bool> Applies, int Cells)[] CodeDifferences =
	[
		("Japanese eras aren't rendered (decision 8): ggge shows the Gregorian year", code => code.Contains("ggge"), 2),
		("[DBNum1] shows East Asian numerals, which aren't rendered (decision 8)", code => code.Contains("DBNum"), 2),
		("elapsed time after a date part, and General in a date, can't be read (step 4's decision 1)", code => !FormatCode.Compile(code).IsReadable, 4),
	];

	/// <summary>The corpus workbooks with Excel's answers and its CSV exports, as theory data.</summary>
	public static IEnumerable<object[]> AnsweredWorkbooks => ExcelAnswersTests.AnsweredWorkbooks;

	/// <summary>The written set's workbooks this runtime has, as theory data.</summary>
	public static IEnumerable<object[]> WrittenWorkbooks => Written.Workbooks.Select(w => new object[] { w.Name });

	[Fact]
	public void ShowsExcelsTextOfEveryCode()
	{
		// excel-formats.json: each code Excel takes, as Excel keeps it, over 2024-01-15 12:00 and a day and a half, in a
		// column 80 wide. Fills repeat to that width, which Excel's export would drop, and nothing records it: left out.
		var root = Load("excel-formats.json");
		var codes = root.Element("codes")!.Elements("item")
			.Where(c => c.Element("valid")!.Value == "true" && !c.Element("excelCode")!.Value.Contains('*'))
			.Select(c => (Code: c.Element("excelCode")!.Value, Date: c.Element("text")!.Value, Duration: c.Element("durationText")!.Value))
			.ToList();
		var rows = new StringBuilder();
		for (var i = 0; i < codes.Count; i++)
		{
			rows.Append($"<row r=\"{i + 1}\"><c r=\"A{i + 1}\" s=\"{i + 1}\"><v>45306.5</v></c><c r=\"B{i + 1}\" s=\"{i + 1}\"><v>1.5</v></c></row>");
		}

		var package = Package(rows.ToString(), codes.Select((c, i) => (164 + i, (string?)c.Code, true)));
		var failures = new List<string>();
		var listed = new int[CodeDifferences.Length];
		var shown = Texts(package, Formatted);
		var raw = Texts(package, Raw);
		for (var i = 0; i < codes.Count; i++)
		{
			foreach (var (column, expected) in new[] { (0, codes[i].Date), (1, codes[i].Duration) })
			{
				var key = ((long)i + 1, column);
				var want = ExcelShown.Hashes(expected) ? raw[key] : expected;
				if (shown[key] == want)
				{
					continue;
				}

				var difference = Array.FindIndex(CodeDifferences, d => d.Applies(codes[i].Code));
				if (difference >= 0)
				{
					listed[difference]++;
				}
				else
				{
					failures.Add($"{codes[i].Code} {(column == 0 ? "45306.5" : "1.5")}: Excel '{expected}', ours '{shown[key]}'");
				}
			}
		}

		Assert.True(failures.Count == 0, $"{failures.Count} of {codes.Count * 2}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < CodeDifferences.Length; i++)
		{
			Assert.True(CodeDifferences[i].Cells == listed[i], $"{CodeDifferences[i].Reason}: {listed[i]} cells, listed {CodeDifferences[i].Cells}");
		}

		Assert.True(codes.Count > 160, $"{codes.Count} codes");
	}

	[Fact]
	public void ShowsExcelsTextOfEveryBuiltInId()
	{
		// excel-formats.json's cells: 45306.5 in each built-in id from 0 to 163, a custom date, applyNumberFormat="0", and
		// numFmts entries that redefine built-in ids, each a workbook of its own here, since a redefinition is the workbook's.
		// The accounting ids (41 to 44) fill their column, 80 wide, which Excel's export would drop: left out.
		var failures = new List<string>();
		var count = 0;
		foreach (var cell in Load("excel-formats.json").Element("cells")!.Elements("item").Where(c => !c.Element("excelCode")!.Value.Contains('*')))
		{
			var id = int.Parse(cell.Element("id")!.Value, CultureInfo.InvariantCulture);
			var code = cell.Element("code") is { } c && c.Attribute("type")?.Value != "null" ? c.Value : null;
			var applied = cell.Element("applied")!.Value == "true";
			var expected = cell.Element("text")!.Value;
			var package = Package("<row r=\"1\"><c r=\"A1\" s=\"1\"><v>45306.5</v></c></row>", [(id, code, applied)]);
			var shown = Texts(package, Formatted)[(1, 0)];
			var want = ExcelShown.Hashes(expected) ? Texts(package, Raw)[(1, 0)] : expected;
			count++;
			if (shown != want)
			{
				failures.Add($"{cell.Element("name")!.Value}: Excel '{expected}', ours '{shown}'");
			}
		}

		Assert.True(failures.Count == 0, $"{failures.Count} of {count}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		Assert.True(count > 160, $"{count} cells");
	}

	[Fact]
	public void ShowsAnEmptyCodeAsGeneralWhateverItsId()
	{
		// Excel 365 shows General for a numFmt whose code is empty, of a built-in id (0, 2, and 14, a date's) or a custom one
		// (step 7's probe, Csv\_bench\results\phase8\step7\emptycode). ClosedXML writes id 0 so.
		foreach (var id in new[] { 0, 2, 14, 200 })
		{
			var package = Package(
				"<row r=\"1\"><c r=\"A1\" s=\"1\"><v>45306.5</v></c><c r=\"B1\" s=\"1\"><v>-1234.5</v></c><c r=\"C1\" s=\"1\"><v>0.25</v></c><c r=\"D1\" s=\"1\"><v>1234.5678</v></c></row>",
				[(id, "", true)]);
			var shown = Texts(package, Formatted);

			Assert.Equal(["45306.5", "-1234.5", "0.25", "1234.5678"], Enumerable.Range(0, 4).Select(c => shown[(1, c)]));
		}
	}

	[Theory]
	[MemberData(nameof(AnsweredWorkbooks))]
	public void ShowsTheCorpusAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var exports = ExcelExport.Load(file) ?? throw new InvalidOperationException($"{file} has no CSV exports: run the corpus tool's exports command.");
		Assert.False(exports.Refused);
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		var differences = new List<string>();
		foreach (var pair in answers.Sheets)
		{
			var export = exports.Sheets.Single(s => s.Name == pair.Key);
			Compare(workbook, pair.Key, pair.Value, export, differences);
		}

		AssertListed(file, differences, CorpusDifferences);
	}

	[Theory]
	[MemberData(nameof(WrittenWorkbooks))]
	public void ShowsTheWrittenSetAsExcelDoes(string name)
	{
		var (written, answers) = WrittenAnswersTests.Comparable(name, output);
		if (answers is null)
		{
			return;
		}

		var bytes = Writing.Written.Checked(Written.Write(written));
		var exports = ExcelExport.Load("excel-written/" + name) ?? throw new InvalidOperationException($"{name} has no CSV exports: run the corpus tool's exports command.");
		Assert.True(exports.Sha256 == Written.PartsHash(bytes), $"{name}: the workbook isn't the one Excel exported; run the corpus tool's exports command.");
		using var workbook = ExcelWorkbook.Open(new MemoryStream(bytes, false));
		var differences = new List<string>();
		foreach (var sheet in answers.Sheets)
		{
			var export = exports.Sheets.Single(s => s.Name == sheet.Name);
			Compare(workbook, sheet.Name, sheet.Cells, export, differences);
		}

		AssertListed(name, differences, WrittenDifferences);
	}

	/// <summary>Compares every cell of a sheet that Excel answered about with its text as CsvHelper.Excel shows it.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="cells">Excel's answers about its cells.</param>
	/// <param name="export">Excel's CSV export of it.</param>
	/// <param name="differences">Takes each difference, as <c>Sheet!A1: ...</c>.</param>
	private static void Compare(ExcelWorkbook workbook, string sheet, Dictionary<(long Row, int Column), ExcelCell> cells, ExcelExportSheet export, List<string> differences)
	{
		var shown = Texts(workbook, sheet, Formatted);
		var raw = Texts(workbook, sheet, Raw);
		foreach (var key in cells.Keys.OrderBy(k => k.Row).ThenBy(k => k.Column))
		{
			var cell = cells[key];
			if (cell.Type == "empty")
			{
				continue;
			}

			var ours = shown.TryGetValue(key, out var text) ? text : "";
			var exported = export.Text(key.Row, key.Column) ?? cell.Text;
			var expected = ExcelShown.Expected(cell.Text, exported, cell.NumberFormat);
			var want = expected ?? (raw.TryGetValue(key, out var own) ? own : "");
			if (ours != want)
			{
				differences.Add($"{new SheetLocation(sheet).Cell(key.Column, key.Row)}: ours '{CellValue.Show(ours)}', Excel '{CellValue.Show(want)}'{(expected is null ? " (can't show)" : "")} in {cell.NumberFormat}");
			}
		}
	}

	/// <summary>Checks that the differences are the listed ones, and every listed one is found.</summary>
	/// <param name="file">The workbook.</param>
	/// <param name="differences">The differences found.</param>
	/// <param name="list">The listed differences.</param>
	private static void AssertListed(string file, List<string> differences, (string File, string Cell, string Reason)[] list)
	{
		var listed = list.Where(d => d.File == file).ToList();
		static bool Matches(string difference, string cell) => difference.StartsWith(cell.EndsWith("!", StringComparison.Ordinal) ? cell : cell + ":", StringComparison.Ordinal);
		var unexplained = differences.Where(d => !listed.Any(l => Matches(d, l.Cell))).ToList();
		var unseen = listed.Where(l => !differences.Any(d => Matches(d, l.Cell))).Select(l => l.Cell).ToList();
		Assert.True(unexplained.Count == 0 && unseen.Count == 0, $"{file}: not listed:\n{string.Join("\n", unexplained)}\nlisted but not found: [{string.Join(", ", unseen)}]");
	}

	/// <summary>A sheet's text as a reader with these options gives it, by row and 0-based column.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>Each field's text.</returns>
	private static Dictionary<(long Row, int Column), string> Texts(ExcelWorkbook workbook, string sheet, ExcelReaderOptions options)
	{
		using var reader = workbook.OpenReader(sheet, options);
		var texts = new Dictionary<(long Row, int Column), string>();
		while (reader.Read())
		{
			for (var column = 0; column < reader.FieldCount; column++)
			{
				texts[(reader.LineNumber, column)] = reader.GetString(column);
			}
		}

		return texts;
	}

	/// <summary>A package's first sheet's text as a reader with these options gives it.</summary>
	/// <param name="package">The package.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>Each field's text.</returns>
	private static Dictionary<(long Row, int Column), string> Texts(TestPackage package, ExcelReaderOptions options)
	{
		using var workbook = ExcelWorkbook.Open(package.ToStream());

		return Texts(workbook, workbook.Sheets[0].Name, options);
	}

	/// <summary>
	/// A workbook of one sheet whose cell formats are xf 0, General, then one for each format given: its id, its code when
	/// <c>numFmts</c> defines it, and its <c>applyNumberFormat</c>.
	/// </summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <param name="formats">The formats, xf 1 on.</param>
	/// <returns>The package.</returns>
	private static TestPackage Package(string rows, IEnumerable<(int Id, string? Code, bool Applied)> formats)
	{
		var custom = new StringBuilder();
		var xfs = new StringBuilder("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
		var count = 1;
		var defined = 0;
		foreach (var (id, code, applied) in formats)
		{
			if (code is not null)
			{
				custom.Append($"<numFmt numFmtId=\"{id}\" formatCode=\"{Sheets.Escape(code).Replace("\"", "&quot;")}\"/>");
				defined++;
			}

			xfs.Append($"<xf numFmtId=\"{id}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"{(applied ? " applyNumberFormat=\"1\"" : " applyNumberFormat=\"0\"")}/>");
			count++;
		}

		var styles = TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\">" +
			$"<numFmts count=\"{defined}\">{custom}</numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
			"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
			$"<cellXfs count=\"{count}\">{xfs}</cellXfs></styleSheet>";

		return TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet(rows))).Set("xl/styles.xml", styles);
	}

	/// <summary>A JSON file of the corpus, read as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <param name="file">The file, relative to the corpus folder.</param>
	/// <returns>Its root.</returns>
	private static XElement Load(string file)
	{
		using var reader = JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(CorpusFiles.PathOf(file)), XmlDictionaryReaderQuotas.Max);

		return XElement.Load(reader);
	}
}
