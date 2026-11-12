namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The format codes Excel opens and refuses, as Excel 365 answered through COM on 2026-11-06 and 2026-11-07, and the
/// forms the check refuses because they weren't checked.
/// </summary>
public class FormatCheckTests
{
	/// <summary>Codes Excel opened.</summary>
	public static TheoryData<string> Opened => new()
	{
		"",
		"General",
		"general",
		"0;General",
		"General\" x\"",
		"0.00",
		"#,##0.00",
		"#,##0,,",
		"0%",
		"0%%",
		"0.0000",
		"0.00E+00",
		"0.0E+0",
		"##0.0E-0",
		"# ?/8",
		"??/??",
		"0;0;0;@",
		"0;;0",
		"@",
		"@ @",
		"[Red]0",
		"[red]0",
		"[Color10]0",
		"[Color56]0",
		"[<100]0",
		"[>=100]0;[<=-1]0",
		"[<>0]0",
		"[=1.5]0",
		"[$-409]0",
		"[$\u20ac-407]#,##0.00",
		"[$\u20ac]0",
		"[$-7]mmmm",
		"[$-40c]mmmm",
		"[$-12345678]0",
		"[$-1000]mmmm",
		"[DBNum1]0",
		"[DBNum2]0",
		"0\u2030",
		"0 \u20ac",
		"0 \u00a3",
		"0 \u00a4",
		"0\u00b0",
		"0\u00a0",
		"0 |",
		"0 &",
		"0 ^~{}",
		"$#,##0",
		"0 5",
		"0 \\k",
		"0\"\"",
		"0\";\"",
		"0.00\" kg\"",
		"*-0",
		"_)0",
		"0.0.0",
		"yyyy-mm-dd",
		"YYYY-MM-DD",
		"m",
		"dd.mm.yyyy",
		"mmm d, yyyy",
		"m/d/yyyy h:mm AM/PM",
		"h:mm a/p",
		"hh:mm:ss.000",
		"ss.0",
		"ss.00",
		"hh:mm.000",
		"dd.0",
		"mm.00",
		".000",
		"[h]:mm:ss",
		"[hh]:mm",
		"[mm]:ss",
		"[ss]",
		"aaaa",
		"aaa",
		"bbbb",
		"ggge",
		"yyyy e",
		"[$-411]ggge\"\u5e74\"",
		"[$-411]e-m-d",
		"yyyy\\\"mm",
		"hh\":\"mm",
		// Phase 5's step 7, after the format fuzzer (2026-11-11): exponents, fractions and dates' slashes, b and B1, the
		// era's e, brackets of different kinds, elapsed time first, points after a date token, text last, and long codes.
		"0E+0",
		"#E+#",
		"d/",
		"m/d",
		"d /",
		"-d/",
		"#/#",
		"?/?",
		"0/0",
		"0/#",
		"#/1",
		"0/ 8",
		"0 /8",
		"0 / 8",
		"0 0/8",
		"0/8 0",
		"0/8-",
		"-0/8",
		"0\"x\"/8",
		"0%/8",
		"0/8%",
		"0/8;0/8",
		"b",
		"bb",
		"bbbbb",
		"B1",
		"B2",
		"B1yyyy",
		"B1d",
		"B2b",
		"B1yyyy;B2yyyy",
		"B1;B2",
		"e+",
		"e-",
		"e+d",
		"e-m-d",
		"yyyy e-m",
		"ee",
		"[Red][<1]0",
		"[<1][Red]0",
		"[DBNum1][$-409]0",
		"[DBNum1][Red][<1]0",
		"[<1]0;[<2]0;0",
		"[<1]0;[Red][<2]0",
		"0;[<1]0",
		"[<1]0;@",
		"[Red]0;[Blue]0",
		"[h]h",
		"[mm]:ss",
		"[hh]:mm",
		"[s].0",
		"[h]:mm:ss.000",
		"d[ss]",
		"[ss]d",
		"[mm]mmmm",
		"y.",
		"ss.",
		"y. y",
		"y.0",
		"y.000",
		"h.000",
		"s.000",
		"0;@",
		"0;0;@",
		"General;@",
		"h AM/PM",
		"0;0;0;\"x\"",
		"0;0;0;",
		"0;0;0;-",
		"0;0;0;@@",
		"0;0;0;[Red]@",
		"0;0;0;General",
		"@,",
		"0\\x/8",
		"0\\0/8",
		"# ?\\0/8",
		"0 1/8",
		"0-/8",
		"0)/8",
		"0\"0\"/8",
		"[DBNum3].0000%",
		"[DBNum3]0.00",
		"[DBNum3]#,##0",
		"[DBNum1]0E+0",
		"[DBNum1]0/8",
		"General%",
		"General;General",
		"#,##0E+0",
		"#,##0.00E+00",
		"##0.0E+0",
		"0,E+0",
		"0E+0 0",
		"General,",
		"General-",
		"d\"5\"",
		"y.0/",
		"ss.0/",
		"ss.0-",
		"0." + new string('0', 100),
		"0" + new string('%', 30),
		new string('=', 83),
		new string('.', 245),
		"\"" + new string('x', 60) + "\"" + new string('=', 41),
		new string('0', 83),
		"0." + new string('0', 81),
		string.Concat(Enumerable.Repeat("_)", 41)) + "0",
		"[DBNum1]" + new string('0', 75),
		"hh:mm:ss" + new string('-', 75),
		"_(\"$\"* #,##0.00_);_(\"$\"* \\(#,##0.00\\);_(\"$\"* \"-\"??_);_(@_)",
		"_-* #,##0.00 [$€-407]_-;-* #,##0.00 [$€-407]_-;_-* \"-\"?? [$€-407]_-;_-@_-",
		"[$-F800]dddd\\,\\ mmmm\\ dd\\,\\ yyyy",
	};

