using System.Text;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of the formula checks, as Phase 4's "Security and fuzzing" describes: random formulas of the grammar's
/// pieces, strings and quoted names open and closed, brackets of every kind, white space, control characters, CR and LF,
/// lone surrogates, deep nesting and lengths around 8,192, with or without their <c>=</c>.
/// <list type="bullet">
/// <item><description>
/// <see cref="FormulaCheck"/> throws nothing, and <see cref="ExcelFormula"/>'s constructor nothing but
/// <see cref="ArgumentException"/>, exactly when the check finds a problem, with its message; <c>Parse</c> throws
/// <see cref="FormatException"/> where <c>TryParse</c> fails, and gives the constructor's formula otherwise.
/// </description></item>
/// <item><description>
/// An accepted formula is its own text again, has no CR outside its strings, and keeps what the checks say a formula
/// keeps: with a <c>)</c>, <c>(</c>, quote or apostrophe more, an operator at its start or end, or a tab after it, it is
/// refused; in parentheses, with a CR LF after it, padded with spaces to 8,192, or with a string of anything appended with
/// <c>&amp;</c>, accepted.
/// </description></item>
/// <item><description>
/// Accepted formulas, written to workbooks, pass the validator and read back as their text
/// (<see cref="ExcelFormulas.FormulaText"/>), a lone surrogate as U+FFFD.
/// </description></item>
/// </list>
/// <c>CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS</c> sets the number of formulas (and a workbook for each 200), and
/// <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the run checked.</param>
public class FormulaCheckFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of formulas a run checks, unless <c>CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS</c> says otherwise.</summary>
	private const int DefaultFormulas = 20_000;

	/// <summary>The formulas for each workbook the end-to-end test writes.</summary>
	private const int FormulasPerWorkbook = 200;

	/// <summary>The most UTF-16 code units a formula has.</summary>
	private const int MaxLength = 8192;

	/// <summary>
	/// The pieces formulas are made of: references, names and functions, numbers, errors and booleans, strings and quoted
	/// names closed and not, structured references with their escapes, arrays, brackets of each kind, operators, white
	/// space and line breaks, control characters, escapes, other scripts, and surrogates paired and lone.
	/// </summary>
	private static readonly string[] Pieces =
	[
		"A1", "$A$1", "B2:C3", "A:A", "1:1", "Sheet1!A1", "'Q1 Orders'!B2", "'it''s'!C3", "[1]Sheet1!A1", "Table1[Col]", "Table1[[#This Row],[Col]]",
		"T[a'[b]", "T[a']b]", "T['#x]", "{1,2;3,4}", "{\"a}\",1}", "SUM(", "IF(", "_xlfn.XLOOKUP(", "LEN(", "MyName", "1.5E+3", ".5", "12", "TRUE",
		"#REF!", "#N/A", "\"str\"", "\"a\"\"b\"", "\"\"", "\"(\"", "\"[\"", "\"'\"", "\"", "'", "''", "(", ")", "[", "]", "{", "}",
		"+", "-", "*", "/", "^", "&", "=", "<", ">", "<>", "<=", ":", ",", ";", "!", "@", "#", "%", " ", "  ", "\n", "\r", "\r\n", "\t",
		Units(0), Units(1), Units(0x1F), Units(0x7F), "_x0041_", "_x000D_", "_x0041", Units(0xE9), Units(0x65E5, 0x672C), Units(0xA0),
		Units(0xD83D, 0xDE00), Units(0xD800), Units(0xDC00), Units(0xFFFE),
	];

	[Fact]
	public void ChecksRandomFormulasConsistently()
	{
		var formulas = Setting("CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS", DefaultFormulas);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + formulas && failures.Count < MaxFailures; seed++)
		{
			var formula = RandomFormula(new Random(seed));
			try
			{
				if (Check(formula, tally) is { } problem)
				{
					failures.Add($"Seed {seed}, {Show(formula)}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed}, {Show(formula)}: {Describe(e)}");
			}
		}

		Report(output, $"Seeds {first} to {first + formulas - 1}:", tally);
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS");
	}

	[Fact]
	public async Task WritesAcceptedFormulasAsTheirText()
	{
		var formulas = Setting("CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS", DefaultFormulas);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var written = 0;
		for (var start = first; start < first + formulas && failures.Count < MaxFailures; start += FormulasPerWorkbook)
		{
			// The accepted formulas of the seeds, as fields of five columns.
			var accepted = Enumerable.Range(start, Math.Min(FormulasPerWorkbook, first + formulas - start))
				.Select(seed => (Seed: seed, Text: RandomFormula(new Random(seed))))
				.Where(f => FormulaCheck.Check(Body(f.Text).AsSpan(), out _) is null)
				.Select(f => (f.Seed, Formula: new ExcelFormula(f.Text)))
				.ToList();
			var async = start / FormulasPerWorkbook % 2 == 1;
			try
			{
				if (await WriteProblemAsync(accepted, async) is { } problem)
				{
					failures.Add($"Seeds {start} to {start + FormulasPerWorkbook - 1} ({(async ? "async" : "sync")}): {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seeds {start} to {start + FormulasPerWorkbook - 1} ({(async ? "async" : "sync")}): {Describe(e)}");
			}

			written += accepted.Count;
		}

		output.WriteLine($"Seeds {first} to {first + formulas - 1}: {written} accepted formulas written and read back.");
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS");
	}

	/// <summary>Checks a formula: the check, the constructor, <c>Parse</c> and <c>TryParse</c>, and what an accepted one keeps.</summary>
	/// <param name="text">The formula, with or without its <c>=</c>.</param>
	/// <param name="tally">Takes whether it was accepted.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? Check(string text, SortedDictionary<string, int> tally)
	{
		var body = Body(text);
		string? problem;
		string? normalized;
		try
		{
			problem = FormulaCheck.Check(body.AsSpan(), out normalized);
		}
		catch (Exception e)
		{
			return $"the check threw {Describe(e)}";
		}

		ExcelFormula formula = default;
		ArgumentException? refused = null;
		try
		{
			formula = new ExcelFormula(text);
		}
		catch (ArgumentException e) when (e.GetType() == typeof(ArgumentException))
		{
			refused = e;
		}

		if ((refused is null) != (problem is null) || (refused is not null && (!refused.Message.StartsWith(problem!, StringComparison.Ordinal) || refused.ParamName != "formula")))
		{
			return $"the check says {Show(problem ?? "nothing")}, the constructor {(refused is null ? "accepts it" : $"throws \"{refused.Message}\" for {refused.ParamName}")}";
		}

		var parsed = ExcelFormula.TryParse("=" + body, null, out var parsedFormula);
		FormatException? parseError = null;
		ExcelFormula parseResult = default;
		try
		{
			parseResult = ExcelFormula.Parse("=" + body);
		}
		catch (FormatException e)
		{
			parseError = e;
		}

		if (parsed != (problem is null) || (parseError is null) != parsed || (parsed && (parsedFormula != formula || parseResult != formula)))
		{
			return $"TryParse {(parsed ? "parses" : "fails")}, Parse {(parseError is null ? "parses" : "throws")}, and the check says {Show(problem ?? "nothing")}";
		}

		if (problem is not null)
		{
			Count(tally, "formulas: refused");

			return null;
		}

		Count(tally, "formulas: accepted");

		return AcceptedProblem(body, normalized, formula, tally);
	}

	/// <summary>
	/// Checks an accepted formula: its text, again; no CR outside its strings; and what the checks say of the formula
	/// with more around it.
	/// </summary>
	/// <param name="body">The formula without its <c>=</c>.</param>
	/// <param name="normalized">The check's normalized formula, or <see langword="null"/>.</param>
	/// <param name="formula">The formula.</param>
	/// <param name="tally">Takes the relations checked.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? AcceptedProblem(string body, string? normalized, ExcelFormula formula, SortedDictionary<string, int> tally)
	{
		var kept = normalized ?? body;
		if (formula.Formula != kept || formula.ToString() != "=" + kept)
		{
			return $"the formula is {Show(formula.ToString())}, for {Show(kept)}";
		}

		if (new ExcelFormula(formula.ToString()) != formula || new ExcelFormula(formula.Formula) != formula || ExcelFormula.Parse(formula.ToString()) != formula)
		{
			return "the formula isn't its own text again";
		}

		if (kept.Length > body.Length || (body.IndexOf('"') < 0 && kept.IndexOf('\r') >= 0))
		{
			return $"its normalized form {Show(kept)} keeps a CR or grows";
		}

		var refused = new[] { kept + ")", "(" + kept, kept + "\"", kept + "'", kept + "+", kept + " *", kept + "#", kept + "@", "*" + kept, " &" + kept, kept + "\t", kept + Units(1) };
		foreach (var more in refused)
		{
			if (more.Length <= MaxLength && FormulaCheck.Check(more.AsSpan(), out _) is null)
			{
				return $"{Show(more)} is accepted";
			}
		}

		var appended = "&\"" + Units(1) + "\r\n\t([{\"\"'" + Units(0xD800) + "\"";
		var accepted = new[] { ("(" + kept + ")", "(" + kept + ")"), (kept + "\r\n", kept + "\n"), (kept + appended, kept + appended), (kept.PadRight(MaxLength), kept.PadRight(MaxLength)) };
		foreach (var (more, expected) in accepted)
		{
			if (more.Length > MaxLength)
			{
				continue;
			}

			if (FormulaCheck.Check(more.AsSpan(), out var moreNormalized) is { } problem)
			{
				return $"{Show(more)} is refused: {problem}";
			}

			if ((moreNormalized ?? more) != expected)
			{
				return $"{Show(more)} is normalized as {Show(moreNormalized ?? more)}, for {Show(expected)}";
			}
		}

		if (kept.Length < MaxLength && FormulaCheck.Check((kept + new string(' ', MaxLength + 1 - kept.Length)).AsSpan(), out _) is null)
		{
			return "padded past 8,192 code units, it is accepted";
		}

		Count(tally, "formulas: accepted, with every relation checked");

		return null;
	}

	/// <summary>
	/// Writes formulas to a workbook, as fields of five columns, two ways, and checks it: the validator, and each formula
	/// read back as its text.
	/// </summary>
	/// <param name="formulas">The formulas and their seeds.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> WriteProblemAsync(List<(int Seed, ExcelFormula Formula)> formulas, bool async)
	{
		const int columns = 5;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Formulas");
			for (var i = 0; i < formulas.Count; i++)
			{
				if (i % 2 == 0)
				{
					writer.WriteFormula(formulas[i].Formula.ToString());
				}
				else
				{
					writer.WriteField<ExcelFormula>(formulas[i].Formula);
				}

				if (i % columns == columns - 1 || i == formulas.Count - 1)
				{
					await Written.EndRecordAsync(writer, a);
				}
			}

			await Written.DisposeAsync(writer, a);
		}, async, Written.Ragged());

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(book.Sheets[0], Written.AllRows with { Formulas = ExcelFormulas.FormulaText });
		var read = new List<string>();
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				read.Add(reader.GetString(i));
			}
		}

		for (var i = 0; i < formulas.Count; i++)
		{
			var expected = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(formulas[i].Formula.ToString()));
			var got = i < read.Count ? read[i] : "nothing";
			if (!string.Equals(got, expected, StringComparison.Ordinal))
			{
				return $"seed {formulas[i].Seed}'s {Show(formulas[i].Formula.ToString())} reads back as {Show(got)}";
			}
		}

		return null;
	}

	/// <summary>A formula without its <c>=</c>, as the constructor checks it.</summary>
	/// <param name="text">The formula, with or without its <c>=</c>.</param>
	/// <returns>The formula after its <c>=</c>.</returns>
	private static string Body(string text) => text.Length > 0 && text[0] == '=' ? text.Substring(1) : text;

	/// <summary>
	/// A random formula: mostly a few pieces; now and then nested parentheses thousands deep, or a long one near 8,192
	/// code units; one in four with its <c>=</c>.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The formula.</returns>
	private static string RandomFormula(Random random)
	{
		var formula = new StringBuilder();
		switch (random.Next(40))
		{
			case 0:
				var depth = random.Next(1, MaxLength / 2);
				formula.Append('(', depth).Append(Pieces[random.Next(Pieces.Length)]).Append(')', depth - random.Next(2));
				break;
			case 1:
				var length = MaxLength - 4 + random.Next(9);
				while (formula.Length < length)
				{
					formula.Append(random.Next(2) == 0 ? "A1+" : "\"x\"&");
				}

				formula.Length = length - 2;
				formula.Append(random.Next(2) == 0 ? "A1" : " 1");
				break;
			default:
				for (var n = random.Next(1, 13); n > 0; n--)
				{
					formula.Append(Pieces[random.Next(Pieces.Length)]);
				}

				break;
		}

		return (random.Next(4) == 0 ? "=" : "") + formula;
	}

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
}
