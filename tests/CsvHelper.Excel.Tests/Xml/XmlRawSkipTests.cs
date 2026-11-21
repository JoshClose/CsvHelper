using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>
/// Skipping an element by a raw search for its end tag (<see cref="XmlScanner.TrySkipRaw"/>, <see cref="XmlScanner.SkipRaw"/>,
/// <see cref="XmlScanner.SkipRawAsync"/>, <see cref="XmlElements.SkipElementRawAsync"/>), and the qualified names of tags
/// (<see cref="XmlScanner.QualifiedName"/>). Each skip runs in every <see cref="ScanMode"/>, and both with the refilling
/// skip and with <see cref="XmlScanner.TrySkipRaw"/> and a refill loop.
/// </summary>
public class XmlRawSkipTests
{
	/// <summary>The size of the windows that can't grow, in the 64 KB modes; the 16-byte modes keep their 16 bytes.</summary>
	private const int FixedSize = 64;

	/// <summary>What the tokenizer says of a <c>&lt;</c> in an attribute value.</summary>
	private const string LessThanInValue = "an attribute value has '<', which XML doesn't allow";

	/// <summary>The most elements nest below a row in the random rows.</summary>
	private const int MaxDepth = 3;

	/// <summary>The prefixes of the random documents' elements: none, or <c>x:</c>.</summary>
	private static readonly string[] Prefixes = ["", "x:"];

	/// <summary>The names of the elements in the random rows: the row's own name, with and without a prefix, and names that hold it.</summary>
	private static readonly string[] ElementNames = ["c", "v", "t", "is", "row", "x:row", "xx:row", "rows", "rowBreaks", "arrow", "x:c"];

	/// <summary>The names of the attributes in the random rows.</summary>
	private static readonly string[] AttributeNames = ["s", "t", "x:r", "row", "p", "xx:row"];

	/// <summary>The pieces of the random attribute values: the name, marks and markup ends, but never a <c>&lt;</c>.</summary>
	private static readonly string[] ValuePieces =
	[
		"abc", "1", " ", ">", "/", "!", "?", "row", "/row>", "x:row", "&lt;", "&gt;", "&amp;", "-->", "?>", "]]>", "!--", "'",
		"\"",
	];

	/// <summary>The pieces of the random text: the name, marks and markup ends, but never a <c>&lt;</c>.</summary>
	private static readonly string[] TextPieces =
	[
		"abc", " ", "\n", "!", "?", ">", "&lt;", "&gt;", "&amp;", "arrow", "row", "rows", "x:row", "/row>", "!--", "?>", "-->",
		"!DOCTYPE", "'", "\"",
	];

	/// <summary>The comments, CDATA sections and processing instructions of the random rows, holding tags and other markup.</summary>
	private static readonly string[] MarkupPieces =
	[
		"<!-- </row> <row> -->", "<!---->", "<!-- <![CDATA[ <? </x:row> -->", "<!-- - -->", "<![CDATA[</row>]]>",
		"<![CDATA[<row r=\"1\">]]>", "<![CDATA[]]>", "<![CDATA[ ] ]] ]> <!-- <? ]]>", "<![CDATA[x:row</x:row></sheetData>]]>",
		"<?pi </row> ?>", "<?x?>", "<?pi a?b > <!-- <![CDATA[ ?>", "<?pi </x:row>?>",
	];

	/// <summary>The white space of the random rows.</summary>
	private static readonly string[] Spaces = [" ", "\n", "\t", "\r\n  "];

	/// <summary>How a test skips.</summary>
	private enum SkipWay
	{
		/// <summary>Token by token: <see cref="XmlScanner.SkipTo"/>, or <see cref="XmlScanner.SkipToAsync"/>.</summary>
		Tokens,

		/// <summary>Raw, refilling: <see cref="XmlScanner.SkipRaw"/>, or <see cref="XmlScanner.SkipRawAsync"/>.</summary>
		Raw,

		/// <summary>Raw, with <see cref="XmlScanner.TrySkipRaw"/> and a refill after each <see langword="false"/>.</summary>
		RawLoop,
	}

