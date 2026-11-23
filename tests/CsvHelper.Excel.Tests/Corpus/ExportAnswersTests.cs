using System.Globalization;
using CsvHelper.Excel.Corpus;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Excel's own CSV export as the conversion's oracle (Phase 8's step 7): every worksheet of the corpus workbooks with
/// Excel's answers, of the written set and of the display set, converted by <see cref="ExcelConvert.ToCsv(CsvReader, CsvWriter)"/>
/// from a reader under <see cref="ExcelText.Formatted"/>, writes the fields Excel's "CSV UTF-8" writes
/// (<c>Files/excel-csv</c>, and the display set's in <c>excel-display.json</c>). The files are compared as records, so the
/// byte-order mark and quoting, which Phase 6 lists, don't count; and cell by cell, so the used range, from whose top-left
/// cell Excel's export starts, doesn't either. Where Excel can't show a value it writes 255 <c>#</c>, and the conversion
/// <see cref="ExcelText.Raw"/>'s text (decision 5). They agree but in the cells listed, each with its reason.
/// </summary>
public class ExportAnswersTests(ITestOutputHelper output)
{
	/// <summary>Reads as <see cref="ExcelText.Formatted"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Formatted = new() { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } };

	/// <summary>Reads as <see cref="ExcelText.Raw"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Raw = Formatted with { Text = ExcelText.Raw };

	/// <summary>
	/// The display set's cases whose field differs from Excel's export, each with its reason and how many cases it covers on
	/// this runtime: <see cref="DisplayAnswersTests"/>' differences from <c>Range.Text</c>, but for the calendars the export
	/// writes as Gregorian dates, as the conversion does; and decision 18's Buddhist years, which the export writes as
	/// Gregorian ones, and Thai names, which it writes in English.
	/// </summary>
	private static readonly (string Reason, Func<DisplayCell, bool> Applies, int Cells)[] DisplayDifferences =
	[
		("[DBNum1] to [DBNum3] show East Asian numerals, which aren't rendered (Phase 8's decision 8)", c => CodeOf(c).Contains("DBNum"), 16),
		("a locale code's numeral system (Arabic-Indic and Thai digits) and B2's Hijri calendar aren't rendered (decision 8), where the export writes them; it writes [$-1060409]'s Hijri dates as Gregorian ones, as the conversion does",
			c => new[] { "[$-2010401]", "[$-1060409]", "[$-D07041E]", "[$-2000000]", "[$-D000409]" }.Any(CodeOf(c).Contains) || CodeOf(c).StartsWith("B2", StringComparison.Ordinal), 12),
		("Japanese eras, g and e under [$-411] (令和6年), aren't rendered (decision 8): g shows nothing and e the year",
			c => CodeOf(c) is "[$-411]e" or "[$-411]g" || CodeOf(c).StartsWith("[$-411]ggge", StringComparison.Ordinal), 13),
#if NETFRAMEWORK
		("NLS's names are Excel's for every locale probed", _ => false, 0),
#else
		("ICU's names differ from Excel's (decision 9): Russian in lower case and abbreviated with a point (январь, янв., сб), Japanese and Korean abbreviated months with 月 and 월",
			c => CodeOf(c).StartsWith("[$-419]", StringComparison.Ordinal) || CodeOf(c) is "[$-411]mmm" or "[$-412]mmm", 30),
#endif
		("empty text reads as empty (decision 13), where Excel puts it through a text section (\"a\"@\"b\" shows ab)", c => c.Kind == 's' && c.Value.Length == 0, 8),
		("a fixed denominator past 32,768 can't be read (step 3's decision 1): Excel shows fractions that aren't the value (?/50000 shows 0.5 as 1/5)",
			c => System.Text.RegularExpressions.Regex.Matches(CodeOf(c), @"/(\d+)").Cast<System.Text.RegularExpressions.Match>().Any(m => m.Groups[1].Value.Length > 5 || int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) > 32768), 81),
		("four doubles show a unit more or less in Excel than its arithmetic, as step 7 fitted it, gives: 0.0005 and 0.0015 seconds past midnight of day 0 in thousandths, 00:07.25 in tenths, and 0:54:24.6185 in thousandths",
			c => c.Value is "5.787037037037037E-09" or "1.736111111111111E-08" or "8.391203703703703E-05" or "0.03778493634259259", 10),
		("Excel reads [s] m as s [] (its NumberFormat), and shows text no rule found (step 4's decision 6)", c => CodeOf(c) == "[s] m", 3),
		("elapsed time after a date part, and General in a date, can't be read (step 4's decision 1): Excel shows text no rule found (yyyy-mm-dd [h]:mm shows 12 hours for most values)",
			c => !FormatCode.Compile(CodeOf(c)).IsReadable, 50),
		("Buddhist years show as Range.Text shows them (decision 18), 2567 for 2024, where Excel's export writes the Gregorian year",
			c => FormatCode.Compile(CodeOf(c)).Numbers.Any(s => s.Tokens.Any(t => t.Kind == FormatTokenKind.BuddhistYear)), 79),
		("Thai month and day names ([$-41E]) show as Range.Text shows them, where Excel's export writes them in English", c => CodeOf(c).Contains("41E]"), 6),
	];

	/// <summary>The corpus workbooks with Excel's answers and its CSV exports, as theory data.</summary>
	public static IEnumerable<object[]> AnsweredWorkbooks => ExcelAnswersTests.AnsweredWorkbooks;

	/// <summary>The written set's workbooks this runtime has, as theory data.</summary>
	public static IEnumerable<object[]> WrittenWorkbooks => Written.Workbooks.Select(w => new object[] { w.Name });

	[Theory]
	[MemberData(nameof(AnsweredWorkbooks))]
	public async Task ConvertsTheCorpusAsExcelExportsIt(string file)
	{
		var exports = ExcelExport.Load(file) ?? throw new InvalidOperationException($"{file} has no CSV exports: run the corpus tool's exports command.");
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		var differences = new List<string>();
		foreach (var export in exports.Sheets)
		{
			Assert.True(export.Exported, $"{file}: Excel didn't export {export.Name}");
			Assert.True(export.Bom, $"{file}: Excel's CSV UTF-8 of {export.Name} has no byte-order mark");
			await CompareAsync(workbook, export, differences);
		}

		AssertListed(file, differences, ShownCorpusTests.CorpusDifferences);
	}

	[Theory]
	[MemberData(nameof(WrittenWorkbooks))]
	public async Task ConvertsTheWrittenSetAsExcelExportsIt(string name)
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
		foreach (var export in exports.Sheets)
		{
			Assert.True(export.Exported, $"{name}: Excel didn't export {export.Name}");
			await CompareAsync(workbook, export, differences);
		}

		AssertListed(name, differences, ShownCorpusTests.WrittenDifferences);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ConvertsTheDisplaySetAsExcelExportsIt(bool async)
	{
		var failures = new List<string>();
		var listed = new int[DisplayDifferences.Length];
		var cases = 0;
		foreach (var answers in DisplayAnswers.Instance.Workbooks)
		{
			using var workbook = ExcelWorkbook.Open(DisplayAnswers.PathOf(answers.File));
			var sheet = workbook.Sheets[0].Name;
			var converted = await ConvertAsync(workbook, sheet, Formatted, async);
			var raw = await ConvertAsync(workbook, sheet, Raw, async);
			foreach (var cell in answers.Cells)
			{
				cases++;
				var key = (cell.Row, 2);
				var ours = converted.TryGetValue(key, out var field) ? field : "";
				var expected = ExcelShown.Hashes(cell.Export) ? (raw.TryGetValue(key, out var own) ? own : "") : cell.Export;
				if (ours == expected)
				{
					continue;
				}

				var difference = Array.FindIndex(DisplayDifferences, d => d.Applies(cell));
				if (difference >= 0)
				{
					listed[difference]++;
				}
				else
				{
					failures.Add($"{answers.File} {cell}: Excel's export '{CellValue.Show(expected)}', ours '{CellValue.Show(ours)}'");
				}
			}
		}

		Assert.True(failures.Count == 0, $"{failures.Count} of {cases}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < DisplayDifferences.Length; i++)
		{
			Assert.True(DisplayDifferences[i].Cells == listed[i], $"{DisplayDifferences[i].Reason}: {listed[i]} cases, listed {DisplayDifferences[i].Cells}");
		}
	}

	/// <summary>
	/// Compares a worksheet's conversion with Excel's export, cell by cell: every field of the export, from the used range's
	/// top-left cell, and every field of the conversion, which outside the export's range must be empty. The asynchronous
	/// conversion must write the same fields.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="export">Excel's export of the sheet.</param>
	/// <param name="differences">Takes each difference, as <c>Sheet!A1: ...</c>.</param>
	/// <returns>The work.</returns>
	private static async Task CompareAsync(ExcelWorkbook workbook, ExcelExportSheet export, List<string> differences)
	{
		var converted = await ConvertAsync(workbook, export.Name, Formatted, false);
		Assert.Equal(converted, await ConvertAsync(workbook, export.Name, Formatted, true));
		var raw = await ConvertAsync(workbook, export.Name, Raw, false);
		var (top, left) = export.Start;
		var keys = new HashSet<(long Row, int Column)>(converted.Where(p => p.Value.Length > 0).Select(p => p.Key));
		for (var r = 0; r < export.Records.Count; r++)
		{
			for (var c = 0; c < export.Records[r].Length; c++)
			{
				keys.Add((top + r, left + c));
			}
		}

		foreach (var key in keys.OrderBy(k => k.Row).ThenBy(k => k.Column))
		{
			var ours = converted.TryGetValue(key, out var field) ? field : "";
			var exported = export.Text(key.Row, key.Column) ?? "";
			var expected = ExcelShown.Hashes(exported) ? (raw.TryGetValue(key, out var own) ? own : "") : exported;
			if (ours != expected)
			{
				differences.Add($"{new SheetLocation(export.Name).Cell(key.Column, key.Row)}: ours '{CellValue.Show(ours)}', Excel's export '{CellValue.Show(expected)}'");
			}
		}
	}

	/// <summary>
	/// A worksheet converted to CSV by <see cref="ExcelConvert.ToCsv(CsvReader, CsvWriter)"/> from a reader with these options,
	/// each record's fields by the row the reader read it from and their 0-based columns.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="async">Whether to convert asynchronously.</param>
	/// <returns>Each field of the CSV.</returns>
	private static async Task<Dictionary<(long Row, int Column), string>> ConvertAsync(ExcelWorkbook workbook, string sheet, ExcelReaderOptions options, bool async)
	{
		// The reader's rows, in order: the conversion writes a record for each. Its records can be longer than the first, as
		// the conversion's own writer allows (ExcelToCsvOptions).
		var rows = new List<long>();
		using (var reader = await workbook.OpenReaderAsync(async, sheet, options))
		{
			while (reader.Read())
			{
				rows.Add(reader.LineNumber);
			}
		}

		using var csv = new MemoryStream();
		using (var reader = await workbook.OpenReaderAsync(async, sheet, options))
		using (var writer = CsvWriter.Create(csv, new CsvWriterOptions { ValidateFieldCount = false }, leaveOpen: true))
		{
			if (async)
			{
				await ExcelConvert.ToCsvAsync(reader, writer);
			}
			else
			{
				ExcelConvert.ToCsv(reader, writer);
			}
		}

		var fields = new Dictionary<(long Row, int Column), string>();
		using var records = CsvReader.Create(csv.ToArray(), new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false });
		var index = 0;
		while (records.Read())
		{
			Assert.True(index < rows.Count, $"{sheet}: the conversion has more records than the reader's {rows.Count}");
			for (var column = 0; column < records.FieldCount; column++)
			{
				fields[(rows[index], column)] = records.GetString(column);
			}

			index++;
		}

		Assert.Equal(rows.Count, index);

		return fields;
	}

	/// <summary>A case's code as the set wrote it, or a built-in id's as Excel gives it.</summary>
	/// <param name="cell">The case.</param>
	/// <returns>The code.</returns>
	private static string CodeOf(DisplayCell cell) => cell.Code ?? cell.NumberFormat;

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
}
