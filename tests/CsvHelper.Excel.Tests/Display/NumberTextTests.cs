using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// The text numbers show in format codes, and text and booleans through text sections (<see cref="NumberText"/>), against
/// what Excel 365 (16.0 build 20527, en-US) showed through COM on 2026-11-16: every number, text and boolean cell of Phase
/// 8's design probes and step 3's (<see cref="ExcelAnswers"/>), and Phase 5's answers about General and number codes.
/// </summary>
public partial class NumberTextTests
{
	/// <summary>
	/// The cells whose text differs from Excel's, by their codes, each with its reason and how many cells it covers: what
	/// Phase 8's decision 8 leaves out, and the codes the renderer can't read though Excel shows them, whose cells show their
	/// raw text (decision 5).
	/// </summary>
	private static readonly (string Reason, Func<string, bool> Applies, int Cells)[] Differences =
	[
		("[DBNum1] to [DBNum3] show East Asian numerals, which aren't rendered (decision 8)", code => code.Contains("DBNum"), 16),
		("a locale code's numeral system (Arabic-Indic, Thai) isn't rendered (decision 8)", code => code.Contains("[$-2000000]") || code.Contains("[$-D000409]"), 6),
		("a fixed denominator past 32,768 isn't read: Excel shows fractions that aren't the value (?/50000 shows 0.5 as 1/5)", code => !FormatCode.Compile(code).IsReadable, 81),
	];