	/// <summary>Codes Excel refused (Excel opened none of these workbooks).</summary>
	public static TheoryData<string, string> Refused => new()
	{
		{ "\"days", "a quote that isn't closed" },
		{ "[Red0", "a bracket that isn't closed" },
		{ "0;0;0;0;0", "more than 4 sections" },
		{ "0\\", "ends with \\" },
		{ "0_", "ends with _" },
		{ "0*", "ends with *" },
		{ "[Purple]0", "[Purple]" },
		{ "[Color0]0", "[Color0]" },
		{ "[Color57]0", "[Color57]" },
		{ "[ > 100]0", "[ > 100]" },
		{ "[hm]", "[hm]" },
		{ "[foo]0", "[foo]" },
		{ "0.00e+00", "lower case" },
		{ "0.00E00", "without its sign" },
		{ "E+0", "without a digit placeholder before it" },
		{ "0.00 kg", "the letter k" },
		{ "0 g", "0 in a section with a date or time" },
		{ "0 y", "0 in a section with a date or time" },
		{ "0 e", "0 in a section with a date or time" },
		{ "0 h", "0 in a section with a date or time" },
		{ "0 s", "0 in a section with a date or time" },
		{ "ss.0000", "4 digits" },
		{ "0:00", ": in a section without a date or time" },
		{ "dd @", "@ in a section with a date or time" },
		{ "dd %", "% in a section with a date or time" },
		{ "dd ?", "? in a section with a date or time" },
		{ "yyyy #", "# in a section with a date or time" },
		{ "0 @", "@ in a section with digit placeholders" },
		{ "General 0", "General in a section with digit placeholders" },
		{ "0.00E+", "without a digit placeholder after it" },
		{ "hh:mm:ss.0000", "4 digits" },
		// Phase 5's step 7, after the format fuzzer (2026-11-11).
		{ "E+", "an exponent without a digit placeholder before it" },
		{ "E+d", "an exponent without a digit placeholder before it" },
		{ "yyyy E-m", "an exponent without a digit placeholder before it" },
		{ "0E+", "without a digit placeholder after it" },
		{ "0.0E+", "without a digit placeholder after it" },
		{ "E", "E without + or - after it" },
		{ "Ed", "E without + or - after it" },
		{ "/", "/ without a digit placeholder or a date or time token before it" },
		{ "/d", "/ without a digit placeholder or a date or time token before it" },
		{ "-/m", "/ without a digit placeholder or a date or time token before it" },
		{ "$/", "/ without a digit placeholder or a date or time token before it" },
		{ "\"x\"/", "/ without a digit placeholder or a date or time token before it" },
		{ "@/", "/ without a digit placeholder or a date or time token before it" },
		{ "General/", "/ without a digit placeholder or a date or time token before it" },
		{ "0/", "/ without a denominator after it" },
		{ "#/", "/ without a denominator after it" },
		{ "0 /", "/ without a denominator after it" },
		{ "0/$", "/ without a denominator after it" },
		{ "0//8", "/ without a denominator after it" },
		{ "0/8/", "/ without a denominator after it" },
		{ "# ??/??/??", "more than one /" },
		{ "0/8 0/8", "more than one /" },
		{ "0.0 ?/?", "a fraction with a decimal point" },
		{ "#.# ?/?", "a fraction with a decimal point" },
		{ "0/8.", "a fraction with a decimal point" },
		{ "0/8E+00", "a fraction with an exponent" },
		{ "#,##0/8", "a fraction with a ," },
		{ "#,##0\\0/8", "a fraction with a ," },
		{ "[hh] AM/PM", "elapsed time with AM/PM or A/P" },
		{ "[h]:mm AM/PM", "elapsed time with AM/PM or A/P" },
		{ "[ss] A/P", "elapsed time with AM/PM or A/P" },
		{ "0;0;0;0", "a digit placeholder in its fourth section" },
		{ "0;0;0;.0", "a digit placeholder in its fourth section" },
		{ "0;0;0;#", "a digit placeholder in its fourth section" },
		{ "0;0;0;yyyy", "a date or time in its fourth section" },
		{ "0;0;0;bbbb", "a date or time in its fourth section" },
		{ "0;0;0;%", "% in its fourth section" },
		{ "@.", ". in a section with @" },
		{ ".@", ". in a section with @" },
		{ "@%", "% in a section with @" },
		{ "% @", "% in a section with @" },
		{ "General@", "General with @" },
		{ "General General", "General twice" },
		{ "GeneralGeneral", "General twice" },
		{ "0E+0,0", "a , after its exponent" },
		{ "0E+00E-0", "two exponents" },
		{ "General;;;.", ". in its fourth section" },
		{ "0E+0#,##0", "a , after its exponent" },
		{ "0E+00,0", "a , after its exponent" },
		{ "@General", "General with @" },
		{ "General.", "General with ." },
		{ ".General", "General with ." },
		{ "d 5", "a digit outside quotes in a section with a date or time" },
		{ "yyyy1", "a digit outside quotes in a section with a date or time" },
		{ "1d", "a digit outside quotes in a section with a date or time" },
		{ "[h]1", "a digit outside quotes in a section with a date or time" },
		{ "d/8", "a digit outside quotes in a section with a date or time" },
		{ "ss.08", "a digit outside quotes in a section with a date or time" },
		{ "y.0 8", "a digit outside quotes in a section with a date or time" },
		{ "B", "B outside quotes" },
		{ "0B", "B outside quotes" },
		{ "B0", "B outside quotes" },
		{ "B3", "B outside quotes" },
		{ "bB", "B outside quotes" },
		{ "yyyyB", "B outside quotes" },
		{ "B1B2", "B1 or B2 twice" },
		{ "B1 B2", "B1 or B2 twice" },
		{ "bb B1", "B1 or B2 after a date or time token" },
		{ "[Red][Red]0", "a second color" },
		{ "[Red][Blue]0", "a second color" },
		{ "[Color10][Red]0", "a second color" },
		{ "[<1][<2]0", "a second condition" },
		{ "[$-409][$-409]0", "a second currency or locale" },
		{ "[$€]0[$-409]", "a second currency or locale" },
		{ "[DBNum1][DBNum3]0", "a second DBNum" },
		{ "[h][h]", "a second elapsed time" },
		{ "[h]:[mm]", "a second elapsed time" },
		{ "[ss][h]", "a second elapsed time" },
		{ "hh[hh]", "after an hour, a minute or a second" },
		{ "ss[ss]", "after an hour, a minute or a second" },
		{ "h:[mm]", "after an hour, a minute or a second" },
		{ "hh:mm:ss[hh]", "after an hour, a minute or a second" },
		{ "0;0;[<1]0", "a condition in its third or fourth section" },
		{ "[<1]0;[<2]0;[<3]0", "a condition in its third or fourth section" },
		{ "[<1]@", "a condition in a section with @" },
		{ "@;0", "@ in a section before its last" },
		{ "@;@", "@ in a section before its last" },
		{ "@;0;0", "@ in a section before its last" },
		{ "@;General", "@ in a section before its last" },
		{ "[<1]0;@;0", "@ in a section before its last" },
		{ ".y", ". before its first date or time token" },
		{ ".ss", ". before its first date or time token" },
		{ ". ss", ". before its first date or time token" },
		{ "-.y", ". before its first date or time token" },
		{ ".000y", ". before its first date or time token" },
		{ ".‰ss", ". before its first date or time token" },
		{ "0" + new string('%', 40), "more than 30 %" },
		{ new string('=', 84), "longer than Excel opens" },
		{ "0" + new string('=', 83), "longer than Excel opens" },
		{ "0.00" + new string('=', 82), "longer than Excel opens" },
		{ "#,##0.00" + new string('=', 81), "longer than Excel opens" },
		{ "\"" + new string('x', 60) + "\"" + new string('=', 43), "longer than Excel opens" },
		{ "0." + new string('0', 128), "longer than Excel opens" },
		{ new string('.', 250), "longer than Excel opens" },
		{ new string('€', 84), "longer than Excel opens" },
		{ string.Concat(Enumerable.Repeat("_)", 84)), "longer than Excel opens" },
		{ string.Concat(Enumerable.Repeat("\\=", 84)), "longer than Excel opens" },
	};

