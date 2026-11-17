using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// Format codes compiled (<see cref="FormatCode"/>): their sections, conditions, tokens and locale codes, the built-in
/// ids' codes, and the codes the renderer can't read, against what Excel 365 (16.0 build 20527, en-US) showed through COM
/// on 2026-11-16 and 2026-11-17 (<c>Csv\_bench\results\phase8\design</c>, and <c>step4</c> for dates).
/// </summary>
/// <remarks>
/// A section's tokens are written in a short form: a literal in quotes, a placeholder with its role (<c>I</c> integer,
/// <c>D</c> decimal, <c>E</c> exponent, <c>N</c> numerator, <c>V</c> denominator, <c>-</c> none), <c>.</c> the point,
/// <c>E+</c> an exponent, <c>@</c>, <c>General</c>, and date and time parts as letters repeated to their count: <c>y</c>,
/// <c>M</c> a month, <c>d</c>, <c>a</c> a weekday, <c>h</c>, <c>m</c> a minute, <c>s</c>, <c>0</c> fractions of a second,
/// <c>[h]</c>, <c>[m]</c> and <c>[s]</c> elapsed time, <c>AM/PM</c>, <c>A/P:</c> and its
/// letters, <c>e</c>, <c>g</c> and <c>b</c>.
/// </remarks>
public partial class FormatCodeTests
{
	/// <summary>Every code Excel showed values in reads, and every code Excel refused doesn't.</summary>
	[Fact]
	public void ReadsTheCodesExcelShowed()
	{
		Assert.Equal(410, CodesExcelShowed.Length);
		foreach (var code in CodesExcelShowed)
		{
			Assert.True(FormatCode.Compile(code).IsReadable, code);
		}

		Assert.Equal(5, CodesExcelRefused.Length);
		foreach (var code in CodesExcelRefused)
		{
			Assert.False(FormatCode.Compile(code).IsReadable, code);
		}
	}

	/// <summary>Codes Excel wouldn't open, and the forms Excel refused, can't be read: a cell shows its raw text.</summary>
	/// <param name="code">The code.</param>
	[Theory]
	[InlineData("\"abc")]
	[InlineData("[Red")]
	[InlineData("0\\")]
	[InlineData("0_")]
	[InlineData("0*")]
	[InlineData("0;0;0;@;0")]
	[InlineData("[Foo]0")]
	[InlineData("[]0")]
	[InlineData("[Color0]0")]
	[InlineData("[Color57]0")]
	[InlineData("[>abc]0")]
	[InlineData("[>]0")]
	[InlineData("[$-12@]0")]
	[InlineData("0.00e+00")]
	[InlineData("#e-0")]
	[InlineData("0:00")]
	[InlineData("0;0:00")]
	[InlineData("0.00 General")]
	[InlineData("0 General")]
	[InlineData("General.")]
	[InlineData("0E+")]
	[InlineData("0.0E+")]
	[InlineData("0.00E-")]
	[InlineData("0E+\" x\"")]
	[InlineData("0E+;0")]
	[InlineData("0;0;[=0]\"z\"")]
	[InlineData("0;0;[>5]\"z\"")]
	[InlineData("[>100]0;0.00;[=0]\"z\"")]
	[InlineData("0;[<0]@")]
	[InlineData("ss.0000")]
	[InlineData("h 0 #")]
	[InlineData("yyyy 0")]
	[InlineData("yyyy ?")]
	[InlineData("yyyy 0.0")]
	[InlineData("h:mm%")]
	[InlineData("yyyy E+0")]
	[InlineData("h:mm@;@")]
	[InlineData("[h]:mm AM/PM")]
	[InlineData("d [h] A/P")]
	[InlineData("yyyy General")]
	[InlineData("yyyy-mm-dd [h]:mm")]
	[InlineData("d[ss]")]
	[InlineData("m [s]")]
	[InlineData("ggg [m]")]
	[InlineData("?/32769")]
	[InlineData("# ?/50000")]
	[InlineData("# ?/100000000")]
	public void CantReadCodesExcelRefuses(string code)
	{
		var compiled = FormatCode.Compile(code);
		Assert.False(compiled.IsReadable);
		Assert.Empty(compiled.Numbers);
		Assert.Null(compiled.Text);
		Assert.Equal(code, compiled.Code);
	}

