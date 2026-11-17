using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Sheet;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Phase 8's security cases of displayed text (its step 8), with Excel's answers where it opens the file
/// (<c>Csv\_bench\results\phase8\step8</c>, <c>excel12.jsonl</c> and <c>excel13.jsonl</c>): fills and <c>_</c> before a
/// surrogate pair, locale codes of ids and tags the runtime doesn't know, codes of 255 UTF-16 units and longer ones, text
/// sections over the longest text a cell holds, and codes Excel refuses that a file can still hold.
/// </summary>
public class DisplaySecurityTests
{
	/// <summary>A surrogate pair: U+1F600.</summary>
	private static readonly string Pair = Units(0xD83D, 0xDE00);

	/// <summary>U+FFFD, which UTF-8 holds a lone surrogate as.</summary>
	private static readonly string Replacement = Units(0xFFFD);

	/// <summary>
	/// <c>_</c> and <c>*</c> take one UTF-16 unit, as Excel does: before a surrogate pair, its high surrogate, so that its low
	/// one shows as itself, U+FFFD in UTF-8. <c>\</c> takes the pair, which shows the same either way. Excel's answers: its
	/// CSV export where a fill repeats, else <c>Range.Text</c>.
	/// </summary>
	[Fact]
	public void TakesOneUnitAfterAFillOrASpace()
	{
		(string Code, double Value, string Shown)[] cases =
		[
			("*" + Pair + "0", 1234.5, Replacement + "1235"),
			("*" + Pair + "0", 0, Replacement + "0"),
			("0*" + Pair, 1234.5, "1235" + Replacement),
			("_" + Pair + "0", 1234.5, " " + Replacement + "1235"),
			("_" + Pair + "0", -1, "- " + Replacement + "1"),
			("0_" + Pair, 1234.5, "1235 " + Replacement),
			("0.0_" + Pair + ";-0.0_" + Pair, -1, "-1.0 " + Replacement),
			("0.0_" + Pair + ";-0.0_" + Pair, 0, "0.0 " + Replacement),
			("\\" + Pair + "0", 1234.5, Pair + "1235"),
			("\\" + Pair + "0", -1, "-" + Pair + "1"),
			("\"" + Pair + "\"0", 1234.5, Pair + "1235"),
			(Pair + "0", 1234.5, Pair + "1235"),
			("[$" + Pair + "-409]0", 1234.5, Pair + "1235"),
			("*" + Units(0x5E74) + "0", 1234.5, "1235"),
			("0_" + Units(0x5E74) + "x", 1234.5, "1235 x"),
		];
		foreach (var (code, value, shown) in cases)
		{
			Assert.Equal(shown, Render(code, value));
		}

		Assert.Equal("abc " + Replacement + "x", RenderText("0;0;0;@_" + Pair + "x", "abc"));

		// A lone surrogate at a code's end, after _ or *, is the unit they take; a lone one elsewhere shows as U+FFFD.
		Assert.Equal("1 ", Render("0_" + Units(0xD83D), 1));
		Assert.Equal("1", Render("0*" + Units(0xDE00), 1));
		Assert.Equal("1" + Replacement, Render("0" + Units(0xDE00), 1));

		// Measured, as auto-fit compiles codes, _ is the unit it takes, which with the pair's low surrogate after it is the pair.
		var measured = new ShownText(new byte[64], true);
		Assert.True(NumberText.TryWrite(FormatCode.Compile("0_" + Pair, true), 1, DisplayCulture.Invariant, false, ref measured));
		Assert.Equal("1" + Pair, Encoding.UTF8.GetString(measured.Written.ToArray()));
	}

