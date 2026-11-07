using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CsvHelper.Excel.Corpus;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The written set (<c>tools/CsvHelper.Excel.Corpus/Written.cs</c>) against Excel 365's answers about it
/// (<see cref="WrittenAnswers"/>, from the corpus tool's <c>written</c> command): each workbook written again is the one
/// Excel read, by its parts' SHA-256; and Excel reads in it what was written: every cell's value and type, the text it
/// shows of every formatted cell (the translation's oracle, against .NET's text), of the writer's own date formats and of
/// formulas, its number formats, and its column widths. LibreOffice shows the same text, but where listed.
/// </summary>
/// <remarks>
/// <c>cultures.xlsx</c> reads culture data other than the invariant culture's, which differs between ICU versions; on a
/// runtime whose data isn't the data its answers were made with (<see cref="Written.CultureData"/>), it isn't compared,
/// and the tests say so in their output. It and <c>types-net.xlsx</c> use .NET's newer types, so .NET Framework has neither.
/// </remarks>
/// <param name="output">The tests' output.</param>
public class WrittenAnswersTests(ITestOutputHelper output)
{
	private const string Subnormal = "Excel reads a subnormal double as 0; LibreOffice and CsvHelper.Excel keep it";

	private const string FirstRecordWidths = "a column's width comes from its first data record (decision 14), and a later value that needs more shows ######## until Phase 5's auto-fit";

	private const string JapaneseMonths = "Excel shows ja-JP's abbreviated month ([$-411]mmm) as its number alone (1), where .NET writes 1月";

	private const string GermanMarch = "ICU's de-DE abbreviates March as Mär, and Excel's [$-407]mmm as Mrz";

	private const string LibreOffice1900 = "LibreOffice's 1900 date system has no 1900-02-29, so serials before 61 show a day earlier there (1 is 1899-12-31)";

	private const string LibreOfficeHashes = "LibreOffice's CSV export shows a value its column is too narrow for, which Excel fills with #";

	private const string LibreOfficeFmt = "LibreOffice shows #FMT for TimeSpan.MaxValue in [h]:mm:ss.000";

	private const string LibreOfficeLines = "LibreOffice reads a CR in text that has an LF as a line break, LF (CR LF as one LF)";

	private const string LibreOfficeFormulaEscapes = "LibreOffice doesn't decode _xHHHH_ in formulas, as Excel does (and as it does in text)";

	private const string LibreOfficeMissingSheet = "LibreOffice shows a reference to a missing sheet as #NAME?, Excel as #REF!";

	private const string LibreOfficePercent = "LibreOffice shows a percent's result in a percent format (2.00%), Excel in General";

	private const string LibreOfficeAP = "LibreOffice shows A/P in lower case";

	private const string LibreOfficeGermanAmPm = "LibreOffice shows AM/PM under [$-407] as de-DE's vorm. and nachm.";

	/// <summary>The cells where Excel reads otherwise than the oracle says, and why. A cell of <c>Sheet!</c> alone stands for every cell of the sheet.</summary>
	private static readonly (string File, string Cell, string Reason)[] Differences =
	[
		("types.xlsx", "Numbers!F15", Subnormal),
		("types.xlsx", "Widths!B3", FirstRecordWidths),
		("types.xlsx", "Widths!C3", FirstRecordWidths),
		("formats.xlsx", "Early!A3", FirstRecordWidths + ": the first is text, before 1900, so the column gets no width"),
		.. new[] { "Custom!I2", "Custom!I3", "Custom!I4" }.Select(c => ("cultures.xlsx", c, JapaneseMonths)),
		("cultures.xlsx", "Custom!A3", GermanMarch),
	];