	/// <summary>Each section's tokens: numbers, fractions, dates and times, literals, as Excel reads them.</summary>
	/// <param name="code">The code.</param>
	/// <param name="expected">Its first section's tokens.</param>
	[Theory]
	[InlineData("General", "General")]
	[InlineData("0", "0I")]
	[InlineData("#,##0.00", "#I #I #I 0I . 0D 0D")]
	[InlineData("0.0#", "0I . 0D #D")]
	[InlineData("?.??", "?I . ?D ?D")]
	[InlineData("#.##.#", "#I . #D #D '.' #D")]
	[InlineData(".00", ". 0D 0D")]
	[InlineData("0.0,,\"M\"", "0I . 0D 'M'")]
	[InlineData("#,", "#I")]
	[InlineData("0 ,", "0I ' ,'")]
	[InlineData("0,.0", "0I . 0D")]
	[InlineData("#,##0,.00", "#I #I #I 0I . 0D 0D")]
	[InlineData("0.0,0", "0I . 0D 0D")]
	[InlineData("0.0%,", "0I . 0D '%,'")]
	[InlineData(",##0", "',' #I #I 0I")]
	[InlineData("0%", "0I '%'")]
	[InlineData("%0", "'%' 0I")]
	[InlineData("##0.0E+0", "#I #I 0I . 0D E+ 0E")]
	[InlineData("0.00E-00", "0I . 0D 0D E- 0E 0E")]
	[InlineData("#E+#", "#I E+ #E")]
	[InlineData("\"Total: \"General", "'Total: ' General")]
	[InlineData("General\" units\"", "General ' units'")]
	[InlineData("0.00_);(0.00)", "0I . 0D 0D ' '")]
	[InlineData("_(* #,##0_)", "' ' #I #I #I 0I ' '")]
	[InlineData("$#,##0", "'$' #I #I #I 0I")]
	[InlineData("0\\x", "0I 'x'")]
	[InlineData("0!x", "0I '!x'")]
	[InlineData("000-00-0000", "0I 0I 0I '-' 0I 0I '-' 0I 0I 0I 0I")]
	[InlineData("# ?/?", "#I ' ' ?N / ?V")]
	[InlineData("# ??/16", "#I ' ' ?N ?N / =16")]
	[InlineData("# ?/10", "#I ' ' ?N / =10")]
	[InlineData("0/ 8", "0N / ' ' =8")]
	[InlineData("?/?\" in\"", "?N / ?V ' in'")]
	[InlineData("#,### ?/?", "#I #I #I #I ' ' ?N / ?V")]
	[InlineData("0\\/0", "0I '/' 0I")]
	[InlineData("yyyy-mm-dd hh:mm:ss.000", "yyyy '-' MM '-' dd ' ' hh ':' mm ':' ss '.' 000")]
	[InlineData("m/d/yyyy h:mm", "M '/' d '/' yyyy ' ' h ':' mm")]
	[InlineData("mm:ss.0", "mm ':' ss '.' 0")]
	[InlineData("[h]:mm:ss", "[h] ':' mm ':' ss")]
	[InlineData("[hhh]:mm", "[hhh] ':' mm")]
	[InlineData("[h] yyyy", "[h] ' ' yyyy")]
	[InlineData("h d m", "h ' ' d ' ' m")]
	[InlineData("h mmm m", "h ' ' MMM ' ' m")]
	[InlineData("h AM/PM m", "h ' ' AM/PM ' ' m")]
	[InlineData("ss m", "ss ' ' m")]
	[InlineData("s d m", "s ' ' d ' ' m")]
	[InlineData("[h] d m", "[h] ' ' d ' ' m")]
	[InlineData("hh:mm:mm", "hh ':' mm ':' MM")]
	[InlineData("h:mm d m", "h ':' mm ' ' d ' ' M")]
	[InlineData("mm ss mm", "mm ' ' ss ' ' MM")]
	[InlineData("m yyyy s", "M ' ' yyyy ' ' s")]
	[InlineData("m:s.0", "m ':' s '.' 0")]
	[InlineData("[s] m", "[s] ' ' M")]
	[InlineData("[mm] m", "[mm] ' ' M")]
	[InlineData("h:mm:ss.000.0", "h ':' mm ':' ss '.' 000 '.' 0")]
	[InlineData("yyyy.00", "yyyy '.' 00")]
	[InlineData("d \"days\" h:mm", "d ' days ' h ':' mm")]
	[InlineData("h:mm AM/PM", "h ':' mm ' ' AM/PM")]
	[InlineData("h:mm a/p", "h ':' mm ' ' A/P:ap")]
	[InlineData("h:mm a", "h ':' mm ' a'")]
	[InlineData("h:mm AM", "h ':' mm ' A' M")]
	[InlineData("h:mm A/P/X", "h ':' mm ' ' A/P:AP '/X'")]
	[InlineData("dddd, mmmm d, yyyy", "dddd ', ' MMMM ' ' d ', ' yyyy")]
	[InlineData("mmmmm yy yyy y", "MMMMM ' ' yy ' ' yyy ' ' y")]
	[InlineData("aaa aaaa", "aaa ' ' aaaa")]
	[InlineData("e ee b bb bbbb g ggg", "e ' ' ee ' ' b ' ' bb ' ' bbbb ' ' g ' ' ggg")]
	[InlineData("B2yyyy", "yyyy")]
	[InlineData("YYYY-MM-DD", "yyyy '-' MM '-' dd")]
	[InlineData("\"note: \"@", "'note: ' @")]
	[InlineData("[Red]0", "0I")]
	[InlineData("[Color 10]0", "0I")]
	[InlineData("[Red]", "")]
	public void ReadsTokens(string code, string expected)
	{
		var compiled = FormatCode.Compile(code);
		Assert.True(compiled.IsReadable, code);
		var section = compiled.Numbers.Length > 0 ? compiled.Numbers[0] : compiled.Text!;
		Assert.Equal(expected, Describe(compiled, section));
	}

