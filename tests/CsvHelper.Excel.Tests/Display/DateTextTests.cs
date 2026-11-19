using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// The text numbers show in date, time and elapsed-time sections and in system formats (<see cref="DateText"/>), against
/// what Excel 365 (16.0 build 20527, en-US) showed through COM on 2026-11-16 and 2026-11-17: every cell of Phase 8's probes
/// whose code shows a date (<see cref="ProbeAnswers"/>), in both date systems, and the rules one by one.
/// </summary>
public class DateTextTests
{
	/// <summary>
	/// The cells whose text differs from Excel's, each with its reason and how many cells it covers on this runtime: what
	/// Phase 8's decision 8 leaves out, the names the runtime's culture data gives otherwise (decision 9), the codes the
	/// renderer can't read though Excel shows them, whose cells show their raw text (decision 5), and a half no rule found.
	/// </summary>
	private static readonly (string Reason, Func<string, string, bool> Applies, int Cells)[] Differences =
	[
		("Japanese eras, g and e under [$-411] (令和6年), aren't rendered (decision 8): g shows nothing and e the year",
			(code, _) => code is "[$-411]e" or "[$-411]g" || code.StartsWith("[$-411]ggge", StringComparison.Ordinal), 13),
		("a calendar or numeral system, a locale code's (Hijri, Arabic-Indic and Thai digits) or B2's, isn't rendered (decision 8)",
			(code, _) => code.Contains("[$-2010401]") || code.Contains("[$-1060409]") || code.Contains("[$-D07041E]") || code.StartsWith("B2", StringComparison.Ordinal), 14),
#if NETFRAMEWORK
		("NLS's names are Excel's for every locale probed", (_, _) => false, 0),
#else
		("ICU's names differ from Excel's (decision 9): Russian in lower case and abbreviated with a point (январь, янв., пн), Japanese and Korean abbreviated months with 月 and 월",
			(code, _) => code.StartsWith("[$-419]", StringComparison.Ordinal) || code is "[$-411]mmm" or "[$-412]mmm", 30),
#endif
		("elapsed time after a date part, and General in a date, can't be read: Excel shows text no rule found (yyyy-mm-dd [h]:mm shows 12 hours for most values)",
			(code, _) => !FormatCode.Compile(code).IsReadable, 50),
		("Excel reads [s] m as s [] (its NumberFormat), and shows text no rule found", (code, _) => code == "[s] m", 3),
		("0.0005 and 0.0015 seconds past midnight of day 0 (5.787037037037037E-09 and 1.736111111111111E-08) show a thousandth more in Excel than its arithmetic, as step 7 fitted it to 2,196 answers, gives",
			(_, value) => value is "5.787037037037037E-09" or "1.736111111111111E-08", 6),
	];

	/// <summary>Every cell of the probes whose code shows a date shows Excel's text, but for the listed differences.</summary>
	[Fact]
	public void ShowsExcelsAnswers()
	{
		var failures = new List<string>();
		var checkedCells = 0;
		var listed = new int[Differences.Length];
		foreach (var (id, code, builtIn, kind, value, date1904, expected) in ProbeAnswers.Cells)
		{
			var compiled = FormatCode.Compile(code ?? DisplayCulture.Invariant.BuiltIn(builtIn));
			if (!compiled.HasDate)
			{
				continue;
			}

			var shown = kind == 'n' ? Render(compiled, double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture), date1904) :
				RenderText(compiled, kind == 'b' ? (value == "1" ? "TRUE" : "FALSE") : value);
			checkedCells++;
			if (shown == expected)
			{
				continue;
			}

			var difference = Array.FindIndex(Differences, d => d.Applies(compiled.Code, value));
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

		Assert.True(checkedCells > 3_500, $"{checkedCells} cells");
	}