	/// <summary>Skips rows to their end tags, and reads on after them.</summary>
	/// <param name="xml">The part: a row in <c>w</c> and <c>s</c>, followed by an empty <c>after</c>.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><c r=\"A1\" t=\"s\"><v>12</v></c><c r=\"B1\"/></row><after/></s></w>", "")]
	[InlineData("<x:w xmlns:x=\"urn:x\"><x:s><x:row r=\"1\"><x:c r=\"A1\"><x:v>1</x:v></x:c></x:row><x:after/></x:s></x:w>", "x:")]
	[InlineData("<w><s><row r=\"1\">\r\n  <c><is><t xml:space=\"preserve\"> a </t></is></c>\r\n</row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"></row><after/></s></w>", "")]
	[InlineData("<w><s><row>text</row><after/></s></w>", "")]
	public Task StopsAfterTheEndTagThatReachesTheDepth(string xml, string prefix) => SkipsRowAsync(xml, prefix);

	/// <summary>Passes over the name where it isn't a tag of the name: in longer names, after another prefix, and in text and values.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><rows></rows><rowBreaks count=\"1\"/><c>arrow row rows /row></c><x:row a=\"row\">x:row</x:row><c r=\"row\" t='/row>'/><arrow></arrow></row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><c>row</c>rowrow<rowrow/></row><after/></s></w>", "")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><row></row><xx:row></xx:row><x:rows/><x:rowBreaks/><y:row/>x:row xx:row<x:c r=\"x:row\"/></x:row><x:after/></x:s></x:w>", "x:")]
	public Task PassesOverWhatIsntATagOfTheName(string xml, string prefix) => SkipsRowAsync(xml, prefix);

	/// <summary>Reads end tags of the name with white space after the name, as the tokenizer does.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><c/></row  ><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><row></row\r\n\t></row \t><after/></s></w>", "")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:c/></x:row\n><x:after/></x:s></x:w>", "x:")]
	public Task ReadsEndTagsOfTheNameWithWhiteSpace(string xml, string prefix) => SkipsRowAsync(xml, prefix);

	/// <summary>Counts nested elements of the name, unless they are empty, so that their end tags don't end the skip.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><row/><row r=\"x\"><row></row><row /></row><c/></row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><row\t/><row\n><row><row></row></row></row></row><after/></s></w>", "")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:row><x:row/></x:row><row></row><x:row\r\n/></x:row><x:after/></x:s></x:w>", "x:")]
	public Task CountsNestedElementsOfTheName(string xml, string prefix) => SkipsRowAsync(xml, prefix);

	/// <summary>
	/// Skips comments, CDATA sections and processing instructions to their ends, so that the name in them is no hit, and
	/// neither is the start of other markup in them.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><!-- </row> --><![CDATA[</row>]]><?pi </row> ?><c><!--</row>--><![CDATA[ ]] ] </row> ]]></c></row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><!-- - -- --- </row> --><![CDATA[]]]]><![CDATA[ ]]] </row> ]]><?pi ? </row> ??></row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><?pi?><?x </row>?><!----><![CDATA[]]></row><after/></s></w>", "")]
	[InlineData("<w><s><row r=\"1\"><!-- <![CDATA[ <? --><![CDATA[ <!-- <? ]]><?pi <!-- <![CDATA[ ?></row><after/></s></w>", "")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><!-- </x:row> --><![CDATA[</x:row>]]><?x </x:row>?></x:row><x:after/></x:s></x:w>", "x:")]
	public Task SkipsMarkupThatHoldsTheName(string xml, string prefix) => SkipsRowAsync(xml, prefix);

	/// <summary>Skips a <c>sheetData</c> past markup that holds its end tag, from its start tag.</summary>
	/// <param name="xml">The part: a worksheet with <c>sheetData</c> and <c>mergeCells</c>.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<worksheet><sheetData><row r=\"1\"><c><v>1</v></c></row><!-- </sheetData> --><row r=\"2\"><c t=\"inlineStr\"><is><t><![CDATA[</sheetData>]]></t></is></c></row><?pi </sheetData>?></sheetData><mergeCells count=\"1\"><mergeCell ref=\"A1:B2\"/></mergeCells></worksheet>", "")]
	[InlineData("<x:worksheet xmlns:x=\"urn:x\"><x:sheetData><x:row r=\"1\"/><!-- </x:sheetData> --><x:row r=\"2\"><x:c><x:v>2</x:v></x:c></x:row><![CDATA[</x:sheetData>]]><?x </x:sheetData>?></x:sheetData\r\n><x:mergeCells count=\"1\"><x:mergeCell ref=\"A1:B2\"/></x:mergeCells></x:worksheet>", "x:")]
	public async Task SkipsSheetData(string xml, string prefix)
	{
		var bytes = Utf8(xml);
		string[] expected =
		[
			$"EndTag 1 {prefix}sheetData",
			$"at {xml.IndexOf($"<{prefix}mergeCells", StringComparison.Ordinal)}",
			$"StartTag 2 {prefix}mergeCells",
			$"StartTag 2 {prefix}mergeCell empty",
			$"EndTag 1 {prefix}mergeCells",
			$"EndTag 0 {prefix}worksheet",
		];

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 2);
			await SkipByAsync(scanner, way, prefix + "sheetData", 1, mode);
			Assert.Equal(expected, await DescribeRestAsync(scanner, mode));
		});
	}