	/// <summary>A number section's facts: placeholders, grouping, scaling, percents and the exponent.</summary>
	[Fact]
	public void CountsANumbersParts()
	{
		var section = First("#,##0.00");
		Assert.Equal(FormatSectionKind.Number, section.Kind);
		Assert.Equal(6, section.Placeholders);
		Assert.Equal(4, section.IntegerPlaceholders);
		Assert.Equal("###0", section.IntegerKinds);
		Assert.Equal(2, section.DecimalPlaceholders);
		Assert.True(section.Grouping);
		Assert.Equal(0, section.Scale);

		section = First("#,##0,,");
		Assert.True(section.Grouping);
		Assert.Equal(2, section.Scale);

		section = First("0,0");
		Assert.True(section.Grouping);
		Assert.Equal("00", section.IntegerKinds);

		section = First("0%%");
		Assert.Equal(2, section.Percents);

		section = First("00.00E+000");
		Assert.True(section.HasExponent);
		Assert.Equal(2, section.IntegerPlaceholders);
		Assert.Equal(2, section.DecimalPlaceholders);
		Assert.Equal(3, section.ExponentPlaceholders);
		Assert.Equal(3, section.ExponentDigits);

		section = First("#E+#");
		Assert.Equal(1, section.ExponentDigits);
		Assert.Equal('+', section.Tokens.Single(t => t.Kind == FormatTokenKind.Exponent).Placeholder);

		section = First("\"zero\"");
		Assert.Equal(0, section.Placeholders);
		Assert.False(section.IsEmpty);
	}