	/// <summary>Every number, text and boolean cell of the probes shows Excel's text, but for the listed differences; dates are step 4's.</summary>
	[Fact]
	public void ShowsExcelsAnswers()
	{
		var failures = new List<string>();
		var checkedCells = 0;
		var listed = new int[Differences.Length];
		foreach (var (id, code, builtIn, kind, value, expected) in ExcelAnswers)
		{
			var compiled = FormatCode.Compile(code ?? DisplayCulture.Invariant.BuiltIn(builtIn));
			if (compiled.HasDate)
			{
				continue;
			}

			var shown = kind == 'n' ? Render(compiled, double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)) :
				RenderText(compiled, kind == 'b' ? (value == "1" ? "TRUE" : "FALSE") : value);
			checkedCells++;
			if (shown == expected)
			{
				continue;
			}

			var difference = Array.FindIndex(Differences, d => d.Applies(compiled.Code));
			if (difference >= 0)
			{
				listed[difference]++;
			}
			else
			{
				failures.Add($"{id} {compiled.Code} {value}: Excel '{expected ?? "#"}', shown '{shown ?? "#"}'");
			}
		}

		Assert.True(failures.Count == 0, $"{failures.Count} of {checkedCells}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
		for (var i = 0; i < Differences.Length; i++)
		{
			Assert.True(Differences[i].Cells == listed[i], $"{Differences[i].Reason}: {listed[i]} cells, listed {Differences[i].Cells}");
		}

		Assert.True(checkedCells > 4_000, $"{checkedCells} cells");
	}

	/// <summary>Phase 5's answers about General: each number's text.</summary>
	[Fact]
	public void ShowsPhase5sGeneralAnswers()
	{
		var count = 0;
		foreach (var (key, entries) in NumberFormatWidthTests.Lines(NumberFormatWidthTests.General))
		{
			var value = double.Parse(key, CultureInfo.InvariantCulture);
			Assert.Equal(NumberFormatWidthTests.Parse(entries).Single().Text, Render("General", value));
			count++;
		}

		Assert.True(count > 90);
	}

	/// <summary>
	/// Phase 5's answers about number codes, but for those whose text Excel gave at a column's width: fills, and <c>@</c>
	/// and <c>"$"General</c>, whose General Excel shortened to the column.
	/// </summary>
	[Fact]
	public void ShowsPhase5sCodeAnswers()
	{
		var count = 0;
		foreach (var (code, entries) in NumberFormatWidthTests.Lines(NumberFormatWidthTests.Codes))
		{
			if (code.Contains('*') || code is "@" or "\"$\"General")
			{
				continue;
			}

			var texts = NumberFormatWidthTests.Parse(entries);
			for (var i = 0; i < NumberFormatWidthTests.Values.Length; i++)
			{
				Assert.Equal(texts[i].Text, Render(code, NumberFormatWidthTests.Values[i]));
				count++;
			}
		}

		Assert.True(count > 600, $"{count} cells");
	}

	/// <summary>Each rule, as Excel showed it (the probes above).</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="expected">Excel's text.</param>
	[Theory]
	// 15 digits, then half away from zero to the places shown.
	[InlineData("0.00", 2.675, "2.68")]
	[InlineData("0.00", 1.005, "1.01")]
	[InlineData("0", 1.2345678901234568E+17, "123456789012346000")]
	[InlineData("0.00", -0.004, "0.00")]
	[InlineData("0.00", -0.005, "-0.01")]
	[InlineData("0.0E+00", 9.95, "1.0E+01")]
	// Placeholders, grouping wherever the comma stands, scaling, percents.
	[InlineData("#.##", 0, ".")]
	[InlineData("?.??", 0.05, " .05")]
	[InlineData("0.0??", 0.05, "0.05 ")]
	[InlineData("#0.0#0", 12, "12.00")]
	[InlineData(".00", 12, "12.00")]
	[InlineData("\"x\".00", 12, "x12.00")]
	[InlineData("0-0", 1234.5678, "123-5")]
	[InlineData("0\"a\"0\"b\"0", 1234567.891, "12345a6b8")]
	[InlineData("#,##0-00", 1234567.891, "1,234,5-68")]
	[InlineData("#,##0\" \"000", 12, "0, 012")]
	[InlineData("?,???", 0, "     ")]
	[InlineData("?,???,???", 12, "       12")]
	[InlineData(",##0", 1234567.891, ",1234568")]
	[InlineData("#,", 1E+20, "100000000000000000")]
	[InlineData("0.0,,\"M\"", -1234.5678, "0.0M")]
	[InlineData("0,.0", 1234567.891, "1234.6")]
	[InlineData("0.0,0", 12, "12.00")]
	[InlineData("0.0%,", 12, "1200.0%,")]
	[InlineData("%0", 0.05, "%5")]
	[InlineData("0%%", 0.05, "500%%")]
	[InlineData("0.0.0", 1234567.891, "1234567.8.9")]
	// Exponents: a multiple of the integer placeholders, placed as integers, without percents.
	[InlineData("##0.0E+0", 1E-100, "100.0E-102")]
	[InlineData("##0.0E+0", 0, "000.0E+0")]
	[InlineData("#,##0.0E+0", 1234567.891, "123.5E+4")]
	[InlineData("#,##0.0E+0", 0, "0,000.0E+0")]
	[InlineData("?0.0E+0", 1E+100, " 1.0E+100")]
	[InlineData("?.?E+0", 0, "0. E+0")]
	[InlineData("0.0E+??", 1234.5, "1.2E+ 3")]
	[InlineData("0.0E+0?", 5, "5.0E+00")]
	[InlineData("0.0E+##", 0, "0.0E+0")]
	[InlineData("0.00E-00", 1234.5678, "1.23E03")]
	[InlineData("0.0E+0%", 1234.5, "1.2E+3%")]
	[InlineData("%0.0E+0", 1234.5, "%1.2E+3")]
	// Sections and signs.
	[InlineData("\"x\"", -0.001, "-x")]
	[InlineData("\"Total: \"General", -0.5, "-Total: 0.5")]
	[InlineData("0;(0)", -0.4, "(0)")]
	[InlineData("0;-0;\"zero\"", -0.0, "zero")]
	[InlineData("0;-0;\"zero\"", -1E-20, "-0")]
	[InlineData("0;\"neg\"", 0, "0")]
	[InlineData(";0", 5, "")]
	[InlineData("@@", -50, "-50")]
	[InlineData("[Red]", -1, "-1")]
	[InlineData("0;[Red]", -1, "1")]
	[InlineData("[$-409]", 1234.5, "")]
	[InlineData("[>5]", 7, "7")]
	[InlineData("[>5];0", 7, "7")]
	// Conditions: the places' conditions, the rest, and the minus signs.
	[InlineData("[<-100]0;0", -50, "50")]
	[InlineData("[>100]0;0.00", -50, "-50.00")]
	[InlineData("[>-10]0;0", -20, "20")]
	[InlineData("[>-10]0;0", -5, "-5")]
	[InlineData("[=-1]0;0", -1, "1")]
	[InlineData("[<>-1]0;0", -1, "1")]
	[InlineData("[=0]0;0", -5, "-5")]
	[InlineData("[<0]0", -0.5, "1")]
	[InlineData("[<-10]0", -0.5, "0.5")]
	[InlineData("[>100]\"big\";[<-100]\"small\";\"mid\"", -50, "-mid")]
	[InlineData("[>100]0;0.00;\"x\"", -150, "150.00")]
	[InlineData("[>100]0;0.00;\"x\"", 50, "x")]
	[InlineData("0;[<-5]0;\"z\"", -0.5, "-z")]
	[InlineData("0.0;[>100]0", 150, "150.0")]
	[InlineData("[>100]0;[<-100]0;", -50, "-")]
	// Fractions: convergents in doubles, fixed denominators, zero fractions, and literals before a hidden whole number.
	[InlineData("# ?/?", 0.3, " 2/7")]
	[InlineData("# ?/?", 0.1, "0    ")]
	[InlineData("# ?/?", -0.001, "-0    ")]
	[InlineData("# ??/??", 3.14159265358979, "3  1/7 ")]
	[InlineData("# ??/??", 0.37, " 10/27")]
	[InlineData("# ?/?", 0.37, " 3/8")]
	[InlineData("??/??", 0.37, "10/27")]
	[InlineData("???/???", 0.999, "998/999")]
	[InlineData("?/?", 12345.678, "37037/3")]
	[InlineData("?/?", -0.001, "0/1")]
	[InlineData("# #/#", 5, "5")]
	[InlineData("# 0/0", 0, "0/1")]
	[InlineData("# 0/0", 5, "5 0/1")]
	[InlineData("#\"x\"?/?", 0.5, " 1/2")]
	[InlineData("#\" - \"?/?", 0.5, "   1/2")]
	[InlineData("#\"x\"#/#", 0.5, "1/2")]
	[InlineData("0\"x\"?/?", 0.5, "0x1/2")]
	[InlineData("# ?/?\" in\"", 0, "0     in")]
	[InlineData("# ?#/#?", 0, "0     ")]
	[InlineData("# 0?/?0", 0.5, "01/20")]
	[InlineData("#,### ?/?", 1234567.1, "1,234,567 1/9")]
	[InlineData("# ?/?", 123456789012345.67, "123456789012346 2/3")]
	[InlineData("# ?/8", 0.25, " 2/8")]
	[InlineData("# ?/2", 0.75, "1    ")]
	[InlineData("# ?/10", 0.05, " 1/10")]
	[InlineData("?/16", 2047.9375, "32767/16")]
	[InlineData("?/?", 1073741823.5, "2147483647/2")]
	[InlineData("?/?", 1073741824.5, "1073741825/1")]
	[InlineData("?/?", 999999999.75, "1000000000/1")]
	[InlineData("?/?", 2147483646.5, "2147483647/1")]
	[InlineData("?/?", 536870912.25, "536870912/1")]
	[InlineData("??/??", 123456789.123, "987654313/8 ")]
	[InlineData("?/0?", 0.5, "1/02")]
	[InlineData("?/?0", 0.5, "1/20")]
	[InlineData("?/#0", 0.5, "1/2")]
	[InlineData("?/??0", 0.5, "1/2 0")]
	[InlineData("?#/??", 0.5, " 1/2 ")]
	// General.
	[InlineData("General", 123456789012, "1.23457E+11")]
	[InlineData("General", 0.30000000000000004, "0.3")]
	[InlineData("General", 1.7976931348623157E+308, "1.7977E+308")]
	[InlineData("General", 5E-324, "0")]
	// Excel holds a number below the smallest normal double as 0, which takes the zero section.
	[InlineData("0;-0;\"zero\"", -5E-324, "zero")]
	[InlineData("[<0]\"neg\";\"pos\"", -2.2E-308, "pos")]
	[InlineData("0;-0;\"zero\"", -2.2250738585072014E-308, "-0")]
	[InlineData("0.0E+00", 2.2250738585072014E-308, "2.2E-308")]
	[InlineData("General", -1.2345E-05, "-0.000012345")]
	public void ShowsEachRule(string code, double value, string expected)
	{
		Assert.Equal(expected, Render(code, value));
	}

	/// <summary>Values Excel can't show, codes the renderer can't read, and, until step 4, dates: the cell shows its raw text.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	[Theory]
	[InlineData("[>5]\"a\";[<-5]\"b\"", 0)]
	[InlineData("0;[>5]0", -5)]
	[InlineData("?/?", 2147483647)]
	[InlineData("?/?", -2147483647)]
	[InlineData("?/?", 1E+20)]
	[InlineData("0/4", 8192)]
	[InlineData("?/32768", 0.99999)]
	[InlineData("# ?/32768", 0.99999)]
	[InlineData("0", 1E+255)]
	[InlineData("0", -1E+254)]
	[InlineData("\"ab\"0", 1E+253)]
	[InlineData("#,##0", 1E+192)]
	[InlineData("# ?/?", 1E+251)]
	[InlineData("0", double.NaN)]
	[InlineData("0", double.PositiveInfinity)]
	[InlineData("?/50000", 0.5)]
	[InlineData("0;0;[=0]\"z\"", 0)]
	[InlineData("0.00e+00", 1)]
	[InlineData("yyyy-mm-dd", 45306)]
	[InlineData("[$-F800]0.00", 45306)]
	public void CantShow(string code, double value)
	{
		Assert.Null(Render(code, value));
	}

	/// <summary>The longest texts Excel shows: 255 characters, the minus sign and literals counted.</summary>
	[Fact]
	public void ShowsUpTo255Characters()
	{
		Assert.Equal(255, Render("0", 1E+254)!.Length);
		Assert.Equal(255, Render("0", -1E+253)!.Length);
		Assert.Equal(255, Render("\"ab\"0", 1E+252)!.Length);
		Assert.Equal(255, Render("#,##0", 1E+191)!.Length);
		Assert.Equal(255, Render("# ?/?", 1E+250)!.Length);
	}

	/// <summary>Text and booleans through a text section; <see langword="null"/> where the text shows as it is or can't be shown.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The text.</param>
	/// <param name="expected">Excel's text, or <see langword="null"/>.</param>
	[Theory]
	[InlineData("\"a\"@\"b\"", "abc", "aabcb")]
	[InlineData("\"a\"@\"b\"", "TRUE", "aTRUEb")]
	[InlineData("@@", "FALSE", "FALSEFALSE")]
	[InlineData("@\" \"@", "abc", "abc abc")]
	[InlineData("0;-0;0;\"t: \"@", "abc", "t: abc")]
	[InlineData("\"pos\";\"neg\";\"zero\";\"text\"", "abc", "text")]
	[InlineData(";;;", "abc", "")]
	[InlineData("@*-", "abc", "abc")]
	[InlineData("_(@_)", "abc", " abc ")]
	[InlineData("0.00", "abc", null)]
	[InlineData("General", "abc", null)]
	[InlineData("0.00e+00;@", "abc", null)]
	public void ShowsTextThroughTheTextSection(string code, string value, string? expected)
	{
		var compiled = FormatCode.Compile(code);
		var room = new byte[1024];
		var text = new ShownText(room);
		var shown = NumberText.TryWriteText(compiled, Encoding.UTF8.GetBytes(value), ref text);
		Assert.Equal(expected, shown ? Encoding.UTF8.GetString(text.Written.ToArray()) : null);
	}

	/// <summary>Text past 255 characters can't be shown through a text section, but for <c>@</c> alone.</summary>
	[Fact]
	public void ShowsTextUpTo255Characters()
	{
		Assert.Equal(new string('e', 1100), RenderText(FormatCode.Compile("@"), new string('e', 1100)));
		Assert.Equal("x" + new string('a', 254), RenderText(FormatCode.Compile("\"x\"@"), new string('a', 254)));
		Assert.Null(RenderText(FormatCode.Compile("\"x\"@"), new string('b', 255)));
		Assert.NotNull(RenderText(FormatCode.Compile("@\"y\"@"), new string('a', 127)));
		Assert.Null(RenderText(FormatCode.Compile("@\"y\"@"), new string('b', 128)));
	}

	/// <summary>Another culture's decimal and group separators, in codes and in General; <c>?</c>'s spaces stay spaces.</summary>
	[Fact]
	public void ShowsTheCulturesSeparators()
	{
		var german = CultureInfo.GetCultureInfo("de-DE");
		Assert.Equal("1.234.567,89", Render("#,##0.00", 1234567.891, german));
		Assert.Equal("1,5", Render("General", 1.5, german));
		Assert.Equal("1,23457E+11", Render("General", 123456789012, german));
		Assert.Equal("1,2E+03", Render("0.0E+00", 1234.5, german));
		Assert.Equal("12,00", Render(".00", 12, german));
		Assert.Equal("     ", Render("?,???", 0, german));

		var french = CultureInfo.GetCultureInfo("fr-FR");
		var group = french.NumberFormat.NumberGroupSeparator;
		Assert.Equal("1" + group + "234" + group + "567,89", Render("#,##0.00", 1234567.891, french));
	}

	/// <summary>A text written into too little room counts the bytes it needs, and its characters as UTF-16 units.</summary>
	[Fact]
	public void CountsWhatDoesntFit()
	{
		var code = FormatCode.Compile("\"é\"#,##0.00\"" + char.ConvertFromUtf32(0x1F600) + "\"");
		var room = new byte[64];
		var full = new ShownText(room);
		Assert.True(NumberText.TryWrite(code, 1234567.891, DisplayCulture.Invariant, ref full));
		Assert.True(full.Fits);
		Assert.Equal("é1,234,567.89" + char.ConvertFromUtf32(0x1F600), Encoding.UTF8.GetString(full.Written.ToArray()));
		Assert.Equal(18, full.Length);
		Assert.Equal(15, full.Chars);

		var small = new ShownText(new byte[4]);
		Assert.True(NumberText.TryWrite(code, 1234567.891, DisplayCulture.Invariant, ref small));
		Assert.False(small.Fits);
		Assert.Equal(full.Length, small.Length);
		Assert.Equal(full.Chars, small.Chars);

		var counting = new ShownText(Span<byte>.Empty);
		Assert.True(NumberText.TryWrite(code, 1234567.891, DisplayCulture.Invariant, ref counting));
		Assert.Equal(full.Length, counting.Length);

		// Room for exactly the text holds it all.
		var exact = new ShownText(new byte[18]);
		Assert.True(NumberText.TryWrite(code, 1234567.891, DisplayCulture.Invariant, ref exact));
		Assert.True(exact.Fits);
		Assert.Equal(full.Written.ToArray(), exact.Written.ToArray());
	}

	/// <summary>Random codes over random numbers: the renderer never throws, its text is bounded, and its length doesn't depend on the room.</summary>
	[Fact]
	public void ShowsAnyCodeWithoutThrowing()
	{
		var random = new Random(30716);
		var alphabet = "0#?.,%/:;@\"\\_*[]$-+()ESeEMmdhsAaPpGgBbn1234567890 <>=!€" + char.ConvertFromUtf32(0x1F600);
		double[] values = [0, -0.0, 1, -1, 0.5, 0.3, 1234.5678, -1234.5678, 1E-07, 1E+20, -1E+300, 5E-324, 2147483646.5, 0.999999999, 12345.678];
		var room = new byte[4096];
		var shown = 0;
		for (var i = 0; i < 20_000; i++)
		{
			var length = random.Next(0, 20);
			var code = new StringBuilder(length);
			for (var j = 0; j < length; j++)
			{
				code.Append(alphabet[random.Next(alphabet.Length)]);
			}

			var compiled = FormatCode.Compile(code.ToString());
			var value = random.Next(4) == 0 ? (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-20, 20)) : values[random.Next(values.Length)];
			var text = new ShownText(room);
			if (!NumberText.TryWrite(compiled, value, DisplayCulture.Invariant, ref text))
			{
				continue;
			}

			shown++;
			Assert.True(text.Fits, code.ToString());
			Assert.InRange(text.Chars, 0, NumberText.MaxChars);
			var counting = new ShownText(Span<byte>.Empty);
			Assert.True(NumberText.TryWrite(compiled, value, DisplayCulture.Invariant, ref counting));
			Assert.Equal(text.Length, counting.Length);
		}

		Assert.True(shown > 5_000, $"{shown} shown");
	}

#if NET10_0_OR_GREATER
	/// <summary>Rendering allocates nothing: numbers, fractions, exponents, conditions, General and text sections.</summary>
	[OptimizedFact]
	public void AllocatesNothing()
	{
		string[] codes = ["#,##0.00", "# ??/??", "##0.0E+0", "[>=100]0;[<=-100]0;0.00", "General", "0.0%", "?/?", "\"a\"@\"b\""];
		var compiled = codes.Select(FormatCode.Compile).ToArray();
		var text = Encoding.UTF8.GetBytes("abc");
		var room = new byte[256];
		double[] numbers = [1234.5678, -0.37, 0, 1E+20];
		Render(compiled, numbers, text, room);
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 100; i++)
		{
			Render(compiled, numbers, text, room);
		}

		Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

