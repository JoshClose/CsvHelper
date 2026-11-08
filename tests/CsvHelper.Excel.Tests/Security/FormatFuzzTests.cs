using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of date and time formats, as Phase 4's "Security and fuzzing" describes: random .NET formats (standard
/// ones, valid or not, and custom ones of every token, literals, quotes, escapes and stray letters), in cultures of
/// several calendars, for each kind of value, with random values at Excel's edges and away from them.
/// <list type="bullet">
/// <item><description>
/// The translator throws nothing, and gives no code or one that <see cref="FormatCheck"/> passes and the reader takes
/// for a date's; and none where this test's own reading of the format says it can't (another calendar, a day or a month
/// without a year, an offset, an era, <c>F</c>, four or more fraction digits, a year of one, three or five digits, <c>U</c>).
/// </description></item>
/// <item><description>
/// Reading a value's text back never keeps a value whose text differs: the cell's value, as the member's type, formats to
/// the field's text, of any <see cref="DateTimeKind"/> or offset, since the cell has neither.
/// </description></item>
/// <item><description>
/// End to end, through workbooks of random formats' columns (<see cref="ExcelWorkbookWriter.AddSheet(string, MemberFormats)"/>),
/// fields of the column's kind and others, in its format and others, under each <see cref="ExcelDateTimeOffsets"/>: each
/// cell is a date of the translation's code and the value read back, or a text cell of the field's text, as the
/// translation says, and the workbook passes the validator.
/// </description></item>
/// </list>
/// <c>CSVHELPER_EXCEL_FUZZ_FORMATS</c> sets the number of formats (and a workbook for each 20 of them), and
/// <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the run translated.</param>
public class FormatFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of formats a run translates, unless <c>CSVHELPER_EXCEL_FUZZ_FORMATS</c> says otherwise.</summary>
	private const int DefaultFormats = 3000;

	/// <summary>The formats for each workbook the end-to-end test writes.</summary>
	private const int FormatsPerWorkbook = 20;

	/// <summary>The values each format's text is read back from.</summary>
	private const int ValuesPerFormat = 4;

	/// <summary>The milliseconds of a day.</summary>
	private const long MillisecondsPerDay = 86_400_000;

	/// <summary>The serial of 1900-03-01, the first day counted from 1899-12-30 rather than 1899-12-31.</summary>
	private const long March1900 = 61 * MillisecondsPerDay;

	/// <summary>The milliseconds from 0001-01-01 to 1899-12-31, which serials before 1900-03-01 count from.</summary>
	private static readonly long Before = new DateTime(1899, 12, 31).Ticks / TimeSpan.TicksPerMillisecond;

	/// <summary>The milliseconds from 0001-01-01 to 1899-12-30, which serials from 1900-03-01 count from.</summary>
	private static readonly long After = new DateTime(1899, 12, 30).Ticks / TimeSpan.TicksPerMillisecond;

	/// <summary>
	/// The pieces of custom formats: every token in its lengths, separators, literals quoted and escaped, <c>%</c>, letters
	/// that aren't tokens, and quotes and escapes left open.
	/// </summary>
	private static readonly string[] Tokens =
	[
		"d", "dd", "ddd", "dddd", "ddddd", "M", "MM", "MMM", "MMMM", "MMMMM", "y", "yy", "yyy", "yyyy", "yyyyy", "h", "hh", "H", "HH",
		"m", "mm", "s", "ss", "f", "ff", "fff", "ffff", "fffffff", "F", "FF", "FFF", "t", "tt", "z", "zz", "zzz", "K", "g", "gg",
		":", "/", "-", ".", ",", " ", "T", "'at'", "\"o'clock\"", "'it\\'s'", "\\d", "\\\\", "%d", "%M", "%y", "%h", "%H", "x", "Q", "e", "E",
		"[", "]", "$", "@", "#", "0", "_x0041_", Units(0x5E74), Units(0x6708), Units(0x65E5), "'", "\"", "\\", "%",
	];

	/// <summary>The standard formats, and letters that are none (which .NET refuses), but <c>O</c>, .NET's default.</summary>
	private static readonly string[] Standard = ["d", "D", "f", "F", "g", "G", "m", "M", "r", "R", "s", "t", "T", "u", "U", "y", "Y", "Q", "x", "K", "z", "e"];

	/// <summary>
	/// The cultures: the invariant one, Gregorian ones of each kind of names and separators, and those of other calendars
	/// (Hijri, Persian, Thai Buddhist).
	/// </summary>
	private static readonly string[] CultureNames =
	[
		"", "en-US", "en-GB", "de-DE", "fr-FR", "ja-JP", "zh-CN", "ko-KR", "ar-SA", "fa-IR", "th-TH", "he-IL", "hi-IN", "ru-RU", "tr-TR",
		"sv-SE", "fi-FI", "pt-BR", "es-ES", "nl-NL", "pl-PL", "cs-CZ", "el-GR", "vi-VN", "uk-UA",
	];

	/// <summary>The cultures this machine has.</summary>
	private static readonly Lazy<List<CultureInfo>> Cultures = new(() => CultureNames.Select(TryCulture).OfType<CultureInfo>().ToList());

	/// <summary>The kinds of values a format formats on this target.</summary>
	private static readonly CellKind[] Kinds =
	[
		CellKind.DateTime, CellKind.DateTimeOffset,
#if NET
		CellKind.DateOnly, CellKind.TimeOnly,
#endif
	];

	/// <summary>Dates at Excel's edges and .NET's.</summary>
	private static readonly DateTime[] Edges =
	[
		new(1, 1, 1), new(1899, 12, 30), new DateTime(1899, 12, 31, 23, 59, 59).AddTicks(9_999_999), new(1900, 1, 1), new(1900, 2, 28, 12, 0, 0),
		new(1900, 3, 1), new(2024, 2, 29), new DateTime(9999, 12, 31, 23, 59, 59).AddTicks(9_999_999), new DateTime(2024, 1, 15, 23, 59, 59, 999).AddTicks(5_000),
	];

	[Fact]
	public void TranslatesRandomFormatsOrLeavesThemText()
	{
		var formats = Setting("CSVHELPER_EXCEL_FUZZ_FORMATS", DefaultFormats);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + formats && failures.Count < MaxFailures; seed++)
		{
			try
			{
				if (CheckFormat(seed, tally) is { } problem)
				{
					failures.Add($"Seed {seed}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed}: {Describe(e)}");
			}
		}

		Report(output, $"Seeds {first} to {first + formats - 1}:", tally);
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_FORMATS");
	}

	[Fact]
	public async Task WritesRandomFormatsAsTheirText()
	{
		var workbooks = Math.Max(1, Setting("CSVHELPER_EXCEL_FUZZ_FORMATS", DefaultFormats) / FormatsPerWorkbook);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			try
			{
				if (await CheckWorkbookAsync(seed, tally) is { } problem)
				{
					failures.Add($"Seed {seed}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed}: {Describe(e)}");
			}
		}

		Report(output, $"Seeds {first} to {first + workbooks - 1}:", tally);
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_FORMATS");
	}

	/// <summary>
	/// Translates a seed's format for a culture and a kind of value, and checks the translation, then reads back the texts
	/// of a few values.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <param name="tally">Takes how the format and the values were taken.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? CheckFormat(int seed, SortedDictionary<string, int> tally)
	{
		var random = new Random(seed);
		var format = RandomFormat(random);
		var culture = RandomCulture(random);
		var kind = Kinds[random.Next(Kinds.Length)];
		var describe = $"{Show(format)} in {Name(culture)} for {kind}";
		DateFormat? translation;
		try
		{
			translation = FormatTranslator.Translate(format, kind, culture);
		}
		catch (Exception e)
		{
			return $"{describe}: the translator threw {Describe(e)}";
		}

		var rule = Rule(format, culture);
		Count(tally, translation is not null ? "formats: translated" : rule is not null ? "formats: not translated, as the rules say" : "formats: not translated");
		if (translation is null)
		{
			return null;
		}

		if (rule is not null)
		{
			return $"{describe}: translated as {Show(translation.Code)}, though {rule}";
		}

		string? problem;
		try
		{
			problem = CodeProblem(translation);
		}
		catch (Exception e)
		{
			return $"{describe}: the translation {Show(translation.Code)} threw {Describe(e)}";
		}

		if (problem is not null)
		{
			return $"{describe}: {problem}";
		}

		for (var n = 0; n < ValuesPerFormat; n++)
		{
			var value = Value.Random(random, kind);
			if (value.Format(format, culture) is not { } text)
			{
				Count(tally, "values: not formattable");

				continue;
			}

			bool read;
			long milliseconds;
			try
			{
				read = translation.TryRead(Encoding.UTF8.GetBytes(text), out milliseconds);
			}
			catch (Exception e)
			{
				return $"{describe}: reading {Show(text)} back threw {Describe(e)}";
			}

			if (read && !translation.HasDate && milliseconds < 0)
			{
				return $"{describe}, code {Show(translation.Code)}: {Show(text)} of {value}, a time of day, reads back before 1900";
			}

			if (!read || milliseconds < 0)
			{
				Count(tally, read ? "values: text, before 1900" : "values: text, not read back");

				continue;
			}

			if (CellProblem(value, format, culture, text, milliseconds, translation.HasDate) is { } shown)
			{
				return $"{describe}, code {Show(translation.Code)}: {shown}";
			}

			Count(tally, "values: typed");

			// Text that .NET reads but doesn't write (in another case, as parsing ignores case) is kept only as a value
			// that formats to it.
			foreach (var other in new[] { text.ToLowerInvariant(), text.ToUpperInvariant() }.Where(t => t != text).Distinct())
			{
				if (translation.TryRead(Encoding.UTF8.GetBytes(other), out var otherMilliseconds) && otherMilliseconds >= 0 &&
					CellProblem(value, format, culture, other, otherMilliseconds, translation.HasDate) is { } otherShown)
				{
					return $"{describe}, code {Show(translation.Code)}, text in another case: {otherShown}";
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Writes a seed's workbook of random formats' columns and checks each cell against the translation: a date of its
	/// code and the value read back, or the field's text.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <param name="tally">Takes the cells.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckWorkbookAsync(int seed, SortedDictionary<string, int> tally)
	{
		var random = new Random(seed);
		var async = random.Next(2) == 1;
		var options = new ExcelWriterOptions
		{
			DateTimeOffsets = (ExcelDateTimeOffsets)random.Next(3),
			Strings = random.Next(2) == 0 ? ExcelStrings.Inline : ExcelStrings.Shared,
			Csv = new CsvWriterOptions { ValidateFieldCount = false },
		};
		var columns = Enumerable.Range(0, random.Next(1, 7)).Select(_ => new Column(RandomFormat(random), random.Next(4) == 0 ? null : RandomCulture(random), Kinds[random.Next(Kinds.Length)])).ToList();
		var rows = Enumerable.Range(0, random.Next(1, 6)).Select(_ => columns.Select(c => Field.Of(random, c, options)).ToList()).ToList();
		var members = new MemberFormats(columns.Select((c, i) => new MemberFormat("c" + i, 0, -1, c.Format, c.Culture)).ToArray());
		var describe = $"{options.DateTimeOffsets}, columns {string.Join(", ", columns)}";
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Formats", members);
			for (var i = 0; i < columns.Count; i++)
			{
				writer.WriteField("c" + i);
			}

			await Written.EndRecordAsync(writer, a);
			foreach (var row in rows)
			{
				foreach (var field in row)
				{
					field.WriteTo(writer);
				}

				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async, options);

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return $"{describe}: the validator says: {string.Join("; ", errors.Take(3))}";
		}

		var cells = SheetCells(workbook);
		var read = Written.Read(workbook)[0].Rows;
		for (var r = 0; r < rows.Count; r++)
		{
			for (var c = 0; c < columns.Count; c++)
			{
				var field = rows[r][c];
				var reference = CellReference.ColumnName(c) + (r + 2).ToString(CultureInfo.InvariantCulture);
				var at = $"{describe}: {reference}, {field}";
				cells.TryGetValue(reference, out var cell);
				if (string.IsNullOrEmpty(field.Text))
				{
					// A field that couldn't be formatted is written as null, and an empty text (FF of whole seconds) is empty too.
					if (cell is not null)
					{
						return $"{at}: a cell, for an empty field";
					}

					continue;
				}

				if (cell is null)
				{
					return $"{at}: no cell";
				}

				if (field.Expected is { } expected)
				{
					Count(tally, "cells: dates");
					if (cell.Type is not null || cell.Code != expected.Code || cell.Value != expected.Milliseconds / (double)MillisecondsPerDay)
					{
						return $"{at}: a cell of type {cell.Type ?? "number"}, code {Show(cell.Code ?? "")}, value {cell.Value}, for a date of {Show(expected.Code)}, serial {expected.Milliseconds / (double)MillisecondsPerDay}";
					}

					continue;
				}

				Count(tally, "cells: text");
				var shown = r + 1 < read.Count ? read[r + 1].Split('|').ElementAtOrDefault(c) : null;
				if (cell.Type is not ("inlineStr" or "s") || !string.Equals(shown, field.Text, StringComparison.Ordinal))
				{
					return $"{at}: a cell of type {cell.Type ?? "number"} read as {Show(shown ?? "nothing")}, for text";
				}
			}
		}

		return null;
	}

	/// <summary>What is wrong with a translation's code, or <see langword="null"/>.</summary>
	/// <param name="translation">The translation.</param>
	/// <returns>The problem, or <see langword="null"/>.</returns>
	private static string? CodeProblem(DateFormat translation)
	{
		if (FormatCheck.Problem(translation.Code) is { } problem)
		{
			return $"the code {Show(translation.Code)} fails FormatCheck: {problem}";
		}

		if (NumberFormats.Classify(translation.Code) != SheetCellKind.DateNumber)
		{
			return $"the code {Show(translation.Code)} reads as {NumberFormats.Classify(translation.Code)}, not a date's";
		}

		var width = translation.Width;

		return double.IsNaN(width) || double.IsInfinity(width) || width < 0 ? $"the code {Show(translation.Code)} has the width {width}" : null;
	}

	/// <summary>
	/// What is wrong with a value's cell, or <see langword="null"/>: the cell's value, as the value's type, of any kind or
	/// offset, must format to the field's text, since the cell keeps neither.
	/// </summary>
	/// <param name="value">The value written.</param>
	/// <param name="format">Its format.</param>
	/// <param name="culture">Its culture.</param>
	/// <param name="text">Its text.</param>
	/// <param name="milliseconds">The cell's serial, in milliseconds.</param>
	/// <param name="hasDate">Whether the format has a date, rather than a time of day alone.</param>
	/// <returns>The problem, or <see langword="null"/>.</returns>
	private static string? CellProblem(Value value, string format, IFormatProvider culture, string text, long milliseconds, bool hasDate)
	{
		if (!hasDate && milliseconds >= MillisecondsPerDay)
		{
			return $"{Show(text)} of {value} is a time of day of {milliseconds} ms";
		}

		var cell = new DateTime(hasDate ? (milliseconds + (milliseconds < March1900 ? Before : After)) * TimeSpan.TicksPerMillisecond : milliseconds * TimeSpan.TicksPerMillisecond);
		foreach (var shown in value.Shown(cell, format, culture))
		{
			if (!string.Equals(shown, text, StringComparison.Ordinal))
			{
				return $"{Show(text)} of {value} was kept as {cell.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}, which shows {Show(shown)}";
			}
		}

		return null;
	}

	/// <summary>
	/// Why this test's own reading of a format says Excel can't show its text, or <see langword="null"/>: a calendar other
	/// than the Gregorian (but for <c>r</c>, <c>s</c> and <c>u</c>, which are the invariant culture's), <c>U</c>, or a custom
	/// format's day or month without a year, offset, era, <c>F</c>, four or more fraction digits, or a year of one, three or
	/// five or more digits.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <param name="culture">Its culture.</param>
	/// <returns>The reason, or <see langword="null"/>.</returns>
	private static string? Rule(string format, CultureInfo culture)
	{
		if (format.Length == 1)
		{
			return format switch
			{
				"r" or "R" or "s" or "u" => null,
				"U" => "U depends on the machine's time zone",
				_ => culture.DateTimeFormat.Calendar is not GregorianCalendar ? "its culture's calendar isn't the Gregorian" : null,
			};
		}

		if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
		{
			return "its culture's calendar isn't the Gregorian";
		}

		// A day or a month without a year reads back in the year 1.
		var tokens = CustomTokens(format).ToList();
		if (tokens.Any(t => t.Letter is 'd' or 'M') && !tokens.Any(t => t.Letter == 'y'))
		{
			return "it has a date without a year";
		}

		foreach (var (letter, run) in tokens)
		{
			switch (letter)
			{
				case 'z' or 'K':
					return "it has an offset";
				case 'g':
					return "it has an era";
				case 'F':
					return "it has F";
				case 'f' when run >= 4:
					return "it has four or more fraction digits";
				case 'y' when run is 1 or 3 or >= 5:
					return $"it has a year of {run} digits";
			}
		}

		return null;
	}

	/// <summary>
	/// The tokens of a custom format, as runs of one letter: outside quoted text (in which a backslash escapes), after
	/// <c>%</c>, and not escaped by a backslash.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <returns>Each token's letter and its run's length.</returns>
	private static IEnumerable<(char Letter, int Run)> CustomTokens(string format)
	{
		for (var i = 0; i < format.Length;)
		{
			var c = format[i];
			if (c is '\'' or '"')
			{
				var j = i + 1;
				while (j < format.Length && format[j] != c)
				{
					j += format[j] == '\\' ? 2 : 1;
				}

				i = j + 1;
			}
			else if (c == '\\')
			{
				i += 2;
			}
			else if ("dfFghHKmMstyz".IndexOf(c) >= 0)
			{
				var run = 1;
				while (i + run < format.Length && format[i + run] == c)
				{
					run++;
				}

				yield return (c, run);
				i += run;
			}
			else
			{
				i++;
			}
		}
	}

	/// <summary>The cells of a workbook's one sheet, by reference: their type, their number format's code and their value.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The cells.</returns>
	private static Dictionary<string, Cell> SheetCells(byte[] workbook)
	{
		XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
		var styles = XDocument.Parse(Written.Entry(workbook, "xl/styles.xml"));
		var codes = styles.Descendants(main + "numFmt").ToDictionary(n => (int)n.Attribute("numFmtId")!, n => CorpusFiles.DecodeEscapes((string)n.Attribute("formatCode")!));
		codes[46] = "[h]:mm:ss";
		var formats = styles.Descendants(main + "cellXfs").Single().Elements(main + "xf").Select(x => (int)x.Attribute("numFmtId")!).ToList();
		var sheet = XDocument.Parse(Written.Entry(workbook, "xl/worksheets/sheet1.xml"));

		return sheet.Descendants(main + "c").ToDictionary(c => (string)c.Attribute("r")!, c =>
		{
			var style = (int?)c.Attribute("s") ?? 0;
			var value = c.Element(main + "v")?.Value;

			return new Cell(
				(string?)c.Attribute("t"),
				style > 0 && codes.TryGetValue(formats[style], out var code) ? code : null,
				value is null ? double.NaN : double.Parse(value, CultureInfo.InvariantCulture));
		});
	}

	/// <summary>A random format: one in four standard, the rest of one to six custom tokens.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The format.</returns>
	private static string RandomFormat(Random random)
	{
		if (random.Next(4) == 0)
		{
			return Standard[random.Next(Standard.Length)];
		}

		var format = new StringBuilder();
		for (var n = random.Next(1, 7); n > 0; n--)
		{
			format.Append(Tokens[random.Next(Tokens.Length)]);
		}

		return format.ToString();
	}

	/// <summary>A random culture of those this machine has.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The culture.</returns>
	private static CultureInfo RandomCulture(Random random) => Cultures.Value[random.Next(Cultures.Value.Count)];

	/// <summary>A culture, if this machine has it.</summary>
	/// <param name="name">Its name.</param>
	/// <returns>The culture, or <see langword="null"/>.</returns>
	private static CultureInfo? TryCulture(string name)
	{
		try
		{
			return CultureInfo.GetCultureInfo(name);
		}
		catch (CultureNotFoundException)
		{
			return null;
		}
	}

	/// <summary>A culture's name for a failure.</summary>
	/// <param name="culture">The culture.</param>
	/// <returns>Its name, or <c>the invariant culture</c>.</returns>
	private static string Name(CultureInfo? culture) => culture is null ? "the writer's culture" : culture.Name.Length == 0 ? "the invariant culture" : culture.Name;

	/// <summary>Writes a tally.</summary>
	/// <param name="output">The test's output.</param>
	/// <param name="title">Its first line.</param>
	/// <param name="tally">The counts.</param>
	private static void Report(ITestOutputHelper output, string title, SortedDictionary<string, int> tally)
	{
		output.WriteLine(title);
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}
	}

	/// <summary>A cell of the sheet: its type (none for a number), its number format's code, and its value.</summary>
	/// <param name="Type">Its <c>t</c>, or <see langword="null"/> for a number.</param>
	/// <param name="Code">Its number format's code, or <see langword="null"/> for General.</param>
	/// <param name="Value">Its value, or NaN for none.</param>
	private sealed record Cell(string? Type, string? Code, double Value);

	/// <summary>A date cell a field must make: its code and its serial in milliseconds.</summary>
	/// <param name="Code">The translation's code.</param>
	/// <param name="Milliseconds">The serial.</param>
	private sealed record DateCell(string Code, long Milliseconds);

	/// <summary>A column of the end-to-end test: its member's format and culture, and the kind of its values.</summary>
	/// <param name="Format">The format.</param>
	/// <param name="Culture">The culture, or <see langword="null"/> for the writer's.</param>
	/// <param name="Kind">The kind of most of its values.</param>
	private sealed record Column(string Format, CultureInfo? Culture, CellKind Kind)
	{
		/// <summary>The column for a failure.</summary>
		/// <returns>The description.</returns>
		public override string ToString() => $"{Show(Format)} in {Name(Culture)} for {Kind}";
	}

	/// <summary>
	/// A field of the end-to-end test: a value of the column's kind or another, in the column's format or another, with the
	/// column's culture; its text, and the date cell it must make, if any.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="format">The format it is written in.</param>
	/// <param name="provider">The culture it is written in.</param>
	/// <param name="text">Its text, or <see langword="null"/> when it can't be formatted, and is written as null.</param>
	/// <param name="expected">The date cell it must make, or <see langword="null"/> for a text cell.</param>
	private sealed class Field(Value value, string format, IFormatProvider provider, string? text, DateCell? expected)
	{
		/// <summary>Its text, or <see langword="null"/> when it can't be formatted.</summary>
		public string? Text { get; } = text;

		/// <summary>The date cell it must make, or <see langword="null"/> for a text cell.</summary>
		public DateCell? Expected { get; } = expected;

		/// <summary>
		/// A random field of a column, and the cell the column's translation says it makes: a date cell when the column's
		/// format translates for the field's kind, its text reads back, and it isn't before 1900; an offset's only with
		/// <see cref="ExcelDateTimeOffsets.ClockTime"/>.
		/// </summary>
		/// <param name="random">The seed's random numbers.</param>
		/// <param name="column">The column.</param>
		/// <param name="options">The writer's options.</param>
		/// <returns>The field.</returns>
		public static Field Of(Random random, Column column, ExcelWriterOptions options)
		{
			var kind = random.Next(5) == 0 ? Kinds[random.Next(Kinds.Length)] : column.Kind;
			var value = Value.Random(random, kind);
			var format = random.Next(8) == 0 ? RandomFormat(random) : column.Format;
			var provider = column.Culture ?? (IFormatProvider)CultureInfo.InvariantCulture;
			var text = value.Format(format, provider);
			DateCell? expected = null;
			if (text is not null && (kind != CellKind.DateTimeOffset || options.DateTimeOffsets == ExcelDateTimeOffsets.ClockTime) &&
				FormatTranslator.Translate(column.Format, kind, provider) is { } translation &&
				translation.TryRead(Encoding.UTF8.GetBytes(text), out var milliseconds) && milliseconds >= 0)
			{
				expected = new DateCell(translation.Code, milliseconds);
			}

			return new Field(value, format, provider, text, expected);
		}

		/// <summary>Writes the field: its value in its format and culture, or null when that can't be formatted.</summary>
		/// <param name="writer">The sheet's writer.</param>
		public void WriteTo(CsvWriter writer)
		{
			if (Text is null)
			{
				writer.WriteField((string?)null);

				return;
			}

			value.WriteTo(writer, format, provider);
		}

		/// <summary>The field for a failure.</summary>
		/// <returns>The description.</returns>
		public override string ToString() => $"{value} in {Show(format)}, {Show(Text ?? "not formattable")}";
	}

	/// <summary>A value of a kind: a date and time of a <see cref="DateTimeKind"/>, with an offset for a <see cref="DateTimeOffset"/>.</summary>
	/// <param name="kind">The kind.</param>
	/// <param name="dateTime">The date and time.</param>
	/// <param name="offset">The offset, for a <see cref="DateTimeOffset"/>.</param>
	private sealed class Value(CellKind kind, DateTime dateTime, TimeSpan offset)
	{
		/// <summary>
		/// A random value: an edge one time in eight, else of a year near Excel's first ones or of any, at a random time
		/// cut to the minute, the second, the millisecond or not.
		/// </summary>
		/// <param name="random">The seed's random numbers.</param>
		/// <param name="kind">The kind.</param>
		/// <returns>The value.</returns>
		public static Value Random(Random random, CellKind kind)
		{
			DateTime date;
			if (random.Next(8) == 0)
			{
				date = Edges[random.Next(Edges.Length)];
			}
			else
			{
				var year = random.Next(3) == 0 ? random.Next(1, 10000) : random.Next(1890, 2110);
				var day = new DateTime(year, 1, 1).AddDays(random.Next(DateTime.IsLeapYear(year) ? 366 : 365));
				var ticks = (long)(random.NextDouble() * TimeSpan.TicksPerDay);
				var unit = random.Next(4) switch { 0 => TimeSpan.TicksPerMinute, 1 => TimeSpan.TicksPerSecond, 2 => TimeSpan.TicksPerMillisecond, _ => 1 };
				date = day.AddTicks(ticks - (ticks % unit));
			}

			return new Value(kind, DateTime.SpecifyKind(date, (DateTimeKind)random.Next(3)), TimeSpan.FromMinutes(15 * random.Next(-56, 57)));
		}

		/// <summary>The value's text in a format, or <see langword="null"/> when .NET can't format it so.</summary>
		/// <param name="format">The format.</param>
		/// <param name="provider">The culture.</param>
		/// <returns>The text, or <see langword="null"/>.</returns>
		public string? Format(string format, IFormatProvider provider)
		{
			try
			{
				return kind switch
				{
					CellKind.DateTimeOffset => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), offset).ToString(format, provider),
#if NET
					CellKind.DateOnly => DateOnly.FromDateTime(dateTime).ToString(format, provider),
					CellKind.TimeOnly => TimeOnly.FromDateTime(dateTime).ToString(format, provider),
#endif
					_ => dateTime.ToString(format, provider),
				};
			}
			catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException)
			{
				// A format .NET refuses, a date its calendar lacks, or a UTC time off DateTime's range.
				return null;
			}
		}

		/// <summary>
		/// The texts a cell's value shows as the value's type, which must all be the field's: a <see cref="DateTime"/> of no
		/// kind and in UTC, a <see cref="DateTimeOffset"/> at offset zero and another (but in the formats that show UTC).
		/// </summary>
		/// <param name="cell">The cell's value.</param>
		/// <param name="format">The format.</param>
		/// <param name="provider">The culture.</param>
		/// <returns>The texts.</returns>
		public IEnumerable<string> Shown(DateTime cell, string format, IFormatProvider provider)
		{
			switch (kind)
			{
				case CellKind.DateTimeOffset:
					yield return Try(() => new DateTimeOffset(cell, TimeSpan.Zero).ToString(format, provider));
					if (format is not ("u" or "r" or "R") && cell.Ticks >= TimeSpan.TicksPerDay)
					{
						yield return Try(() => new DateTimeOffset(cell, TimeSpan.FromMinutes(90)).ToString(format, provider));
					}

					break;
#if NET
				case CellKind.DateOnly:
					yield return Try(() => DateOnly.FromDateTime(cell).ToString(format, provider));

					break;
				case CellKind.TimeOnly:
					yield return Try(() => TimeOnly.FromDateTime(cell).ToString(format, provider));

					break;
#endif
				default:
					yield return Try(() => DateTime.SpecifyKind(cell, DateTimeKind.Unspecified).ToString(format, provider));
					yield return Try(() => DateTime.SpecifyKind(cell, DateTimeKind.Utc).ToString(format, provider));

					break;
			}
		}

		/// <summary>Writes the value in a format and a culture, as the record writer writes a member.</summary>
		/// <param name="writer">The sheet's writer.</param>
		/// <param name="format">The format.</param>
		/// <param name="provider">The culture.</param>
		public void WriteTo(CsvWriter writer, string format, IFormatProvider provider)
		{
			switch (kind)
			{
				case CellKind.DateTimeOffset:
					writer.WriteField(new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), offset), format.AsSpan(), provider);

					break;
#if NET
				case CellKind.DateOnly:
					writer.WriteField(DateOnly.FromDateTime(dateTime), format.AsSpan(), provider);

					break;
				case CellKind.TimeOnly:
					writer.WriteField(TimeOnly.FromDateTime(dateTime), format.AsSpan(), provider);

					break;
#endif
				default:
					writer.WriteField(dateTime, format.AsSpan(), provider);

					break;
			}
		}

		/// <summary>The value for a failure.</summary>
		/// <returns>The description.</returns>
		public override string ToString() =>
			$"{kind} {dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}{(kind == CellKind.DateTimeOffset ? " " + offset : kind == CellKind.DateTime ? " " + dateTime.Kind : "")}";

		/// <summary>A text, or the exception that formatting it threw.</summary>
		/// <param name="format">Formats the text.</param>
		/// <returns>The text, or the exception's type.</returns>
		private static string Try(Func<string> format)
		{
			try
			{
				return format();
			}
			catch (Exception e)
			{
				return $"<{e.GetType().Name}>";
			}
		}
	}
}