	/// <summary>The cells where LibreOffice shows otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeDifferences =
	[
		("types.xlsx", "Dates!B5", LibreOffice1900),
		("types.xlsx", "Dates!B6", LibreOffice1900),
		("types.xlsx", "Widths!B3", LibreOfficeHashes),
		("types.xlsx", "Widths!C3", LibreOfficeFmt),
		("formats.xlsx", "Early!A3", LibreOfficeHashes),
		("types-net.xlsx", "Newer!B3", LibreOffice1900),
		("types-net.xlsx", "Newer!B5", LibreOffice1900),
		.. new[] { "text.xlsx", "text-shared.xlsx" }.SelectMany(f => new[] { (f, "Text!B5", LibreOfficeLines), (f, "Text!B6", LibreOfficeLines) }),
		("formulas.xlsx", "Calc!D5", LibreOfficeFormulaEscapes),
		("formulas.xlsx", "Calc!D6", LibreOfficeFormulaEscapes),
		("formulas.xlsx", "Calc!E6", LibreOfficeLines),
		("formulas.xlsx", "Calc!D7", LibreOfficeFormulaEscapes),
		("formulas.xlsx", "Calc!D15", LibreOfficeMissingSheet),
		("formulas.xlsx", "Calc!D16", LibreOfficePercent),
		.. new[] { "Custom!H2", "Custom!H3", "Custom!H4" }.Select(c => ("formats.xlsx", c, LibreOfficeAP)),
		.. new[] { "Custom!D2", "Custom!D3", "Custom!D4" }.Select(c => ("cultures.xlsx", c, LibreOfficeGermanAmPm)),
		.. new[] { "Custom!I2", "Custom!I3", "Custom!I4" }.Select(c => ("cultures.xlsx", c, JapaneseMonths + "; LibreOffice shows .NET's")),
		("cultures.xlsx", "Custom!A3", GermanMarch + "; LibreOffice shows ICU's"),
	];

	/// <summary>The workbooks of the set on this target.</summary>
	public static TheoryData<string> Workbooks
	{
		get
		{
			var data = new TheoryData<string>();
			foreach (var workbook in Written.Workbooks)
			{
				data.Add(workbook.Name);
			}

			return data;
		}
	}