	/// <summary>
	/// A locale code's language as Excel reads it: an id of any number of hex digits, its low 32 bits; an id the runtime
	/// doesn't know is no language, so that the reader's culture names (Excel drops it, <c>[$-1234]</c> reading as
	/// <c>[$]</c>, and reads <c>[$-400]</c>, <c>[$-800]</c> and <c>[$-C00]</c> as the user's language); a tag by its longest
	/// prefix that is a culture the runtime has; hex letters that name a culture, a tag. Excel's answers in en-US, which
	/// the invariant culture is.
	/// </summary>
	[Fact]
	public void NamesLanguagesAsExcelReadsLocaleCodes()
	{
		const string English = "January Monday PM";
		const string German = "Januar Montag PM";
		string[] english =
		[
			"0", "7F", "400", "800", "C00", "1000", "1234", "ABCD", "12345678", "FFFF", "FFFFFFFF", "123456789", "4407", "7C07", "F809", "en-US-x-foo",
			"x-foo", "zz-ZZ", "en-ZZ",
		];
		foreach (var id in english)
		{
			Assert.Equal(English, Render($"[$-{id}]mmmm dddd AM/PM", 45306.5));
		}

		string[] german = ["de", "de-DE", "DE-de", "de-CH-x-foo", "de-DE-1996", "de-ZZ", "807"];
		foreach (var id in german)
		{
			Assert.Equal(German, Render($"[$-{id}]mmmm dddd AM/PM", 45306.5));
		}

		Assert.Equal("Jänner Montag PM", Render("[$-C07]mmmm dddd AM/PM", 45306.5));
		Assert.Equal("janvier lundi PM", Render("[$-fr-FR-u-nu-arab]mmmm dddd AM/PM", 45306.5));
		Assert.Equal("janvier lundi AM", Render("[$-fr-x]mmmm dddd AM/PM", 45306.25));
		Assert.Equal("1月 月曜日 午後", Render("[$-ja-JP-u-ca-japanese]mmmm dddd AM/PM", 45306.5));
		Assert.Equal("1月 月曜日 午前", Render("[$-ja-u-ca-japanese]mmmm dddd AM/PM", 45306.25));
		Assert.Equal("Monday, January 15, 2024", Render("[$-1F800]mmmm dddd AM/PM", 45306.5));

		// Excel names the language of an id whose sublanguage is 0x3E or 0x3F (F801 Arabic, F807 German, F80C French, FC11
		// Japanese), which the runtime doesn't know; the renderer, as for any id the runtime doesn't know, the culture's.
		// And Excel names qps-ploc, which neither runtime has, in its pseudo-locale.
		foreach (var id in new[] { "F801", "F807", "F80C", "FC11", "qps-ploc" })
		{
			Assert.Equal(English, Render($"[$-{id}]mmmm dddd AM/PM", 45306.5));
		}

		// In German, an id the runtime doesn't know names in German, as no locale code does.
		var de = CultureInfo.GetCultureInfo("de-DE");
		foreach (var id in new[] { "0", "400", "1234", "FFFF", "123456789", "zz-ZZ", "x-foo" })
		{
			Assert.Equal(German, Render($"[$-{id}]mmmm dddd AM/PM", 45306.5, de));
		}

		Assert.Equal(English, Render("[$-409]mmmm dddd AM/PM", 45306.5, de));
		Assert.Equal(English, Render("[$-en-ZZ]mmmm dddd AM/PM", 45306.5, de));
	}

	/// <summary>
	/// Codes of tags the runtime doesn't have keep nothing, whatever their number: neither the names nor the runtime's own
	/// cultures, which it makes up for any tag of a language's shape and keeps for the process's life. Ids keep at most a
	/// language each, whatever their upper bits.
	/// </summary>
	[Fact]
	public void KeepsNothingForLocaleCodesTheRuntimeLacks()
	{
		_ = FormatCode.Compile("[$-de-DE]mmmm");
		var kept = DateNames.Kept;
		for (var i = 0; i < 5000; i++)
		{
			var tag = "qq-" + (char)('A' + (i / 26 % 26)) + (char)('A' + (i % 26)) + "-v" + i.ToString("D4", CultureInfo.InvariantCulture);
			Assert.True(Render($"[$-{tag}]mmmm", 45306.5) == "January", tag);
			_ = FormatCode.Compile($"[$-{(i << 16) | 0x407:X}]mmmm");
		}

		// Other tests compile codes at the same time, which can keep a few known languages.
		Assert.InRange(DateNames.Kept - kept, 0, 64);
	}

	/// <summary>
	/// A code of 255 UTF-16 units reads whole; a longer one, its first 255, as Excel cuts the codes it opens: 300 zeros show
	/// 255, the second section of <c>0…0;"neg"0</c> is gone, <c>#…#0</c> of 256 shows 0 as nothing, and <c>[Red]</c> before
	/// 251 zeros shows 250.
	/// </summary>
	[Fact]
	public void ReadsCodesOf255UnitsAndCutsLongerOnes()
	{
		var digits = new string('0', 251) + "1235";
		Assert.Equal(digits, Render(new string('0', 255), 1234.5678));
		foreach (var length in new[] { 256, 300, 10_000, 1_000_000 })
		{
			var code = FormatCode.Compile(new string('0', length));
			Assert.Equal(255, code.Code.Length);
			Assert.Equal(digits, Render(code, 1234.5678));
		}

		Assert.Equal(new string('0', 255), Render(new string('0', 300) + ";\"neg\"0", 0));
		Assert.Null(Render(new string('0', 300) + ";\"neg\"0", -1));
		Assert.Equal("", Render(new string('#', 255) + "0", 0));
		Assert.Equal("1235", Render(new string('#', 256), 1234.5678));
		Assert.Equal(new string('0', 246) + "1235", Render("[Red]" + new string('0', 251), 1234.5678));
		Assert.Equal("-" + new string('0', 249) + "1", Render("[Red]" + new string('0', 251), -1));

		// A cut through a surrogate pair leaves its high surrogate, which shows as U+FFFD.
		var split = Render("00" + string.Concat(Enumerable.Repeat(Pair, 127)), 5);
		Assert.Equal("05" + string.Concat(Enumerable.Repeat(Pair, 126)) + Replacement, split);

		// A code of a million units of empty quotes is cut inside a quote, and can't be read.
		Assert.False(FormatCode.Compile(string.Concat(Enumerable.Repeat("\"\"", 500_000)) + "0").IsReadable);
	}

