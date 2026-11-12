using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of Excel's number format codes, as columns' formats (<see cref="ExcelColumnOptions.Format"/> and
/// <see cref="ExcelColumnAttribute.Format"/>) take them: random codes of the grammar's pieces (placeholders, separators,
/// exponents, fractions, percents, literals quoted, escaped and open, fills and spaces, colors, conditions, locales,
/// <c>[DBNum]</c>, elapsed times, dates and times in every length, <c>@</c> and <c>General</c>, sections, and characters
/// that are none of these), and lengths around 255. <see cref="FormatCheck"/> throws nothing, and a column's options take
/// a code exactly when it passes. Each code that passes measures every kind of value without throwing
/// (<see cref="NumberFormatWidth"/>), at a width that is a number of pixels, and workbooks of a column for each, auto-fitted
/// over numbers, dates, durations, booleans and text, pass the Open XML SDK's validator and read back.
/// <c>CSVHELPER_EXCEL_FUZZ_CODES</c> sets the number of codes, <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed, and
/// <c>CSVHELPER_EXCEL_FUZZ_SAVE</c> a folder to save each workbook in, for the corpus tool's <c>fuzzed</c> command to
/// open in Excel, and <c>CSVHELPER_EXCEL_FUZZ_CODES_PER_WORKBOOK</c> the codes of each (20 by default; 1 tells which code
/// Excel repairs).
/// </summary>
/// <param name="output">Shows what the run checked.</param>
public class FormatCodeFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of codes a run checks, unless <c>CSVHELPER_EXCEL_FUZZ_CODES</c> says otherwise.</summary>
	private const int DefaultCodes = 10000;

	/// <summary>The codes that pass that each workbook has, one column each, unless <c>CSVHELPER_EXCEL_FUZZ_CODES_PER_WORKBOOK</c> says otherwise.</summary>
	private const int DefaultCodesPerWorkbook = 20;

	/// <summary>The pieces of codes.</summary>
	private static readonly string[] Pieces =
	[
		// Placeholders, separators, exponents, fractions and percents.
		"0", "0", "#", "?", "0.00", "#,##0", ".", ",", ",,", "%", Units(0x2030), "E+", "E-", "e+", "E+00", "E-0", "/", "# ?/?", "??/??", "0/8",
		// Literals: bare, quoted, escaped, open; fills and spaces.
		" ", "-", "+", "(", ")", "$", Units(0x20AC), ":", "^", "{", "}", "<", ">", "=", "!", "&", "'", "~", "|", "\"kg\"", "\"", "\"\"", "\";\"", "\\",
		"\\x", "\\\\", "_", "_)", "_x", "*", "*-", "* ",
		// Brackets: colors, conditions, locales, DBNum, elapsed times, and ones Excel refuses.
		"[Red]", "[red]", "[GREEN]", "[Color10]", "[Color57]", "[Color0]", "[<100]", "[>=1.5]", "[=0]", "[<>-1]", "[ > 1]", "[$-409]", "[$" + Units(0x20AC) + "-407]",
		"[$-411]", "[$-F800]", "[$$-409]", "[DBNum1]", "[DBNum3]", "[DBNum4]", "[h]", "[hh]", "[mm]", "[ss]", "[hm]", "[", "]", "[foo]",
		// Dates and times.
		"y", "yy", "yyy", "yyyy", "yyyyy", "m", "mm", "mmm", "mmmm", "mmmmm", "d", "dd", "ddd", "dddd", "h", "hh", "s", "ss", ".0", ".000", ".0000",
		"AM/PM", "am/pm", "A/P", "a/p", "aaa", "aaaa", "g", "ggg", "e", "ee", "b", "bbbb", "yyyy-mm-dd", "hh:mm:ss", "m/d/yyyy h:mm",
		// Text, General, sections, and characters that are none of these.
		"@", "@", "General", "general", ";", ";", ";;", "`", "k", "B", "N", "x", "Q", Units(0x65E5), Units(0x5E74), Units(0xD83D, 0xDE00), "\t", "\n", Units(1),
		Units(0xA0),
	];

	/// <summary>Numbers each code measures, in days for dates: 0, signs, fractions, large and small, dates, and Excel's last date.</summary>
	private static readonly double[] Numbers = [0, 1, -1, 0.5, -0.25, 1234567.891, -0.001, 1e15, 1e-10, 45306.4375, 2958465.9999999884, 2958466, 1e308, -1e308];

	[Fact]
	public async Task ChecksRandomFormatCodesConsistently()
	{
		var codes = Setting("CSVHELPER_EXCEL_FUZZ_CODES", DefaultCodes);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var save = Environment.GetEnvironmentVariable("CSVHELPER_EXCEL_FUZZ_SAVE");
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var perWorkbook = Math.Max(1, Setting("CSVHELPER_EXCEL_FUZZ_CODES_PER_WORKBOOK", DefaultCodesPerWorkbook));
		var passed = new List<(int Seed, string Code)>();
		for (var seed = first; seed < first + codes && failures.Count < MaxFailures; seed++)
		{
			var code = Code(seed);
			try
			{
				if (Check(code, tally) is { } problem)
				{
					failures.Add($"Seed {seed} ({Show(code)}): {problem}");
				}
				else if (FormatCheck.Problem(code) is null)
				{
					passed.Add((seed, code));
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed} ({Show(code)}): {Describe(e)}");
			}
		}

		for (var start = 0; start < passed.Count && failures.Count < MaxFailures; start += perWorkbook)
		{
			var batch = passed.Skip(start).Take(perWorkbook).ToList();
			try
			{
				if (await WorkbookProblemAsync(batch, save, tally) is { } problem)
				{
					failures.Add($"Seeds {string.Join(", ", batch.Select(b => b.Seed))}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seeds {string.Join(", ", batch.Select(b => b.Seed))}: {Describe(e)}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + codes - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_CODES");
	}

	/// <summary>A random code, from its seed: one to eight pieces, or now and then one piece repeated to around 255 characters.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The code.</returns>
	private static string Code(int seed)
	{
		var random = new Random(seed);
		var code = new StringBuilder();
		if (random.Next(20) == 0)
		{
			var piece = Pieces[random.Next(Pieces.Length)];
			var length = FormatCheck.MaxLength + random.Next(-3, 4);
			while (code.Length < length)
			{
				code.Append(piece);
			}

			return code.ToString(0, length);
		}

		for (var i = random.Next(1, 9); i > 0; i--)
		{
			code.Append(Pieces[random.Next(Pieces.Length)]);
		}

		return code.ToString();
	}

	/// <summary>Checks a code: the check throws nothing, a column's options agree with it, and a code that passes measures every value.</summary>
	/// <param name="code">The code.</param>
	/// <param name="tally">Takes what was checked.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? Check(string code, SortedDictionary<string, int> tally)
	{
		var problem = FormatCheck.Problem(code);
		var options = new ExcelColumnOptions { Format = code }.Problem();
		if ((problem is null) != (options is null))
		{
			return $"the check says {problem ?? "nothing"}, and a column's options {options ?? "nothing"}";
		}

		if (problem is not null)
		{
			Count(tally, "codes refused");

			return null;
		}

		Count(tally, "codes that pass");
		var width = new NumberFormatWidth(code);
		foreach (var number in Numbers)
		{
			foreach (var widest in new[] { false, true })
			{
				var text = width.Number(number, widest);
				if (text is null)
				{
					return $"{number.ToString("R", CultureInfo.InvariantCulture)} has no text";
				}
			}

			var pixels = width.NumberPixels(number);
			if (!(pixels >= 0 && pixels < 1e7))
			{
				return $"{number.ToString("R", CultureInfo.InvariantCulture)} measures {pixels} pixels";
			}
		}

		foreach (var text in new[] { "", "abc", Units(0x65E5, 0x672C), "TRUE" })
		{
			if (width.Text(text) is null || !(width.TextPixels(ColumnWidths.TextPixels(text.AsSpan())) >= 0))
			{
				return $"the text {Show(text)} has no width";
			}
		}

		return null;
	}

	/// <summary>
	/// Writes a workbook of a column for each code, auto-fitted over every kind of value, and checks it: the validator, and
	/// the values read back.
	/// </summary>
	/// <param name="codes">The codes, with their seeds.</param>
	/// <param name="save">A folder to save the workbook in, or <see langword="null"/>.</param>
	/// <param name="tally">Takes what was written.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> WorkbookProblemAsync(List<(int Seed, string Code)> codes, string? save, SortedDictionary<string, int> tally)
	{
		var names = codes.Select(c => "c" + c.Seed.ToString(CultureInfo.InvariantCulture)).ToList();
		var columns = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal);
		for (var i = 0; i < codes.Count; i++)
		{
			columns[names[i]] = new ExcelColumnOptions { Format = codes[i].Code };
		}

		// A row of each kind: numbers, dates, durations, a boolean, nothing (an empty row, which reads back as one) and text.
		object?[] values =
		[
			1234567.891, -0.001, 0, 1e15, new DateTime(2024, 1, 15, 10, 30, 0, 123), DateTime.MaxValue, new DateTime(1900, 1, 1), TimeSpan.FromHours(1.5),
			TimeSpan.FromDays(100000), true, null, "abc",
		];
		var async = codes[0].Seed % 2 == 0;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Codes", new ExcelSheetOptions { Columns = columns });
			foreach (var name in names)
			{
				writer.WriteField(name);
			}

			await Written.EndRecordAsync(writer, a);
			foreach (var value in values)
			{
				foreach (var _ in names)
				{
					writer.WriteField(value);
				}

				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async);

		if (save is not null)
		{
			Directory.CreateDirectory(save);
			File.WriteAllBytes(Path.Combine(save, $"codes-{codes[0].Seed.ToString(CultureInfo.InvariantCulture)}.xlsx"), workbook);
		}

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		using var opened = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = opened.OpenReader(Written.AllRows);
		var rows = 0;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				_ = reader.GetString(i);
			}

			rows++;
		}

		if (rows != values.Length + 1)
		{
			return $"{rows} rows read back, of {values.Length + 1}";
		}

		Count(tally, "workbooks");

		return null;
	}
}
