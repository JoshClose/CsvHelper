using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The shared strings part: the text of each string, rich text and phonetic runs, decoding, and the table's limits. The
/// tests of the part's XML run in every <see cref="ScanMode"/>; the tests through a package load synchronously and
/// asynchronously.
/// </summary>
public class SharedStringsTests
{
	/// <summary>U+FFFD, which a surrogate escape that is not part of a pair becomes.</summary>
	private static readonly string Replacement = ((char)0xFFFD).ToString();

	/// <summary>U+1F600, a character outside the Basic Multilingual Plane.</summary>
	private static readonly string Grinning = char.ConvertFromUtf32(0x1F600);

	[Fact]
	public async Task ReadsEachStringInOrder()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(Sst("<si><t>Name</t></si><si><t>Amount</t></si><si><t>x</t></si>", " count=\"5\" uniqueCount=\"3\""), mode);

			Assert.Equal(["Name", "Amount", "x"], Packages.Strings(table));
			Assert.Equal(XmlScan.PartName, table.PartName);
			foreach (var index in new[] { 3, -1, int.MinValue, int.MaxValue })
			{
				Assert.False(table.TryGet(index, out var text));
				Assert.True(text.IsEmpty);
			}
		});
	}

	[Fact]
	public async Task KeepsThePlaceOfEmptyStrings()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(Sst("<si/><si><t>a</t></si><si><t/></si><si><t></t></si><si></si><si><t>b</t></si><si><r><t/></r></si><si><r/></si>"), mode);

			Assert.Equal(["", "a", "", "", "", "b", "", ""], Packages.Strings(table));
		});
	}

	[Fact]
	public async Task JoinsRichTextRuns()
	{
		// As Excel writes them, and a t before the runs, which the schema allows.
		var runs =
			"<si><r><rPr><b/><sz val=\"11\"/><color theme=\"1\"/><rFont val=\"Aptos Narrow\"/><family val=\"2\"/><scheme val=\"minor\"/></rPr><t>Bold</t></r>" +
			"<r><rPr><sz val=\"11\"/><color theme=\"1\"/><rFont val=\"Aptos Narrow\"/><family val=\"2\"/><scheme val=\"minor\"/></rPr><t xml:space=\"preserve\"> and plain</t></r></si>" +
			"<si><t>a</t><r><t>b</t></r><r><rPr/><t>c</t><t>d</t></r></si>";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["Bold and plain", "abcd"], Packages.Strings(await ReadAsync(Sst(runs), mode)));
		});
	}

	[Fact]
	public async Task SkipsPhoneticRuns()
	{
		var phonetic =
			"<si><t>東京</t><rPh sb=\"0\" eb=\"2\"><t>トウキョウ</t></rPh><phoneticPr fontId=\"1\" type=\"noConversion\"/></si>" +
			"<si><r><t>日本</t></r><r><t>語</t></r><rPh sb=\"0\" eb=\"2\"><t>ニホン</t></rPh><rPh sb=\"2\" eb=\"3\"><t>ゴ</t></rPh><phoneticPr fontId=\"1\"/></si>";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["東京", "日本語"], Packages.Strings(await ReadAsync(Sst(phonetic), mode)));
		});
	}

	[Fact]
	public async Task TrimsATextUnlessItsSpaceIsPreserved()
	{
		// Each as Excel 365 reads it (checked through COM on 2026-11-02).
		var cases = new (string Xml, string Expected)[]
		{
			("<si><t>  plain  </t></si>", "plain"),
			("<si><t xml:space=\"preserve\">  preserved  </t></si>", "  preserved  "),
			("<si><t>   </t></si>", ""),
			("<si><t>\n\ttabs and lines\t\n</t></si>", "tabs and lines"),
			("<si><t>a  b</t></si>", "a  b"),
			("<si><t>a\nb</t></si>", "a\nb"),
			("<si><r><t>bold </t></r><r><t> plain</t></r></si>", "boldplain"),
			("<si><r><t xml:space=\"preserve\">run </t></r><r><t> two</t></r></si>", "run two"),
			("<si xml:space=\"preserve\"><t>  on si  </t></si>", "  on si  "),
			("<si><r xml:space=\"preserve\"><t> on r </t></r></si>", " on r "),
			("<si><t>&#32;references&#32;</t></si>", "references"),
			("<si><t>&#9;tab and line&#10;</t></si>", "tab and line"),
			("<si><t><![CDATA[  cdata  ]]></t></si>", "cdata"),
			("<si><t>_x0020_escapes_x0020_</t></si>", " escapes "),
		};
		var xml = Sst(string.Concat(cases.Select(c => c.Xml)));
		var indented = "\r\n  <si>\r\n    <t>a</t>\r\n  </si>\r\n  <si>\r\n    <r>\r\n      <t>b</t>\r\n    </r>\r\n  </si>\r\n";
		var root = Sst("<si><t>  under the root  </t></si><si><t xml:space=\"default\">  default  </t></si>", " xml:space=\"preserve\"");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(cases.Select(c => c.Expected), Packages.Strings(await ReadAsync(xml, mode)));
			Assert.Equal(["a", "b"], Packages.Strings(await ReadAsync(Sst(indented), mode)));
			Assert.Equal(["  under the root  ", "default"], Packages.Strings(await ReadAsync(root, mode)));
		});
	}

	[Fact]
	public async Task DecodesTheXmlOfEachText()
	{
		var xml =
			"<si><t>&amp;&lt;&gt;&quot;&apos;</t></si>" +
			"<si><t>&#233;&#xE9;&#x1F600;</t></si>" +
			"<si><t><![CDATA[<b>&amp;</b>]]></t></si>" +
			"<si><t>a\r\nb\rc</t></si>" +
			"<si><t xml:space=\"preserve\">a&#13;&#10;b&#xD;</t></si>" +
			"<si><t>a<!-- not text -->b<?pi not text?>c</t></si>" +
			"<si><t>a<![CDATA[\r\n]]>b</t></si>";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["&<>\"'", "éé" + Grinning, "<b>&amp;</b>", "a\nb\nc", "a\r\nb\r", "abc", "a\nb"],
				Packages.Strings(await ReadAsync(Sst(xml), mode)));
		});
	}

	[Fact]
	public async Task DecodesTheEscapesOfEachText()
	{
		var cases = new (string Xml, string Expected)[]
		{
			("<t>_x0041_</t>", "A"),
			("<t>_x005F_x0041_</t>", "_x0041_"),
			("<t>_x000D__x000A_</t>", "\r\n"),
			("<t>_x0009_tab</t>", "\ttab"),
			("<t>_xD83D__xDE00_</t>", Grinning),
			("<t>_xD83D_</t>", Replacement),
			("<t>_xDE00__xD83D_</t>", Replacement + Replacement),
			("<t>_x00e9__x00E9_</t>", "éé"),
			("<t>_X0041_ _x004_ _x00G1_ _x _</t>", "_X0041_ _x004_ _x00G1_ _x _"),
			("<t>__x0041__</t>", "_A_"),

			// The escape pass comes after the XML is decoded, over the whole text of the t.
			("<t>&#95;x0041_</t>", "A"),
			("<t><![CDATA[_x0041_]]></t>", "A"),
			("<t>_x00<!-- -->41_</t>", "A"),
			("<t>_x00<![CDATA[41_]]></t>", "A"),

			// Each t is a value of its own: an escape doesn't continue into the next run, nor a surrogate pair.
			("<r><t>_x00</t></r><r><t>41_</t></r>", "_x0041_"),
			("<r><t>_xD83D_</t></r><r><t>_xDE00_</t></r>", Replacement + Replacement),
		};
		var xml = Sst(string.Concat(cases.Select(c => $"<si>{c.Xml}</si>")));

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(cases.Select(c => c.Expected), Packages.Strings(await ReadAsync(xml, mode)));
		});
	}

	[Fact]
	public async Task MatchesLocalNamesInEitherNamespace()
	{
		var prefixed = $"<x:sst xmlns:x=\"{TestPackage.MainNamespace}\"><x:si><x:t>a</x:t></x:si><x:si><x:r><x:rPr/><x:t>b</x:t></x:r><x:rPh><x:t>no</x:t></x:rPh></x:si></x:sst>";
		var strict = $"<sst xmlns=\"{TestPackage.StrictMainNamespace}\"><si><t>a</t></si><si><r><t>b</t></r></si></sst>";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["a", "b"], Packages.Strings(await ReadAsync(prefixed, mode)));
			Assert.Equal(["a", "b"], Packages.Strings(await ReadAsync(strict, mode)));
		});
	}

	[Fact]
	public async Task SkipsWhatIsNotAStringsText()
	{
		var xml =
			"<si>no<t>a</t>no</si>" +
			"<si><r>no<t>b</t>no</r></si>" +
			"<si><t>c<b>no<t>no</t></b>d</t></si>" +
			"<si><unknown><t>no</t><si><t>no</t></si></unknown><t>e</t></si>" +
			"<si><r><unknown><t>no</t></unknown><t>f</t></r></si>" +
			"<extLst><ext uri=\"{00000000-0000-0000-0000-000000000000}\"><si><t>no</t></si></ext></extLst>" +
			"<si><t>g</t></si>";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["a", "b", "cd", "e", "f", "g"], Packages.Strings(await ReadAsync(Sst(xml), mode)));
		});
	}

	[Fact]
	public async Task TakesUniqueCountAsAHintOnly()
	{
		var strings = "<si><t>a</t></si><si><t>b</t></si>";

		foreach (var attributes in new[] { "", " uniqueCount=\"0\"", " uniqueCount=\"1\"", " uniqueCount=\"1000000000\"", " uniqueCount=\"99999999999\"", " uniqueCount=\"-3\"", " uniqueCount=\"abc\"", " uniqueCount=\" 2\"", " count=\"x\"" })
		{
#if NET
			var before = GC.GetAllocatedBytesForCurrentThread();
#endif
			var table = await ReadAsync(Sst(strings, attributes), ScanMode.All[0]);

			Assert.Equal(["a", "b"], Packages.Strings(table));

			// A billion strings declared reserve no more than the cap (a million), and the table is trimmed after.
			Assert.True(table.MemorySize < 1024, $"{attributes}: {table.MemorySize} bytes");
#if NET
			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.True(allocated < 8 << 20, $"{attributes}: {allocated} bytes allocated");
#endif
		}
	}

	[Fact]
	public async Task GrowsAndTrimsWhateverTheSizesDeclared()
	{
		var random = new Random(4);
		var expected = Enumerable.Range(0, 2000).Select(i => new string((char)('a' + i % 26), random.Next(0, 120))).ToList();
		var xml = Sst(string.Concat(expected.Select(s => $"<si><t>{s}</t></si>")));
		var bytes = Encoding.UTF8.GetBytes(xml);
		var needed = expected.Sum(s => s.Length) + 4L * (expected.Count + 1);

		foreach (var declaredSize in new[] { bytes.Length, 0, 1L << 40 })
		{
			await XmlScan.InEveryModeAsync(async mode =>
			{
				var table = await ReadAsync(xml, mode, declaredSize: declaredSize);

				Assert.Equal(expected, Packages.Strings(table));
				Assert.InRange(table.MemorySize, needed, needed * 9 / 8);
			});
		}
	}

	[Fact]
	public async Task ThrowsPastWhatTheTableCanHold()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			// 8 bytes at most, entities included: their XML is longer than the room left, but not their text.
			Assert.Equal(["abcd", "efgh"], Packages.Strings(await ReadAsync(Sst("<si><t>abcd</t></si><si><t>efgh</t></si>"), mode, 8)));
			Assert.Equal(["abcd", "&&&&"], Packages.Strings(await ReadAsync(Sst("<si><t>abcd</t></si><si><t>&amp;&amp;&amp;&amp;</t></si>"), mode, 8)));

			var xml = Sst("<si><t>abcd</t></si><si><t>e&amp;ghi</t></si>");
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(xml, mode, 8));
			Assert.Equal($"The XML of test.xml can't be read at byte {xml.IndexOf("e&amp;")}: its strings come to more than 8 bytes, the most the table can hold.", e.Message);

			// 7 strings at most, with the end of the last.
			Assert.Equal(7, (await ReadAsync(Sst(string.Concat(Enumerable.Repeat("<si/>", 7))), mode, 8)).Count);

			xml = Sst(string.Concat(Enumerable.Repeat("<si></si>", 8)));
			e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(xml, mode, 8));
			Assert.Equal($"The XML of test.xml can't be read at byte {xml.LastIndexOf("</si>")}: it has more than 7 strings, the most the table can hold.", e.Message);
		});
	}

	[Fact]
	public async Task ThrowsForXmlThatIsNotAStringTable()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync("<sharedStrings><si><t>a</t></si></sharedStrings>", mode));
			Assert.Equal("The XML of test.xml can't be read at byte 0: its root element is not sst.", e.Message);

			var xml = TestPackage.Declaration + "<!DOCTYPE sst [<!ENTITY a \"aaaa\">]>" + Sst("<si><t>&a;</t></si>");
			e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(xml, mode));
			Assert.Equal($"The XML of test.xml can't be read at byte {TestPackage.Declaration.Length}: it has a DOCTYPE, which Office Open XML parts must not have.", e.Message);

			xml = Sst("<si><t>a&nbsp;b</t></si>");
			e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(xml, mode));
			Assert.Equal($"The XML of test.xml can't be read at byte {xml.IndexOf('&')}: '&nbsp;' is neither a predefined entity nor a valid character reference.", e.Message);

			e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Sst("<si><t>a</t></si>").Replace("</sst>", ""), mode));
			Assert.EndsWith(": it ends inside an element.", e.Message);
		});
	}

	[Fact]
	public async Task StopsWhenCanceled()
	{
		var bytes = Encoding.UTF8.GetBytes(Sst("<si><t>a</t></si>"));
		using var scanner = XmlScan.Open(bytes, ScanMode.All[1]);
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await SharedStrings.ReadAsync(scanner, bytes.Length, true, canceled.Token));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LoadsThePartTheWorkbookNames(bool async)
	{
		var table = await TestPackage.Workbook("Data").SharedStrings("<t>Name</t>", "<t>Amount</t>").LoadSharedStringsAsync(async);

		Assert.Equal(["Name", "Amount"], Packages.Strings(table));
		Assert.Equal("xl/sharedStrings.xml", table.PartName);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task HasNoStringsWithoutAPart(bool async)
	{
		var none = await TestPackage.Workbook("Data").LoadSharedStringsAsync(async);
		var missing = await TestPackage.Workbook("Data").SharedStrings("<t>a</t>").Remove("xl/sharedStrings.xml").LoadSharedStringsAsync(async);

		foreach (var table in new[] { none, missing })
		{
			Assert.Same(SharedStrings.Empty, table);
			Assert.Equal(0, table.Count);
			Assert.Null(table.PartName);
			Assert.False(table.TryGet(0, out _));
		}

		var empty = await TestPackage.Workbook("Data").SharedStringsPart($"<sst xmlns=\"{TestPackage.MainNamespace}\" count=\"0\" uniqueCount=\"0\"/>").LoadSharedStringsAsync(async);
		Assert.NotSame(SharedStrings.Empty, empty);
		Assert.Equal(0, empty.Count);
		Assert.Equal("xl/sharedStrings.xml", empty.PartName);
		Assert.Equal(4, empty.MemorySize);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsAUtf16Part(bool async)
	{
		var xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" + Sst("<si><t>a</t></si><si><t>東京</t></si>");
		foreach (var encoding in new[] { new UnicodeEncoding(false, true), new UnicodeEncoding(true, true) })
		{
			var package = TestPackage.Workbook("Data").SharedStrings("<t>x</t>").Set("xl/sharedStrings.xml", [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)]);

			Assert.Equal(["a", "東京"], Packages.Strings(await package.LoadSharedStringsAsync(async)));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepsThePartWithinTheLimits(bool async)
	{
		var package = TestPackage.Workbook("Data").SharedStrings("<t>Name</t>", "<t>Amount</t>");
		var size = package.Text("xl/sharedStrings.xml").Length;
		var big = new PartLimits(1L << 20, 1L << 30);

		Assert.Equal(2, (await package.LoadSharedStringsAsync(async, new PartLimits(size, 1L << 30))).Count);

		// Checked against the size the zip declares before inflating, and against the bytes as they inflate.
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadSharedStringsAsync(async, new PartLimits(size - 1, 1L << 30)));
		Assert.Equal($"xl/sharedStrings.xml is larger than ExcelReaderOptions.MaxPartSize ({size - 1} bytes uncompressed): its size in the zip is {size} bytes.", e.Message);

		// The workbook's total: everything the open read, and then the part.
		var total = new[] { "[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/workbook.xml", "xl/sharedStrings.xml" }.Sum(n => package.Text(n).Length);
		Assert.Equal(2, (await package.LoadSharedStringsAsync(async, new PartLimits(big.MaxPartSize, total))).Count);
		e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadSharedStringsAsync(async, new PartLimits(big.MaxPartSize, total - 1)));
		Assert.Equal($"Reading xl/sharedStrings.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({total - 1} bytes uncompressed).", e.Message);
	}

	/// <summary>A shared strings part with these strings, in the main namespace.</summary>
	/// <param name="content">The content of the root: <c>si</c> elements, and anything else.</param>
	/// <param name="attributes">The root's attributes, after its namespace, each with a space before it.</param>
	/// <returns>The part's XML.</returns>
	private static string Sst(string content, string attributes = "") => $"<sst xmlns=\"{TestPackage.MainNamespace}\"{attributes}>{content}</sst>";

	/// <summary>Reads shared strings from XML, as <paramref name="mode"/> says.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="maxLength">The most bytes the table holds, or <see langword="null"/> for an array's limit.</param>
	/// <param name="declaredSize">The size the zip declares, or <see langword="null"/> for the part's real size.</param>
	/// <returns>The shared strings.</returns>
	private static async Task<SharedStrings> ReadAsync(string xml, ScanMode mode, int? maxLength = null, long? declaredSize = null)
	{
		var bytes = Encoding.UTF8.GetBytes(xml);
		using var scanner = XmlScan.Open(bytes, mode);
		var size = declaredSize ?? bytes.Length;

		return mode.Async
			? await SharedStrings.ReadAsync(scanner, size, true, CancellationToken.None, maxLength)
			: SyncOrAsync.Result(SharedStrings.ReadAsync(scanner, size, false, CancellationToken.None, maxLength));
	}
}