	/// <summary>A fraction's facts: its whole number, numerator, and a denominator of placeholders or a number.</summary>
	[Fact]
	public void CountsAFractionsParts()
	{
		var section = First("# ??/??");
		Assert.Equal(FormatSectionKind.Fraction, section.Kind);
		Assert.Equal(1, section.IntegerPlaceholders);
		Assert.Equal(2, section.NumeratorPlaceholders);
		Assert.Equal(2, section.DenominatorPlaceholders);
		Assert.Equal(0, section.Denominator);

		section = First("# ?/8");
		Assert.Equal(0, section.DenominatorPlaceholders);
		Assert.Equal(8, section.Denominator);

		section = First("# ??/100");
		Assert.Equal(100, section.Denominator);

		section = First("?/?");
		Assert.Equal(0, section.IntegerPlaceholders);
		Assert.Equal(1, section.NumeratorPlaceholders);

		section = First("#,### ?/?");
		Assert.True(section.Grouping);
		Assert.Equal(4, section.IntegerPlaceholders);

		// The parts' placeholders, and where the fraction lies among the tokens: "a", the whole number, " x ", the
		// numerator, the bar, the denominator, " in".
		section = First("\"a\"#,##0\" x \"?#/0?\" in\"");
		Assert.Equal("###0", section.IntegerKinds);
		Assert.Equal("?#", section.NumeratorKinds);
		Assert.Equal("0?", section.DenominatorKinds);
		Assert.Equal(5, section.WholeEnd);
		Assert.Equal(6, section.NumeratorStart);
		Assert.Equal(11, section.FractionEnd);
		Assert.Equal(12, section.Tokens.Length);

		section = First("?/16\" in\"");
		Assert.Equal(0, section.WholeEnd);
		Assert.Equal(0, section.NumeratorStart);
		Assert.Equal(3, section.FractionEnd);
		Assert.Equal("", section.DenominatorKinds);
		Assert.Equal(32768, First("?/32768").Denominator);

		Assert.Equal(FormatSectionKind.Number, First("0/").Kind);
		Assert.Equal(FormatSectionKind.Number, First("/0").Kind);
		Assert.Equal(FormatSectionKind.Number, First("0/\"8\"").Kind);
	}

	/// <summary>A date or time section's facts: 12 hours, seconds, fractions of a second, and elapsed time alone.</summary>
	[Fact]
	public void CountsADatesParts()
	{
		var section = First("h:mm:ss.00 AM/PM");
		Assert.Equal(FormatSectionKind.Date, section.Kind);
		Assert.True(section.TwelveHour);
		Assert.True(section.ShowsSeconds);
		Assert.Equal(2, section.SubSecondDigits);
		Assert.False(section.ElapsedAlone);

		section = First("h:mm");
		Assert.False(section.TwelveHour);
		Assert.False(section.ShowsSeconds);

		Assert.True(First("[h]").ElapsedAlone);
		Assert.True(First("[ss]\" seconds\"").ElapsedAlone);
		Assert.False(First("[h]:mm").ElapsedAlone);
		Assert.True(First("[s].000").ShowsSeconds);
		Assert.True(FormatCode.Compile("yyyy-mm-dd").HasDate);
		Assert.True(FormatCode.Compile("0;[h]").HasDate);
		Assert.False(FormatCode.Compile("0.00").HasDate);
		Assert.True(FormatCode.Compile("[$-F800]0.00").HasDate);
	}

	/// <summary>Sections: positive, negative, zero and text; the fourth is text's, and so is a last section with <c>@</c>.</summary>
	/// <param name="code">The code.</param>
	/// <param name="numbers">How many sections numbers take.</param>
	/// <param name="text">Whether it has a text section.</param>
	[Theory]
	[InlineData("General", 1, false)]
	[InlineData("0;-0", 2, false)]
	[InlineData("0;-0;\"zero\"", 3, false)]
	[InlineData("0;-0;\"zero\";@", 3, true)]
	[InlineData("0;0;0;\"text\"", 3, true)]
	[InlineData("@", 0, true)]
	[InlineData("\"t:\"@\"/\"@", 0, true)]
	[InlineData("0;@", 1, true)]
	[InlineData("0;-0;@", 2, true)]
	[InlineData(";;;", 3, true)]
	[InlineData(";0", 2, false)]
	[InlineData("", 1, false)]
	[InlineData("[>5]0", 2, false)]
	[InlineData("[>100]0;0.00", 2, false)]
	[InlineData("[>100]\"big\";[<-100]\"small\";\"mid\"", 3, false)]
	public void SplitsSections(string code, int numbers, bool text)
	{
		var compiled = FormatCode.Compile(code);
		Assert.True(compiled.IsReadable);
		Assert.Equal(numbers, compiled.Numbers.Length);
		Assert.Equal(text, compiled.Text is not null);
	}

