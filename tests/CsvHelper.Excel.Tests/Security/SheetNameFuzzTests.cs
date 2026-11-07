using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of sheet names, as Phase 4's "Security and fuzzing" describes: workbooks of random names (Excel's
/// forbidden characters, apostrophes, control characters, lone surrogates, escapes, compatibility forms, case and
/// accents, copies' suffixes, lengths around 31), and of names alike to those already added (the same, its other
/// normalization, its ASCII letters in the other case). A name the rules refuse throws <see cref="ArgumentException"/>,
/// whose rules this test checks itself: 1 to 31 code units, also as the workbook part writes it (an escaped <c>_</c>, and
/// U+FFFE and U+FFFF, take 7), none of <c>\ / ? * [ ] :</c>, no apostrophe first or last, no control characters or
/// lone surrogates. A name they accept is added, or refused for a sheet the workbook has, which
/// the message names; one that differs from a sheet only in normalization or ASCII case is refused, and no two sheets'
/// names are alike in case once normalized. Each sheet is written, some past their row limit
/// (<see cref="ExcelRowLimit.NewSheet"/> at two rows a sheet), so that copies' names (<c>Orders (2)</c>) are made too:
/// each passes the rules, and is its sheet's name or stem cut to fit. The workbook passes the validator, and
/// CsvHelper.Excel reads its sheets' names and rows as written. <c>CSVHELPER_EXCEL_FUZZ_NAMES</c> sets the number of
/// workbooks, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the run named.</param>
public class SheetNameFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of workbooks a run writes, unless <c>CSVHELPER_EXCEL_FUZZ_NAMES</c> says otherwise.</summary>
	private const int DefaultWorkbooks = 500;

	/// <summary>The most code units a sheet's name has.</summary>
	private const int MaxName = 31;

	/// <summary>The rows each sheet holds before the next one takes the rest.</summary>
	private const int RowsPerSheet = 2;

	/// <summary>The characters Excel forbids in a sheet's name.</summary>
	private static readonly char[] Forbidden = ['\\', '/', '?', '*', '[', ']', ':'];

	/// <summary>
	/// The pieces names are made of: words, Excel's own names, white space, punctuation Excel takes and the characters it
	/// forbids, apostrophes, escapes, copies' suffixes and near misses, control characters, other invisible characters and
	/// the non-characters, letters in their cases, accents composed and not, compatibility forms, scripts, and surrogates.
	/// </summary>
	private static readonly string[] Pieces =
	[
		"Sheet", "Data", "Q1", "Orders", "History", "TRUE", "A1", "R1C1", "x", "1", "2024", " ", "  ",
		"!", "=", "+", "#", "$", "%", "&", ";", ",", "\"", "<", ">", "(", ")", "-", "_", ".", "@", "{", "}", "~", "`", "^",
		"\\", "/", "?", "*", "[", "]", ":", "'", "''",
		"_x0041_", "_X0041_", "_x005F_", "_xD800_", "_x000D_", "_x0041",
		" (2)", " (9)", " (10)", " (999999999)", " (1000000000)", "(2)", " ()", " (a)", " (0)", " (-1)",
		Units(0), Units(9), Units(10), Units(13), Units(0x1F), Units(0x7F), Units(0x85), Units(0x9F),
		Units(0xA0), Units(0x200B), Units(0xFEFF), Units(0xFFFD), Units(0xFFFE), Units(0xFFFF), Units(0x2028),
		Units(0xE9), Units(0x65, 0x301), Units(0x45, 0x301), Units(0xDF), Units(0x1E9E), "SS", "ss", Units(0xFB01), "fi",
		Units(0xFF21), "A", Units(0x212A), "K", "k", Units(0x3C3), Units(0x3C2), Units(0x3A3), Units(0x130), Units(0x131), "i", "I",
		Units(0x49, 0x307), Units(0x69, 0x307), Units(0x1C4), Units(0x1C5), Units(0x1C6), Units(0x2126), Units(0x3A9), Units(0x212B),
		Units(0xC5), Units(0x41, 0x30A), Units(0x65E5, 0x672C), Units(0xFF76), Units(0x30AB), Units(0x304B), Units(0x627, 0x644),
		Units(0xAC00), Units(0x1100, 0x1161), Units(0xD83D, 0xDE00), Units(0xD83D), Units(0xDE00), Units(0xDE00, 0xD83D),
	];

	[Fact]
	public async Task AcceptsOrRefusesRandomSheetNames()
	{
		var workbooks = Setting("CSVHELPER_EXCEL_FUZZ_NAMES", DefaultWorkbooks);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			try
			{
				if (await CheckAsync(seed, tally) is { } problem)
				{
					failures.Add($"Seed {seed}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed}: {Describe(e)}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + workbooks - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_NAMES");
	}

	/// <summary>
	/// Writes a seed's workbook, adding its names one by one and checking how each is taken, then checks the workbook: its
	/// sheets' names, copies included, the validator, and its sheets read back.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <param name="tally">Takes how the names were taken.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckAsync(int seed, SortedDictionary<string, int> tally)
	{
		var random = new Random(seed);
		var async = random.Next(2) == 1;
		var options = new ExcelWriterOptions
		{
			Strings = random.Next(2) == 0 ? ExcelStrings.Inline : ExcelStrings.Shared,
			RowLimit = ExcelRowLimit.NewSheet,
			Csv = new CsvWriterOptions { HasHeader = false },
		};
		var problems = new List<string>();
		var sheets = new List<(string Name, int Records)>();
		var names = new List<string>();
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = RowsPerSheet;
			for (var n = random.Next(1, 9); n > 0 && problems.Count == 0; n--)
			{
				var candidate = NextCandidate(random, sheets.Select(s => s.Name).ToList());
				var rule = Rule(candidate.Name);
				var before = book.SheetNames.ToList();
				CsvWriter writer;
				try
				{
					writer = book.AddSheet(candidate.Name);
				}
				catch (ArgumentException e) when (e.GetType() == typeof(ArgumentException))
				{
					Count(tally, rule is null ? "names: refused, alike to a sheet" : "names: refused by the rules");
					if (e.ParamName != "name")
					{
						problems.Add($"{candidate} threw for the parameter {e.ParamName}");
					}
					else if (rule is null && !before.Any(s => e.Message.StartsWith($"The workbook already has the sheet \"{s}\", which Excel takes \"{candidate.Name}\" to be.", StringComparison.Ordinal)))
					{
						problems.Add($"{candidate}, which the rules accept, was refused: {e.Message}");
					}

					continue;
				}

				Count(tally, "names: added");
				if (rule is not null)
				{
					problems.Add($"{candidate} was added, though {rule}");
				}
				else if (candidate.Alike is { } alike)
				{
					problems.Add($"{candidate} was added beside {Show(alike)}");
				}
				else if (before.FirstOrDefault(s => Alike(s, candidate.Name)) is { } same)
				{
					problems.Add($"{candidate} was added beside {Show(same)}, alike in case once normalized");
				}

				var records = random.Next(4) == 0 ? random.Next(RowsPerSheet + 1, 4 * RowsPerSheet) : random.Next(1, RowsPerSheet + 1);
				for (var r = 0; r < records; r++)
				{
					writer.WriteField($"{sheets.Count}.{r}");
					await Written.EndRecordAsync(writer, a);
				}

				await Written.DisposeAsync(writer, a);
				sheets.Add((candidate.Name, records));
			}

			names.AddRange(book.SheetNames);
		}, async, options);
		if (problems.Count > 0)
		{
			return string.Join("; ", problems);
		}

		Count(tally, $"workbooks: {sheets.Count} sheets added");
		var copiesProblem = CopiesProblem(sheets, names, tally);
		if (copiesProblem is not null)
		{
			return copiesProblem;
		}

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		return ReadProblem(workbook, sheets, names);
	}

	/// <summary>
	/// Checks the workbook's names: each added sheet's, then its copies', which pass the rules, are each named for the
	/// sheet before it, as Excel names a copy (its stem, cut to the longest that fits 31 code units as written), and are
	/// alike to no other.
	/// </summary>
	/// <param name="sheets">The sheets added, with their records.</param>
	/// <param name="names">The workbook writer's sheets' names.</param>
	/// <param name="tally">Takes the copies.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? CopiesProblem(List<(string Name, int Records)> sheets, List<string> names, SortedDictionary<string, int> tally)
	{
		var expected = sheets.Sum(s => SheetsFor(s.Records));
		if (names.Count != expected)
		{
			return $"the workbook has the sheets {string.Join(", ", names.Select(Show))}, {names.Count} rather than {expected}";
		}

		var at = 0;
		foreach (var (name, records) in sheets)
		{
			if (!string.Equals(names[at], name, StringComparison.Ordinal))
			{
				return $"sheet {at + 1} is {Show(names[at])} rather than {Show(name)}";
			}

			for (var copy = 1; copy < SheetsFor(records); copy++)
			{
				var stem = Stem(names[at + copy - 1]);
				var copyName = names[at + copy];
				Count(tally, "names: copies made");
				var suffix = copyName.LastIndexOf(" (", StringComparison.Ordinal);
				if (Rule(copyName) is { } rule)
				{
					return $"the copy {Show(copyName)} of {Show(name)} fails the rules: {rule}";
				}

				if (suffix < 0 || copyName[copyName.Length - 1] != ')' || !copyName.Substring(suffix + 2, copyName.Length - suffix - 3).All(c => c is >= '0' and <= '9'))
				{
					return $"the copy {Show(copyName)} of {Show(name)} doesn't end with a number in parentheses";
				}

				var kept = copyName.Substring(0, suffix);
				var longest = LongestFit(stem, copyName.Substring(suffix));
				if (!string.Equals(kept, longest, StringComparison.Ordinal))
				{
					return $"the copy {Show(copyName)} of {Show(names[at + copy - 1])} keeps {Show(kept)} of its stem {Show(stem)}, rather than {Show(longest)}";
				}

				if (kept.Length < stem.Length)
				{
					Count(tally, "names: copies with the stem cut");
				}
			}

			at += SheetsFor(records);
		}

		for (var i = 0; i < names.Count; i++)
		{
			for (var j = 0; j < i; j++)
			{
				if (Alike(names[i], names[j]))
				{
					return $"the sheets {Show(names[j])} and {Show(names[i])} are alike in case once normalized";
				}
			}
		}

		return null;
	}

	/// <summary>Checks that CsvHelper.Excel reads the sheets' names, and each added sheet's records across its copies.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheets">The sheets added, with their records.</param>
	/// <param name="names">The workbook writer's sheets' names.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? ReadProblem(byte[] workbook, List<(string Name, int Records)> sheets, List<string> names)
	{
		var read = Written.Read(workbook);
		var expectedNames = names.Count == 0 ? ["Sheet1"] : names;
		if (!read.Select(s => s.Sheet).SequenceEqual(expectedNames, StringComparer.Ordinal))
		{
			return $"read the sheets {string.Join(", ", read.Select(s => Show(s.Sheet)))} for {string.Join(", ", expectedNames.Select(Show))}";
		}

		var at = 0;
		for (var s = 0; s < sheets.Count; s++)
		{
			var count = SheetsFor(sheets[s].Records);
			var rows = read.Skip(at).Take(count).SelectMany(r => r.Rows).ToList();
			var expected = Enumerable.Range(0, sheets[s].Records).Select(r => $"{s}.{r}").ToList();
			if (!rows.SequenceEqual(expected, StringComparer.Ordinal))
			{
				return $"the sheet {Show(sheets[s].Name)} and its copies read {string.Join(" / ", rows)} for {string.Join(" / ", expected)}";
			}

			at += count;
		}

		return null;
	}

	/// <summary>The sheets a sheet's records take: itself, and a copy for each two records past its first two.</summary>
	/// <param name="records">The records.</param>
	/// <returns>The sheets.</returns>
	private static int SheetsFor(int records) => (records + RowsPerSheet - 1) / RowsPerSheet;

	/// <summary>
	/// Why the rules refuse a name, or <see langword="null"/> when they accept it: 1 to 31 code units, none of the
	/// forbidden characters, no apostrophe first or last, no control characters (U+0000 to U+001F, U+007F to U+009F), no
	/// lone surrogates, and 31 code units at most as written.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The reason, or <see langword="null"/>.</returns>
	private static string? Rule(string name)
	{
		if (name.Length is 0 or > MaxName)
		{
			return $"it has {name.Length} code units";
		}

		if (name.IndexOfAny(Forbidden) >= 0)
		{
			return "it has a character Excel forbids";
		}

		if (name[0] == '\'' || name[name.Length - 1] == '\'')
		{
			return "it starts or ends with an apostrophe";
		}

		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (c < 0x20 || c is >= (char)0x7F and <= (char)0x9F)
			{
				return "it has a control character";
			}

			if (c is >= (char)0xD800 and <= (char)0xDBFF && i + 1 < name.Length && name[i + 1] is >= (char)0xDC00 and <= (char)0xDFFF)
			{
				i++;
			}
			else if (c is >= (char)0xD800 and <= (char)0xDFFF)
			{
				return "it has a lone surrogate";
			}
		}

		return WrittenLength(name) > MaxName ? $"it takes {WrittenLength(name)} code units as written" : null;
	}

	/// <summary>
	/// The code units a name takes in the workbook part: 7 for U+FFFE and U+FFFF (<c>_xFFFE_</c>), and 7 for an <c>_</c>
	/// that starts <c>_x</c> and four hexadecimal digits before an <c>_</c> or a character written as an escape
	/// (<c>_x005F_</c>), which would read as an escape otherwise.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The code units.</returns>
	private static int WrittenLength(string name)
	{
		var length = name.Length;
		for (var i = 0; i < name.Length; i++)
		{
			if (IsEscaped(name[i]))
			{
				length += 6;
			}
			else if (name[i] == '_' && i + 6 < name.Length && name[i + 1] == 'x' && name.Substring(i + 2, 4).All(Uri.IsHexDigit) && (name[i + 6] == '_' || IsEscaped(name[i + 6])))
			{
				length += 6;
			}
		}

		return length;
	}

	/// <summary>Whether a character is written as an escape: a control character but tab and LF, U+FFFE or U+FFFF.</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsEscaped(char c) => (c < ' ' && c is not ('\t' or '\n')) || c is (char)0xFFFE or (char)0xFFFF;

	/// <summary>
	/// The longest start of a stem that a copy's name keeps: with the suffix, at most 31 code units and at most 31 as
	/// written, and not between a surrogate pair's halves.
	/// </summary>
	/// <param name="stem">The stem.</param>
	/// <param name="suffix">The copy's suffix, such as <c> (2)</c>.</param>
	/// <returns>The start kept.</returns>
	private static string LongestFit(string stem, string suffix)
	{
		for (var length = stem.Length; length > 0; length--)
		{
			var kept = stem.Substring(0, length);
			if (length < stem.Length && char.IsHighSurrogate(stem[length - 1]))
			{
				continue;
			}

			if (kept.Length + suffix.Length <= MaxName && WrittenLength(kept + suffix) <= MaxName)
			{
				return kept;
			}
		}

		return "";
	}

	/// <summary>
	/// Whether two names are alike in case, as they are or once normalized (NFC), which Excel takes to be the same name
	/// (and the validator, which compares them in case): the writer may be stricter, but never less.
	/// </summary>
	/// <param name="a">A name, which passes the rules.</param>
	/// <param name="b">Another.</param>
	/// <returns>Whether they are alike.</returns>
	private static bool Alike(string a, string b) =>
		string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
		string.Equals(Normalized(a, NormalizationForm.FormC), Normalized(b, NormalizationForm.FormC), StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A name normalized, around the non-characters that the normalization refuses (U+FFFE on .NET, every one on .NET
	/// Framework), which compose with nothing: each run between them on its own.
	/// </summary>
	/// <param name="name">The name, which has no lone surrogates.</param>
	/// <param name="form">The form.</param>
	/// <returns>The normalized name.</returns>
	private static string Normalized(string name, NormalizationForm form)
	{
		try
		{
			return name.Normalize(form);
		}
		catch (ArgumentException)
		{
			var runs = new List<string>();
			var run = new StringBuilder();
			foreach (var c in name)
			{
				if (c is >= (char)0xFDD0 and <= (char)0xFDEF or (char)0xFFFE or (char)0xFFFF || (char.IsLowSurrogate(c) && (c & 0x3FE) == 0x3FE))
				{
					// A plane's last two code points end in a low surrogate of DFFE or DFFF, its high one already in the run.
					var high = char.IsLowSurrogate(c) ? run[run.Length - 1].ToString() : "";
					run.Length -= high.Length;
					runs.Add(run.ToString().Normalize(form) + high + c);
					run.Clear();
				}
				else
				{
					run.Append(c);
				}
			}

			return string.Concat(runs) + run.ToString().Normalize(form);
		}
	}

	/// <summary>The stem of a sheet's copies: its name before a space and one to nine digits in parentheses at its end, or the name.</summary>
	/// <param name="name">The sheet's name.</param>
	/// <returns>The stem.</returns>
	private static string Stem(string name)
	{
		var open = name.LastIndexOf(" (", StringComparison.Ordinal);
		var digits = open < 0 ? "" : name.Substring(open + 2, name.Length - open - 2);

		return open >= 0 && digits.Length is >= 2 and <= 10 && digits[digits.Length - 1] == ')' && digits.Substring(0, digits.Length - 1).All(c => c is >= '0' and <= '9')
			? name.Substring(0, open)
			: name;
	}

	/// <summary>
	/// A name to add: a random one, or one alike to a sheet the workbook has (the same name, its NFC or NFD form, its ASCII
	/// letters in the other case but before an accent, which the writer must refuse) or near it (in upper or lower case,
	/// or longer).
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="added">The names added so far.</param>
	/// <returns>The name.</returns>
	private static Candidate NextCandidate(Random random, List<string> added)
	{
		if (added.Count > 0 && random.Next(3) == 0)
		{
			var source = added[random.Next(added.Count)];
			switch (random.Next(7))
			{
				case 0:
					return new Candidate(source, "the same name as " + Show(source), source);
				case 1:
					return new Candidate(Normalized(source, NormalizationForm.FormC), "the NFC of " + Show(source), source);
				case 2:
					return new Candidate(Normalized(source, NormalizationForm.FormD), "the NFD of " + Show(source), source);
				case 3:
					return new Candidate(SwapAsciiCase(source), "the ASCII case swapped of " + Show(source), source);
				case 4:
					return new Candidate(source.ToUpperInvariant(), "the upper case of " + Show(source), null);
				case 5:
					return new Candidate(source.ToLowerInvariant(), "the lower case of " + Show(source), null);
				default:
					return new Candidate(source + Pieces[random.Next(Pieces.Length)], "a longer " + Show(source), null);
			}
		}

		if (random.Next(50) == 0)
		{
			return new Candidate("", "empty", null);
		}

		var target = random.Next(1, MaxName + 5);
		var name = new StringBuilder();
		while (name.Length < target)
		{
			name.Append(Pieces[random.Next(Pieces.Length)]);
		}

		if (random.Next(3) == 0)
		{
			// Cut to its length, perhaps between a surrogate pair's halves.
			name.Length = target;
		}

		return new Candidate(name.ToString(), "random", null);
	}

	/// <summary>A name's ASCII letters in the other case, but those before a combining mark, which composes with them.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The name with its case swapped.</returns>
	private static string SwapAsciiCase(string name)
	{
		var swapped = name.ToCharArray();
		for (var i = 0; i < swapped.Length; i++)
		{
			var beforeMark = i + 1 < name.Length && CharUnicodeInfo.GetUnicodeCategory(name[i + 1]) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
			if (!beforeMark && swapped[i] is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
			{
				swapped[i] = (char)(swapped[i] ^ 0x20);
			}
		}

		return new string(swapped);
	}

	/// <summary>A name to add, how it was made, and the sheet it must be refused beside, if any.</summary>
	/// <param name="Name">The name.</param>
	/// <param name="How">How it was made.</param>
	/// <param name="Alike">The sheet's name that it must be refused beside, when the rules accept it; or <see langword="null"/>.</param>
	private sealed record Candidate(string Name, string How, string? Alike)
	{
		/// <summary>The name and how it was made, for a failure.</summary>
		/// <returns>The description.</returns>
		public override string ToString() => $"{Show(Name)} ({How})";
	}
}