	/// <summary>Each rule, as Excel showed it (the probes above).</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="date1904">Whether the 1904 date system counts it.</param>
	/// <param name="expected">Excel's text.</param>
	[Theory]
	// The 1900 date system: serial 0 is 1900-01-00, 60 is 1900-02-29, and the days before March fall a weekday early.
	[InlineData("yyyy-mm-dd", 0, false, "1900-01-00")]
	[InlineData("yyyy-mm-dd", 60, false, "1900-02-29")]
	[InlineData("yyyy-mm-dd", 61, false, "1900-03-01")]
	[InlineData("dddd", 1, false, "Sunday")]
	[InlineData("dddd", 61, false, "Thursday")]
	[InlineData("m/d/yyyy", 45351, false, "2/29/2024")]
	[InlineData("yyyy-mm-dd", 2958465.99999, false, "9999-12-31")]
	// Rounded to the precision shown before the parts are taken; minutes aren't rounded.
	[InlineData("yyyy-mm-dd", 45306.99999999, false, "2024-01-16")]
	[InlineData("h:mm:ss.000", 45306.99999999, false, "23:59:59.999")]
	[InlineData("hh:mm:ss.00", 45306.99999999, false, "00:00:00.00")]
	[InlineData("h:mm", 45306.999994, false, "23:59")]
	[InlineData("h:mm AM/PM", 0.999999, false, "12:00 AM")]
	[InlineData("h:mm:ss.0 AM/PM", 0.999999, false, "11:59:59.9 PM")]
	[InlineData("h:mm:ss", 0.041666665666666665, false, "1:00:00")]
	[InlineData("h:mm:ss", 5.787037037037037E-06, false, "0:00:01")]
	[InlineData("h:mm:ss", 1.736111111111111E-05, false, "0:00:02")]
	[InlineData("h:mm:ss", 45306.000005787035, false, "0:00:00")]
	[InlineData("h:mm:ss", 5.78125E-06, false, "0:00:00")]
	[InlineData("ss.00", 1.736111111111111E-07, false, "00.02")]
	[InlineData("ss.0", 2.8935185185185184E-06, false, "00.3")]
	// The double nearest a half goes either way, as Excel's arithmetic takes it: half the unit added to the serial, then
	// the parts truncated (step 7's probe of halves).
	[InlineData("h:mm:ss", 2.8935185185185183E-05, false, "0:00:02")]
	[InlineData("h:mm:ss", 61.000028935185185, false, "0:00:03")]
	[InlineData("h:mm:ss", 45306.37855902778, false, "9:05:07")]
	[InlineData("h:mm:ss", 45306.37855902779, false, "9:05:08")]
	[InlineData("mm:ss.0", 45319.37855613426, false, "05:07.2")]
	[InlineData("h:mm:ss", 2958465.3785590264, false, "9:05:07")]
	[InlineData("h:mm:ss", 2958465.378559028, false, "9:05:08")]
	// The hours, minutes and seconds truncated, and the fractions taken from the seconds' rest (step 7's probes of truncation):
	// 58.599999999999994 seconds show 58.5, where the seconds times ten, 586 in doubles, would show 58.6.
	[InlineData("h:mm:ss", 0.0004571759259259258, false, "0:00:40")]
	[InlineData("h:mm:ss.0", 0.19928877314814814, false, "4:46:58.5")]
	[InlineData("h:mm:ss.00", 0.1048529513888889, false, "2:30:59.29")]
	[InlineData("h:mm:ss.000", 0.025443385416666665, false, "0:36:38.308")]
	[InlineData("h:mm:ss.00", 2.2858796296296297E-05, false, "0:00:01.98")]
	[InlineData("h:mm:ss.000", 0.0001135474537037037, false, "0:00:09.811")]
	[InlineData("h:mm:ss.000", 45306.123456789, false, "2:57:46.667")]
	[InlineData("h:mm:ss.000.0", 45306.5208333, false, "12:29:59.997.9")]
	[InlineData("ss.0.0", 45306.999994, false, "59.5.5")]
	[InlineData("h:mm.00", 45306.999994, false, "23:59.48")]
	[InlineData("yyyy.00", 1234.5678, false, "1903.92")]
	// Elapsed time: the whole hours, minutes or seconds, padded to its letters; alone, any value.
	[InlineData("[h]:mm:ss", 1000.5, false, "24012:00:00")]
	[InlineData("[h]:mm:ss", 0.999999999, false, "24:00:00")]
	[InlineData("[hhh]:mm", 0.25, false, "006:00")]
	[InlineData("[mm]:ss.0", 0.99999, false, "1439:59.1")]
	[InlineData("[s].000", 45306.000000005784, false, "3914438400.000")]
	[InlineData("[s].000", 2.8935185185185185E-08, false, "0.003")]
	[InlineData("[s]", 45306.000005787035, false, "3914438401")]
	[InlineData("[s]", 2958465.000005787, false, "255611376001")]
	[InlineData("[s]", 1234.000005787, false, "106617600")]
	[InlineData("[s] yyyy", 45306.000005787035, false, "3914438401 2024")]
	[InlineData("[h]:mm:ss", 45306.000005787035, false, "1087344:00:00")]
	[InlineData("[h]:mm:ss", 1.0000057870370371, false, "24:00:01")]
	[InlineData("[h]", -0.5, false, "-12")]
	[InlineData("[h]", -1E-09, false, "-0")]
	[InlineData("[s]", -5.787037037037037E-06, false, "-1")]
	[InlineData("[h]\" h\"", -1.5, false, "-36 h")]
	[InlineData("\"x\"[h]", -1, false, "-x24")]
	[InlineData("[h];[h]", -0.5, false, "12")]
	[InlineData("[h];-[h]", -0.5, false, "-12")]
	[InlineData("[h];0", -0.5, false, "1")]
	[InlineData("[h]", 2958466, false, "71003184")]
	[InlineData("[h]", 1E+10, false, "240000000000")]
	[InlineData("[h]", 1.23456789012345E+15, false, "29629629362962800")]
	[InlineData("[m]", 1.23456789012345E+20, false, "177777776177777000000000")]
	[InlineData("[hh]", double.MaxValue, false, "00")]
	[InlineData("[h] yyyy", 45306.75, false, "1087362 2024")]
	[InlineData("[h]:mm d", 1.5, false, "36:00 1")]
	[InlineData("d \"days\" h:mm", 32.5, false, "1 days 12:00")]
	// The 1904 date system: a minus sign and the absolute value's, but in a section that shows none.
	[InlineData("yyyy-mm-dd", 0, true, "1904-01-01")]
	[InlineData("yyyy-mm-dd", -1, true, "-1904-01-02")]
	[InlineData("yyyy-mm-dd", -1E-09, true, "-1904-01-01")]
	[InlineData("dddd", -0.5, true, "-Friday")]
	[InlineData("h:mm:ss", -1462.5, true, "-12:00:00")]
	[InlineData("[h]:mm:ss", -1, true, "-24:00:00")]
	[InlineData("[h]:mm;-[h]:mm", -0.5, true, "-12:00")]
	[InlineData("\"Date: \"yyyy-mm-dd", -1, true, "-Date: 1904-01-02")]
	[InlineData("yyyy-mm-dd;yyyy-mm-dd", -1, true, "1904-01-02")]
	[InlineData("0;[h]", -1, true, "24")]
	[InlineData("[<0]yyyy-mm-dd;0", -1, true, "1904-01-02")]
	[InlineData("yyyy-mm-dd", 2957003.99999, true, "9999-12-31")]
	[InlineData("yyyy-mm-dd", -2957003.99999, true, "-9999-12-31")]
	[InlineData("[s].00", -1, true, "-86400.00")]
	// Sections, conditions and colors as numbers have them.
	[InlineData("yyyy-mm-dd;\"neg\"", -1, false, "neg")]
	[InlineData("yyyy-mm-dd;;\"zero\"", -1, false, "")]
	[InlineData("yyyy-mm-dd;;\"zero\"", -5E-324, false, "zero")]
	[InlineData("[>=0]yyyy-mm-dd;0", -0.5, false, "1")]
	[InlineData("[Red]yyyy-mm-dd", 45306.5, false, "2024-01-15")]
	[InlineData("yyyy-mm-dd;@", 45306.5, false, "2024-01-15")]
	// Years, months, days and their names.
	[InlineData("yy y yyy yyyyy", 367, false, "01 01 1901 1901")]
	[InlineData("e ee", 45306.5, false, "2024 2024")]
	[InlineData("b bb bbb bbbb", 45306.5, false, "67 67 2567 2567")]
	[InlineData("b bbbb", 2958465, false, "54 10542")]
	[InlineData("g ggg", 45306.5, false, " ")]
	[InlineData("m mm mmm mmmm mmmmm mmmmmm", 59, false, "2 02 Feb February F February")]
	[InlineData("d dd ddd dddd ddddd", 1, false, "1 01 Sun Sunday Sunday")]
	[InlineData("aaa aaaa", 45306.5, false, "Mon Monday")]
	[InlineData("aa", 45306.5, false, "aa")]
	[InlineData("hhh sss", 0.25, false, "06 00")]
	[InlineData("h:m:s", 45306.5208333, false, "12:30:0")]
	// m as a month or a minute.
	[InlineData("h d m", 45306.5208333, false, "12 15 30")]
	[InlineData("h mmm m", 45306.5208333, false, "12 Jan 30")]
	[InlineData("h AM/PM m", 45306.5208333, false, "12 PM 30")]
	[InlineData("ss m", 45306.5208333, false, "00 30")]
	[InlineData("s d m", 0.0423, false, "55 0 0")]
	[InlineData("hh:mm:mm", 45306.5208333, false, "12:30:01")]
	[InlineData("h:mm d m", 45306.5208333, false, "12:30 15 1")]
	[InlineData("mm ss mm", 0.0423, false, "00 55 01")]
	[InlineData("m yyyy s", 0.0423, false, "1 1900 55")]
	[InlineData("mm:ss AM/PM", 0.5208333333333334, false, "30:00 PM")]
	// AM/PM and A/P, and letters that aren't tokens.
	[InlineData("h:mm AM/PM", 0, false, "12:00 AM")]
	[InlineData("hh:mm am/pm", 0.5416666666666666, false, "01:00 PM")]
	[InlineData("h AM/PM", 0.5, false, "12 PM")]
	[InlineData("h:mm a/p", 0.5, false, "12:00 p")]
	[InlineData("a/P", 0.5, false, "P")]
	[InlineData("a/P", 0.25, false, "a")]
	[InlineData("h:mm A/P/X", 0.25, false, "6:00 A/X")]
	[InlineData("h:mm a", 0.5416666666666666, false, "13:00 a")]
	[InlineData("h:mm AM", 0.5, false, "12:00 A1")]
	// Locale codes' names and designators; the language changes no separator.
	[InlineData("[$-407]mmmm dddd", 45306.5, false, "Januar Montag")]
	[InlineData("[$-40C]mmmm dddd", 45306.5, false, "janvier lundi")]
	[InlineData("[$-411]aaaa", 45306.5, false, "月曜日")]
	[InlineData("[$-411]AM/PM h:mm", 0.5, false, "午後 12:00")]
	[InlineData("[$-412]AM/PM h:mm", 0.25, false, "오전 6:00")]
	[InlineData("[$-407]h:mm AM/PM", 0.75, false, "6:00 PM")]
	[InlineData("[$-407]dd/mm/yyyy", 45306.5, false, "15/01/2024")]
	[InlineData("[$-de-DE]mmmm", 45306.5, false, "Januar")]
	[InlineData("[$-1010409]d/m/yyyy", 45306.5, false, "15/1/2024")]
	// The system formats: the long date or long time whatever else the section holds, in the first section alone.
	[InlineData("[$-F800]dddd, mmmm dd, yyyy", 45297.04237, false, "Saturday, January 6, 2024")]
	[InlineData("[$-F800]0.00", 45306.5, false, "Monday, January 15, 2024")]
	[InlineData("[$-x-sysdate]yyyy", 0.5, false, "Saturday, January 0, 1900")]
	[InlineData("[$-F400]yyyy-mm-dd", 0.0423, false, "1:00:55 AM")]
	[InlineData("[$-F800]", 1, false, "Sunday, January 1, 1900")]
	[InlineData("[$-F800];0", -1, false, "1")]
	[InlineData("[$-F800]dddd;[$-F400]h:mm;[$-F800]yyyy", -1, true, "-Saturday, January 2, 1904")]
	[InlineData("[$-F400]h:mm;[$-F800]dddd", -0.5, true, "-12:00:00 PM")]
	[InlineData("0;[$-F800]dddd", -1, true, "Saturday")]
	[InlineData("0;0;[$-F800]dddd", 0, false, "Saturday")]
	public void ShowsEachRule(string code, double value, bool date1904, string expected)
	{
		Assert.Equal(expected, Render(code, value, date1904));
	}