		static void Render(FormatCode[] compiled, double[] numbers, byte[] value, byte[] room)
		{
			foreach (var code in compiled)
			{
				foreach (var number in numbers)
				{
					var shown = new ShownText(room);
					NumberText.TryWrite(code, number, DisplayCulture.Invariant, ref shown);
				}

				var text = new ShownText(room);
				NumberText.TryWriteText(code, value, ref text);
			}
		}
	}
#endif

	/// <summary>A number's text in a code, in a culture.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(string code, double value, IFormatProvider? provider = null) => Render(FormatCode.Compile(code), value, provider);

	/// <summary>A number's text in a compiled code, in a culture.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(FormatCode code, double value, IFormatProvider? provider = null)
	{
		var room = new byte[1024];
		var text = new ShownText(room);
		if (!NumberText.TryWrite(code, value, DisplayCulture.For(provider), ref text))
		{
			return null;
		}

		Assert.True(text.Fits);

		return Encoding.UTF8.GetString(text.Written.ToArray());
	}

	/// <summary>A text's text in a code: through its text section, or as it is.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The text.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show it.</returns>
	private static string? RenderText(FormatCode code, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		var room = new byte[(bytes.Length * 4) + 1024];
		var text = new ShownText(room);
		if (NumberText.TryWriteText(code, bytes, ref text))
		{
			return Encoding.UTF8.GetString(text.Written.ToArray());
		}

		// No text section shows the text as it is; one that can't show it leaves the raw text, which a # answer stands for.
		return code.Text is null || !code.IsReadable ? value : null;
	}
}