	/// <summary>Empty sections, and one of a locale alone, show nothing; one of a color, a condition or DBNum alone General.</summary>
	[Fact]
	public void KnowsEmptySections()
	{
		var compiled = FormatCode.Compile(";;;");
		Assert.All(compiled.Numbers, section => Assert.True(section.IsEmpty));
		Assert.True(compiled.Text!.IsEmpty);
		Assert.True(First("[$-409]").IsEmpty);
		Assert.False(First("[Red]").IsEmpty);
		Assert.False(First("[DBNum1]").IsEmpty);
		Assert.False(First("[>5]").IsEmpty);
		Assert.False(First("[$-F800]").IsEmpty);
		Assert.False(First("[$USD]").IsEmpty);
	}

	/// <summary>Conditions: each operator and its number; one number section with a condition is followed by General.</summary>
	[Fact]
	public void ReadsConditions()
	{
		var compiled = FormatCode.Compile("[>5]0");
		Assert.True(compiled.IsConditional);
		Assert.Equal(ConditionOperator.Greater, compiled.Numbers[0].Condition);
		Assert.Equal(5, compiled.Numbers[0].Operand);
		Assert.Equal(ConditionOperator.None, compiled.Numbers[1].Condition);
		Assert.True(compiled.Numbers[1].HasGeneral);
		Assert.Equal("General", Describe(compiled, compiled.Numbers[1]));

		var cases = new (string Code, ConditionOperator Operator, double Operand)[]
		{
			("[=0]0", ConditionOperator.Equal, 0),
			("[<>-1]0", ConditionOperator.NotEqual, -1),
			("[<-10]0", ConditionOperator.Less, -10),
			("[<=9999999]0", ConditionOperator.LessOrEqual, 9999999),
			("[>0.5]0", ConditionOperator.Greater, 0.5),
			("[>=-100.25]0", ConditionOperator.GreaterOrEqual, -100.25),
		};
		foreach (var (code, op, operand) in cases)
		{
			var section = First(code);
			Assert.Equal(op, section.Condition);
			Assert.Equal(operand, section.Operand);
		}

		Assert.False(FormatCode.Compile("0;-0").IsConditional);
		var less = First("[<0]0");
		Assert.True(less.Takes(-1));
		Assert.False(less.Takes(0));
		Assert.True(First("0").Takes(-1));
		Assert.True(First("[<>0]0").Takes(1));
		Assert.False(First("[<>0]0").Takes(0));
		Assert.True(First("[>=1]0").Takes(1));
		Assert.True(First("[<=1]0").Takes(1));
		Assert.True(First("[=1]0").Takes(1));
		Assert.False(First("[>1]0").Takes(1));
	}

