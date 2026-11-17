using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// GridCraft over the display set (Phase 8's step 7, decision 17): the text it shows in each case
/// (<see cref="GridCraft.Cells"/>' <c>cell.get</c>), against the text Excel 365 shows (<see cref="DisplayAnswers"/>), its
/// differences listed by cause, as <see cref="GridCraftTests"/> lists them for the corpus. A run of <c>#</c> stands for a
/// value neither can show, whatever its length. It runs where GridCraft is installed; CI doesn't install it.
/// </summary>
public class GridCraftDisplayTests(ITestOutputHelper output)
{
	/// <summary>The locale codes of calendars and numeral systems.</summary>
	private static readonly string[] Calendars = ["[$-2010401]", "[$-1060409]", "[$-D07041E]", "[$-2000000]", "[$-D000409]"];

	/// <summary>The codes Excel shows text no rule found for, which the renderer can't read: elapsed time after a date part, General in a date, and [s] m.</summary>
	private static readonly string[] Unread = ["yyyy-mm-dd [h]:mm", "d [h]", "m/d [m]", "yyyy [s]", "dddd [h]", "mmm [hh]:mm:ss", "yyyy [h]:mm:ss.0", "yyyy General", "[s] m", "m [s]"];

	/// <summary>The codes whose m GridCraft reads otherwise than Excel beside other tokens.</summary>
	private static readonly string[] Minutes = ["h d m", "ss m", "s m", "h yyyy m", "h mmm m", "h dddd m", "s d m", "h d d m", "[h] d m", "h AM/PM m", "s mm", "h mmmmm m"];

	/// <summary>
	/// The cases where GridCraft shows otherwise than Excel, each with its cause and how many cases it covers, tried in order:
	/// each case counts for the first that applies. Each takes whether the workbook uses the 1904 date system, the case, Excel's
	/// text (<see langword="null"/> where it can't show the value) and GridCraft's.
	/// </summary>
	private static readonly (string Reason, Func<bool, DisplayCell, string?, string, bool> Applies, int Cells)[] Differences =
	[
		("[DBNum1] to [DBNum3]: Western digits", (_, c, _, _) => Code(c).Contains("DBNum"), 16),
		("a locale code's calendar or numeral system (Hijri, Arabic-Indic and Thai digits, the Buddhist calendar): Gregorian dates and Western digits",
			(_, c, _, _) => Calendars.Any(Code(c).Contains) || Code(c).StartsWith("B2", StringComparison.Ordinal), 15),
		("elapsed time after a date part, General in a date, and [s] m, which Excel shows by no rule found: the time of day, or the code's tokens", (_, c, _, _) => Unread.Contains(Code(c)), 53),
		("b, g and aaa shown as written, and e under [$-411] as the Gregorian year: no Buddhist years, eras or aaa",
			(_, c, _, _) => Regex.IsMatch(Unquoted(Code(c)), "(?<![A-Za-z\\]])(b{1,5}|g{1,3}|a{3,4})(?![A-Za-z])") || Code(c) == "[$-411]e", 210),
		("a locale code's language names months and days, and AM and PM, in English",
			(_, c, _, _) => Regex.IsMatch(Code(c), "\\[\\$[^\\]]*-([0-9A-Fa-f]{3,8}|[a-z]{2}-[A-Z]{2})\\]") && !Code(c).Contains("F800") && !Code(c).Contains("F400"), 144),
		("the system formats, [$-F800] and [$-F400], show the code's own tokens, not the long date and time", (_, c, _, _) => Code(c).Contains("F800") || Code(c).Contains("F400"), 154),
		("built-in ids 27 to 36 and 50 to 81 show as General, 39 and 40 without _)'s space, and 47 as mmss.0",
			(_, c, _, _) => c.BuiltIn is (>= 27 and <= 36) or 39 or 40 or 47 or (>= 50 and <= 81), 218),
		("a * fill shows as a space", (_, c, _, _) => Unquoted(Code(c)).Contains('*') || c.BuiltIn is >= 41 and <= 44, 126),
		("a boolean shows without its format's text section", (_, c, _, _) => c.Kind == 'b', 24),
		("a subnormal number is kept, where Excel holds 0", (_, c, _, _) => c.Kind == 'n' && Math.Abs(Number(c)) is > 0 and < 2.2250738585072014E-308, 47),
		("the 1904 date system's negatives show as ########", (date1904, c, _, theirs) => date1904 && c.Kind == 'n' && Number(c) < 0 && ExcelShown.Hashes(theirs), 101),
		("elapsed time negative, or past 15 digits, shows as ########", (_, c, _, theirs) => Regex.IsMatch(Unquoted(Code(c)), "\\[(h+|m+|s+)\\]", RegexOptions.IgnoreCase) && ExcelShown.Hashes(theirs), 240),
		("a value Excel can't show at any width shows: a negative date in a later section, a time rounding past the last date, a value no condition takes, a fraction or a number past its limits, a text past 255 characters",
			(_, _, excel, theirs) => excel is null && !ExcelShown.Hashes(theirs), 178),
		("General counts a minus sign in its 11 characters, and shows small numbers in scientific notation", (_, c, _, _) => Code(c).Contains("General"), 23),
		("a condition's section shows no minus sign, or one more", (_, c, excel, theirs) => Condition(c) && excel is not null && excel.Replace("-", "") == theirs.Replace("-", ""), 178),
		("a condition takes another section", (_, c, _, _) => Condition(c), 16),
		("fractions: the closest fraction rather than Excel's convergents, zero fractions' spaces, a minus sign on a fraction rounding to 0, and fixed denominators past 32,768 as written",
			(_, c, _, _) => Regex.IsMatch(Unquoted(Code(c)), "[#0?]\\s*/\\s*[#0?\\d]") && !Regex.IsMatch(Unquoted(Code(c)).Replace("General", ""), "[ymdhs]", RegexOptions.IgnoreCase), 239),
		("m beside other tokens is a month where Excel shows a minute, or the reverse", (_, c, _, _) => Minutes.Contains(Code(c)), 42),
		("hhh and sss are padded to three digits", (_, c, _, _) => Code(c) is "hhh" or "sss", 10),
		("a half rounds the other way: the time of day by the double's own value, where Excel adds half the unit to the serial; elapsed time in doubles, where Excel takes 15 digits",
			(_, _, excel, theirs) => excel is not null && excel.Length == theirs.Length && excel.Zip(theirs, (a, b) => a == b || (char.IsDigit(a) && char.IsDigit(b))).All(same => same), 337),
		("a code of a color alone shows nothing", (_, c, _, _) => Code(c) == "[Red]", 3),
		("a comma among the decimals groups them, and one after a percent scales", (_, c, _, _) => Code(c) is "0.0,0" or "0.0%,", 10),
	];