	/// <summary>Values Excel can't show in a date, and date codes the renderer can't read: the cell shows its raw text.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="date1904">Whether the 1904 date system counts it.</param>
	[Theory]
	[InlineData("yyyy-mm-dd", -1, false)]
	[InlineData("yyyy-mm-dd", -1E-09, false)]
	[InlineData("h:mm;h:mm", -0.5, false)]
	[InlineData("0;yyyy-mm-dd", -1, false)]
	[InlineData("[<0]h:mm;0", -0.5, false)]
	[InlineData("[h]:mm", -0.5, false)]
	[InlineData("[h]:mm;-[h]:mm", -0.5, false)]
	[InlineData("[s].00", -1, false)]
	[InlineData("[$-F800]dddd", -1, false)]
	[InlineData("yyyy-mm-dd", 2958466, false)]
	[InlineData("yyyy-mm-dd", 2958465.999999999, false)]
	[InlineData("yyyy-mm-dd", 1E+10, false)]
	[InlineData("[h]:mm", 2958466, false)]
	[InlineData("[s].00", 2958466, false)]
	[InlineData("yyyy-mm-dd", 2957004, true)]
	[InlineData("yyyy-mm-dd", 2957003.999999999, true)]
	[InlineData("yyyy-mm-dd", -2957004, true)]
	[InlineData("[h]:mm", 2957004, true)]
	[InlineData("[h]", 1E+254, false)]
	[InlineData("[m]", -1E+252, false)]
	[InlineData("yyyy-mm-dd [h]:mm", 45306.5, false)]
	[InlineData("yyyy General", 0.5, false)]
	[InlineData("m [s]", 45306.5, false)]
	[InlineData("[h]:mm AM/PM", 0.5, false)]
	[InlineData("ss.0000", 0.5, false)]
	[InlineData("yyyy 0", 45306.5, false)]
	public void CantShow(string code, double value, bool date1904)
	{
		Assert.Null(Render(code, value, date1904));
	}