	/// <summary>A reader under <see cref="ExcelText.Formatted"/> of a code of a million units shows each cell in its first 255.</summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>A task.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsALongCodeAsItsFirst255Units(bool async)
	{
		// A styles part of a million-unit code, read under Formatted over a thousand cells.
		var code = new string('0', 1_000_000);
		var rows = string.Concat(Enumerable.Range(1, 1000).Select(r => $"<row r=\"{r}\"><c r=\"A{r}\" s=\"0\"><v>{r}</v></c></row>"));
		var fields = await ReadAsync(Book([code], rows), async, ExcelText.Formatted);
		Assert.Equal(1000, fields.Count);
		Assert.Equal(new string('0', 254) + "1", fields[0]);
		Assert.Equal(new string('0', 251) + "1000", fields[999]);
	}

	/// <summary>
	/// Text sections over the longest text a cell holds, 32,767 characters: Excel can't show more than 255 through one, so
	/// the text reads as it is (<see cref="ExcelText.Raw"/>'s), however many <c>@</c>s the section has; <c>@</c> alone shows
	/// any text. The renderer stops at the 256th character, and doesn't count a text too long to fit.
	/// </summary>
	[Fact]
	public void ShowsNoTextSectionOverTextPast255Characters()
	{
		var longest = new string('z', 32767);
		foreach (var code in new[] { "\"x\"@", "@@", "@\"x\"", new string('@', 255), "0;0;0;\"x\"@" })
		{
			Assert.Null(RenderText(code, longest));
		}

		Assert.Equal(longest, RenderText("@", longest));
		Assert.Equal("x" + new string('z', 254), RenderText("\"x\"@", new string('z', 254)));
		Assert.Null(RenderText("\"x\"@", new string('z', 255)));
		Assert.Equal(new string('z', 255), RenderText("@@@", new string('z', 85)));
		Assert.Null(RenderText("@@@", new string('z', 86)));

		// Of 800 bytes that are no characters, which the reader never gives, a section with @ shows nothing: too many bytes.
		var bytes = Enumerable.Repeat((byte)0x80, 800).ToArray();
		var text = new ShownText(new byte[2048]);
		Assert.False(NumberText.TryWriteText(FormatCode.Compile("\"x\"@"), bytes, ref text));
		text = new ShownText(new byte[2048]);
		Assert.True(NumberText.TryWriteText(FormatCode.Compile("@"), bytes, ref text));
	}

	/// <summary>
	/// A reader under <see cref="ExcelText.Formatted"/> reads text of 32,767 characters through text sections as it is, and
	/// converts it so.
	/// </summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>A task.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheLongestTextThroughTextSections(bool async)
	{
		// Shared and inline strings of 32,767 characters through four text sections, and one of 254 that "x"@ shows.
		var longest = new string('z', 32767);
		var rows =
			$"<row r=\"1\"><c r=\"A1\" s=\"0\" t=\"s\"><v>0</v></c><c r=\"B1\" s=\"1\" t=\"s\"><v>0</v></c><c r=\"C1\" s=\"2\" t=\"inlineStr\"><is><t>{longest}</t></is></c>" +
			$"<c r=\"D1\" s=\"3\" t=\"s\"><v>0</v></c><c r=\"E1\" s=\"0\" t=\"s\"><v>1</v></c></row>";
		var package = Book(["\"x\"@", "@@", new string('@', 255), "@"], rows).SharedStrings($"<t>{longest}</t>", $"<t>{new string('z', 254)}</t>");
		var fields = await ReadAsync(package, async, ExcelText.Formatted);
		Assert.Equal([longest, longest, longest, longest, "x" + new string('z', 254)], fields);

		using var csv = new MemoryStream();
		var options = new ExcelToCsvOptions { Excel = new ExcelReaderOptions { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } } };
		if (async)
		{
			await ExcelConvert.ToCsvAsync(package.ToStream(), csv, options);
		}
		else
		{
			ExcelConvert.ToCsv(package.ToStream(), csv, options);
		}

