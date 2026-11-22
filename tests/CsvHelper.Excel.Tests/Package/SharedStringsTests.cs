using System.Text;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The shared strings part: the text of each string, rich text and phonetic runs, decoding, the table's chunks, and its
/// limits. The tests of the part's XML run in every <see cref="ScanMode"/>, those of strings of mebibytes in each mode that
/// reads more than a byte at a time, and many with chunks of a few bytes, which put the chunks' ends where a test needs
/// them; the tests through a package load synchronously and asynchronously.
/// </summary>
public class SharedStringsTests
{
	/// <summary>4 MiB: a stretch of the offsets, and the largest chunk of several strings.</summary>
	private const int MiB4 = 4 << 20;

	/// <summary><c>Array.MaxLength</c>, which .NET Framework lacks: the most bytes the table holds.</summary>
	private const int ArrayMaxLength = 0x7FFFFFC7;

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
	public async Task EndsAStringAtItsChunksEnd()
	{
		// Chunks of 16 bytes. The first string fills the first chunk, and the empty string after it starts at the chunk's end.
		// The next string moves to a new chunk with nothing, which the string after it fills; the last moves to a third.
		var expected = new[] { "0123456789abcdef", "", "ghij", "klmnopqrstuv", "", "w" };
		var xml = Sst(string.Concat(expected.Select(s => s.Length == 0 ? "<si/>" : $"<si><t>{s}</t></si>")));

		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, maxChunk: 16);

			Assert.Equal(expected, Packages.Strings(table));