	/// <summary>Codes Excel opened that the check refuses, since their forms weren't checked or aren't needed.</summary>
	public static TheoryData<string> Stricter => new()
	{
		// Excel opens these as other codes: [$]0, [>0]0;General and [$-23450000]0.
		"[$-ZZZ]0",
		"[>abc]0",
		"[$-123456789]0",
		// Literal letters, and A/P in a number.
		"aa",
		"0 A/P",
		// Phase 5's step 7: B in upper case but in B1 and B2 (Excel reads B, b, BB and Byyyy as the Buddhist year, but
		// refuses B alone and bB), elapsed time after m, which may be a month, and long codes past the check's count.
		"BB",
		"Bb",
		"BBB",
		"Byyyy",
		"B b",
		"B-",
		"Bh",
		"mmmm[mm]",
		"mm[mm]",
		"m[m]",
		"mm:[ss]",
		"AM/PM [h]",
		"General:",
		"0E+0,",
		"0,/8",
		new string('0', 255),
		new string('#', 255),
		"#,##0.00" + new string('=', 80),
		"\"" + new string('x', 125) + "\"",
		"yyyy" + new string('-', 82),
		"0." + new string('0', 126),
	};

	/// <summary>Codes Excel opened (and LibreOffice, on 2026-11-08) that the check refused before Phase 5.</summary>
	public static TheoryData<string> OpenedInPhase5 => new()
	{
		"[DBNum3]0",
		"0`",
		"0 `",
	};

