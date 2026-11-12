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

	private const string FirstRecordWidths = "in Phase 4's look, without auto-fit, a column's width comes from its first data record (decision 14), and a later value that needs more shows ########";

	private const string NewSheetWidths = "with NewSheet at four rows, the first sheet fills after two records, before auto-fit's 100 held rows end, and every sheet takes its widths (step 3): a later record that needs more shows ########";

	private const string TitleCounts = "Excel's AutoFit counts a title over one column, which isn't merged; auto-fit counts no title";

	private const string TextCap = "auto-fit caps text at MaxAutoFitWidth (60 characters, or 20 here); Excel's AutoFit doesn't";

	private const string HeldRows = "auto-fit measures only the held rows (AutoFit = 2); Excel's AutoFit measures every row";

	private const string FormulaWidth = "auto-fit gives a column of formulas the default width at least, since their values are unknown; Excel's AutoFit measures the values";

	private const string Midnight = "a DateTime column keeps the width of a date and time when every held value is at midnight (step 3), where Excel's AutoFit fits the dates";

	private const string NewSheetFit = "with NewSheet, every sheet takes the first sheet's widths (step 3), where Excel's AutoFit fits each sheet's own cells";

	private const string JapaneseMonths = "Excel shows ja-JP's abbreviated month ([$-411]mmm) as its number alone (1), where .NET writes 1月";

	private const string GermanMarch = "ICU's de-DE abbreviates March as Mär, and Excel's [$-407]mmm as Mrz";

	private const string LibreOffice1900 = "LibreOffice's 1900 date system has no 1900-02-29, so serials before 61 show a day earlier there (1 is 1899-12-31)";

	private const string LibreOfficeHashes = "LibreOffice's CSV export shows a value its column is too narrow for, which Excel fills with #";

	private const string LibreOfficeLines = "LibreOffice reads a CR in text that has an LF as a line break, LF (CR LF as one LF)";

	private const string LibreOfficeFormulaEscapes = "LibreOffice doesn't decode _xHHHH_ in formulas, as Excel does (and as it does in text)";

	private const string LibreOfficeMissingSheet = "LibreOffice shows a reference to a missing sheet as #NAME?, Excel as #REF!";

	private const string LibreOfficePercent = "LibreOffice shows a percent's result in a percent format (2.00%), Excel in General";

	private const string LibreOfficeAP = "LibreOffice shows A/P in lower case";

	private const string LibreOfficeGermanAmPm = "LibreOffice shows AM/PM under [$-407] as de-DE's vorm. and nachm.";

	private const string LibreOfficeBooleans = "LibreOffice shows a boolean in a column's number format as its number, 1 or 0; Excel as TRUE or FALSE, in the format's text section when it has one";

	private const string LibreOfficeTargets = "LibreOffice writes a file's or a relative path's target relative to the folder it saves in (step 5)";

	/// <summary>The cells where Excel reads otherwise than the oracle says, and why. A cell of <c>Sheet!</c> alone stands for every cell of the sheet.</summary>
	private static readonly (string File, string Cell, string Reason)[] Differences =
	[
		("types.xlsx", "Numbers!F15", Subnormal),
		("types.xlsx", "Widths!B3", FirstRecordWidths),
		("formats.xlsx", "Early!A3", FirstRecordWidths + ": the first is text, before 1900, so the column gets no width"),
		.. new[] { "Custom!I2", "Custom!I3", "Custom!I4" }.Select(c => ("cultures.xlsx", c, JapaneseMonths)),
		("cultures.xlsx", "Custom!A3", GermanMarch),
		("newsheet-look.xlsx", "'Orders (2)'!C3", NewSheetWidths),
		("newsheet-look.xlsx", "'Orders (3)'!A3", NewSheetWidths),
	];

	/// <summary>The columns auto-fit sizes otherwise than Excel's own AutoFit would, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] AutoFitDifferences =
	[
		("look.xlsx", "'One column'!A", TitleCounts),
		("autofit.xlsx", "Text!B", TextCap),
		("autofit.xlsx", "Capped!A", TextCap + "; " + HeldRows),
		("autofit.xlsx", "Formulas!C", FormulaWidth),
		("autofit.xlsx", "Midnight!A", Midnight),
		("columns.xlsx", "Options!K", TextCap),
		.. new[] { "B", "C", "D", "F", "G", "H" }.Select(c => ("newsheet-look.xlsx", "'Orders (2)'!" + c, NewSheetFit)),
		.. new[] { "A", "B", "C", "F", "G" }.Select(c => ("newsheet-look.xlsx", "'Orders (3)'!" + c, NewSheetFit)),
	];

	/// <summary>Where Excel shows a look otherwise than the oracle says, and why. None: every sheet looks as its settings say.</summary>
	private static readonly (string File, string Cell, string Reason)[] LookDifferences = [];

	/// <summary>Where ClosedXML reads the look otherwise than Excel shows it, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ClosedXmlDifferences =
	[
		("tables.xlsx", "Renamed!table Renamed", "ClosedXML doesn't decode _x000A_ in a table's column name, which a line feed in an attribute is written as (step 4); Excel does"),
	];

	/// <summary>Where LibreOffice keeps the look otherwise than Excel shows it, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeLookDifferences =
	[
		("links-all.xlsx", "Sites!link B3", LibreOfficeTargets),
		("links-all.xlsx", "Sites!link B4", LibreOfficeTargets),
		("links-all.xlsx", "Sites!link B5", "LibreOffice writes a jump within the workbook as a link to its own file, at the cell"),
	];

	/// <summary>The cells where LibreOffice shows otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeDifferences =
	[
		("types.xlsx", "Dates!B5", LibreOffice1900),
		("types.xlsx", "Dates!B6", LibreOffice1900),
		("types.xlsx", "Widths!B3", LibreOfficeHashes),
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
		("newsheet-look.xlsx", "'Orders (2)'!C3", LibreOfficeHashes),
		.. new[] { "Attributes", "Options" }.SelectMany(s => new[] { (s + "!I2", LibreOfficeBooleans), (s + "!I3", LibreOfficeBooleans), (s + "!I4", LibreOfficeBooleans), (s + "!H3", LibreOffice1900) })
			.Select(d => ("columns.xlsx", d.Item1, d.Item2)),
		("columns.xlsx", "Mixed!B4", LibreOfficeBooleans),
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
				else if (answer is not null && WrittenOracle.ExcelCode(codes.TryGetValue(key, out var code) ? code : "General") is var written && written != answer.NumberFormat)
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
			// A hidden column's width is 0.
			var (widths, hidden) = (parts[sheet.Name].Widths, parts[sheet.Name].Hidden);
			var expected = sheet.Widths.Select((_, c) => hidden.Contains(c) ? 0 : WrittenOracle.ExcelWidth(widths.TryGetValue(c, out var w) ? w : null, answers.Dpi));
			Assert.True(expected.SequenceEqual(sheet.Widths), $"{sheet.Name}: written [{string.Join(", ", expected)}], Excel's [{string.Join(", ", sheet.Widths)}]");
		}
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public void AutoFitsAsWideAsExcel(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		// Each column auto-fit sized is as wide as Excel's own AutoFit makes it, so that its cells show whole, and at most
		// a tenth and half a character wider: auto-fit's widths are about 5% wider at 144 DPI (step 3).
		var parts = WrittenOracle.Parts(Written.Write(workbook));
		var differences = new List<string>();
		var (fitted, narrower) = (0, 0);
		foreach (var sheet in answers.Sheets)
		{
			// A hidden column shows no width, and Excel's AutoFit would show it.
			foreach (var column in parts[sheet.Name].Fitted.Where(c => c < sheet.Widths.Count && !parts[sheet.Name].Hidden.Contains(c)).OrderBy(c => c))
			{
				var (ours, excel) = (sheet.Widths[column], sheet.AutoFit[column]);
				fitted++;
				narrower += excel < ours ? 1 : 0;
				if (excel is null || ours < excel || ours > excel * 1.1 + 0.5)
				{
					differences.Add($"{SheetLocation.Quote(sheet.Name)}!{CellReference.ColumnName(column)}: auto-fit {ours}, Excel's AutoFit {excel}");
				}
			}
		}

		// Excel's AutoFit ran: it narrows most columns a little, so that answers that copied the widths written can't pass.
		Assert.True(fitted == 0 || narrower * 2 > fitted, $"{name}: Excel's AutoFit is narrower than auto-fit in {narrower} of {fitted} columns.");
		AssertListed(name, differences, AutoFitDifferences);
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public void ExcelShowsTheLook(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		var expected = WrittenOracle.Looks(workbook, answers.Sheets.ToDictionary(s => s.Name, s => s.Used));
		Assert.Equal(expected.Select(s => s.Sheet), answers.Sheets.Select(s => s.Name));
		var differences = new List<string>();
		foreach (var ((sheet, look), excel) in expected.Zip(answers.Sheets, (e, a) => (e, a)))
		{
			CompareLooks(sheet, look.Entries(), excel.Look.Entries(), "Excel", differences);
		}

		// The filters' hidden names, as Excel writes them; Excel's own (_xlfn.) aside.
		var names = WrittenOracle.FilterNames(expected).ToDictionary(n => n.Name, n => $"{n.RefersTo}{(n.Visible ? "" : " hidden")}");
		var excelNames = answers.Names.Where(n => !n.Name.StartsWith("_xlfn.", StringComparison.Ordinal)).ToDictionary(n => n.Name, n => $"{n.RefersTo}{(n.Visible ? "" : " hidden")}");
		CompareLooks("names", names, excelNames, "Excel", differences);
		AssertListed(name, differences, LookDifferences);
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public void ClosedXmlReadsTheLook(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		var looks = ClosedXmlLook.Read(Written.Write(workbook), [.. answers.Sheets.Select(s => s.Used)]);
		Assert.Equal(answers.Sheets.Count, looks.Count);
		var differences = new List<string>();
		foreach (var (sheet, look) in answers.Sheets.Zip(looks, (s, l) => (s, l)))
		{
			// ClosedXML sizes no rows.
			var excel = sheet.Look.Entries().Where(e => !e.Key.StartsWith("row ", StringComparison.Ordinal)).ToDictionary(e => e.Key, e => e.Value);
			CompareLooks(sheet.Name, excel, look.Entries(), "ClosedXML", differences);
		}

		AssertListed(name, differences, ClosedXmlDifferences);
	}

	[LibreOfficeTheory]
	[MemberData(nameof(Workbooks))]
	public void LibreOfficeKeepsTheLook(string name)
	{
		var (workbook, answers) = Comparable(name);
		if (answers is null)
		{
			return;
		}

		// LibreOffice saves the workbook as .xlsx, which ClosedXML reads; what it does its own way is left out of both sides.
		var looks = ClosedXmlLook.Read(File.ReadAllBytes(LibreOffice.WrittenLook(workbook)), [.. answers.Sheets.Select(s => s.Used)]);
		Assert.Equal(answers.Sheets.Count, looks.Count);
		var differences = new List<string>();
		foreach (var (sheet, look) in answers.Sheets.Zip(looks, (s, l) => (s, l)))
		{
			var excel = sheet.Look.Entries().Where(e => !e.Key.StartsWith("row ", StringComparison.Ordinal)).ToDictionary(e => e.Key, e => e.Value);
			CompareLooks(sheet.Name, LibreOfficeTerms(sheet, excel), LibreOfficeTerms(sheet, look.Entries()), "LibreOffice", differences);
		}

		AssertListed(name, differences, LibreOfficeLookDifferences);
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
			ExpectedKind.Text => answer.Type == "text" && answer.Value2 == cell.Text && (cell.Shows is null || answer.Text == cell.Shows),
			ExpectedKind.Boolean => answer.Type == "boolean" && answer.Value2 == (cell.Boolean ? "true" : "false") && answer.Text == cell.Shows,
			ExpectedKind.Number => (cell.NumberType is null ? answer.Type is "number" or "date" or "currency" : answer.Type == cell.NumberType)
				&& Number(answer) == cell.Number
				&& (cell.Shows is null ? !IsHashes(answer.Text) : answer.Text == cell.Shows),
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

	/// <summary>
	/// A look's entries in terms both Excel's and LibreOffice's can be compared in, leaving out what LibreOffice does its own
	/// way when it saves a workbook: it gives each cell of a merged range its first cell's look, so only a range's first cell
	/// is compared; it names a font of its own for East Asian text, so fonts aren't compared; it wraps text with a line
	/// break, so a cell with one isn't compared for wrap; and it spells format codes its own way (<c>\$</c>, <c>[RED]</c>,
	/// <c>yyyy\-mm\-dd</c>), so codes are compared without escapes, quotes or case.
	/// </summary>
	/// <param name="sheet">Excel's answers about the sheet: its merged ranges and cells.</param>
	/// <param name="entries">The look's entries.</param>
	/// <returns>The entries in those terms.</returns>
	private static Dictionary<string, string> LibreOfficeTerms(WrittenAnswerSheet sheet, IReadOnlyDictionary<string, string> entries)
	{
		var merged = sheet.Look.Merged
			.Select(r => r.Split(':'))
			.SelectMany(c => Cells(c[0], c[1]).Skip(1))
			.ToHashSet(StringComparer.Ordinal);
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var (key, value) in entries.Select(e => (e.Key, e.Value)))
		{
			if (merged.Contains(key))
			{
				continue;
			}

			if (!IsCell(key))
			{
				result[key] = value;

				continue;
			}

			var lines = CellOf(sheet, key) is { } cell && cell.Value2.IndexOfAny(['\n', '\r']) >= 0;
			var kept = value.Split([", "], StringSplitOptions.None)
				.Where(p => !p.StartsWith("font ", StringComparison.Ordinal) && !(p == "wrap" && lines))
				.Select(p => p.StartsWith("format ", StringComparison.Ordinal) ? string.Concat(p.Where(c => c is not ('\\' or '"'))).ToUpperInvariant() : p)
				.ToList();
			if (kept.Count > 0)
			{
				result[key] = string.Join(", ", kept);
			}
		}

		return result;
	}

	/// <summary>The cells of a range, row by row.</summary>
	/// <param name="first">Its first cell, such as <c>A1</c>.</param>
	/// <param name="last">Its last cell.</param>
	/// <returns>Each cell's reference.</returns>
	private static IEnumerable<string> Cells(string first, string last)
	{
		var (top, left) = Position(first);
		var (bottom, right) = Position(last);
		for (var row = top; row <= bottom; row++)
		{
			for (var column = left; column <= right; column++)
			{
				yield return CellReference.ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
			}
		}
	}

	/// <summary>A reference's row and 0-based column.</summary>
	/// <param name="reference">The reference, such as <c>B12</c>.</param>
	/// <returns>The row and column.</returns>
	private static (long Row, int Column) Position(string reference)
	{
		var letters = new string([.. reference.TakeWhile(char.IsLetter)]);

		return (long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture), letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1)) - 1);
	}

	/// <summary>Excel's answer about the cell a look's entry is about, or <see langword="null"/> when it is about no cell with a value.</summary>
	/// <param name="sheet">Excel's answers about the sheet.</param>
	/// <param name="key">The entry's key: a cell's reference, or what else it is about.</param>
	/// <returns>The cell.</returns>
	private static ExcelCell? CellOf(WrittenAnswerSheet sheet, string key) => IsCell(key) && sheet.Cells.TryGetValue(Position(key), out var cell) ? cell : null;

	/// <summary>Whether a look's entry is about a cell: its key is a reference, such as <c>B12</c>.</summary>
	/// <param name="key">The entry's key.</param>
	/// <returns>Whether it is a cell's.</returns>
	private static bool IsCell(string key) =>
		key.Length > 1 && key[0] is >= 'A' and <= 'Z' && key.SkipWhile(c => c is >= 'A' and <= 'Z').All(char.IsDigit) && char.IsDigit(key[key.Length - 1]);

	/// <summary>Whether Excel shows a cell as <c>#</c> signs alone, as it shows a number its column is too narrow for.</summary>
	/// <param name="text">The text Excel shows.</param>
	/// <returns>Whether it is <c>#</c> signs.</returns>
	private static bool IsHashes(string text) => text.Length > 0 && text.All(c => c == '#');

	/// <summary>Compares a sheet's look, entry by entry, each difference starting with the sheet and what it is about.</summary>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="expected">The look expected.</param>
	/// <param name="actual">The look read.</param>
	/// <param name="reader">Who read it, for the message.</param>
	/// <param name="differences">The differences found.</param>
	private static void CompareLooks(string sheet, IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual, string reader, List<string> differences)
	{
		foreach (var key in expected.Keys.Union(actual.Keys).OrderBy(k => k, StringComparer.Ordinal))
		{
			var (e, a) = (expected.TryGetValue(key, out var x) ? x : null, actual.TryGetValue(key, out var y) ? y : null);
			if (e != a)
			{
				differences.Add($"{SheetLocation.Quote(sheet)}!{key}: {CellValue.Show(e ?? "nothing")} expected, {reader} {CellValue.Show(a ?? "nothing")}");
			}
		}
	}

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
