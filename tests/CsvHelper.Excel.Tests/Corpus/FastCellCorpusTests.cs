using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Security;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The corpus with the sheet scanner's fast path on and off (Phase 9's step 3): every worksheet of every workbook the corpus
/// tests read gives the same cells (their text and kind, read as values and as formulas) and the same structure (hidden rows
/// and columns, merged ranges) through the scanner, and the same records, or the same exception and message, through the
/// public API in several readings, once with the workbook's fast cells (<c>ExcelWorkbook.fastCells</c>) and once with every
/// cell read by the fallback; Excel's answers hold for the fallback too (<see cref="ExcelAnswersTests"/>, whose structure
/// answers hold both ways since the structures are the same). And the cells the fast path leaves to the fallback in each
/// workbook (<see cref="SheetScanner.FallbackCells"/>) are the ones <see cref="RecordedDeclines"/> records, so that a change
/// to the fast path, or a producer's cells it no longer takes, is noticed.
/// </summary>
/// <param name="output">Shows the declines measured, by sheet, and the table they make.</param>
public class FastCellCorpusTests(ITestOutputHelper output)
{
	/// <summary>
	/// The readings compared through the public API: plain, formatted, formulas as text with error cells as null, the
	/// switches that skip and fill, rows below the first two (above which rows are read only for shared formulas), and
	/// columns that leave cells out.
	/// </summary>
	private static readonly (string Name, ExcelReaderOptions Options)[] Readings =
	[
		("raw", Options()),
		("formatted", Options() with { Text = ExcelText.Formatted }),
		("formulas as text and error cells as null", Options() with { Formulas = ExcelFormulas.FormulaText, ErrorCells = ExcelErrorCells.Null }),
		("hidden rows and columns skipped and merged cells filled", Options() with { SkipHiddenRows = true, SkipHiddenColumns = true, FillMergedCells = true }),
		("rows 3 on with formulas as text", Options() with { Range = "3:1048576", Formulas = ExcelFormulas.FormulaText }),
		("columns B to D formatted", Options() with { Range = "B:D", Text = ExcelText.Formatted }),
	];