	[Fact]
	public void AnswersForEveryWorkbook()
	{
#if NET
		string[] other = [];
#else
		string[] other = ["types-net.xlsx", "cultures.xlsx"];
#endif
		Assert.Equal(Written.Workbooks.Select(w => w.Name).Concat(other).OrderBy(n => n, StringComparer.Ordinal), WrittenAnswers.Files);

		// Excel opened every workbook as it is, and answered at a DPI whose widths the tests know.
		Assert.All(WrittenAnswers.Files, f =>
		{
			var answers = WrittenAnswers.Load(f);
			Assert.False(answers.Refused, f);
			Assert.Empty(answers.Repaired);
			Assert.Contains(answers.Dpi, new[] { 96, 144 });
		});

		// Only cultures.xlsx reads culture data.
		Assert.Equal(["cultures.xlsx"], WrittenAnswers.Files.Where(f => WrittenAnswers.Load(f).CultureData is not null));
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public async Task WritesTheWorkbookExcelAnswered(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		Assert.True(
			answers.Sha256 == Written.PartsHash(Written.Write(workbook)),
			$"{name} isn't the workbook Excel answered about: the writer or the content set changed. Run the corpus tool's written command "
			+ "(dotnet run --project tools/CsvHelper.Excel.Corpus -c Release -- written) and review what changed in Files/excel-written.");
		Assert.Equal(answers.Sha256, Written.PartsHash(await Written.WriteAsync(workbook)));
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public void ExcelReadsWhatWasWritten(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		var expected = WrittenOracle.Cells(workbook);
		var parts = WrittenOracle.Parts(Written.Write(workbook));
		Assert.Equal(expected.Select(s => s.Sheet), answers.Sheets.Select(s => s.Name));
		var differences = new List<string>();
		foreach (var ((sheet, cells), excel) in expected.Zip(answers.Sheets, (e, a) => (e, a)))
		{
			var codes = parts[sheet].Codes;
			foreach (var key in cells.Keys.Union(excel.Cells.Keys).OrderBy(k => k.Row).ThenBy(k => k.Column))
			{
				var location = new SheetLocation(sheet).Cell(key.Column, key.Row);
				var cell = cells.TryGetValue(key, out var e) ? e : null;
				var answer = excel.Cells.TryGetValue(key, out var x) ? x : null;
				if (Difference(cell, answer) is { } difference)
				{
					differences.Add($"{location}: {difference} ({cell?.Source ?? "no cell written"})");
				}
				else if (answer is not null && (codes.TryGetValue(key, out var code) ? code : "General") is var written && written != answer.NumberFormat)
				{
					differences.Add($"{location}: format {written} written, Excel's {answer.NumberFormat}");
				}
			}
		}

		AssertListed(name, differences);
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public void ExcelShowsTheWrittenWidths(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		var parts = WrittenOracle.Parts(Written.Write(workbook));
		foreach (var sheet in answers.Sheets)
		{
			var widths = parts[sheet.Name].Widths;
			var expected = sheet.Widths.Select((_, c) => WrittenOracle.ExcelWidth(widths.TryGetValue(c, out var w) ? w : null, answers.Dpi));
			Assert.True(expected.SequenceEqual(sheet.Widths), $"{sheet.Name}: written [{string.Join(", ", expected)}], Excel's [{string.Join(", ", sheet.Widths)}]");
		}
	}

	[Fact]
	public void CountsTheFormattedCells()
	{
		// The translation's oracle compares this many cells in members' formats with .NET's text; of them, Excel reads these
		// many as numbers, in the translated format. The rest are text: formats that aren't translated (formats.xlsx has 11
		// such columns, cultures.xlsx 4), and in formats.xlsx two weekdays that aren't the 1st's, so their text
		// (ddd MMMM yyyy) reads back as no date, and a date before 1900.
		var expected = new Dictionary<string, string> { ["types-net.xlsx"] = "21 of 21", ["formats.xlsx"] = "125 of 161", ["cultures.xlsx"] = "198 of 210" };
		var comparable = Written.Workbooks.Where(w => w.Cultures.Count == 0 || WrittenAnswers.Load(w.Name).CultureData == Written.CultureData(w.Cultures)).ToList();
		var counts = comparable.Select(w =>
		{
			var answers = WrittenAnswers.Load(w.Name);
			var formatted = WrittenOracle.Cells(w)
				.Zip(answers.Sheets, (e, a) => e.Cells.Where(c => c.Value.Kind == ExpectedKind.Formatted).Select(c => a.Cells.TryGetValue(c.Key, out var x) ? x : null))
				.SelectMany(c => c)
				.ToList();

			return $"{w.Name} {formatted.Count(c => c?.Type is "date" or "number")} of {formatted.Count}";
		});
		Assert.Equal(comparable.Select(w => $"{w.Name} {(expected.TryGetValue(w.Name, out var count) ? count : "0 of 0")}"), counts);
	}

	[LibreOfficeTheory]
	[MemberData(nameof(Workbooks))]
	public void LibreOfficeShowsWhatExcelShows(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		var differences = new List<string>();
		if (!LibreOffice.CanExport(workbook))
		{
			// LibreOffice can't name a CSV file after one of its sheets, so it saves it as .xlsx, which CsvHelper.Excel reads.
			using var saved = ExcelWorkbook.Open(LibreOffice.WrittenAsXlsx(workbook));
			Assert.Equal(answers.Sheets.Select(s => s.Name), saved.Sheets.Select(s => s.Name));
			foreach (var sheet in answers.Sheets)
			{
				using var reader = saved.OpenReader(saved.Sheets.Single(s => s.Name == sheet.Name), new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
				var rows = new List<string[]>();
				while (reader.Read())
				{
					rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)]);
				}

				Compare(sheet, rows, differences);
			}
		}
		else
		{
			foreach (var sheet in answers.Sheets)
			{
				if (LibreOffice.WrittenSheet(workbook, sheet.Name) is { } rows)
				{
					Compare(sheet, rows, differences);
				}
				else
				{
					differences.Add($"{SheetLocation.Quote(sheet.Name)}!: LibreOffice exported no such sheet");
				}
			}
		}

		AssertListed(name, differences, LibreOfficeDifferences);
	}

	[Fact]
	public void PinsTheParts()
	{
		// The hash covers each entry's name and bytes, inflated, and their lengths: the same parts deflated otherwise hash
		// the same, and a part that differs by a byte doesn't.
		var workbook = Written.Workbooks.Single(w => w.Name == "types.xlsx");
		var fastest = Written.Write(workbook);
		var stored = Written.Write(new WrittenWorkbook(workbook.Name, workbook.Description, workbook.Options with { CompressionLevel = CompressionLevel.NoCompression }, workbook.Sheets));
		Assert.NotEqual(fastest, stored);
		Assert.Equal(Written.PartsHash(fastest), Written.PartsHash(stored));

		using (var stream = new MemoryStream())
		{
			using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
			{
				foreach (var (entry, text) in new[] { ("a", "xy"), ("bc", "") })
				{
					using var writer = new StreamWriter(archive.CreateEntry(entry).Open(), new UTF8Encoding(false));
					writer.Write(text);
				}
			}

			byte[] layout = [1, 0, 0, 0, 0, 0, 0, 0, (byte)'a', 2, 0, 0, 0, 0, 0, 0, 0, (byte)'x', (byte)'y', 2, 0, 0, 0, 0, 0, 0, 0, (byte)'b', (byte)'c', 0, 0, 0, 0, 0, 0, 0, 0];
			using var sha = SHA256.Create();
			Assert.Equal(string.Concat(sha.ComputeHash(layout).Select(b => b.ToString("x2", CultureInfo.InvariantCulture))), Written.PartsHash(stream.ToArray()));
		}

		var changed = Written.Write(new WrittenWorkbook(workbook.Name, workbook.Description, workbook.Options, [.. workbook.Sheets.Take(1)]));
		Assert.NotEqual(Written.PartsHash(fastest), Written.PartsHash(changed));
	}

	[Fact]
	public void FingerprintsTheCultureData()
	{
		Assert.Equal(Written.CultureData(["de-DE", "fr-FR"]), Written.CultureData(["fr-FR", "de-DE", "fr-FR"]));
		Assert.NotEqual(Written.CultureData(["de-DE"]), Written.CultureData(["fr-FR"]));
		Assert.NotEqual(Written.CultureData(["de-DE"]), Written.CultureData(["de-DE", "fr-FR"]));
	}

	/// <summary>A workbook of the set and its answers, or <see langword="null"/> answers when this runtime's culture data isn't theirs.</summary>
	/// <param name="name">The workbook's name.</param>
	/// <returns>The workbook and its answers.</returns>
	private (WrittenWorkbook Workbook, WrittenAnswers? Answers) Comparable(string name)
	{
		var workbook = Written.Workbooks.Single(w => w.Name == name);
		var answers = WrittenAnswers.Load(name);
		Assert.Equal(answers.Cultures, workbook.Cultures);
		if (answers.CultureData is { } data && data != Written.CultureData(workbook.Cultures))
		{
			// The operating system the answers were made on has their data, unless the fingerprint itself changed, which must
			// not leave the workbook uncompared everywhere.
			Assert.False(
				System.Runtime.InteropServices.RuntimeInformation.OSDescription == answers.Os,
				$"{name}: the culture data's fingerprint isn't the answers' on the system they were made on ({answers.Os}); if Written.CultureData changed, run the corpus tool's written command.");
			output.WriteLine($"{name} isn't compared: this runtime's data of {string.Join(", ", workbook.Cultures)} isn't the data Excel's answers were made with.");

			return (workbook, null);
		}

		return (workbook, answers);
	}

	/// <summary>
	/// How Excel's answer about a cell differs from what was written, or <see langword="null"/> when it doesn't: values and
	/// types exactly, and the text Excel shows where the oracle knows it.
	/// </summary>
	/// <param name="cell">What was written, or <see langword="null"/> for no cell.</param>
	/// <param name="answer">Excel's answer, or <see langword="null"/> for no cell.</param>
	/// <returns>The difference.</returns>
	private static string? Difference(ExpectedCell? cell, ExcelCell? answer)
	{
		if (cell is null || answer is null)
		{
			return cell is null && answer is null ? null : $"written {Describe(cell)}, Excel {Describe(answer)}";
		}

		var same = cell.Kind switch
		{
			ExpectedKind.Text => answer.Type == "text" && answer.Value2 == cell.Text,
			ExpectedKind.Boolean => answer.Type == "boolean" && answer.Value2 == (cell.Boolean ? "true" : "false") && answer.Text == cell.Shows,
			ExpectedKind.Number => answer.Type == cell.NumberType && Number(answer) == cell.Number && (cell.Shows is null || answer.Text == cell.Shows),
			ExpectedKind.Formatted => answer.Text == cell.Text
				&& (cell.Typed ? answer.Type == cell.NumberType && Number(answer) == cell.Number : answer.Type == "text" && answer.Value2 == cell.Text),
			_ => ExcelAnswersTests.Shown(cell.Formula) == answer.Formula && (cell.Shows is null || answer.Text == cell.Shows),
		};

		return same ? null : $"written {Describe(cell)}, Excel {Describe(answer)}";
	}

	/// <summary>An expected cell, for a message.</summary>
	/// <param name="cell">The cell, or <see langword="null"/>.</param>
	/// <returns>Its description.</returns>
	private static string Describe(ExpectedCell? cell) => cell is null ? "no cell" : cell.Kind switch
	{
		ExpectedKind.Text => $"text {CellValue.Show(cell.Text!)}",
		ExpectedKind.Boolean => $"boolean {cell.Boolean}",
		ExpectedKind.Number => $"{cell.NumberType} {cell.Number.ToString("R", CultureInfo.InvariantCulture)} shown {CellValue.Show(cell.Shows ?? "?")}",
		ExpectedKind.Formatted => cell.Typed
			? $"formatted {CellValue.Show(cell.Text!)}, typed as {cell.NumberType} {cell.Number.ToString("R", CultureInfo.InvariantCulture)}"
			: $"formatted {CellValue.Show(cell.Text!)}, as text",
		_ => $"formula {CellValue.Show(cell.Formula!)} shown {CellValue.Show(cell.Shows ?? "?")}",
	};

	/// <summary>Excel's answer about a cell, for a message.</summary>
	/// <param name="answer">The answer, or <see langword="null"/>.</param>
	/// <returns>Its description.</returns>
	private static string Describe(ExcelCell? answer) =>
		answer is null ? "no cell" : $"{answer.Type} {CellValue.Show(answer.Value2)} shown {CellValue.Show(answer.Text)}{(answer.Formula is null ? "" : " formula " + CellValue.Show(answer.Formula))}";

	/// <summary>A number's <c>Value2</c>.</summary>
	/// <param name="answer">Excel's answer.</param>
	/// <returns>The number, or <see cref="double.NaN"/> when it isn't one.</returns>
	private static double Number(ExcelCell answer) =>
		double.TryParse(answer.Value2, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

	/// <summary>
	/// Compares the text LibreOffice shows of a sheet's cells with Excel's: text cells by their whole text (Excel's
	/// <c>Text</c> stops at 8,221 characters), numbers in General by value, since the two show their own digits, and the
	/// rest by the text shown.
	/// </summary>
	/// <param name="sheet">Excel's answers about the sheet.</param>
	/// <param name="rows">LibreOffice's rows, from row 1, each field the cell's text.</param>
	/// <param name="differences">The differences found, each starting with its cell.</param>
	private static void Compare(WrittenAnswerSheet sheet, List<string[]> rows, List<string> differences)
	{
		var keys = sheet.Cells.Keys
			.Concat(rows.SelectMany((r, i) => r.Select((f, c) => (Row: (long)i + 1, Column: c, Field: f)).Where(f => f.Field.Length > 0).Select(f => (f.Row, f.Column))))
			.Distinct()
			.OrderBy(k => k.Row)
			.ThenBy(k => k.Column);
		foreach (var key in keys)
		{
			var shown = key.Row <= rows.Count && key.Column < rows[(int)key.Row - 1].Length ? rows[(int)key.Row - 1][key.Column] : "";
			var answer = sheet.Cells.TryGetValue(key, out var x) ? x : null;
			var same = answer switch
			{
				null => shown.Length == 0,
				{ Type: "text" } => shown == answer.Value2,
				{ Type: "empty" } => shown.Length == 0,
				{ Type: "number" or "date" or "currency", NumberFormat: "General" } =>
					double.TryParse(shown, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && Math.Abs(value - Number(answer)) <= 1e-14 * Math.Max(1, Math.Abs(Number(answer))),
				_ => shown == answer.Text,
			};
			if (!same)
			{
				differences.Add($"{new SheetLocation(sheet.Name).Cell(key.Column, key.Row)}: LibreOffice shows {CellValue.Show(shown)}, Excel {Describe(answer)}");
			}
		}
	}

	/// <summary>Fails unless the differences are those listed for the workbook, each found.</summary>
	/// <param name="name">The workbook.</param>
	/// <param name="differences">The differences, each starting with its cell.</param>
	/// <param name="list">The differences listed, or <see langword="null"/> for <see cref="Differences"/>.</param>
	private static void AssertListed(string name, List<string> differences, (string File, string Cell, string Reason)[]? list = null)
	{
		static bool Matches(string difference, string cell) => difference.StartsWith(cell.EndsWith("!", StringComparison.Ordinal) ? cell : cell + ":", StringComparison.Ordinal);

		var listed = (list ?? Differences).Where(d => d.File == name).ToList();
		var unexplained = differences.Where(d => !listed.Any(l => Matches(d, l.Cell))).ToList();
		var unseen = listed.Where(l => !differences.Any(d => Matches(d, l.Cell))).Select(l => l.Cell).ToList();
		Assert.True(unexplained.Count == 0 && unseen.Count == 0, $"{name}: not listed:\n{string.Join("\n", unexplained)}\nlisted but not found: [{string.Join(", ", unseen)}]");
	}
}