	/// <summary>Codes not checked through COM that the check refuses: conditions without a number, or with text after it.</summary>
	public static TheoryData<string> NotChecked => new()
	{
		"[=]0",
		"[<1.]0",
		"[<100x]0",
		"[<-]0",
	};

	[Theory]
	[MemberData(nameof(Opened))]
	[MemberData(nameof(OpenedInPhase5))]
	public void PassesCodesExcelOpens(string code) => Assert.Null(FormatCheck.Problem(code));

	[Theory]
	[MemberData(nameof(Refused))]
	public void RefusesCodesExcelRefuses(string code, string reason)
	{
		var problem = FormatCheck.Problem(code);

		Assert.NotNull(problem);
		Assert.Contains(reason, problem);
	}

	[Theory]
	[MemberData(nameof(Stricter))]
	[MemberData(nameof(NotChecked))]
	public void RefusesFormsThatWerentChecked(string code) => Assert.NotNull(FormatCheck.Problem(code));

	[Fact]
	public void KeepsCodesToExcelsLength()
	{
		// Excel keeps 255 characters, and opens less than that of characters that show as themselves (step 7): the check
		// counts 3 for each of them, and 1 for a placeholder.
		Assert.Equal("The format code is 256 characters long, and Excel keeps 255.", FormatCheck.Problem(new string('0', 256)));
		Assert.Null(FormatCheck.Problem(new string('0', 249)));
		Assert.Null(FormatCheck.Problem(new string('=', 83)));
		Assert.Equal(
			$"The format code \"{new string('=', 84)}\" is longer than Excel opens: it counts 252, 3 for each character outside quotes that shows as itself and 2 for each in quotes, and the check takes 249.",
			FormatCheck.Problem(new string('=', 84)));
	}

