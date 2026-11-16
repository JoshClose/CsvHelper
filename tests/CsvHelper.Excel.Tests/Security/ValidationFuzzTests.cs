using System.Text;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Corpus;
using DocumentFormat.OpenXml.Packaging;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of columns' lists and prompts (Phase 7's step 8): workbooks of sheets whose columns have random lists and
/// prompts (control characters, <c>_xHHHH_</c>, surrogate pairs and lone surrogates, quotes, commas, white space at either
/// end, values and prompts around 255 characters, repeats) under random header names, protected or not, with a password
/// now and then, and with lists and prompts Excel wouldn't open planted now and then. The test's own rules, from the
/// design's, say which settings are refused, which lists go inline and which in the lists sheet, under which names, and
/// what each validation, prompt and title holds; each workbook passes the Open XML SDK's validator and holds them, as its
/// XML says once its escapes are decoded (a lone surrogate written as U+FFFD), and its lists sheet reads back through
/// CsvHelper.Excel's reader. <c>CSVHELPER_EXCEL_FUZZ_VALIDATIONS</c> sets the number of workbooks,
/// <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed, and <c>CSVHELPER_EXCEL_FUZZ_SAVE</c> a folder to save each workbook in,
/// for the corpus tool's <c>fuzzed</c> command to open in Excel.
/// </summary>
/// <param name="output">Shows what the run wrote.</param>
public class ValidationFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of workbooks a run writes, unless <c>CSVHELPER_EXCEL_FUZZ_VALIDATIONS</c> says otherwise.</summary>
	private const int DefaultWorkbooks = 300;

	/// <summary>The most characters of a list's value and of a prompt that Excel opens.</summary>
	private const int MaxText = 255;

	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>
	/// The pieces of lists' values, prompts, header names and passwords: words, white space at either end, line breaks and
	/// tabs, commas and quotes, XML's characters, escapes and a literal escape, controls, a no-break space, U+FFFE and
	/// U+FFFF, a surrogate pair and lone surrogates, and letters past ASCII.
	/// </summary>
	private static readonly string[] Pieces =
	[
		"a", "Red", "North", " ", "\t", "\n", "\r\n", ",", "\"", "\"\"", "'", "&", "<", ">", "_x0041_", "_x005F_", "_x", "=SUM(1)", Units(1), Units(0x1F),
		Units(0x7F), Units(0xA0), Units(0xFFFE), Units(0xFFFF), Units(0xD83D, 0xDE00), Units(0xD800), Units(0xDC00), Units(0xE9), Units(0x65E5, 0x672C),
	];

	[Fact]
	public void WritesRandomListsAndPromptsAsTheRulesSay()
	{
		var workbooks = Setting("CSVHELPER_EXCEL_FUZZ_VALIDATIONS", DefaultWorkbooks);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var save = Environment.GetEnvironmentVariable("CSVHELPER_EXCEL_FUZZ_SAVE");
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			try
			{
				if (Check(seed, tally, save) is { } problem)
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

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_VALIDATIONS");
	}

	/// <summary>Writes a seed's workbook and checks it: the refusals, the validator, each column's validation, and the lists sheet.</summary>
	/// <param name="seed">The seed.</param>
	/// <param name="tally">Takes what was written.</param>
	/// <param name="save">A folder to save the workbook in, or <see langword="null"/>.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? Check(int seed, SortedDictionary<string, int> tally, string? save)
	{
		var random = new Random(seed);
		var sheets = new List<SheetInput>();
		for (var i = 0; i < 1 + random.Next(3); i++)
		{
			// The first sheet is never refused, so that the workbook has one.
			sheets.Add(SheetInput.Of(random, $"Sheet{i + 1}", plant: i > 0 && random.Next(4) == 0));
		}

		using var stream = new MemoryStream();
		var written = new List<SheetInput>();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			foreach (var sheet in sheets)
			{
				CsvWriter writer;
				try
				{
					writer = workbook.AddSheet(sheet.Name, sheet.Options);
				}
				catch (ArgumentException e)
				{
					if (sheet.Refusal is null)
					{
						return $"{sheet.Name} was refused: {e.Message}";
					}

					Count(tally, "refused: " + sheet.Refusal);

					continue;
				}

				if (sheet.Refusal is not null)
				{
					return $"{sheet.Name} was taken, though {sheet.Refusal}";
				}

				using (writer)
				{
					foreach (var name in sheet.Header)
					{
						writer.WriteField(name);
					}

					writer.EndRecord();
					for (var r = 0; r < sheet.Rows; r++)
					{
						foreach (var _ in sheet.Header)
						{
							writer.WriteField("r" + r.ToString(System.Globalization.CultureInfo.InvariantCulture));
						}

						writer.EndRecord();
					}
				}

				written.Add(sheet);
			}
		}

		var bytes = stream.ToArray();
		if (save is not null)
		{
			Directory.CreateDirectory(save);
			File.WriteAllBytes(Path.Combine(save, $"validation-{seed:D6}.xlsx"), bytes);
		}

		if (Writing.Written.ValidationErrors(bytes) is { Count: > 0 } errors)
		{
			return "the validator: " + string.Join("; ", errors.Take(3));
		}

		// The lists that don't go inline, each once, in the order of the sheets and their columns.
		var lists = new List<string[]>();
		foreach (var sheet in written)
		{
			foreach (var column in sheet.Columns.Where(c => c.List is not null))
			{
				var distinct = column.List!.Distinct(StringComparer.Ordinal).ToArray();
				if (!Inline(distinct) && !lists.Any(l => l.SequenceEqual(distinct)))
				{
					lists.Add(distinct);
				}
			}
		}

		var listsName = "Lists";
		for (var n = 2; written.Any(s => string.Equals(s.Name, listsName, StringComparison.OrdinalIgnoreCase)); n++)
		{
			listsName = "Lists" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		using var package = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
		var book = package.WorkbookPart!;
		var parts = book.Workbook!.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>()
			.Select(s => (Name: s.Name!.Value!, State: s.State?.InnerText, Part: (WorksheetPart)book.GetPartById(s.Id!.Value!)))
			.ToList();
		var expectedNames = written.Select(s => s.Name).Concat(lists.Count > 0 ? [listsName] : []).ToList();
		if (!parts.Select(p => p.Name).SequenceEqual(expectedNames))
		{
			return $"sheets [{string.Join(", ", parts.Select(p => p.Name))}], expected [{string.Join(", ", expectedNames)}]";
		}

		foreach (var (sheet, index) in written.Select((s, i) => (s, i)))
		{
			var xml = XDocument.Load(parts[index].Part.GetStream());
			var validations = xml.Root!.Element(Main + "dataValidations")?.Elements(Main + "dataValidation").ToList() ?? [];
			var expected = sheet.Columns.Where(c => c.List is not null || c.Prompt is not null).ToList();
			if (validations.Count != expected.Count)
			{
				return $"{sheet.Name}: {validations.Count} validations, expected {expected.Count}";
			}

			foreach (var (column, validation) in expected.Zip(validations, (c, v) => (c, v)))
			{
				if (CheckValidation(sheet, column, validation, lists) is { } problem)
				{
					return $"{sheet.Name}!{column.Letters}: {problem}";
				}

				Count(tally, column.List is null ? "a prompt alone" : Inline([.. column.List.Distinct(StringComparer.Ordinal)]) ? "a list inline" : "a list in the lists sheet");
			}

			Count(tally, sheet.Options.Protect ? sheet.Options.Password is null ? "protected" : "protected with a password" : "not protected");
		}

		if (lists.Count > 0)
		{
			if (CheckLists(bytes, package, parts[parts.Count - 1].State, listsName, lists) is { } problem)
			{
				return problem;
			}

			Count(tally, "a lists sheet of " + (lists.Count == 1 ? "1 list" : "several lists"));
		}

		return null;
	}

	/// <summary>
	/// Checks a column's validation: its cells, from the row below the header to the last; a list's type, and its values
	/// inline, quoted, or its name in the lists sheet; a prompt, and its title, the header's name cut to 32 characters
	/// between characters; each with its escapes decoded.
	/// </summary>
	/// <param name="sheet">The sheet.</param>
	/// <param name="column">The column.</param>
	/// <param name="validation">The validation's element.</param>
	/// <param name="lists">The lists in the lists sheet, in order.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? CheckValidation(SheetInput sheet, ColumnInput column, XElement validation, List<string[]> lists)
	{
		var header = sheet.Options.Title is null ? 1 : 2;
		var sqref = (string?)validation.Attribute("sqref");
		if (sqref != $"{column.Letters}{header + 1}:{column.Letters}1048576")
		{
			return $"over {sqref}";
		}

		if (column.List is { } values)
		{
			var distinct = values.Distinct(StringComparer.Ordinal).ToArray();
			var formula = validation.Element(Main + "formula1")?.Value ?? "";
			var expected = Inline(distinct)
				? "\"" + Written(string.Join(",", distinct)).Replace("\"", "\"\"") + "\""
				: $"_CsvHelperList{lists.FindIndex(l => l.SequenceEqual(distinct)) + 1}";
			if ((string?)validation.Attribute("type") != "list" || CorpusFiles.DecodeEscapes(formula) != expected)
			{
				return $"type {(string?)validation.Attribute("type")}, formula {Show(CorpusFiles.DecodeEscapes(formula))}, expected {Show(expected)}";
			}
		}
		else if (validation.Attribute("type") is not null || validation.Element(Main + "formula1") is not null)
		{
			return "a prompt alone has a type or a formula";
		}

		var prompt = (string?)validation.Attribute("prompt");
		var title = (string?)validation.Attribute("promptTitle");
		var expectedTitle = column.Prompt is null || column.Name.Length == 0 ? null : Title(Written(column.Name));
		if ((prompt is null ? null : CorpusFiles.DecodeEscapes(prompt)) != (column.Prompt is null ? null : Written(column.Prompt)))
		{
			return $"prompt {Show(prompt ?? "none")}, expected {Show(column.Prompt ?? "none")}";
		}

		if ((title is null ? null : CorpusFiles.DecodeEscapes(title)) != expectedTitle)
		{
			return $"title {Show(title ?? "none")}, expected {Show(expectedTitle ?? "none")}";
		}

		return null;
	}

	/// <summary>Checks the lists sheet: very hidden, protected, each list a column of its values, and a hidden name over each.</summary>
	/// <param name="bytes">The workbook's bytes.</param>
	/// <param name="package">The workbook, open.</param>
	/// <param name="state">The lists sheet's state.</param>
	/// <param name="name">The lists sheet's name.</param>
	/// <param name="lists">The lists, in order.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? CheckLists(byte[] bytes, SpreadsheetDocument package, string? state, string name, List<string[]> lists)
	{
		if (state != "veryHidden")
		{
			return $"the lists sheet is {state ?? "visible"}";
		}

		using (var workbook = ExcelWorkbook.Open(new MemoryStream(bytes)))
		using (var reader = workbook.OpenReader(name, new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } }))
		{
			var rows = new List<string[]>();
			while (reader.Read())
			{
				rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)]);
			}

			for (var c = 0; c < lists.Count; c++)
			{
				var read = rows.Select(r => c < r.Length ? r[c] : "").Take(lists[c].Length).ToArray();
				var expected = lists[c].Select(Written).ToArray();
				if (!read.SequenceEqual(expected) || rows.Skip(lists[c].Length).Any(r => c < r.Length && r[c].Length > 0))
				{
					return $"the lists sheet's column {c + 1}: [{string.Join(", ", read.Select(Show))}], expected [{string.Join(", ", expected.Select(Show))}]";
				}
			}
		}

		var names = package.WorkbookPart!.Workbook!.DefinedNames?.Elements<DocumentFormat.OpenXml.Spreadsheet.DefinedName>()
			.Where(n => n.Name!.Value!.StartsWith("_CsvHelperList", StringComparison.Ordinal))
			.Select(n => (n.Name!.Value!, n.Text, n.Hidden?.Value == true))
			.ToList() ?? [];
		var quoted = SheetLocation.Quote(name);
		var expectedNames = lists.Select((l, i) => ($"_CsvHelperList{i + 1}", $"{quoted}!${CellReference.ColumnName(i)}$1:${CellReference.ColumnName(i)}${l.Length}", true)).ToList();

		return names.SequenceEqual(expectedNames) ? null : $"names [{string.Join(", ", names)}], expected [{string.Join(", ", expectedNames)}]";
	}

	/// <summary>
	/// Whether a list goes inline, as the design says: its values, joined by commas, of at most 255 characters, and none with
	/// a comma or white space at either end.
	/// </summary>
	/// <param name="values">The values, each once.</param>
	/// <returns>Whether it goes inline.</returns>
	private static bool Inline(string[] values) =>
		string.Join(",", values).Length <= MaxText && values.All(v => !v.Contains(',') && !char.IsWhiteSpace(v[0]) && !char.IsWhiteSpace(v[v.Length - 1]));

	/// <summary>
	/// A prompt's title, as the design says and the schema limits it: the header's name, as written, cut to 32 characters
	/// between characters, and shorter while its length as written is longer (<see cref="WrittenLength"/>).
	/// </summary>
	/// <param name="name">The header's name, as written.</param>
	/// <returns>The title.</returns>
	private static string Title(string name)
	{
		var title = Cut(name, 32);
		while (title.Length > 1 && WrittenLength(title) > 32)
		{
			title = Cut(title, title.Length - 1);
		}

		return title;
	}

	/// <summary>
	/// A prompt's or a title's length as written in the file and read back by XML: a tab, a line break and CR one each, as
	/// character references; another control character, U+FFFE and U+FFFF seven, as <c>_xHHHH_</c>; an <c>_</c> seven where
	/// it would start an escape, as <c>_x005F_</c>, when <c>_x</c>, four hexadecimal digits and an <c>_</c> or a character
	/// written as an escape follow; a lone surrogate one, as U+FFFD; the rest one each.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>Its length as written.</returns>
	private static int WrittenLength(string text)
	{
		static bool Escaped(char c) => (c < ' ' && c is not ('\t' or '\n' or '\r')) || c is '\uFFFE' or '\uFFFF';

		var written = Written(text);
		var length = 0;
		for (var i = 0; i < written.Length; i++)
		{
			var c = written[i];
			var escapeLike = c == '_' && i + 6 < written.Length && written[i + 1] == 'x' && written.Substring(i + 2, 4).All(Uri.IsHexDigit)
				&& (written[i + 6] == '_' || Escaped(written[i + 6]));
			length += Escaped(c) || escapeLike ? 7 : 1;
		}

		return length;
	}

	/// <summary>A header's name cut to a length between characters: before a surrogate that would end it.</summary>
	/// <param name="text">The name.</param>
	/// <param name="length">The length.</param>
	/// <returns>The name cut.</returns>
	private static string Cut(string text, int length) =>
		text.Length <= length ? text : text.Substring(0, char.IsHighSurrogate(text[length - 1]) ? length - 1 : length);

	/// <summary>A text as the writer writes it, as UTF-8: a lone surrogate becomes U+FFFD.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text written.</returns>
	private static string Written(string text) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));

	/// <summary>A random text of pieces, cut to a length: often short, often around 255 characters.</summary>
	/// <param name="random">The randomness.</param>
	/// <param name="length">The length, or -1 for a random one.</param>
	/// <returns>The text.</returns>
	private static string Text(Random random, int length = -1)
	{
		if (length < 0)
		{
			length = random.Next(3) switch
			{
				0 => 1 + random.Next(8),
				1 => MaxText - 4 + random.Next(5),
				_ => 1 + random.Next(MaxText),
			};
		}

		var text = new StringBuilder();
		while (text.Length < length)
		{
			text.Append(Pieces[random.Next(Pieces.Length)]);
		}

		return text.ToString(0, length);
	}

	/// <summary>A column's input: its header name, and its list and prompt.</summary>
	/// <param name="Name">Its header name.</param>
	/// <param name="Letters">Its letters.</param>
	/// <param name="List">Its list, or <see langword="null"/>.</param>
	/// <param name="Prompt">Its prompt, or <see langword="null"/>.</param>
	private sealed record ColumnInput(string Name, string Letters, string[]? List, string? Prompt);

	/// <summary>A sheet's input: its name, header, columns' settings, rows, and what makes the writer refuse it.</summary>
	private sealed class SheetInput
	{
		/// <summary>Its name.</summary>
		public string Name { get; private init; } = "";

		/// <summary>Its header's names, distinct.</summary>
		public List<string> Header { get; private init; } = [];

		/// <summary>Its columns.</summary>
		public List<ColumnInput> Columns { get; private init; } = [];

		/// <summary>The options it is added with.</summary>
		public ExcelSheetOptions Options { get; private init; } = new();

		/// <summary>The rows after its header.</summary>
		public int Rows { get; private init; }

		/// <summary>What makes the writer refuse it, by the design's rules, or <see langword="null"/> when it is taken.</summary>
		public string? Refusal { get; private init; }

		/// <summary>A random sheet.</summary>
		/// <param name="random">The randomness.</param>
		/// <param name="name">Its name.</param>
		/// <param name="plant">Whether to plant a setting Excel wouldn't open.</param>
		/// <returns>The sheet.</returns>
		public static SheetInput Of(Random random, string name, bool plant)
		{
			var header = new List<string>();
			var count = 1 + random.Next(6);
			while (header.Count < count)
			{
				// Names of up to 40 characters, so that some prompts' titles are cut, between characters or before a pair.
				var candidate = random.Next(3) == 0 ? Text(random, 28 + random.Next(13)) : Text(random, 1 + random.Next(10));
				// Distinct as written: names that differ only in lone surrogates are written alike, as U+FFFD.
				if (!header.Any(h => Written(h) == Written(candidate)))
				{
					header.Add(candidate);
				}
			}

			var columns = new List<ColumnInput>();
			var options = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal);
			string? refusal = null;
			for (var c = 0; c < header.Count; c++)
			{
				string[]? list = null;
				string? prompt = null;
				if (random.Next(3) > 0)
				{
					var values = new List<string>();
					for (var v = random.Next(2) == 0 ? 1 + random.Next(4) : 1 + random.Next(40); v > 0; v--)
					{
						// Repeats now and then.
						values.Add(values.Count > 0 && random.Next(6) == 0 ? values[random.Next(values.Count)] : Text(random));
					}

					list = [.. values];
				}

				if (random.Next(3) > 0)
				{
					prompt = Text(random);
					while (WrittenLength(prompt) > MaxText)
					{
						prompt = Cut(prompt, prompt.Length - 1);
					}
				}

				if (plant && refusal is null && random.Next(2) == 0)
				{
					switch (random.Next(5))
					{
						case 0:
							list = [.. list ?? ["a"], Text(random, MaxText + 1 + random.Next(10))];
							refusal = "a list's value has more than 255 characters";

							break;
						case 1:
							prompt = Text(random, MaxText + 1 + random.Next(10));
							refusal = "a prompt has more than 255 characters";

							break;
						case 4:
							// 255 characters at most, more as written: each control character an escape of 7.
							prompt = new string('a', 220 + random.Next(30)) + Units(1, 2, 3, 0x1F, 0xFFFE, 0xFFFF);
							refusal = "a prompt is longer than 255 characters as written";

							break;
						case 2:
							list = [];
							refusal = "a list is empty";

							break;
						default:
							prompt = "";
							refusal = "a prompt is empty";

							break;
					}
				}

				columns.Add(new ColumnInput(header[c], CellReference.ColumnName(c), list, prompt));
				if (list is not null || prompt is not null)
				{
					// By the name as written: a lone surrogate in a header's name is written, and found, as U+FFFD.
					options[Written(header[c])] = new ExcelColumnOptions { List = list, Prompt = prompt };
				}
			}

			var protect = random.Next(2) == 0;
			string? password = protect && random.Next(2) == 0 ? Text(random, 1 + random.Next(MaxText)) : null;
			if (plant && refusal is null)
			{
				protect = true;
				password = Text(random, MaxText + 1 + random.Next(10));
				refusal = "a password has more than 255 characters";
			}

			return new SheetInput
			{
				Name = name,
				Header = header,
				Columns = columns,
				Options = new ExcelSheetOptions
				{
					Title = random.Next(4) == 0 ? "Title" : null,
					Protect = protect,
					Password = password,
					Columns = options.Count == 0 ? null : options,
					AutoFit = random.Next(2) == 0 ? 0 : 100,
				},
				Rows = random.Next(4),
				Refusal = refusal,
			};
		}
	}
}