	/// <summary>
	/// The cells of each corpus workbook that the fast path declines, summed over its worksheets: whole reads into a 64 KB
	/// window, with the defaults (<see cref="CorpusFiles.FallbackCells"/>). Mostly cells with formulas and the cells of
	/// producers that write attributes or white space the fast path doesn't take. The table is measured, not derived: when
	/// it and the corpus disagree, <see cref="DeclinesTheRecordedCells"/> fails with every workbook's count as lines to
	/// paste here, and the test's output lists the counts by sheet; check each change against what the producer writes
	/// before pasting. A workbook added to the corpus needs its line too.
	/// </summary>
	private static readonly Dictionary<string, long> RecordedDeclines = new(StringComparer.Ordinal)
	{
		// Measured at Phase 9's step 3 (whole reads into a 64 KB window). Every count is the workbook's cells of other forms
		// (formulas, t="d", xml:space, POI's and MiniExcel's "r t s" order, pandas's " />") and, in the sheets over 64 KB
		// (orders-1000, packageassets-1695), the cells a window's end cut.
		["closedxml/messy.xlsx"] = 13,
		["closedxml/typed.xlsx"] = 84,
		["epplus/basic.xlsx"] = 8,
		["excel-mac/github-321.xlsx"] = 0,
		["excel-mac/test_conditional_formatting.xlsx"] = 1,
		["excel/basic.xlsx"] = 8,
		["excel/date1904.xlsx"] = 8,
		["excel/macro.xlsm"] = 8,
		["excel/messy.xlsx"] = 13,
		["excel/orders-1000.xlsx"] = 4,
		["excel/packageassets-1695.xlsx"] = 3,
		["excel/packageassets-report-250.xlsx"] = 0,
		["excel/strict.xlsx"] = 15,
		["excel/template.xltx"] = 8,
		["excel/typed-1904.xlsx"] = 91,
		["excel/typed-strict.xlsx"] = 136,
		["excel/typed.xlsx"] = 91,
		["google/64450.xlsx"] = 0,
		["google/66365.xlsx"] = 1,
		["largexlsx/basic.xlsx"] = 9,
		["libreoffice/basic-ooxml.xlsx"] = 13,
		["libreoffice/basic.xlsx"] = 13,
		["libreoffice/messy.xlsx"] = 13,
		["libreoffice/typed.xlsx"] = 100,
		["miniexcel/basic.xlsx"] = 78,
		["openpyxl/basic.xlsx"] = 10,
		["openpyxl/messy.xlsx"] = 13,
		["openpyxl/typed-isodates.xlsx"] = 91,
		["openpyxl/typed.xlsx"] = 85,
		["openxmlsdk/basic.xlsx"] = 8,
		["pandas/openpyxl.xlsx"] = 106,
		["pandas/xlsxwriter.xlsx"] = 0,
		["poi/messy.xlsx"] = 119,
		["poi/sxssf.xlsx"] = 9,
		["poi/typed.xlsx"] = 345,
		["poi/xssf.xlsx"] = 86,
		["spreadcheetah/basic.xlsx"] = 8,
		["xlsxwriter/basic.xlsx"] = 8,
		["xlsxwriter/constant_memory.xlsx"] = 10,
		["xlsxwriter/messy.xlsx"] = 13,
		["xlsxwriter/typed.xlsx"] = 91,
		["closedxml/basic.xlsx"] = 8,
	};

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void ReadsTheSameCellsWithTheFastPathAndWithout(string file)
	{
		foreach (var formulas in new[] { ExcelFormulas.CachedValue, ExcelFormulas.FormulaText })
		{
			var fast = CorpusFiles.Read(file, out _, formulas, fastCells: true);
			var fallback = CorpusFiles.Read(file, out _, formulas, fastCells: false);
			var differences = CellDifferences(fast, fallback);
			Assert.True(differences.Count == 0, $"{file}, formulas {formulas}: fast cells and the fallback differ:\n{string.Join("\n", differences.Take(20))}");
		}

		var fastStructures = CorpusFiles.Structures(file, fastCells: true);
		var fallbackStructures = CorpusFiles.Structures(file, fastCells: false);
		Assert.Equal(fastStructures.Keys.OrderBy(k => k, StringComparer.Ordinal), fallbackStructures.Keys.OrderBy(k => k, StringComparer.Ordinal));
		foreach (var pair in fastStructures)
		{
			var fast = pair.Value;
			var fallback = fallbackStructures[pair.Key];
			Assert.True(fast.LastRow == fallback.LastRow, $"{file}, {pair.Key}: the last row is {fast.LastRow} with fast cells, {fallback.LastRow} without.");
			Assert.True(fast.HiddenRows.SetEquals(fallback.HiddenRows), $"{file}, {pair.Key}: the hidden rows differ.");
			Assert.True(fast.HiddenColumns.SequenceEqual(fallback.HiddenColumns), $"{file}, {pair.Key}: the hidden columns differ.");
			Assert.True(fast.Merged.SequenceEqual(fallback.Merged), $"{file}, {pair.Key}: the merged ranges differ.");
		}
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void ReadsTheSameRecordsWithTheFastPathAndWithout(string file)
	{
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
		{
			foreach (var (name, options) in Readings)
			{
				workbook.fastCells = true;
				var fast = Read(workbook, sheet, options);
				workbook.fastCells = false;
				var fallback = Read(workbook, sheet, options);

				var difference = FirstDifference(fast, fallback);
				Assert.True(difference is null, $"{file}, {sheet.Name}, {name}: {difference}");
			}
		}
	}

	[Theory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsEveryCellAsExcelDoesWithoutTheFastPath(string file) => ExcelAnswersTests.AssertReadsAsExcelDoes(file, fastCells: false);

	[Fact]
	public void DeclinesTheRecordedCells()
	{
		var measured = new SortedDictionary<string, long>(StringComparer.Ordinal);
		output.WriteLine("The cells the fast path declines, by sheet (whole reads into a 64 KB window, the defaults); sheets without any left out:");
		foreach (var file in CorpusFiles.Workbooks)
		{
			var counts = CorpusFiles.FallbackCells(file);
			measured[file] = counts.Sum(c => c.Cells);
			foreach (var (sheet, cells) in counts.Where(c => c.Cells > 0))
			{
				output.WriteLine($"  {file}, {sheet}: {cells}");
			}
		}

		var differences = new List<string>();
		foreach (var pair in measured)
		{
			if (!RecordedDeclines.TryGetValue(pair.Key, out var recorded))
			{
				differences.Add($"{pair.Key}: {pair.Value} declined, and none recorded");
			}
			else if (recorded != pair.Value)
			{
				differences.Add($"{pair.Key}: {pair.Value} declined, and {recorded} recorded");
			}
		}

		foreach (var file in RecordedDeclines.Keys.Where(k => !measured.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
		{
			differences.Add($"{file}: {RecordedDeclines[file]} recorded, and not in the corpus");
		}

		var table = string.Join("\n", measured.Select(p => $"\t\t[\"{p.Key}\"] = {p.Value},"));
		output.WriteLine("The table measured:");
		output.WriteLine(table);

		Assert.True(
			differences.Count == 0,
			$"The declines measured aren't the ones recorded:\n{string.Join("\n", differences)}\n\nThe table measured, for RecordedDeclines once each change is understood:\n{table}");
	}

	/// <summary>The options of the readings: no header, and empty rows kept, so that every row is a record.</summary>
	/// <returns>The options.</returns>
	private static ExcelReaderOptions Options() => new() { Csv = new() { HasHeader = false, SkipEmptyLines = false } };

	/// <summary>The cells that two readings of a workbook differ in, by text or kind, exactly; one line per cell, ordered.</summary>
	/// <param name="fast">The cells read with fast cells.</param>
	/// <param name="fallback">The cells read by the fallback.</param>
	/// <returns>The differences, such as <c>Data!B3: fast Number:42, fallback Text:42</c>.</returns>
	private static List<string> CellDifferences(SheetCells fast, SheetCells fallback)
	{
		var differences = new List<string>();
		foreach (var sheet in fast.Keys.Union(fallback.Keys).OrderBy(k => k, StringComparer.Ordinal))
		{
			var withFast = fast.TryGetValue(sheet, out var a) ? a : [];
			var withFallback = fallback.TryGetValue(sheet, out var b) ? b : [];
			foreach (var key in withFast.Keys.Union(withFallback.Keys).OrderBy(k => k.Row).ThenBy(k => k.Column))
			{
				var hasFast = withFast.TryGetValue(key, out var x);
				var hasFallback = withFallback.TryGetValue(key, out var y);
				if (hasFast && hasFallback && x == y)
				{
					continue;
				}

				var location = new SheetLocation(sheet).Cell(key.Column, key.Row);
				differences.Add($"{location}: fast {(hasFast ? x.ToString() : "valueless")}, fallback {(hasFallback ? y.ToString() : "valueless")}");
			}
		}

		return differences;
	}

	/// <summary>Reads a sheet's records, each its line number and its fields, and then what it throws, if it throws.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The records, and the exception's type and message last.</returns>
	private static List<string> Read(ExcelWorkbook workbook, ExcelSheet sheet, ExcelReaderOptions options)
	{
		var records = new List<string>();
		try
		{
			using var reader = workbook.OpenReader(sheet, options);
			while (reader.Read())
			{
				records.Add($"{reader.LineNumber}: {Workbooks.Fields(reader)}");
			}
		}
		catch (Exception e) when (FuzzTests.IsExpected(e))
		{
			records.Add($"{e.GetType().Name}: {e.Message}");
		}

		return records;
	}

	/// <summary>The first record two reads differ in, described; <see langword="null"/> when they are the same.</summary>
	/// <param name="fast">The records read with fast cells.</param>
	/// <param name="fallback">The records read by the fallback.</param>
	/// <returns>The difference, or <see langword="null"/>.</returns>
	private static string? FirstDifference(List<string> fast, List<string> fallback)
	{
		for (var i = 0; i < Math.Max(fast.Count, fallback.Count); i++)
		{
			var withFast = i < fast.Count ? fast[i] : "(no record)";
			var withFallback = i < fallback.Count ? fallback[i] : "(no record)";
			if (withFast != withFallback)
			{
				return $"record {i + 1} of {fast.Count} and {fallback.Count}: fast cells read \"{withFast}\", the fallback \"{withFallback}\"";
			}
		}

		return null;
	}
}
