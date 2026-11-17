using System.Globalization;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// LibreOffice as the oracle of the culture's built-in formats and separators (Phase 8's step 7, decision 17): the display
/// set's built-in ids, as LibreOffice exports them in de-DE, fr-FR and ja-JP through a profile whose locale is set, against
/// the reader under <see cref="ExcelText.Formatted"/> with that culture as its <c>FormatProvider</c>. Only what follows the
/// culture (decision 7) is compared: the separators of the number ids, the short date of ids 14 and 22, and the currency
/// ids. LibreOffice's own rules elsewhere (ids 15 to 21 localized, <c>_)</c> as a character's width, fractions' figure
/// spaces, dates before 1900-03-01 a day early, and its locale's percent) differ in 2,106 of the design's 6,024 cells,
/// which the comparison leaves out. CI installs LibreOffice; elsewhere the tests are skipped without it.
/// </summary>
public class LibreOfficeCultureTests
{
	/// <summary>The built-in ids whose text follows the culture: separators (1 to 4, 11, 61, 62), the short date (14, 22) and the currency (5 to 8, 42, 44, 63 to 66).</summary>
	private static readonly int[] Compared = [1, 2, 3, 4, 5, 6, 7, 8, 11, 14, 22, 42, 44, 61, 62, 63, 64, 65, 66];

	/// <summary>LibreOffice's own rule: a <c>?</c> placeholder without a digit is a figure space (U+2007), where Excel shows a space.</summary>
	private const string FigureSpaces = "LibreOffice shows a ? placeholder without a digit as a figure space (U+2007), where Excel shows a space (id 44's zero)";

	/// <summary>
	/// The cases whose text differs from LibreOffice's, by locale, each with its reason and how many cases it covers on this
	/// runtime: where .NET's culture data isn't LibreOffice's, and LibreOffice's own rule for <c>?</c>.
	/// </summary>
	private static readonly Dictionary<string, (string Reason, Func<string, string, bool> Applies, int Cells)[]> Differences = new(StringComparer.Ordinal)
	{
		["de-DE"] =
		[
			(FigureSpaces, (ours, theirs) => ours == theirs.Replace((char)0x2007, ' '), 1),
		],
		["fr-FR"] =
		[
			(FigureSpaces, (ours, theirs) => ours == theirs.Replace((char)0x2007, ' '), 1),
			("fr-FR's group separator is a narrow no-break space (U+202F) in .NET's data, and a no-break space (U+00A0) in LibreOffice's",
				(ours, theirs) => ours.Replace((char)0x202F, (char)0x00A0) == theirs, 42),
		],
		["ja-JP"] =
		[
#if NETFRAMEWORK
			(FigureSpaces, (ours, theirs) => ours == theirs.Replace((char)0x2007, ' '), 1),
			("NLS's yen is LibreOffice's (U+00A5)", (_, _) => false, 0),
#else
			(FigureSpaces, (ours, theirs) => ours == theirs.Replace((char)0x2007, ' '), 0),
			("ja-JP's yen is full-width (U+FFE5) in ICU's data, and U+00A5 in LibreOffice's, which shows a ? placeholder as a figure space too (id 44's zero)",
				(ours, theirs) => ours.Replace((char)0xFFE5, (char)0x00A5) == theirs.Replace((char)0x2007, ' '), 50),
#endif
		],
	};

	[LibreOfficeTheory]
	[InlineData("de-DE")]
	[InlineData("fr-FR")]
	[InlineData("ja-JP")]
	public async Task ShowsTheCulturesBuiltInsAsLibreOfficeDoes(string locale)
	{
		var answers = DisplayAnswers.Instance.Workbooks.Single(w => w.File == "builtins.xlsx");
		var theirs = LibreOffice.DisplaySheet("builtins.xlsx", locale)!.Where(r => r.Length > 0).ToDictionary(r => r[0], r => r.Length > 2 ? r[2] : "", StringComparer.Ordinal);
		var options = new ExcelReaderOptions { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false, FormatProvider = CultureInfo.GetCultureInfo(locale) } };
		var ours = await DisplayAnswersTests.ReadAsync(answers.File, options, false);
		var differences = Differences[locale];
		var listed = new int[differences.Length];
		var failures = new List<string>();
		var count = 0;
		foreach (var cell in answers.Cells.Where(c => Compared.Contains(c.BuiltIn)))
		{
			// LibreOffice shows serials before 61 a day early, and negative ones as dates before 1900, which Excel can't show.
			if (cell.BuiltIn is 14 or 22 && cell.Kind == 'n' && double.Parse(cell.Value, CultureInfo.InvariantCulture) < 61)
			{
				continue;
			}

			count++;
			var mine = ours[cell.Row];
			var other = theirs[$"builtins:{cell.Row}"];
			if (mine == other)
			{
				continue;
			}

			var difference = Array.FindIndex(differences, d => d.Applies(mine, other));
			if (difference >= 0)
			{
				listed[difference]++;
			}
			else
			{
				failures.Add($"{cell}: LibreOffice '{CellValue.Show(other)}', ours '{CellValue.Show(mine)}'");
			}
		}

		Assert.True(failures.Count == 0, $"{locale}: {failures.Count} of {count}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < differences.Length; i++)
		{
			Assert.True(differences[i].Cells == listed[i], $"{locale}: {differences[i].Reason}: {listed[i]} cases, listed {differences[i].Cells}");
		}

		Assert.True(count > 80, $"{count} cases");
	}
}