	/// <summary>Locale codes: a currency's text, a language's names, the system formats, tags; calendars and numerals ignored.</summary>
	[Fact]
	public void ReadsLocaleCodes()
	{
		Assert.Equal("'€ ' #I . 0D 0D", Describe(FormatCode.Compile("[$€-407] #.00")));
		Assert.Equal("'USD ' #I", Describe(FormatCode.Compile("[$USD] #")));
		Assert.Equal("'$' 0I", Describe(FormatCode.Compile("[$$-409]0")));
		Assert.Equal("0I", Describe(FormatCode.Compile("[$-407]0")));
		Assert.True(First("[$-407]0").HasLocale);

		Assert.Equal("Januar", Encoding.UTF8.GetString(First("[$-407]mmmm").Names!.Months[0]));
		Assert.Equal("January", Encoding.UTF8.GetString(First("[$-409]mmmm").Names!.Months[0]));
		Assert.Same(DateNames.English, First("[$-409]mmmm").Names);
		Assert.Equal("Januar", Encoding.UTF8.GetString(First("[$-de-DE]mmmm").Names!.Months[0]));
		Assert.Equal("January", Encoding.UTF8.GetString(First("[$-en-US]mmmm").Names!.Months[0]));
		Assert.Same(DateNames.English, First("[$-1060409]d/m/yyyy").Names);
		Assert.Same(DateNames.English, First("[$-D000409]0").Names);
		Assert.Equal("Januar", Encoding.UTF8.GetString(First("[$-1060407]mmmm").Names!.Months[0]));
		Assert.Null(First("[$-2000000]0").Names);

		// An id of more than eight hex digits is its low 32 bits, and one the runtime doesn't know names no language, as
		// Excel reads them (Phase 8's step 8): [$-123456789] is [$-23456789], whose 6789 Excel drops.
		Assert.True(First("[$-123456789]mmmm").HasLocale);
		Assert.Null(First("[$-123456789]mmmm").Names);
		Assert.Equal("Januar", Encoding.UTF8.GetString(First("[$-100000407]mmmm").Names!.Months[0]));
		Assert.Null(First("[$-1234]mmmm").Names);
		Assert.Equal("Januar", Encoding.UTF8.GetString(First("[$-de]mmmm").Names!.Months[0]));
		Assert.Null(First("[$-ABCD]mmmm").Names);
		Assert.Null(First("mmmm").Names);

		Assert.Equal(SystemFormat.LongDate, First("[$-F800]dddd, mmmm dd, yyyy").System);
		Assert.Equal(SystemFormat.LongDate, First("[$-x-sysdate]dddd").System);
		Assert.Equal(SystemFormat.LongTime, First("[$-F400]h:mm:ss AM/PM").System);
		Assert.Equal(SystemFormat.LongTime, First("[$-x-systime]h").System);
		Assert.Equal(SystemFormat.LongDate, First("[$-F800]0.00").System);
		Assert.Equal(SystemFormat.None, First("[$-409]h").System);

		// A system format counts in the first section alone, and there it is every date section's.
		var spread = FormatCode.Compile("[$-F800]dddd;[$-F400]h:mm;0");
		Assert.Equal(SystemFormat.LongDate, spread.Numbers[1].System);
		Assert.Equal(SystemFormat.None, spread.Numbers[2].System);
		Assert.Equal(SystemFormat.None, FormatCode.Compile("0;[$-F800]dddd").Numbers[1].System);
		Assert.Equal(SystemFormat.None, FormatCode.Compile("0;0;[$-F400]h").Numbers[2].System);
		Assert.True(FormatCode.Compile("0;[$-F800]dddd").HasDate);

		Assert.True(First("[DBNum1][$-804]0").HasDBNum);
		Assert.True(First("B1yyyy").HasCalendar);
		Assert.True(First("[Red]0").HasColor);
		Assert.True(First("[color56]0").HasColor);
		Assert.True(First("[ Blue ]0").HasColor);
	}

	/// <summary>Literal text is UTF-8, surrogate pairs whole: escaped, quoted, and as the character after <c>_</c> or <c>*</c>.</summary>
	[Fact]
	public void KeepsLiteralsInUtf8()
	{
		var grin = char.ConvertFromUtf32(0x1F600);
		Assert.Equal("'é' 0I", Describe(FormatCode.Compile("\"é\"0")));
		Assert.Equal("'" + grin + "' 0I", Describe(FormatCode.Compile("\\" + grin + "0")));
		// _ and * take one UTF-16 unit, as Excel does (Phase 8's step 8): a pair's low surrogate shows as U+FFFD.
		Assert.Equal("' " + (char)0xFFFD + "' 0I", Describe(FormatCode.Compile("_" + grin + "0")));
		Assert.Equal("'" + (char)0xFFFD + "' 0I", Describe(FormatCode.Compile("*" + grin + "0")));
		Assert.Equal("'" + grin + "' 0I", Describe(FormatCode.Compile(grin + "0")));
		Assert.Equal("'年' yyyy", Describe(FormatCode.Compile("\"年\"yyyy")));
	}