	/// <summary>Elapsed time and dates can't pass 255 characters, as numbers can't.</summary>
	[Fact]
	public void ShowsUpTo255Characters()
	{
		var literal = "\"" + new string('x', 99) + "\"";
		Assert.Equal(255, Render(literal + "[h]", 1E+154)!.Length);
		Assert.Null(Render(literal + "[h]", 1E+155));
		Assert.Equal(255, Render(literal + "[h]", -1E+153)!.Length);
		Assert.Null(Render(literal + "[h]", -1E+154));
		Assert.Equal(255, Render("[h]", 1E+253)!.Length);
		Assert.Equal(254, Render("[m]", 1E+250)!.Length);

		// Wednesday is 9 letters: 14 of them and their spaces, and a literal of 115, are 255 characters.
		var days = "\"" + new string('y', 99) + "\"\"" + new string('z', 16) + "\"" + string.Concat(Enumerable.Repeat("dddd ", 14));
		Assert.Equal(255, Render(days + "\"\"", 45308.5)!.Length);
		Assert.Null(Render(days + "\"!\"", 45308.5));
	}

	/// <summary>
	/// Another culture's names and designators where no locale code names a language, and its long date, long time and
	/// short date for the system formats and the built-in ids 14 and 22; the separators a code writes stay as written.
	/// </summary>
	[Fact]
	public void ShowsTheCulturesNames()
	{
		var german = CultureInfo.GetCultureInfo("de-DE");
		Assert.Equal("Januar Montag", Render("mmmm dddd", 45306.5, provider: german));
		Assert.Equal("6:00 PM", Render("h:mm AM/PM", 0.75, provider: german));
		Assert.Equal("15/01/2024", Render("dd/mm/yyyy", 45306.5, provider: german));
		Assert.Equal("Montag, 15. Januar 2024", Render("[$-F800]", 45306.5, provider: german));
		Assert.Equal("18:00:00", Render("[$-F400]", 45306.75, provider: german));
		Assert.Equal("15.01.2024", Render(FormatCode.Compile(DisplayCulture.For(german).BuiltIn(14)), 45306.5, provider: german));

		// A locale code names its language's whatever the culture.
		Assert.Equal("January", Render("[$-409]mmmm", 45306.5, provider: german));
		Assert.Equal("月曜日", Render("dddd", 45306.5, provider: CultureInfo.GetCultureInfo("ja-JP")));

		// The long date, long time and short date are .NET's own text of them, in cultures whose patterns have no genitive months.
		var date = new DateTime(2024, 1, 15, 18, 5, 9);
		foreach (var name in new[] { "de-DE", "fr-FR", "ja-JP", "en-GB", "es-ES", "it-IT", "zh-CN" })
		{
			var culture = CultureInfo.GetCultureInfo(name);
			var display = DisplayCulture.For(culture);
			Assert.Equal(date.ToString("D", culture), Render("[$-F800]", 45306.75357638889, provider: culture));
			Assert.Equal(date.ToString("T", culture), Render("[$-F400]", 45306.75357638889, provider: culture));
			Assert.Equal(date.ToString("d", culture), Render(FormatCode.Compile(display.BuiltIn(14)), 45306.75357638889, provider: culture));
		}
	}