	/// <summary>Passes over <c>!</c> and <c>?</c> in text, and over <c>&lt;!</c> that starts neither a comment nor CDATA, reading nothing of it.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task PassesOverMarksInTextAndOtherMarkup()
	{
		var xml = "<w><s><row r=\"1\"><c>Wow! Really? a?b!c ?> !> !-- ?! ?</c>!?<!DOCTYPE x><!x><!-x><![x></row><after/></s></w>";

		await SkipsRowAsync(xml, "");

		// The token skip reads the DOCTYPE, which it rejects.
		var bytes = Utf8(xml);
		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.SkipAsync(2, mode));
			AssertError(e, xml.IndexOf("<!DOCTYPE", StringComparison.Ordinal), "it has a DOCTYPE, which Office Open XML parts must not have");
		});
	}

	/// <summary>Reads nothing of the tags it passes over: a <c>&lt;</c> in a value, a value without quotes, an attribute without a value.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task DoesntReadTheTagsItPassesOver()
	{
		var xml = "<w><s><row r=\"1\"><c p=\"a<b\"/><c p='</rows>'/><c p=\"x>y\" q='1\"2'/><c r=A1/><c a b/></row><after/></s></w>";

		await SkipsRowAsync(xml, "");

		// The token skip reads the tags, and rejects the first '<' in a value.
		var bytes = Utf8(xml);
		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.SkipAsync(2, mode));
			AssertError(e, xml.IndexOf("<b\"", StringComparison.Ordinal), LessThanInValue);
		});
	}

	/// <summary>
	/// Values aren't read: a literal <c>&lt;/row&gt;</c> in one (which isn't XML, as <c>&lt;</c> isn't allowed there) is
	/// an end tag of the name, and ends the skip inside the value.
	/// </summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task StopsAtAnEndTagOfTheNameInAnAttributeValue()
	{
		var xml = "<w><s><row r=\"1\"><c p=\"</row>\"/></row><after/></s></w>";
		var bytes = Utf8(xml);

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			await SkipByAsync(scanner, way, "row", 2, mode);
			Assert.Equal("EndTag 2 row", Describe(scanner));
			Assert.Equal((long)xml.IndexOf("\"/>", StringComparison.Ordinal), scanner.Window.Offset(scanner.Position));
			Assert.True(await scanner.NextAsync(mode));
			Assert.Equal("Text 2 '\"/>'", Describe(scanner));
			Assert.True(await scanner.NextAsync(mode));
			Assert.Equal("EndTag 1 row", Describe(scanner));
		});
	}

	/// <summary>
	/// Skips from deeper inside the element than its start tag: everything open below it is passed over with it. At the
	/// first cell's start tag the depth is 4, and the skip's target 2.
	/// </summary>
	/// <param name="tokens">The number of tokens read before the skip.</param>
	/// <param name="depth">The depth after them.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData(3, 3)]
	[InlineData(4, 4)]
	[InlineData(5, 5)]
	[InlineData(6, 6)]
	[InlineData(7, 6)]
	[InlineData(8, 5)]
	[InlineData(9, 4)]
	[InlineData(10, 3)]
	[InlineData(11, 3)]
	public Task SkipsFromDeeperInsideTheElement(int tokens, int depth) =>
		SkipsRowAsync("<w><s><row r=\"1\"><c r=\"A1\"><is><t>x</t></is></c><c r=\"B1\"/></row><after/></s></w>", "", tokens, depth);

	/// <summary>At the depth already, after an empty element or above the target, returns at once and consumes nothing.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task SkipsNothingAtTheDepth()
	{
		var xml = "<w><s><row r=\"1\"/><after/></s></w>";
		var bytes = Utf8(xml);
		string[] expected =
		[
			"StartTag 2 row empty",
			$"at {xml.IndexOf("<after/>", StringComparison.Ordinal)}",
			"StartTag 2 after empty",
			"EndTag 1 s",
			"EndTag 0 w",
		];

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var position = scanner.Position;
			Assert.Equal(2, scanner.Depth);
			Assert.True(scanner.TrySkipRaw("row"u8, 2));
			Assert.True(scanner.TrySkipRaw("row"u8, 5));
			await SkipByAsync(scanner, way, "row", 2, mode);
			Assert.Equal(position, scanner.Position);
			Assert.Equal(expected, await DescribeRestAsync(scanner, mode));
		});
	}

	/// <summary>Skips one row after another, each from its start tag, past rows that are empty.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task SkipsRowAfterRow()
	{
		var builder = new StringBuilder("<w><s>");
		var expected = new List<string> { "StartTag 1 w", "StartTag 2 s" };
		for (var row = 1; row <= 50; row++)
		{
			if (row % 7 == 0)
			{
				builder.Append("<row r=\"").Append(row).Append("\"/>");
				expected.Add("StartTag 2 row empty");
				expected.Add("skipped: StartTag 2 row empty");
			}
			else
			{
				builder.Append("<row r=\"").Append(row).Append("\"><c r=\"A").Append(row).Append("\"><v>").Append(row).Append("</v></c><!-- </row> --></row>");
				expected.Add("StartTag 3 row");
				expected.Add("skipped: EndTag 2 row");
			}

			if (row % 5 == 0)
			{
				builder.Append("\r\n");
				expected.Add("Text 2 '\r\n'");
			}
		}

		builder.Append("<after/></s></w>");
		expected.AddRange(["StartTag 2 after empty", "EndTag 1 s", "EndTag 0 w"]);
		var xml = Utf8(builder.ToString());

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			var tokens = new List<string>();
			while (await scanner.NextAsync(mode))
			{
				tokens.Add(Describe(scanner));
				if (scanner.IsStartTag("row"u8))
				{
					await SkipByAsync(scanner, way, "row", 2, mode);
					tokens.Add("skipped: " + Describe(scanner));
				}
			}

			Assert.Equal(expected, tokens);
		});
	}

	/// <summary>Reads end tags of the name as the tokenizer does, which rejects one with more than a name.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="name">The name skipped.</param>
	/// <param name="marker">The end tag, which the error is at.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><c/></row x><after/></s></w>", "row", "</row x>")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:row></x:row a=\"1\"></x:row><x:after/></x:s></x:w>", "x:row", "</x:row a")]
	public async Task RejectsAnEndTagOfTheNameWithMoreThanAName(string xml, string name, string marker)
	{
		var bytes = Utf8(xml);
		var offset = xml.IndexOf(marker, StringComparison.Ordinal);

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => SkipByAsync(scanner, way, name, 2, mode));
			AssertError(e, offset, "an end tag has more than a name");
		});
	}

	/// <summary>Reads nested start tags of the name as the tokenizer does, which rejects a <c>&lt;</c> in their values.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="name">The name skipped.</param>
	/// <param name="marker">What starts at the <c>&lt;</c> in the value, which the error is at.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><row p=\"a<b\"></row></row><after/></s></w>", "row", "<b\"")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:c/><x:row p='</x:row>'/></x:row><x:after/></x:s></x:w>", "x:row", "</x:row>'")]
	public async Task RejectsALessThanInTheValueOfANestedStartTag(string xml, string name, string marker)
	{
		var bytes = Utf8(xml);
		var offset = xml.IndexOf(marker, StringComparison.Ordinal);

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => SkipByAsync(scanner, way, name, 2, mode));
			AssertError(e, offset, LessThanInValue);
		});
	}

	/// <summary>
	/// Throws when the part ends inside the skip: inside the element at the end of the part, inside markup at its
	/// <c>&lt;</c>, and inside a tag of the name at its <c>&lt;</c>, as the tokenizer does.
	/// </summary>
	/// <param name="xml">The part, cut off.</param>
	/// <param name="name">The name skipped, after three tokens.</param>
	/// <param name="marker">What starts where the error is, found from the end; <see langword="null"/> for the end of the part.</param>
	/// <param name="detail">What is wrong.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<w><s><row r=\"1\"><c r=\"A1\"><v>1</v></c>", "row", null, "it ends inside an element")]
	[InlineData("<w><s><row r=\"1\"><row></row>a</ro", "row", null, "it ends inside an element")]
	[InlineData("<w><s><row r=\"1\"><row>", "row", null, "it ends inside an element")]
	[InlineData("<w><s><row r=\"1\"><c r=\"A1", "row", null, "it ends inside an element")]
	[InlineData("<w><s><row r=\"1\"><!-- </row> -->x<!-- </row> -", "row", "<!--", "it ends inside a comment")]
	[InlineData("<w><s><row r=\"1\"><!-- x --", "row", "<!--", "it ends inside a comment")]
	[InlineData("<w><s><row r=\"1\"><![CDATA[</row>]]", "row", "<![CDATA[", "it ends inside a CDATA section")]
	[InlineData("<w><s><row r=\"1\"><![CDATA[]]><![CDATA[ ]", "row", "<![CDATA[", "it ends inside a CDATA section")]
	[InlineData("<w><s><row r=\"1\"><?pi </row> ?", "row", "<?pi", "it ends inside a processing instruction")]
	[InlineData("<w><s><row r=\"1\"><c/><row r=\"2", "row", "<row r=\"2", "it ends inside a tag")]
	[InlineData("<w><s><row r=\"1\"><c/></row ", "row", "</row ", "it ends inside a tag")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:c/></x:row\r\n", "x:row", "</x:row", "it ends inside a tag")]
	[InlineData("<x:w><x:s><x:row r=\"1\"><x:row ", "x:row", "<x:row ", "it ends inside a tag")]
	public async Task ThrowsWhenThePartEndsInsideTheSkip(string xml, string name, string? marker, string detail)
	{
		var bytes = Utf8(xml);
		var offset = marker is null ? xml.Length : xml.LastIndexOf(marker, StringComparison.Ordinal);

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => SkipByAsync(scanner, way, name, 2, mode));
			AssertError(e, offset, detail);
		});
	}

	/// <summary>
	/// Never grows the window for what it passes over: in a window that can't grow, it skips a row with 10,000 bytes of a
	/// comment, a CDATA section, a processing instruction, an attribute value or text, each holding the name, keeping only
	/// the last few bytes at each refill. The token skip, which reads all but the text as a token, can't.
	/// </summary>
	/// <param name="open">What starts the long token.</param>
	/// <param name="unit">Ten bytes, repeated 1,000 times.</param>
	/// <param name="close">What ends the long token.</param>
	/// <param name="tokenSkipThrows">Whether the token skip reads it as a token, which doesn't fit the window.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<!--", "a-</row>-b", "-->", true)]
	[InlineData("<![CDATA[", "]</row>]]x", "]]>", true)]
	[InlineData("<?pi ", "?</row>? x", "?>", true)]
	[InlineData("<c p=\"", "row/row>!?", "\"/>", true)]
	[InlineData("", "arrow! ?>x", "", false)]
	public async Task NeverGrowsTheWindowForWhatItPassesOver(string open, string unit, string close, bool tokenSkipThrows)
	{
		const string Before = "<w><s><row r=\"1\"><c/>";
		var xml = Before + open + string.Concat(Enumerable.Repeat(unit, 1000)) + close + "<c/></row><after/></s></w>";
		var bytes = Utf8(xml);
		var expected = RowExpected(xml, "");

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = OpenFixed(bytes, mode);
			var size = scanner.Window.Capacity;
			await ReadTokensAsync(scanner, mode, 3);
			await SkipByAsync(scanner, way, "row", 2, mode, Math.Max("row".Length + 2, 8));
			Assert.Equal(size, scanner.Window.Capacity);
			Assert.Equal(expected, await DescribeRestAsync(scanner, mode));
		});

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = OpenFixed(bytes, mode);
			var size = scanner.Window.Capacity;
			await ReadTokensAsync(scanner, mode, 3);
			if (!tokenSkipThrows)
			{
				await scanner.SkipAsync(2, mode);
				Assert.Equal(expected, await DescribeRestAsync(scanner, mode));

				return;
			}

			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.SkipAsync(2, mode));
			AssertError(e, Before.Length, $"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({size} bytes)");
		});
	}

	/// <summary>A nested start tag of the name is read as a token, which has to fit the window, as the tokenizer needs.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task ReadsANestedStartTagOfTheNameAsATokenThatMustFit()
	{
		var xml = "<w><s><row r=\"1\"><row p=\"" + new string('x', 100) + "\"/></row><after/></s></w>";
		var bytes = Utf8(xml);
		var offset = xml.IndexOf("<row p=", StringComparison.Ordinal);

		await InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = OpenFixed(bytes, mode);
			var size = scanner.Window.Capacity;
			await ReadTokensAsync(scanner, mode, 3);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => SkipByAsync(scanner, way, "row", 2, mode));
			AssertError(e, offset, $"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({size} bytes)");
		});
	}

	/// <summary>Finds the name, markup and markup ends when the window ends at any byte of them: a row skip.</summary>
	/// <returns>A task that completes when every split has passed.</returns>
	[Fact]
	public Task FindsWhatTheWindowSplitsInARow()
	{
		var xml = "<w><s><row r=\"1\"><c>arrow!?</c><!-- </row> --><rows/><![CDATA[</row>]]><?pi </row> ?><row/><row r=\"2\"></row ><x:row></x:row></row><after/></s></w>";

		return SplitsEverywhereAsync(xml, "row", 3, 2, RowExpected(xml, ""));
	}

	/// <summary>Finds the name, markup and markup ends when the window ends at any byte of them: a prefixed row skip.</summary>
	/// <returns>A task that completes when every split has passed.</returns>
	[Fact]
	public Task FindsWhatTheWindowSplitsInAPrefixedRow()
	{
		var xml = "<x:w><x:s><x:row r=\"1\"><row></row><!-- </x:row> --><xx:row/><x:row/><x:row></x:row\t><?x </x:row>?></x:row><x:after/></x:s></x:w>";

		return SplitsEverywhereAsync(xml, "x:row", 3, 2, RowExpected(xml, "x:"));
	}

	/// <summary>Finds the name, markup and markup ends when the window ends at any byte of them: a prefixed <c>sheetData</c> skip.</summary>
	/// <returns>A task that completes when every split has passed.</returns>
	[Fact]
	public Task FindsWhatTheWindowSplitsInSheetData()
	{
		var xml = "<x:worksheet><x:sheetData><x:row r=\"1\"><x:c><x:v>1</x:v></x:c></x:row><!-- </x:sheetData> --><![CDATA[</x:sheetData>]]><?x </x:sheetData>?></x:sheetData ><x:after/></x:worksheet>";
		string[] expected =
		[
			"EndTag 1 x:sheetData",
			$"at {xml.IndexOf("<x:after/>", StringComparison.Ordinal)}",
			"StartTag 1 x:after empty",
			"EndTag 0 x:worksheet",
		];

		return SplitsEverywhereAsync(xml, "x:sheetData", 2, 1, expected);
	}

	/// <summary>Checks its arguments before it skips anything: the depth can't be negative, and the name can't be empty.</summary>
	/// <returns>A task that completes when the checks have passed.</returns>
	[Fact]
	public async Task ChecksItsArguments()
	{
		var xml = Utf8("<w><row r=\"1\"><c/></row><after/></w>");

		foreach (var mode in new[] { ScanMode.All[0], ScanMode.All[1] })
		{
			using var scanner = XmlScan.Open(xml, mode);
			await ReadTokensAsync(scanner, mode, 2);
			var position = scanner.Position;

			var negative = Assert.Throws<ArgumentOutOfRangeException>(() => scanner.TrySkipRaw("row"u8, -1));
			Assert.Equal("targetDepth", negative.ParamName);
			Assert.Throws<ArgumentOutOfRangeException>(() => scanner.SkipRaw("row"u8, -1));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => scanner.SkipRawAsync(Utf8("row"), -1, CancellationToken.None).AsTask());

			// Inside the element, and where nothing would be skipped.
			var empty = Assert.Throws<ArgumentException>(() => scanner.TrySkipRaw(ReadOnlySpan<byte>.Empty, 1));
			Assert.Equal("qualifiedName", empty.ParamName);
			Assert.Throws<ArgumentException>(() => scanner.TrySkipRaw(ReadOnlySpan<byte>.Empty, 2));
			Assert.Throws<ArgumentException>(() => scanner.SkipRaw(ReadOnlySpan<byte>.Empty, 1));
			await Assert.ThrowsAsync<ArgumentException>(() => scanner.SkipRawAsync(ReadOnlyMemory<byte>.Empty, 1, CancellationToken.None).AsTask());

			// Nothing was skipped, and a skip still works.
			Assert.Equal(position, scanner.Position);
			Assert.Equal(2, scanner.Depth);
			await SkipByAsync(scanner, SkipWay.Raw, "row", 1, mode);
			Assert.Equal("EndTag 1 row", Describe(scanner));
		}
	}

	/// <summary>Ends where the token skip ends, with the same tokens after it, in seeded random rows, from any point in a row.</summary>
	/// <returns>A task that completes when every seed has passed.</returns>
	[Fact]
	public async Task EndsWhereTheTokenSkipEnds()
	{
		for (var seed = 0; seed < 200; seed++)
		{
			var random = new Random(seed);
			var xml = Document(random);
			var bytes = Utf8(xml);
			var starts = SkipStarts(bytes);
			Assert.NotEmpty(starts);
			var (token, name) = starts[random.Next(starts.Count)];
			var way = seed % 2 == 0 ? SkipWay.Raw : SkipWay.RawLoop;

			try
			{
				await XmlScan.InEveryModeAsync(async mode =>
				{
					using var tokens = XmlScan.Open(bytes, mode);
					await ReadTokensAsync(tokens, mode, token);
					await SkipByAsync(tokens, SkipWay.Tokens, name, 2, mode);
					var expected = await DescribeRestAsync(tokens, mode);

					using var raw = XmlScan.Open(bytes, mode);
					await ReadTokensAsync(raw, mode, token);
					await SkipByAsync(raw, way, name, 2, mode);
					var actual = await DescribeRestAsync(raw, mode);

					if (!expected.SequenceEqual(actual))
					{
						Assert.Fail($"expected{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}but got{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
					}
				});
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"Seed {seed}, skipping {name} ({way}) after token {token} of{Environment.NewLine}{xml}{Environment.NewLine}{e.Message}", e);
			}
		}
	}

	/// <summary>The qualified name is a start or end tag's name with its prefix, and empty for other tokens.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task QualifiedNameIsATagsNameWithItsPrefix()
	{
		var xml = Utf8("<?xml version=\"1.0\"?><x:w xmlns:x=\"urn:x\"><x:c r=\"A1\">t<![CDATA[d]]></x:c><c/><x:c\r\n\tr=\"B1\" /><c></c ><x:v/><!-- c --></x:w>");
		string[] expected =
		[
			"StartTag 'x:w' 'w'",
			"StartTag 'x:c' 'c'",
			"Text '' ''",
			"CData '' ''",
			"EndTag 'x:c' 'c'",
			"StartTag 'c' 'c'",
			"StartTag 'x:c' 'c'",
			"StartTag 'c' 'c'",
			"EndTag 'c' 'c'",
			"StartTag 'x:v' 'v'",
			"EndTag 'x:w' 'w'",
		];

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			Assert.Equal(XmlTokenKind.None, scanner.Kind);
			Assert.True(scanner.QualifiedName.IsEmpty);
			var names = new List<string>();
			while (await scanner.NextAsync(mode))
			{
				names.Add($"{scanner.Kind} '{Decode(scanner.QualifiedName)}' '{Decode(scanner.LocalName)}'");
			}

			Assert.Equal(XmlTokenKind.End, scanner.Kind);
			Assert.True(scanner.QualifiedName.IsEmpty);
			Assert.Equal(expected, names);
		});
	}

	/// <summary>
	/// Skips an element raw from its start tag, by its qualified name, to where the token skip ends; nothing for an empty
	/// element. The name outlives the refills of a long element.
	/// </summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task SkipsAnElementRawByItsQualifiedName()
	{
		var rows = string.Concat(Enumerable.Range(1, 20).Select(r => $"<x:row r=\"{r}\"><x:c r=\"A{r}\"><x:v>{r}</x:v></x:c></x:row>"));
		var xml = "<x:worksheet xmlns:x=\"urn:x\"><x:sheetData>" + rows + "<!-- </x:sheetData> --></x:sheetData ><x:mergeCells count=\"1\"><x:mergeCell ref=\"A1:B2\"/></x:mergeCells></x:worksheet>";
		var bytes = Utf8(xml);
		string[] expected =
		[
			"EndTag 1 x:sheetData",
			$"at {xml.IndexOf("<x:mergeCells", StringComparison.Ordinal)}",
			"StartTag 2 x:mergeCells",
			"StartTag 2 x:mergeCell empty",
			"EndTag 1 x:mergeCells",
			"EndTag 0 x:worksheet",
		];
		var emptyXml = "<x:worksheet><x:sheetData/><x:mergeCells/></x:worksheet>";
		var emptyBytes = Utf8(emptyXml);
		string[] emptyExpected =
		[
			"StartTag 1 x:sheetData empty",
			$"at {emptyXml.IndexOf("<x:mergeCells", StringComparison.Ordinal)}",
			"StartTag 1 x:mergeCells empty",
			"EndTag 0 x:worksheet",
		];

		await XmlScan.InEveryModeAsync(async mode =>
		{
			foreach (var raw in new[] { true, false })
			{
				using var scanner = XmlScan.Open(bytes, mode);
				await ReadTokensAsync(scanner, mode, 2);
				Assert.True(scanner.IsStartTag("sheetData"u8));
				await (raw ? scanner.SkipElementRawAsync(mode.Async, CancellationToken.None) : scanner.SkipElementAsync(mode.Async, CancellationToken.None));
				Assert.Equal(expected, await DescribeRestAsync(scanner, mode));

				using var empty = XmlScan.Open(emptyBytes, mode);
				await ReadTokensAsync(empty, mode, 2);
				await (raw ? empty.SkipElementRawAsync(mode.Async, CancellationToken.None) : empty.SkipElementAsync(mode.Async, CancellationToken.None));
				Assert.Equal(emptyExpected, await DescribeRestAsync(empty, mode));
			}
		});
	}

	/// <summary>Raw skips are on by default; the scanner doesn't read the setting, so its own raw skip works either way.</summary>
	/// <returns>A task that completes when the checks have passed.</returns>
	[Fact]
	public async Task RawSkipsAreOnByDefaultAndTheScannerDoesntReadIt()
	{
		var mode = ScanMode.All[0];
		using var scanner = XmlScan.Open(Utf8("<w><s><row r=\"1\"><!DOCTYPE x></row><after/></s></w>"), mode);

		Assert.True(scanner.RawSkips);
		scanner.RawSkips = false;
		Assert.False(scanner.RawSkips);
		await ReadTokensAsync(scanner, mode, 3);
		await SkipByAsync(scanner, SkipWay.Raw, "row", 2, mode);
		Assert.Equal("EndTag 2 row", Describe(scanner));
	}

	/// <summary>
	/// Checks, in every mode and both ways of skipping raw, that a skip of the row after <paramref name="tokens"/> tokens
	/// stops after the row's end tag, before the <c>after</c> element that follows it, and that the rest of the part reads.
	/// </summary>
	/// <param name="xml">The part: a row in <c>w</c> and <c>s</c>, followed by an empty <c>after</c> and the end tags.</param>
	/// <param name="prefix">The prefix of the part's elements, and so of the row's name.</param>
	/// <param name="tokens">The number of tokens to read before the skip: 3 is the row's start tag.</param>
	/// <param name="depth">The depth expected after them, if it is checked.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	private static Task SkipsRowAsync(string xml, string prefix, int tokens = 3, int? depth = null)
	{
		var bytes = Utf8(xml);
		var expected = RowExpected(xml, prefix);

		return InEveryWayAsync(async (mode, way) =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			await ReadTokensAsync(scanner, mode, tokens);
			if (depth is not null)
			{
				Assert.Equal(depth.Value, scanner.Depth);
			}

			await SkipByAsync(scanner, way, prefix + "row", 2, mode);
			Assert.Equal(expected, await DescribeRestAsync(scanner, mode));
		});
	}

	/// <summary>What <see cref="DescribeRestAsync"/> gives after a row's skip in the parts of <see cref="SkipsRowAsync"/>.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="prefix">The prefix of the part's elements.</param>
	/// <returns>The descriptions.</returns>
	private static string[] RowExpected(string xml, string prefix) =>
	[
		$"EndTag 2 {prefix}row",
		$"at {xml.LastIndexOf($"<{prefix}after/>", StringComparison.Ordinal)}",
		$"StartTag 2 {prefix}after empty",
		$"EndTag 1 {prefix}s",
		$"EndTag 0 {prefix}w",
	];

	/// <summary>
	/// Skips raw with the window ending at each byte of a part once: over a stream whose reads stop at the byte, so that
	/// each needle, markup start and markup end is split at each of its bytes. Sync and async, and both ways of skipping raw.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="name">The name to skip.</param>
	/// <param name="tokens">The number of tokens to read before the skip.</param>
	/// <param name="targetDepth">The depth to skip to.</param>
	/// <param name="expected">What <see cref="DescribeRestAsync"/> gives after the skip.</param>
	/// <returns>A task that completes when every split has passed.</returns>
	private static async Task SplitsEverywhereAsync(string xml, string name, int tokens, int targetDepth, string[] expected)
	{
		var bytes = Utf8(xml);
		for (var split = 0; split <= bytes.Length; split++)
		{
			foreach (var async in new[] { false, true })
			{
				var mode = new ScanMode(async ? "split, async" : "split", false, XmlWindow.DefaultSize, async);
				foreach (var way in new[] { SkipWay.Raw, SkipWay.RawLoop })
				{
					using var scanner = new XmlScanner(new XmlWindow(new SplitStream(bytes, split), XmlScan.PartName, 1 << 20));
					try
					{
						await ReadTokensAsync(scanner, mode, tokens);
						await SkipByAsync(scanner, way, name, targetDepth, mode);
						Assert.Equal(expected, await DescribeRestAsync(scanner, mode));
					}
					catch (Exception e)
					{
						throw new Xunit.Sdk.XunitException($"Split at {split} ({xml.Substring(0, split)} | {xml.Substring(split)}), {mode}, {way}: {e.Message}", e);
					}
				}
			}
		}
	}

	/// <summary>Runs <paramref name="test"/> in every mode and both ways of skipping raw, and says which failed.</summary>
	/// <param name="test">The test, for one mode and way.</param>
	/// <returns>A task that completes when every mode and way has passed.</returns>
	private static async Task InEveryWayAsync(Func<ScanMode, SkipWay, Task> test)
	{
		foreach (var way in new[] { SkipWay.Raw, SkipWay.RawLoop })
		{
			try
			{
				await XmlScan.InEveryModeAsync(mode => test(mode, way));
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"Skipping {way}: {e.Message}", e);
			}
		}
	}

	/// <summary>Skips to a depth as <paramref name="way"/> and <paramref name="mode"/> say.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="way">How to skip.</param>
	/// <param name="name">The qualified name of the element skipped, for a raw skip.</param>
	/// <param name="targetDepth">The depth to skip to.</param>
	/// <param name="mode">How to read: sync or async.</param>
	/// <param name="keptAtMost">For <see cref="SkipWay.RawLoop"/>, the most bytes a refill may keep: the bytes from <see cref="XmlScanner.Position"/> on.</param>
	/// <returns>A task that completes at the depth.</returns>
	private static async Task SkipByAsync(XmlScanner scanner, SkipWay way, string name, int targetDepth, ScanMode mode, int keptAtMost = int.MaxValue)
	{
		var bytes = Utf8(name);
		if (way == SkipWay.Tokens)
		{
			await scanner.SkipAsync(targetDepth, mode);

			return;
		}

		if (way == SkipWay.Raw)
		{
			if (mode.Async)
			{
				await scanner.SkipRawAsync(bytes, targetDepth, CancellationToken.None);
			}
			else
			{
				scanner.SkipRaw(bytes, targetDepth);
			}

			return;
		}

		while (!scanner.TrySkipRaw(bytes, targetDepth))
		{
			var kept = scanner.Window.End - scanner.Position;
			if (kept > keptAtMost)
			{
				Assert.Fail($"The skip keeps {kept} bytes for the refill, at offset {scanner.Window.Offset(scanner.Position)}; at most {keptAtMost} were expected.");
			}

			if (mode.Async)
			{
				await scanner.RefillAsync(CancellationToken.None);
			}
			else
			{
				scanner.Refill();
			}
		}
	}

	/// <summary>Reads tokens, none of them the end of the part.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="mode">How to read.</param>
	/// <param name="count">The number of tokens.</param>
	/// <returns>A task that completes after the tokens.</returns>
	private static async Task ReadTokensAsync(XmlScanner scanner, ScanMode mode, int count)
	{
		for (var i = 0; i < count; i++)
		{
			Assert.True(await scanner.NextAsync(mode));
		}
	}

	/// <summary>Describes the scanner's token, the offset of the next one, and every token after it to the end of the part.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="mode">How to read.</param>
	/// <returns>The descriptions.</returns>
	private static async Task<List<string>> DescribeRestAsync(XmlScanner scanner, ScanMode mode)
	{
		var described = new List<string> { Describe(scanner), $"at {scanner.Window.Offset(scanner.Position)}" };
		while (await scanner.NextAsync(mode))
		{
			described.Add(Describe(scanner));
		}

		return described;
	}

	/// <summary>Opens a scanner over a window that can't grow: its size limit is its starting size.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it: 16 bytes for the 16-byte modes, <see cref="FixedSize"/> for the others.</param>
	/// <returns>The scanner.</returns>
	private static XmlScanner OpenFixed(byte[] xml, ScanMode mode)
	{
		var size = Math.Min(mode.WindowSize, FixedSize);
		Stream stream = mode.Trickle ? new TrickleStream(xml) : new MemoryStream(xml);

		return new XmlScanner(new XmlWindow(stream, XmlScan.PartName, size, size));
	}

	/// <summary>
	/// Finds where a skip of a row to depth 2 can start in a part, and so ends where the token skip does: at each row's start
	/// tag, and after each token inside a row that isn't inside a nested element of the row's name (whose end tag a raw
	/// skip, which counts from the row, would take for the row's).
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <returns>The number of tokens to read before each skip, and the row's qualified name.</returns>
	private static List<(int Token, string Name)> SkipStarts(byte[] xml)
	{
		var starts = new List<(int Token, string Name)>();
		using var scanner = XmlScan.Open(xml, ScanMode.All[0]);
		var token = 0;
		string? row = null;
		var nested = 0;
		while (scanner.Read())
		{
			token++;
			var name = Decode(scanner.QualifiedName);
			if (row is null)
			{
				if (scanner.Kind == XmlTokenKind.StartTag && scanner.ElementDepth == 3)
				{
					starts.Add((token, name));
					if (!scanner.IsEmptyElement)
					{
						row = name;
						nested = 0;
					}
				}

				continue;
			}

			if (scanner.Depth < 3)
			{
				// The row's end tag.
				row = null;

				continue;
			}

			if (scanner.Kind == XmlTokenKind.StartTag && !scanner.IsEmptyElement && name == row)
			{
				nested++;
			}
			else if (scanner.Kind == XmlTokenKind.EndTag && name == row && nested > 0)
			{
				nested--;
			}

			if (nested == 0)
			{
				starts.Add((token, row));
			}
		}

		return starts;
	}

	/// <summary>Writes a random part: a worksheet whose <c>sheetData</c> has random rows, prefixed or not, followed by an empty element.</summary>
	/// <param name="random">The source of the part's choices.</param>
	/// <returns>The part.</returns>
	private static string Document(Random random)
	{
		var prefix = Pick(random, Prefixes);
		var builder = new StringBuilder();
		if (random.Next(2) == 0)
		{
			builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
		}

		builder.Append('<').Append(prefix).Append("worksheet xmlns=\"urn:main\" xmlns:x=\"urn:x\" xmlns:xx=\"urn:xx\"><").Append(prefix).Append("sheetData>");
		var rows = random.Next(1, 5);
		for (var row = 1; row <= rows; row++)
		{
			Between(random, builder);
			Element(random, builder, prefix + "row", $" r=\"{row}\"", 0);
		}

		Between(random, builder);
		builder.Append("</").Append(prefix).Append("sheetData><").Append(prefix).Append("after/></").Append(prefix).Append("worksheet>");

		return builder.ToString();
	}

	/// <summary>Writes a random element: attributes in quotes of both kinds, and text, markup and elements in it.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="builder">Where the element goes.</param>
	/// <param name="name">The element's qualified name.</param>
	/// <param name="attributes">Attributes it always has, such as a row's <c>r</c>, with the space before them.</param>
	/// <param name="depth">The depth of the element below the row: 0 for the row.</param>
	private static void Element(Random random, StringBuilder builder, string name, string attributes, int depth)
	{
		builder.Append('<').Append(name).Append(attributes);
		foreach (var attribute in AttributeNames.OrderBy(_ => random.Next()).Take(random.Next(3)))
		{
			var quote = random.Next(2) == 0 ? '"' : '\'';
			builder.Append(Pick(random, Spaces)).Append(attribute).Append('=').Append(quote);
			for (var i = random.Next(4); i > 0; i--)
			{
				var piece = Pick(random, ValuePieces);
				builder.Append(piece == quote.ToString() ? (quote == '"' ? "&quot;" : "&apos;") : piece);
			}

			builder.Append(quote);
		}

		if (random.Next(5) == 0)
		{
			builder.Append(random.Next(2) == 0 ? "/>" : " />");

			return;
		}

		builder.Append(random.Next(5) == 0 ? Pick(random, Spaces) + ">" : ">");
		for (var count = random.Next(6); count > 0; count--)
		{
			switch (random.Next(depth < MaxDepth ? 5 : 3))
			{
				case 0:
					for (var i = random.Next(1, 4); i > 0; i--)
					{
						builder.Append(Pick(random, TextPieces));
					}

					break;
				case 1:
					builder.Append(Pick(random, MarkupPieces));

					break;
				case 2:
					builder.Append(Pick(random, Spaces));

					break;
				default:
					Element(random, builder, Pick(random, ElementNames), "", depth + 1);

					break;
			}
		}

		builder.Append("</").Append(name).Append(random.Next(5) == 0 ? Pick(random, Spaces) + ">" : ">");
	}

	/// <summary>Writes what can come between rows: white space, comments, CDATA sections and processing instructions.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="builder">Where it goes.</param>
	private static void Between(Random random, StringBuilder builder)
	{
		for (var count = random.Next(3); count > 0; count--)
		{
			builder.Append(random.Next(2) == 0 ? Pick(random, Spaces) : Pick(random, MarkupPieces));
		}
	}

	/// <summary>Picks one of <paramref name="items"/>.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="items">The items.</param>
	/// <returns>The item picked.</returns>
	private static string Pick(Random random, string[] items) => items[random.Next(items.Length)];

	/// <summary>Checks an error of the XML: its offset and detail (<see cref="XmlError.TryGet"/>), and its message.</summary>
	/// <param name="exception">The error.</param>
	/// <param name="offset">The byte offset expected.</param>
	/// <param name="detail">What is wrong, as expected.</param>
	private static void AssertError(InvalidDataException exception, long offset, string detail)
	{
		Assert.True(XmlError.TryGet(exception, out var actualOffset, out var actualDetail), exception.Message);
		Assert.Equal(detail, actualDetail);
		Assert.Equal(offset, actualOffset);
		Assert.Equal(Message(offset, detail), exception.Message);
	}

	/// <summary>Describes the scanner's token: its kind, the depth after it, and its qualified name or raw content.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <returns>The description.</returns>
	private static string Describe(XmlScanner scanner) => scanner.Kind switch
	{
		XmlTokenKind.StartTag => $"StartTag {scanner.Depth} {Decode(scanner.QualifiedName)}{(scanner.IsEmptyElement ? " empty" : "")}",
		XmlTokenKind.EndTag => $"EndTag {scanner.Depth} {Decode(scanner.QualifiedName)}",
		XmlTokenKind.Text or XmlTokenKind.CData => $"{scanner.Kind} {scanner.Depth} '{Decode(scanner.Content)}'",
		_ => $"{scanner.Kind} {scanner.Depth}",
	};

	/// <summary>Decodes UTF-8 bytes.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The string.</returns>
	private static string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray());

	/// <summary>The message of an error in the tests' part.</summary>
	/// <param name="offset">The byte offset.</param>
	/// <param name="detail">What is wrong.</param>
	/// <returns>The message.</returns>
	private static string Message(long offset, string detail) => $"The XML of {XmlScan.PartName} can't be read at byte {offset}: {detail}.";

	/// <summary>The UTF-8 bytes of a string.</summary>
	/// <param name="text">The string.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

	/// <summary>
	/// A read-only stream over bytes whose reads stop at an offset: a read before it returns no bytes past it, so a window
	/// that reads all it can ends there once, and what is there is split between two reads.
	/// </summary>
	/// <param name="data">The bytes of the stream.</param>
	/// <param name="split">The offset no read crosses.</param>
	private sealed class SplitStream(byte[] data, int split) : Stream
	{
		private int position;

		/// <inheritdoc/>
		public override bool CanRead => true;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => false;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

		/// <inheritdoc/>
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
			Task.FromResult(Take(buffer.AsSpan(offset, count)));

#if NET
		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Take(buffer.Span));
#endif

		/// <inheritdoc/>
		public override void Flush()
		{
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <summary>Copies the next bytes into <paramref name="destination"/>, up to the split while it is ahead.</summary>
		/// <param name="destination">Where the bytes go.</param>
		/// <returns>The number of bytes copied: 0 at the end.</returns>
		private int Take(Span<byte> destination)
		{
			var limit = position < split ? split : data.Length;
			var count = Math.Min(destination.Length, limit - position);
			data.AsSpan(position, count).CopyTo(destination);
			position += count;

			return count;
		}
	}
}