	/// <summary>Every built-in id's code reads, in Excel en-US and in other cultures.</summary>
	[Fact]
	public void ReadsEveryBuiltInId()
	{
		foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("ja-JP"), CultureInfo.GetCultureInfo("fr-FR") })
		{
			var display = DisplayCulture.For(culture);
			for (var id = 0; id < BuiltInFormats.FirstCustom; id++)
			{
				Assert.True(FormatCode.Compile(display.BuiltIn(id)).IsReadable, $"{culture.Name} {id}: {display.BuiltIn(id)}");
			}
		}

		Assert.Same(FormatCode.General, FormatCode.General);
		Assert.Equal("General", Describe(FormatCode.General));
	}

	/// <summary>Random codes compile without throwing: those the code fuzzer of Phase 5 builds, and random characters.</summary>
	[Fact]
	public void CompilesAnyCode()
	{
		var random = new Random(81524);
		var alphabet = "0#?.,%/:;@\"\\_*[]$-+()ESeEyMmdhsAaPpGgBbn1234567890 <>=!€" + char.ConvertFromUtf32(0x1F600) + (char)0xD800;
		for (var i = 0; i < 50_000; i++)
		{
			var length = random.Next(0, 24);
			var code = new StringBuilder(length);
			for (var j = 0; j < length; j++)
			{
				code.Append(alphabet[random.Next(alphabet.Length)]);
			}

			var compiled = FormatCode.Compile(code.ToString());
			if (compiled.IsReadable)
			{
				Assert.InRange(compiled.Numbers.Length, 0, 4);
				foreach (var section in compiled.Numbers)
				{
					Describe(compiled, section);
				}
			}
		}
	}

	/// <summary>The first section a code shows numbers in, or its text section.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The section.</returns>
	private static FormatSection First(string code)
	{
		var compiled = FormatCode.Compile(code);
		Assert.True(compiled.IsReadable, code);

		return compiled.Numbers.Length > 0 ? compiled.Numbers[0] : compiled.Text!;
	}

	/// <summary>A code's first section's tokens in the short form.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The tokens.</returns>
	private static string Describe(FormatCode code) => Describe(code, code.Numbers.Length > 0 ? code.Numbers[0] : code.Text!);

	/// <summary>A section's tokens in the short form.</summary>
	/// <param name="code">The code, which holds the literals.</param>
	/// <param name="section">The section.</param>
	/// <returns>The tokens.</returns>
	private static string Describe(FormatCode code, FormatSection section)
	{
		var parts = new List<string>();
		foreach (var token in section.Tokens)
		{
			var text = Encoding.UTF8.GetString(code.Literal(token).ToArray());
			parts.Add(token.Kind switch
			{
				FormatTokenKind.Literal => "'" + text + "'",
				FormatTokenKind.Digit => token.Placeholder + (token.Role switch
				{
					PlaceholderRole.Integer => "I",
					PlaceholderRole.Decimal => "D",
					PlaceholderRole.Exponent => "E",
					PlaceholderRole.Numerator => "N",
					PlaceholderRole.Denominator => "V",
					_ => "-",
				}),
				FormatTokenKind.Point => ".",
				FormatTokenKind.Exponent => "E" + token.Placeholder,
				FormatTokenKind.Bar => "/",
				FormatTokenKind.FixedDenominator => "=" + text,
				FormatTokenKind.At => "@",
				FormatTokenKind.General => "General",
				FormatTokenKind.Year => new string('y', token.Count),
				FormatTokenKind.Month => new string('M', token.Count),
				FormatTokenKind.Day => new string('d', token.Count),
				FormatTokenKind.WeekDay => new string('a', token.Count),
				FormatTokenKind.Hour => new string('h', token.Count),
				FormatTokenKind.Minute => new string('m', token.Count),
				FormatTokenKind.Second => new string('s', token.Count),
				FormatTokenKind.SubSecond => new string('0', token.Count),
				FormatTokenKind.ElapsedHours => "[" + new string('h', token.Count) + "]",
				FormatTokenKind.ElapsedMinutes => "[" + new string('m', token.Count) + "]",
				FormatTokenKind.ElapsedSeconds => "[" + new string('s', token.Count) + "]",
				FormatTokenKind.AmPm => "AM/PM",
				FormatTokenKind.ShortAmPm => "A/P:" + text,
				FormatTokenKind.EraYear => new string('e', token.Count),
				FormatTokenKind.Era => new string('g', token.Count),
				FormatTokenKind.BuddhistYear => new string('b', token.Count),
				_ => "?",
			});
		}

		return string.Join(" ", parts);
	}
}