	/// <summary>A date written into too little room counts the bytes it needs, and its characters as UTF-16 units.</summary>
	[Fact]
	public void CountsWhatDoesntFit()
	{
		var code = FormatCode.Compile("[$-411]aaaa\"" + char.ConvertFromUtf32(0x1F600) + "\"yyyy-mm-dd hh:mm:ss.000 AM/PM");
		var room = new byte[128];
		var full = new ShownText(room);
		Assert.True(NumberText.TryWrite(code, 45306.5208333, DisplayCulture.Invariant, false, ref full));
		Assert.True(full.Fits);
		Assert.Equal("月曜日" + char.ConvertFromUtf32(0x1F600) + "2024-01-15 12:29:59.997 午後", Encoding.UTF8.GetString(full.Written.ToArray()));
		Assert.Equal(43, full.Length);
		Assert.Equal(31, full.Chars);

		var small = new ShownText(new byte[5]);
		Assert.True(NumberText.TryWrite(code, 45306.5208333, DisplayCulture.Invariant, false, ref small));
		Assert.False(small.Fits);
		Assert.Equal(full.Length, small.Length);
		Assert.Equal(full.Chars, small.Chars);

		var exact = new ShownText(new byte[43]);
		Assert.True(NumberText.TryWrite(code, 45306.5208333, DisplayCulture.Invariant, false, ref exact));
		Assert.True(exact.Fits);
		Assert.Equal(full.Written.ToArray(), exact.Written.ToArray());
	}