		Assert.Equal(string.Join(",", longest, longest, longest, longest, "x" + new string('z', 254)) + "\r\n", Encoding.UTF8.GetString(csv.ToArray()));
	}

	/// <summary>
	/// Codes Excel refuses to open, which it repairs to General, and one it opens but the renderer can't read
	/// (<c>?/50000</c>, which shows fractions that aren't the value): a reader under <see cref="ExcelText.Formatted"/> reads
	/// each without throwing, its cells as <see cref="ExcelText.Raw"/> reads them, since the renderer can't read the code.
	/// </summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>A task.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsCodesExcelRefuses(bool async)
	{
		string[] refused =
		[
			"0.00e+00", "0:00", "0.00 General", "[Color0]0", "[Color57]0", "0E+", "0;0;[=0]0", "0;0;0;@;0", "0\"abc", "[Red0", "hh:mm:ss.0000", "[Purple]0",
			"?/50000", "0\\", "0_", "0*", "0.00 kg",
		];
		var cells = new StringBuilder();
		foreach (var (row, value) in new[] { (1, "<v>1234.5678</v>"), (2, "<v>-1</v>"), (3, "<v>45306.5</v>") })
		{
			cells.Append($"<row r=\"{row}\">");
			for (var c = 0; c < refused.Length; c++)
			{
				cells.Append($"<c r=\"{CellReference.ColumnName(c)}{row}\" s=\"{c}\">{value}</c>");
			}

			cells.Append("</row>");
		}

		cells.Append("<row r=\"4\">");
		for (var c = 0; c < 2 * refused.Length; c++)
		{
			var cell = c < refused.Length ? "t=\"inlineStr\"><is><t>abc</t></is>" : "t=\"b\"><v>1</v>";
			cells.Append($"<c r=\"{CellReference.ColumnName(c)}4\" s=\"{c % refused.Length}\" {cell}</c>");
		}

		cells.Append("</row>");
		var package = Book(refused, cells.ToString());
		foreach (var code in refused)
		{
			Assert.False(FormatCode.Compile(code).IsReadable, code);
		}

		var raw = await ReadAsync(package, async, ExcelText.Raw);
		Assert.Equal(raw, await ReadAsync(package, async, ExcelText.Formatted));
		Assert.Equal(5 * refused.Length, raw.Count);
	}

	/// <summary>A number's text in a code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(string code, double value, IFormatProvider? provider = null) => Render(FormatCode.Compile(code), value, provider);

	/// <summary>A number's text in a compiled code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The number.</param>
	/// <param name="provider">The culture: the invariant culture, Excel en-US, by default.</param>
	/// <returns>The text, or <see langword="null"/> where Excel can't show the number.</returns>
	private static string? Render(FormatCode code, double value, IFormatProvider? provider = null)
	{
		var text = new ShownText(new byte[2048]);
		if (!NumberText.TryWrite(code, value, DisplayCulture.For(provider), false, ref text))
		{
			return null;
		}

		Assert.True(text.Fits);

		return Encoding.UTF8.GetString(text.Written.ToArray());
	}

	/// <summary>A text's text through a code's text section.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The text.</param>
	/// <returns>The text, or <see langword="null"/> where the section doesn't show it.</returns>
	private static string? RenderText(string code, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		var text = new ShownText(new byte[(bytes.Length * 4) + 1024]);

		return NumberText.TryWriteText(FormatCode.Compile(code), bytes, ref text) ? Encoding.UTF8.GetString(text.Written.ToArray()) : null;
	}

	/// <summary>A workbook of one sheet whose cell formats are the codes, in order.</summary>
	/// <param name="codes">The codes.</param>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <returns>The package.</returns>
	private static TestPackage Book(string[] codes, string rows)
	{
		var custom = new StringBuilder();
		var cells = new StringBuilder();
		for (var i = 0; i < codes.Length; i++)
		{
			custom.Append($"<numFmt numFmtId=\"{164 + i}\" formatCode=\"{Sheets.Escape(codes[i]).Replace("\"", "&quot;")}\"/>");
			cells.Append($"<xf numFmtId=\"{164 + i}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		}

		var styles = TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\">" +
			$"<numFmts count=\"{codes.Length}\">{custom}</numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
			"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
			$"<cellXfs count=\"{codes.Length}\">{cells}</cellXfs></styleSheet>";

		return TestPackage.Workbook(new TestSheet("Codes", xml: Sheets.Worksheet(rows))).Set("xl/styles.xml", styles);
	}

	/// <summary>Reads every field of a workbook's sheet, in order.</summary>
	/// <param name="package">The workbook.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="text">What text the reader gives.</param>
	/// <returns>The fields.</returns>
	private static async Task<List<string>> ReadAsync(TestPackage package, bool async, ExcelText text)
	{
		var options = new ExcelReaderOptions { Text = text, Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } };
		using var workbook = await Workbooks.OpenAsync(package, async);
		using var reader = await workbook.OpenReaderAsync(async, options);
		var fields = new List<string>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				fields.Add(reader.GetString(i));
			}
		}

		return fields;
	}
}