			// Two full chunks and the last trimmed to its string, and the starts.
			Assert.Equal(16 + 16 + 1 + (4 * 7), table.MemorySize);
		});
	}

	[Fact]
	public async Task StartsEmptyStringsAtAChunksStart()
	{
		// Chunks of 16 bytes. Before the first text, and after a string longer than a chunk, which has one of its own: the
		// strings after it, empty or not, start at the next stretch, where the next chunk starts, or none does. A chunk a
		// long string moves out of is trimmed to what it holds.
		var x = new string('x', 40);
		var y = new string('y', 17);
		var cases = new (string Xml, string[] Expected, long MemorySize)[]
		{
			("<si/><si><t/></si><si><t>abc</t></si>", new[] { "", "", "abc" }, 3 + (4 * 4)),
			($"<si><t>abc</t></si><si><t>{x}</t></si><si/><si><t></t></si><si><t>def</t></si>", new[] { "abc", x, "", "", "def" }, 3 + 40 + 3 + (4 * 6)),
			($"<si><t>{x}</t></si><si/>", new[] { x, "" }, 40 + (4 * 3)),
			($"<si><t>{x}</t></si><si><t>{y}</t></si><si/>", new[] { x, y, "" }, 40 + 17 + (4 * 4)),
			($"<si><t>{x}</t></si><si><t>  </t></si><si><r><t>{y}</t></r></si>", new[] { x, "", y }, 40 + 17 + (4 * 4)),
		};

		await XmlScan.InEveryModeAsync(async mode =>
		{
			foreach (var (xml, expected, memorySize) in cases)
			{
				var table = await ReadAsync(Sst(xml), mode, maxChunk: 16);

				Assert.Equal(expected, Packages.Strings(table));
				Assert.Equal(memorySize, table.MemorySize);
			}
		});
	}

	[Fact]
	public async Task StartsAnEmptyStringAfterAFullChunkOfFourMebibytes()
	{
		// A first chunk of 4 MiB, which 65,536 strings of 64 bytes fill: the empty string after them starts at the next
		// stretch's first offset, where the chunk of the string after it starts. EndsAStringAtItsChunksEnd reaches the same
		// edge in every mode, at the end of a chunk of 16 bytes, which is not a stretch's end.
		var strings = Enumerable.Range(0, 1 << 16).Select(i => i.ToString("D6") + new string((char)('a' + (i % 26)), 58)).ToList();
		var expected = strings.Concat(new[] { "", "after", "" }).ToList();
		var xml = Sst(string.Concat(expected.Select(s => s.Length == 0 ? "<si/>" : $"<si><t>{s}</t></si>")), $" uniqueCount=\"{expected.Count}\"");

		await InWholeModesAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, declaredSize: 1L << 40);

			Assert.True(expected.SequenceEqual(Packages.Strings(table)), "The strings differ.");
			Assert.Equal(MiB4 + 5 + (4L * (expected.Count + 1)), table.MemorySize);
		});
	}

	[Fact]
	public async Task StartsTheStringAfterALongStringAtTheNextStretch()
	{
		// Chunks of 16 bytes, and strings of 4 MiB in runs of 1 KB, each in a chunk of its own that grows by doubling: each
		// ends at the end of its stretch, and the empty strings after them start at the next, the last where no chunk does.
		// The chunks the long strings moved from are trimmed to their strings.
		var run = Letters(1024, 1);
		var four = string.Concat(Enumerable.Repeat(run, 4096));
		var runs = $"<si>{string.Concat(Enumerable.Repeat($"<r><t>{run}</t></r>", 4096))}</si>";
		var xml = Sst($"<si><t>abc</t></si>{runs}<si/><si><t>z</t></si>{runs}<si/>");

		await InWholeModesAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, maxChunk: 16);

			Assert.True(new[] { "abc", four, "", "z", four, "" }.SequenceEqual(Packages.Strings(table)), "The strings differ.");
			Assert.Equal(3 + MiB4 + 1 + MiB4 + (4 * 7), table.MemorySize);
		});
	}

	[Fact]
	public async Task MovesAStringWithWhatItHas()
	{
		// Chunks of 32 bytes, after a string of 10. The second string moves in its third run, at the escapes after its CDATA,
		// so the start of its t moves with it; its first run was trimmed, and its second has a reference. The third string
		// moves at its start.
		var xml = Sst(
			"<si><t>0123456789</t></si>" +
			"<si><r><t>  ab  </t></r><r><rPr><b/></rPr><t xml:space=\"preserve\"> c&amp;d </t></r><r><t><![CDATA[e<f]]>_x0041__x00e9_</t></r></si>" +
			"<si><t>  0123456789abcdefghij  </t></si>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, maxChunk: 32);

			Assert.Equal(["0123456789", "ab c&d e<fAé", "0123456789abcdefghij"], Packages.Strings(table));

			// Each chunk is trimmed to its string: the first two when a string moves out, the last at the end.
			Assert.Equal(10 + 13 + 20 + (4 * 4), table.MemorySize);
		});
	}

	[Fact]
	public async Task GivesAStringLongerThanAChunkAChunkOfItsOwn()
	{
		// Chunks of 16 bytes: a string of 100 bytes has a chunk of its own, and the first chunk, which it moved out of, is
		// trimmed to its 3 bytes; one of ten runs of 10 starts in a chunk of 16, which grows to 20, 40, 80 and 160 bytes, and
		// is trimmed to 100 at its end; the string after them starts a chunk of 16, trimmed to 1 at the end.
		var x = new string('x', 100);
		var y = string.Concat(Enumerable.Range(0, 10).Select(i => i.ToString("D10")));
		var xml = Sst($"<si><t>abc</t></si><si><t>{x}</t></si><si>{string.Concat(Enumerable.Range(0, 10).Select(i => $"<r><t>{i:D10}</t></r>"))}</si><si><t>z</t></si>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, maxChunk: 16);

			Assert.Equal(["abc", x, y, "z"], Packages.Strings(table));
			Assert.Equal(3 + 100 + 100 + 1 + (4 * 5), table.MemorySize);
		});
	}

	[Fact]
	public async Task ReadsTheSameStringsWhateverTheChunksSizes()
	{
		// Chunks of 1 to 64 bytes put their ends everywhere in these strings: in a run and between runs, in CDATA,
		// references, escapes and white space that is trimmed, and in strings longer than a chunk, which move with what
		// they have, grow a chunk they have to themselves, or have one of their own.
		var cases = new (string Xml, string Expected)[]
		{
			("<si><t>0123456789</t></si>", "0123456789"),
			("<si><r><t>  ab  </t></r><r><rPr><b/></rPr><t xml:space=\"preserve\"> c&amp;d </t></r><r><t><![CDATA[e<f]]>_x0041__x00e9_</t></r></si>", "ab c&d e<fAé"),
			("<si><t>   </t></si>", ""),
			("<si/>", ""),
			("<si><t>xy</t><rPh sb=\"0\" eb=\"1\"><t>no</t></rPh><r><t>_x005F_x0041_</t></r></si>", "xy_x0041_"),
			("<si><t>東京</t><r><t>" + Grinning + "</t></r></si>", "東京" + Grinning),
			("<si><t>" + Letters(70, 2) + "</t></si>", Letters(70, 2)),
			("<si>" + string.Concat(Enumerable.Repeat("<r><t>0123456</t></r>", 10)) + "</si>", string.Concat(Enumerable.Repeat("0123456", 10))),
			("<si><t>" + string.Concat(Enumerable.Repeat("_x0041_", 12)) + "</t></si>", new string('A', 12)),
			("<si><t>" + new string(' ', 80) + "</t></si>", ""),
			("<si></si>", ""),
			("<si><t>&#x1F600;&lt;&gt;</t><t xml:space=\"preserve\">  </t></si>", Grinning + "<>  "),
			("<si><t>end</t></si>", "end"),
		};
		var xml = Sst(string.Concat(cases.Select(c => c.Xml)));
		var expected = cases.Select(c => c.Expected).ToList();

		await XmlScan.InEveryModeAsync(async mode =>
		{
			for (var chunk = 1; chunk <= 64; chunk++)
			{
				var strings = Packages.Strings(await ReadAsync(xml, mode, maxChunk: chunk));

				Assert.True(expected.SequenceEqual(strings), $"Chunks of {chunk} bytes: {string.Join("|", strings)}");
			}
		});
	}

	[Fact]
	public async Task SizesEachChunkFromWhatThePartHasLeft()
	{
		// Chunks of 1 KB at most, and 25 strings of 100 bytes. An estimate 50 bytes over the text makes chunks of 1,024,
		// 1,024 and 550, the last holding the last 500 bytes and kept. One far too large makes three of 1,024, the last
		// trimmed to 500. One far too small (no room for the markup) makes a first of 256 (trimmed to its 2 strings when
		// the third moves), then doubles: 512, 1,024, 1,024, the last trimmed to 800.
		var strings = Enumerable.Range(0, 25).Select(i => Letters(100, 10 + i)).ToList();
		var xml = Sst(string.Concat(strings.Select(t => $"<si><t>{t}</t></si>")), " uniqueCount=\"25\"");
		var cases = new (long DeclaredSize, long MemorySize)[]
		{
			(2500 + (16 * 25) + 50, 1024 + 1024 + 550 + (4 * 26)),
			(1L << 40, 1024 + 1024 + 500 + (4 * 26)),
			(0, 200 + 512 + 1024 + 800 + (4 * 26)),
		};

		await XmlScan.InEveryModeAsync(async mode =>
		{
			foreach (var (declaredSize, memorySize) in cases)
			{
				var table = await ReadAsync(xml, mode, declaredSize: declaredSize, maxChunk: 1024);

				Assert.Equal(strings, Packages.Strings(table));
				Assert.Equal(memorySize, table.MemorySize);
			}
		});
	}

	[Fact]
	public async Task DoublesAChunkOnceTheEstimateHasLessLeftThanAStringNeeds()
	{
		// An estimate of 300 bytes, and one string of six runs: 100 bytes five times, then 80. The first chunk is the 300
		// the estimate has left; the fourth run needs 400, more than it says is left, so the chunk doubles to 600, which
		// the last two runs fit, and its 20 spare bytes are kept. Sized from the estimate alone, it would grow by each run
		// (400, 500, 580), one copy a run.
		var runs = new[] { 100, 100, 100, 100, 100, 80 }.Select((n, i) => Letters(n, 40 + i)).ToList();
		var xml = Sst($"<si>{string.Concat(runs.Select(t => $"<r><t>{t}</t></r>"))}</si>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode, declaredSize: 300);

			Assert.Equal([string.Concat(runs)], Packages.Strings(table));
			Assert.Equal(600 + (4 * 2), table.MemorySize);
		});
	}

	[Fact]
	public async Task ReadsManyStringsWhateverTheSizeDeclared()
	{
		// 150,000 strings of up to 80 bytes, about 6 MB: chunks from the estimate, all of 4 MiB from one far too large,
		// and doubling from 256 bytes for one far too small; each table holds its text and starts, and at most an eighth more.
		var random = new Random(5);
		var expected = Enumerable.Range(0, 150_000).Select(i => Letters(random.Next(0, 81), i)).ToList();
		var xml = Sst(string.Concat(expected.Select(t => $"<si><t>{t}</t></si>")), $" uniqueCount=\"{expected.Count}\"");
		var needed = expected.Sum(t => (long)t.Length) + (4L * (expected.Count + 1));

		await InWholeModesAsync(async mode =>
		{
			foreach (var declaredSize in new long?[] { null, 1L << 40, 0 })
			{
				var table = await ReadAsync(xml, mode, declaredSize: declaredSize);

				Assert.True(expected.SequenceEqual(Packages.Strings(table)), $"Declared {declaredSize}: the strings differ.");
				Assert.InRange(table.MemorySize, needed, needed * 9 / 8);
			}
		});
	}

	[Fact]
	public async Task ThrowsPastWhatTheTableCanHoldInSmallChunks()
	{
		// Chunks of 4 bytes and a table of 12 at most: strings that fill their chunks, move, or have a chunk of their own, and
		// XML longer than the room left whose text fits, or doesn't. Escapes and white space count until the end of their t
		// decodes or trims them.
		var fits = new (string Xml, string[] Expected)[]
		{
			("<si><t>abcd</t></si><si><t>efgh</t></si><si><t>ijkl</t></si>", new[] { "abcd", "efgh", "ijkl" }),
			("<si><t>abcd</t></si><si><t>efg</t></si>", new[] { "abcd", "efg" }),
			("<si><t>abcd</t></si><si><t>efgh</t></si><si><t>&amp;&amp;&amp;&amp;</t></si>", new[] { "abcd", "efgh", "&&&&" }),
			($"<si><t>{string.Concat(Enumerable.Repeat("&amp;", 12))}</t></si>", new[] { new string('&', 12) }),
			("<si><t>ab</t></si><si><r><t>cd</t></r><r><t>efghijkl</t></r></si>", new[] { "ab", "cdefghijkl" }),
			("<si><t>ab</t></si><si><t>_x0041_</t></si><si><t>cdefghijk</t></si>", new[] { "ab", "A", "cdefghijk" }),
		};
		var over = new (string Xml, string At)[]
		{
			("<si><t>abcd</t></si><si><t>efgh</t></si><si><t>ijklm</t></si>", "ijklm"),
			("<si><t>abcd</t></si><si><t>efgh</t></si><si><t>&amp;&amp;&amp;&amp;&amp;</t></si>", "&amp;"),
			($"<si><t>{string.Concat(Enumerable.Repeat("&amp;", 13))}</t></si>", "&amp;"),
			("<si><t>ab</t></si><si><r><t>cd</t></r><r><t>efghijklm</t></r></si>", "efghijklm"),
			("<si><t>  abcdefghijkl  </t></si>", "  abcdefghijkl"),
		};

		await XmlScan.InEveryModeAsync(async mode =>
		{
			foreach (var (xml, expected) in fits)
			{
				Assert.Equal(expected, Packages.Strings(await ReadAsync(Sst(xml), mode, 12, maxChunk: 4)));
			}

			foreach (var (content, at) in over)
			{
				var xml = Sst(content);
				var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(xml, mode, 12, maxChunk: 4));
				Assert.Equal($"The XML of test.xml can't be read at byte {xml.IndexOf(at)}: its strings come to more than 12 bytes, the most the table can hold.", e.Message);
			}
		});
	}

	[Fact]
	public async Task ThrowsPastTheLastStretch()
	{
		// Chunks of 16 bytes and strings of 10: each string has a chunk, and so a stretch, of its own. The 512th is at the
		// last stretch; a 513th would start past the last offset, as would the string after one longer than a chunk there.
		var strings = Enumerable.Range(0, 513).Select(i => i.ToString("D10")).ToList();
		var fits = Sst(string.Concat(strings.Take(512).Select(s => $"<si><t>{s}</t></si>")));
		var over = Sst(string.Concat(strings.Select(s => $"<si><t>{s}</t></si>")));
		var longLast = Sst(string.Concat(strings.Take(511).Select(s => $"<si><t>{s}</t></si>")) + $"<si><t>{new string('x', 20)}</t></si>");
		var message = $"its strings come to more than {ArrayMaxLength} bytes, the most the table can hold.";

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(strings.Take(512), Packages.Strings(await ReadAsync(fits, mode, maxChunk: 16)));

			var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(over, mode, maxChunk: 16));
			Assert.Equal($"The XML of test.xml can't be read at byte {over.IndexOf(strings[512])}: {message}", e.Message);

			e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(longLast, mode, maxChunk: 16));
			Assert.Equal($"The XML of test.xml can't be read at byte {longLast.LastIndexOf("</si>")}: {message}", e.Message);
		});
	}

	[Fact]
	public async Task ReadsStringsOverFourMebibytes()
	{
		// A string of 5 MiB in one t, and one of 4.5 MiB in runs of 64 KB, between strings: each has a chunk of its own,
		// and the strings after them start at the next stretch.
		var one = Letters(5 << 20, 3) + "é東" + Grinning;
		var runs = Enumerable.Range(0, 72).Select(i => Letters(64 << 10, 4 + i)).ToList();
		var expected = new[] { "before", one, "", "between", string.Concat(runs), "after" };
		var xml = Sst($"<si><t>before</t></si><si><t>{one}</t></si><si/><si><t>between</t></si><si>{string.Concat(runs.Select(r => $"<r><t>{r}</t></r>"))}</si><si><t>after</t></si>");
		var needed = expected.Sum(s => (long)Encoding.UTF8.GetByteCount(s)) + (4L * (expected.Length + 1));

		await InWholeModesAsync(async mode =>
		{
			var table = await ReadAsync(xml, mode);

			Assert.True(expected.SequenceEqual(Packages.Strings(table)), "The strings differ.");
			Assert.InRange(table.MemorySize, needed, needed * 9 / 8);
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
	/// <param name="maxChunk">The most bytes a chunk of several strings has, or <see langword="null"/> for 4 MiB.</param>
	/// <returns>The shared strings.</returns>
	private static async Task<SharedStrings> ReadAsync(string xml, ScanMode mode, int? maxLength = null, long? declaredSize = null, int? maxChunk = null)
	{
		var bytes = Encoding.UTF8.GetBytes(xml);

		// A window that can hold the whole part, for a text of several mebibytes.
		using var scanner = XmlScan.Open(bytes, mode, Math.Max(1L << 20, bytes.Length));
		var size = declaredSize ?? bytes.Length;

		return mode.Async
			? await SharedStrings.ReadAsync(scanner, size, true, CancellationToken.None, maxLength, maxChunk)
			: SyncOrAsync.Result(SharedStrings.ReadAsync(scanner, size, false, CancellationToken.None, maxLength, maxChunk));
	}

	/// <summary>
	/// Runs a test of mebibytes in each mode that reads more than a byte at a time, and says which mode failed: a read of a
	/// byte at a time looks for the end of a long text again for each byte that arrives.
	/// </summary>
	/// <param name="test">The test, for one mode.</param>
	/// <returns>A task that completes when every such mode has passed.</returns>
	private static async Task InWholeModesAsync(Func<ScanMode, Task> test)
	{
		foreach (var mode in ScanMode.All.Where(m => !m.Trickle))
		{
			try
			{
				await test(mode);
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"In mode '{mode}': {e.Message}", e);
			}
		}
	}

	/// <summary>Random lowercase letters: a string that a copy from the wrong place doesn't match by chance.</summary>
	/// <param name="length">The number of letters.</param>
	/// <param name="seed">The seed of the letters.</param>
	/// <returns>The letters.</returns>
	private static string Letters(int length, int seed)
	{
		var random = new Random(seed);
		var letters = new char[length];
		for (var i = 0; i < length; i++)
		{
			letters[i] = (char)('a' + random.Next(26));
		}

		return new string(letters);
	}
}