	/// <summary>Random date codes over random numbers in both date systems: the renderer never throws, its text is bounded, and its length doesn't depend on the room.</summary>
	[Fact]
	public void ShowsAnyDateCodeWithoutThrowing()
	{
		var random = new Random(40717);
		string[] pieces =
		[
			"y", "yy", "yyyy", "m", "mm", "mmm", "mmmm", "mmmmm", "d", "dd", "ddd", "dddd", "aaa", "h", "hh", "s", "ss", ".0", ".00",
			".000", "[h]", "[mm]", "[s]", "AM/PM", "A/P", "e", "b", "bb", "g", ":", "/", "-", " ", "\"x\"", "\\T", "[$-407]",
			"[$-411]", "[$-F800]", "[$-F400]", "[Red]", "[>0.5]", ";", "@", "0", "General", char.ConvertFromUtf32(0x1F600),
		];
		double[] values = [0, -0.0, 1, -1, 0.5, -0.5, 59.5, 60, 61, 45306.99999999, 2957003.99999, 2958465.99999, 2958466, 1E+10, -1E+10, 1E+300, 5E-324, 0.999999];
		var room = new byte[4096];
		var shown = 0;
		for (var i = 0; i < 20_000; i++)
		{
			var length = random.Next(1, 8);
			var code = new StringBuilder();
			for (var j = 0; j < length; j++)
			{
				code.Append(pieces[random.Next(pieces.Length)]);
			}

			var compiled = FormatCode.Compile(code.ToString());
			var value = random.Next(4) == 0 ? (random.NextDouble() - 0.3) * Math.Pow(10, random.Next(-8, 12)) : values[random.Next(values.Length)];
			var date1904 = random.Next(2) == 0;
			var text = new ShownText(room);
			if (!NumberText.TryWrite(compiled, value, DisplayCulture.Invariant, date1904, ref text))
			{
				continue;
			}

			shown++;
			Assert.True(text.Fits, code.ToString());
			Assert.InRange(text.Chars, 0, NumberText.MaxChars);
			var counting = new ShownText(Span<byte>.Empty);
			Assert.True(NumberText.TryWrite(compiled, value, DisplayCulture.Invariant, date1904, ref counting));
			Assert.Equal(text.Length, counting.Length);
		}

		Assert.True(shown > 5_000, $"{shown} shown");
	}

#if NET10_0_OR_GREATER
	/// <summary>Rendering allocates nothing: dates, times, elapsed time, names, designators, the 1904 system and the system formats.</summary>
	[OptimizedFact]
	public void AllocatesNothing()
	{
		string[] codes = ["yyyy-mm-dd hh:mm:ss.000", "dddd, mmmm d, yyyy h:mm AM/PM", "[h]:mm:ss", "[s]", "[$-407]mmmm", "[$-F800]", "b A/P", "h d m"];
		var compiled = codes.Select(FormatCode.Compile).ToArray();
		var culture = DisplayCulture.Invariant;
		_ = culture.LongDateFormat;
		var room = new byte[256];
		double[] numbers = [45306.5208333, -1, 0, 1E+20, 2958465.99999];
		Render(compiled, numbers, room);

		// Two passes are measured, and the smaller kept: the runtime allocates once on the thread at a moment the JIT's
		// tiering sets (see ExcelToCsvTests.AllocatesTheSameForAnyNumberOfRows), which can fall in one pass but not in both.
		Assert.Equal(0, Math.Min(Allocated(), Allocated()));

		long Allocated()
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 100; i++)
			{
				Render(compiled, numbers, room);
			}

			return GC.GetAllocatedBytesForCurrentThread() - before;
		}

		static void Render(FormatCode[] compiled, double[] numbers, byte[] room)
		{
			foreach (var code in compiled)
			{
				foreach (var number in numbers)
				{
					var shown = new ShownText(room);
					NumberText.TryWrite(code, number, DisplayCulture.Invariant, number < 0, ref shown);
				}
			}
		}
	}
#endif

	/// <summary>A number's text in a code, in a date system and a culture.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="date1904">Whether the 1904 date system counts it.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(string code, double value, bool date1904 = false, IFormatProvider? provider = null) => Render(FormatCode.Compile(code), value, date1904, provider);

	/// <summary>A number's text in a compiled code, in a date system and a culture.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="date1904">Whether the 1904 date system counts it.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(FormatCode code, double value, bool date1904 = false, IFormatProvider? provider = null)
	{
		var room = new byte[1024];
		var text = new ShownText(room);
		if (!NumberText.TryWrite(code, value, DisplayCulture.For(provider), date1904, ref text))
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
