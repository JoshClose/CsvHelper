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
		// Brackets and characters not checked.
		"[DBNum3]0",
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
		Assert.Null(FormatCheck.Problem(new string('0', 255)));
		Assert.Equal("The format code is 256 characters long, and Excel keeps 255.", FormatCheck.Problem(new string('0', 256)));
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