	[Fact]
	public void SkipsQuotesEscapesAndBrackets()
	{
		// What is quoted, escaped or bracketed isn't a token, a section's end or a date.
		Assert.Null(FormatCheck.Problem("0\" kg; h:mm [Red]\""));
		Assert.Null(FormatCheck.Problem("0\\;0\\;0\\;0\\;0"));
		Assert.Null(FormatCheck.Problem("0_;0;0;@"));
		Assert.Null(FormatCheck.Problem("0* ;0"));
		Assert.Null(FormatCheck.Problem("[$;-409]0"));
		Assert.Null(FormatCheck.Problem("0\\h"));
		Assert.Null(FormatCheck.Problem("yyyy\"0#?%@\""));
		// Each section is a date or not on its own.
		Assert.Null(FormatCheck.Problem("yyyy-mm-dd;0.00;@"));
		Assert.Null(FormatCheck.Problem("0.00;yyyy"));
	}

	[Fact]
	public void SaysWhatIsWrong()
	{
		Assert.Equal("The format code \"0.00 kg\" has the letter k outside quotes, which Excel opens only as a date or time token: put text in quotes.", FormatCheck.Problem("0.00 kg"));
		Assert.Equal("The format code \"0 \u5e74\" has \u5e74 outside quotes: put it in quotes.", FormatCheck.Problem("0 \u5e74"));
		Assert.Equal("The format code \"[Purple]0\" has [Purple], which isn't a color, a condition, a currency or locale, or elapsed time.", FormatCheck.Problem("[Purple]0"));
		Assert.Equal("The format code \"hh:mm:ss.0000\" shows fractions of a second to 4 digits, and Excel shows at most 3.", FormatCheck.Problem("hh:mm:ss.0000"));
		Assert.Equal("The format code \"0;0;0;0;0\" has more than 4 sections, the most Excel opens.", FormatCheck.Problem("0;0;0;0;0"));
		Assert.Equal("The format code \"0.00E+\" has an exponent without a digit placeholder after it.", FormatCheck.Problem("0.00E+"));
	}
}