	[GridCraftFact]
	public void ShowsTheDisplaySetAsExcelDoesButWhereListed()
	{
		var failures = new List<string>();
		var listed = new int[Differences.Length];
		var cases = 0;
		foreach (var answers in DisplayAnswers.Instance.Workbooks)
		{
			var workbook = GridCraft.Open(DisplayAnswers.PathOf(answers.File));
			var sheet = workbook.Sheets[0].Name;
			var cells = GridCraft.Cells(workbook, new Dictionary<string, IEnumerable<(long Row, int Column)>> { [sheet] = answers.Cells.Select(c => (c.Row, 2)) })[sheet];
			foreach (var cell in answers.Cells)
			{
				cases++;
				var theirs = cells.TryGetValue((cell.Row, 2), out var found) ? found.Text : "";
				var expected = cell.Expected;
				if (expected is null ? ExcelShown.Hashes(theirs) : theirs == expected)
				{
					continue;
				}

				var difference = Array.FindIndex(Differences, d => d.Applies(answers.Date1904, cell, expected, theirs));
				if (difference >= 0)
				{
					listed[difference]++;
				}
				else
				{
					failures.Add($"{answers.File} {cell}: Excel '{CellValue.Show(expected ?? "#")}', GridCraft '{CellValue.Show(theirs)}'");
				}
			}
		}

		output.WriteLine($"{GridCraft.Version}: {cases} cases, {listed.Sum()} listed.");
		Assert.True(failures.Count == 0, $"{failures.Count} of {cases}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < Differences.Length; i++)
		{
			Assert.True(Differences[i].Cells == listed[i], $"{Differences[i].Reason}: {listed[i]} cases, listed {Differences[i].Cells}");
		}
	}

	/// <summary>A case's code as the set wrote it, or empty for a built-in id, which its own causes name.</summary>
	/// <param name="cell">The case.</param>
	/// <returns>The code.</returns>
	private static string Code(DisplayCell cell) => cell.Code ?? "";

	/// <summary>A code without its quoted literals.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The rest.</returns>
	private static string Unquoted(string code) => Regex.Replace(code, "\"[^\"]*\"", "");

	/// <summary>Whether a case's code has a condition.</summary>
	/// <param name="cell">The case.</param>
	/// <returns>Whether it has.</returns>
	private static bool Condition(DisplayCell cell) => Regex.IsMatch(Code(cell), "\\[(<|>|=|<>|<=|>=)-?\\d");

	/// <summary>A number case's value.</summary>
	/// <param name="cell">The case.</param>
	/// <returns>The value.</returns>
	private static double Number(DisplayCell cell) => double.Parse(cell.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
}
