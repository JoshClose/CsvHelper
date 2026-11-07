namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The formula checks, against what Excel opened and refused through COM on 2026-11-06 (step 3's probes, in
/// <c>_bench/results/phase4/step3</c>), and the line breaks a formula keeps outside its strings.
/// </summary>
public class FormulaCheckTests
{
	/// <summary>The end of every refusal's message.</summary>
	private const string Refusal = ", and Excel opens no workbook with it.";

	/// <summary>Formulas Excel opens, without their <c>=</c>.</summary>
	public static IEnumerable<object[]> Accepted() =>
		new[]
		{
			// Excel computed these, or showed #NAME?, #REF! or #NULL!.
			"A1*2", "sum(a1:a2)", " A1 + 2 ", "NOSUCHFUNC(1)", "Nope!A1", "A0+1", "hello world", "_xlfn.XLOOKUP(1,A1:A2,A1:A2)",
			"XLOOKUP(1,A1:A2,A1:A2)", "\"a\"\"b\"",
			// Brackets in strings and quoted sheet names.
			"LEN(\"(\")", "LEN(\")\")", "'a(b'!A1", "'Probe'!A1", "'it''s'!A1", "INDEX({\"a\",\"}\"},2)",
			// Braces, structured references and their escapes.
			"SUM({1,2;3,4})", "SUM(Table1[Col])", "SUM(Table1[a'[b])",
			// Operators Excel takes at the end and the start, white space at the end, and empty arguments.
			"A1%", "A1 ", "A1\n", "SUM(1,)", "A1 A2", "@A1", "+A1", "-A1", "#N/A", "#NAME?",
			// Line breaks and anything else in strings.
			"A1+\n1", "\"a\nb\"", "\"a\tb\"", "\"a\rb\"", "\"日本\"&\"é\"",
			// A string longer than the 255 characters Excel's formula bar takes.
			"LEN(\"" + new string('a', 256) + "\")",
		}.Select(f => new object[] { f });

	/// <summary>Formulas Excel refuses, without their <c>=</c>, with what the message says of each.</summary>
	public static IEnumerable<object[]> Refused()
	{
		// Brackets.
		yield return ["SUM(", "has a parenthesis that isn't closed"];
		yield return ["SUM(A1))", "has a ) that closes nothing"];
		yield return [")A1(", "has a ) that closes nothing"];
		yield return ["SUM({1,2)", "closes a brace with )"];
		yield return ["SUM(1})", "closes a parenthesis with }"];
		yield return ["Table1[Col", "has a bracket that isn't closed"];
		yield return ["A1]", "has a ] that closes nothing"];
		yield return ["SUM(A1]", "closes a parenthesis with ]"];

		// Strings and quoted sheet names.
		yield return ["\"abc", "has a string that isn't closed"];
		yield return ["LEN(\"abc)", "has a string that isn't closed"];
		yield return ["'Probe!A1", "has a quoted sheet name that isn't closed"];

		// Operators at the end, also before white space.
		foreach (var op in new[] { "+", "-", "*", "/", "^", "&", "=", "<", ">", ":", ",", "@", "#" })
		{
			yield return ["A1" + op, $"ends with the operator {op}"];
		}

		yield return ["A1<>", "ends with the operator >"];
		yield return ["A1+ ", "ends with the operator +"];
		yield return ["A1+\n", "ends with the operator +"];
		yield return ["-", "ends with the operator -"];

		// Operators at the start, also after white space; = is one, so Excel refuses ==A1.
		foreach (var op in new[] { "*", "/", "^", "&", "=", "<", ">", ":", ",", "%" })
		{
			yield return [op + "A1", $"starts with the operator {op}"];
		}

		yield return [" *A1", "starts with the operator *"];
		yield return ["\n*A1", "starts with the operator *"];

		// Control characters outside strings: Excel refused a tab between tokens and U+0001; the writer refuses a tab at the
		// end too, which Excel opens.
		yield return ["A1+\t1", "has a tab outside its strings"];
		yield return ["A1\t", "has a tab outside its strings"];
		yield return ["'a\tb'!A1", "has a tab outside its strings"];
		yield return ["Table1[a\tb]", "has a tab outside its strings"];
	}

	[Theory]
	[MemberData(nameof(Accepted))]
	public void AcceptsWhatExcelOpens(string formula)
	{
		Assert.Null(FormulaCheck.Check(formula, out _));
	}

	[Theory]
	[MemberData(nameof(Refused))]
	public void RefusesWhatExcelDoesNot(string formula, string problem)
	{
		Assert.Equal($"The formula \"{formula}\" {problem}{Refusal}", FormulaCheck.Check(formula, out var normalized));
		Assert.Null(normalized);
	}

	[Fact]
	public void RefusesControlCharactersOutsideStrings()
	{
		// Not theory data, which xUnit passes through text. Excel refused U+0001 between tokens, and opened it in a string.
		var control = (char)1;

		Assert.Equal($"The formula \"A1+{control}1\" has U+0001 outside its strings{Refusal}", FormulaCheck.Check("A1+" + control + "1", out _));
		Assert.Equal($"The formula \"{(char)0x1F}A1\" has U+001F outside its strings{Refusal}", FormulaCheck.Check((char)0x1F + "A1", out _));
		Assert.Null(FormulaCheck.Check("\"a" + control + "b\"", out _));
		Assert.Null(FormulaCheck.Check("\"a" + (char)0xFFFE + "b\"", out _));
	}

	[Fact]
	public void RefusesEmptyFormulas()
	{
		// Excel opens <f></f> and <f> </f> as empty cells, but a formula of nothing is a mistake.
		const string Empty = "A formula can't be empty: a cell without one is a null or default(ExcelFormula) value.";

		Assert.Equal(Empty, FormulaCheck.Check("", out _));
		Assert.Equal(Empty, FormulaCheck.Check(" ", out _));
		Assert.Equal(Empty, FormulaCheck.Check(" \r\n ", out _));
	}

	[Fact]
	public void CountsLengthInUtf16CodeUnits()
	{
		// Excel opened 8,192 code units, with emoji of two each, and refused 8,193.
		var emoji = char.ConvertFromUtf32(0x1F600);
		var opened = LongFormula(8192, emoji);
		var refused = LongFormula(8193, emoji);

		Assert.Equal(8192, opened.Length);
		Assert.Null(FormulaCheck.Check(opened, out _));
		Assert.Equal("A formula is at most 8,192 characters long, and this one has 8,193.", FormulaCheck.Check(refused, out _));
		Assert.Equal("A formula is at most 8,192 characters long, and this one has 9,000.", FormulaCheck.Check(new string('1', 9000), out _));
		Assert.Null(FormulaCheck.Check(new string('1', 8192), out _));
	}

	[Fact]
	public void QuotesLongFormulasShortened()
	{
		var shown = new string('a', 100);
		Assert.Equal($"The formula \"{shown}...\" has a parenthesis that isn't closed{Refusal}", FormulaCheck.Check(shown + "(", out _));

		// Not between a surrogate pair's halves.
		var emoji = char.ConvertFromUtf32(0x1F600);
		var paired = new string('a', 99) + emoji + "(";
		Assert.Equal($"The formula \"{new string('a', 99)}...\" has a parenthesis that isn't closed{Refusal}", FormulaCheck.Check(paired, out _));
		Assert.Equal($"The formula \"{new string('a', 99)}(\" has a parenthesis that isn't closed{Refusal}", FormulaCheck.Check(new string('a', 99) + "(", out _));
	}

	[Fact]
	public void ReadsBracketsAsStructuredReferencesDo()
	{
		// Inside brackets only brackets count, and an apostrophe escapes the character after it.
		Assert.Null(FormulaCheck.Check("Table1[[#This Row],[Col]]", out _));
		Assert.Null(FormulaCheck.Check("Table1[Price (USD)]", out _));
		Assert.Null(FormulaCheck.Check("Table1[a\"b]", out _));
		Assert.Null(FormulaCheck.Check("Table1[it''s]", out _));
		Assert.Null(FormulaCheck.Check("Table1[a']b]", out _));
		Assert.Null(FormulaCheck.Check("Table1[a'#b]", out _));
		Assert.Null(FormulaCheck.Check("'[Book1.xlsx]Sheet1'!A1", out _));
		Assert.Null(FormulaCheck.Check("\"it's\"&\"[\"", out _));
		Assert.Equal($"The formula \"Table1[a'']]\" has a ] that closes nothing{Refusal}", FormulaCheck.Check("Table1[a'']]", out _));
		Assert.Equal($"The formula \"Table1[a']\" has a bracket that isn't closed{Refusal}", FormulaCheck.Check("Table1[a']", out _));
		Assert.Equal($"The formula \"(Table1[a)]\" has a parenthesis that isn't closed{Refusal}", FormulaCheck.Check("(Table1[a)]", out _));
	}

	[Fact]
	public void KeepsLineBreaksOutsideStringsAsLineFeeds()
	{
		// Excel refuses a CR outside a string (from _x000D_), and reads a literal one as LF; in a string it keeps either.
		Assert.Null(FormulaCheck.Check("IF(A1>0,\n1,\n2)", out var unchanged));
		Assert.Null(unchanged);

		Assert.Null(FormulaCheck.Check("IF(A1>0,\r\n1,\r2)", out var normalized));
		Assert.Equal("IF(A1>0,\n1,\n2)", normalized);

		Assert.Null(FormulaCheck.Check("\"a\r\nb\"&\r\n\"c\rd\"\r", out normalized));
		Assert.Equal("\"a\r\nb\"&\n\"c\rd\"\n", normalized);

		Assert.Null(FormulaCheck.Check("'a\r\nb'!A1+Table1[x'\ry\r]", out normalized));
		Assert.Equal("'a\nb'!A1+Table1[x'\ny\n]", normalized);

		Assert.Null(FormulaCheck.Check("\r\nA1\r\n", out normalized));
		Assert.Equal("\nA1\n", normalized);
	}

	/// <summary>A formula of string literals of emoji joined with <c>&amp;</c>, as the probe that Excel opened and refused.</summary>
	/// <param name="units">Its length in UTF-16 code units.</param>
	/// <param name="emoji">An emoji, two code units.</param>
	/// <returns>The formula.</returns>
	private static string LongFormula(int units, string emoji)
	{
		var term = "\"" + string.Concat(Enumerable.Repeat(emoji, 127)) + "\"";
		var body = string.Join("&", Enumerable.Repeat(term, 31));

		return body + "&\"" + new string('a', units - body.Length - 3) + "\"";
	}
}
