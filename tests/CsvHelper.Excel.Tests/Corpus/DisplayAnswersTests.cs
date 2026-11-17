using System.Globalization;
using CsvHelper.Excel.Tests.Api;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The display set (<c>Files/display</c>, Phase 8's probes of number formats, from the corpus tool's <c>display</c>
/// command) read through <see cref="ExcelText.Formatted"/>, against the text Excel 365 shows in each case
/// (<see cref="DisplayAnswers"/>): <c>Range.Text</c> in a wide column, or Excel's CSV export where a fill or the width
/// changes it (Phase 8's decision 17); and where Excel can't show a value, <see cref="ExcelText.Raw"/>'s text (decision 5).
/// They agree but in the cases listed, each with its reason.
/// </summary>
public class DisplayAnswersTests
{
	/// <summary>The cases whose text differs from Excel's, each with its reason and how many cases it covers on this runtime.</summary>
	private static readonly (string Reason, Func<DisplayCell, bool> Applies, int Cells)[] Differences =
	[
		("[DBNum1] to [DBNum3] show East Asian numerals, which aren't rendered (Phase 8's decision 8)", c => CodeOf(c).Contains("DBNum"), 16),
		("a locale code's or B2's calendar or numeral system (Hijri, Arabic-Indic and Thai digits, the Buddhist calendar) isn't rendered (decision 8)",
			c => new[] { "[$-2010401]", "[$-1060409]", "[$-D07041E]", "[$-2000000]", "[$-D000409]" }.Any(CodeOf(c).Contains) || CodeOf(c).StartsWith("B2", StringComparison.Ordinal), 20),
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
	];

	/// <summary>Reads as <see cref="ExcelText.Formatted"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Formatted = new() { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } };

	/// <summary>Reads as <see cref="ExcelText.Raw"/>, every row a record.</summary>
	private static readonly ExcelReaderOptions Raw = Formatted with { Text = ExcelText.Raw };

	[Fact]
	public void AnswersEveryWorkbookOfTheSet()
	{
		var answers = DisplayAnswers.Instance;
		var files = Directory.EnumerateFiles(CorpusFiles.PathOf("display"), "*.xlsx").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal);

		Assert.Equal(files, answers.Workbooks.Select(w => w.File).OrderBy(f => f, StringComparer.Ordinal));
		Assert.Equal("16.0 20527", answers.Excel);
		Assert.Equal("en-US", answers.Culture);
		Assert.Equal(".,", answers.Separators);
		Assert.Equal(10_639, answers.Workbooks.Sum(w => w.Cells.Count));

		// The 1904 date system's sheets are the workbooks in it.
		Assert.All(answers.Workbooks, w => Assert.Equal(w.File.Contains("1904"), w.Date1904));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ShowsExcelsTextInEveryCase(bool async)
	{
		var failures = new List<string>();
		var listed = new int[Differences.Length];
		var cases = 0;
		foreach (var workbook in DisplayAnswers.Instance.Workbooks)
		{
			var shown = await ReadAsync(workbook.File, Formatted, async);
			var raw = await ReadAsync(workbook.File, Raw, async);
			foreach (var cell in workbook.Cells)
			{
				cases++;
				var ours = shown.TryGetValue(cell.Row, out var text) ? text : null;
				var expected = cell.Expected ?? (raw.TryGetValue(cell.Row, out var own) ? own : null);
				if (ours == expected)
				{
					continue;
				}

				var difference = Array.FindIndex(Differences, d => d.Applies(cell));
				if (difference >= 0)
				{
					listed[difference]++;
				}
				else
				{
					failures.Add($"{workbook.File} {cell}: Excel '{CellValue.Show(expected ?? "(none)")}'{(cell.Expected is null ? " (can't show)" : "")}, ours '{CellValue.Show(ours ?? "(none)")}'");
				}
			}
		}

		Assert.True(failures.Count == 0, $"{failures.Count} of {cases}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < Differences.Length; i++)
		{
			Assert.True(Differences[i].Cells == listed[i], $"{Differences[i].Reason}: {listed[i]} cases, listed {Differences[i].Cells}");
		}

		Assert.Equal(10_639, cases);
	}

	/// <summary>A case's code as the set wrote it, or a built-in id's as Excel gives it.</summary>
	/// <param name="cell">The case.</param>
	/// <returns>The code.</returns>
	private static string CodeOf(DisplayCell cell) => cell.Code ?? cell.NumberFormat;

	/// <summary>A workbook of the set's column C, the cases' values, by row, as a reader with these options gives them.</summary>
	/// <param name="file">The workbook.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>Each row's column C.</returns>
	internal static async Task<Dictionary<long, string>> ReadAsync(string file, ExcelReaderOptions options, bool async)
	{
		using var workbook = async ? await ExcelWorkbook.OpenAsync(DisplayAnswers.PathOf(file)) : ExcelWorkbook.Open(DisplayAnswers.PathOf(file));
		using var reader = await workbook.OpenReaderAsync(async, options);
		var texts = new Dictionary<long, string>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			if (reader.FieldCount > 2)
			{
				texts[reader.LineNumber] = reader.GetString(2);
			}
		}

		return texts;
	}
}
