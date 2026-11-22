using System.Buffers.Text;
using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>
/// The attributes of start tags (<see cref="XmlAttributes"/>), which read the first <see cref="XmlScanner.RecordedValues"/>
/// values from the quotes the tokenizer recorded and the rest from the bytes: against the walk from the bytes alone that
/// every attribute took before the quotes were recorded (<see cref="Walk"/>, a copy of it), for well-formed and malformed
/// tags, in every <see cref="ScanMode"/>; their errors only when they are enumerated; their end at the scanner's next
/// start tag or refill; <see cref="XmlAttributes.GetInt32"/>'s plain digits; and <see cref="XmlScanner.SetPosition"/>.
/// </summary>
public class XmlAttributesTests
{
	private const string NoNameOrValue = "an attribute has no name, or no value";
	private const string NoQuotes = "an attribute value has no quotes";
	private const string LessThanInValue = "an attribute value has '<', which XML doesn't allow";

	private static readonly string[] Spaces = ["", " ", " ", " ", "  ", "\t", "\n", "\r\n", " \t "];
	private static readonly string[] Around = ["", "", "", "", " ", "\t", "\r\n "];

	private static readonly string[] Names =
	[
		"a", "r", "s", "t", "ref", "x:y", "r:id", "xml:space", "xmlns", "xmlns:x", "xmlnsx", "x:", ":x", "a.b-c_d",
		"abcdefghijklmnopqrstuvwxyz",
	];

	private static readonly string[] Values =
	[
		"", "1", "A1", "inlineStr", "x>y", "a b", " 1 ", "&amp;", "&#49;", "&#x20AC;", "&bad;", "&", "/", "=", "/>", ">",
		"\t\r\n",
	];

	// Attributes whose quotes the tokenizer pairs otherwise than a name, '=' and a value would, and other oddities.
	private static readonly string[] Oddities =
	[
		" x'=' <''", " x\"=\" <\"\"", " x'y'=\"v\"", " x\"y\"='v'", " b\"='y'\"", " 'c'", " b='1'c='2'", " =\"1\"", " b",
		" b=1", " x'='a'", "\t\"q\"=\"v\"",
	];

	private static readonly char[] Inserted = ['"', '\'', '=', ' ', '<', '\t', '\n', 'a', '/', '>', ':', '&'];

	/// <summary>The numbers of attributes <see cref="ReadsTheAttributesOfTagsOfAnyLength"/> reads, around the recorded 16, each with and without namespace declarations.</summary>
	public static TheoryData<int, bool> Lengths { get; } = new()
	{
		{ 0, false },
		{ 0, true },
		{ 1, false },
		{ 1, true },
		{ 2, false },
		{ 2, true },
		{ 15, false },
		{ 15, true },
		{ 16, false },
		{ 16, true },
		{ 17, false },
		{ 17, true },
		{ 40, false },
		{ 40, true },
	};

	/// <summary>Reads a tag's attributes, recorded and past the recorded, as written and as the walk from the bytes reads them, wherever the window cuts the tag.</summary>
	/// <param name="count">The number of attributes.</param>
	/// <param name="declarations">Whether namespace declarations are among them.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[MemberData(nameof(Lengths))]
	public async Task ReadsTheAttributesOfTagsOfAnyLength(int count, bool declarations)
	{
		// Prefixes, both quotes, '>' and the other quote in values, references, white space around '=', and namespace
		// declarations (which take recorded quotes, but are no attributes) first, in the middle and last.
		var written = new List<(string Xml, string Qualified, string Local, string Raw, string Decoded)>();
		for (var i = 0; i < count; i++)
		{
			if (declarations && i == count / 2)
			{
				written.Add((" xmlns:x=\"urn:x\"", "xmlns:x", "", "", ""));
			}

			written.Add(Attribute(i));
		}

		if (declarations)
		{
			written.Insert(0, ("\r\n\txmlns=\"urn:main\"", "xmlns", "", "", ""));
			written.Add((" xmlns:r='urn:r'", "xmlns:r", "", "", ""));
		}

		var tag = "<a" + string.Concat(written.Select(a => a.Xml)) + (count % 2 == 0 ? "/>" : "></a>");
		var expected = written.Where(a => !a.Qualified.StartsWith("xmlns", StringComparison.Ordinal)).Select(a => $"{a.Qualified} {a.Local}=[{a.Raw}] {a.Decoded}").ToList();

		await XmlScan.InEveryModeAsync(async mode =>
		{
			// The tag at different places in the window, so that the window's end cuts its values at every place.
			foreach (var padding in new[] { 0, 1, 5, 15, 16, 17 })
			{
				var before = "<r>" + new string(' ', padding);
				var xml = Utf8(before + tag + "</r>");
				using var scanner = XmlScan.Open(xml, mode);
				while (await scanner.NextAsync(mode) && !scanner.IsStartTag("a"u8))
				{
				}

				Assert.True(scanner.IsStartTag("a"u8));
				AssertRecorded(scanner, written.Count);
				var walked = Walk(xml, before.Length + 2, tag.Length - 2 - (count % 2 == 0 ? 2 : 5), out var qualified);
				var read = Read(scanner, qualified);
				Assert.Equal(expected, walked);
				Assert.Equal(expected, read);
			}
		});
	}

	/// <summary>The differential test: random tags, well formed and malformed, read as the walk from the bytes alone reads them.</summary>
	/// <returns>A task that completes when every tag has passed.</returns>
	[Fact]
	public async Task ReadsWhatTheWalkFromTheBytesReads()
	{
		var mode = ScanMode.All[0];
		var outcomes = new Dictionary<string, int>();
		for (var seed = 0; seed < 20_000; seed++)
		{
			var xml = RandomTag(new Random(seed));
			var outcome = await CompareAsync(xml, mode, seed);
			outcomes[outcome] = outcomes.TryGetValue(outcome, out var n) ? n + 1 : 1;
		}

		// Every outcome comes up often: tags that read whole, with recorded and unrecorded values, each error, and tags the
		// tokenizer refuses (which it did before too, and which have no attributes to compare).
		var summary = string.Join(", ", outcomes.OrderBy(o => o.Key, StringComparer.Ordinal).Select(o => $"{o.Key}: {o.Value}"));
		foreach (var outcome in new[] { "read", "read past recorded", NoNameOrValue, NoQuotes, LessThanInValue, "value not decoded", "tag refused" })
		{
			Assert.True(outcomes.TryGetValue(outcome, out var count) && count >= 200, $"Too few '{outcome}': {summary}.");
		}
	}

	/// <summary>The differential test in every <see cref="ScanMode"/>, so that refills come everywhere in the tags.</summary>
	/// <returns>A task that completes when every tag has passed in every mode.</returns>
	[Fact]
	public async Task ReadsWhatTheWalkFromTheBytesReadsInEveryMode()
	{
		for (var seed = 100_000; seed < 100_150; seed++)
		{
			var xml = RandomTag(new Random(seed));
			await XmlScan.InEveryModeAsync(async mode => await CompareAsync(xml, mode, seed));
		}
	}

	/// <summary>Reads tags whose quotes the tokenizer pairs otherwise than the attributes do, from their bytes, as before the quotes were recorded.</summary>
	/// <param name="xml">The tag, <c>&lt;a</c> to <c>/&gt;</c>.</param>
	/// <param name="expected">The attributes, and the error, as <see cref="Read"/> gives them.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData("<a x'=' <''/>", "error 8: " + LessThanInValue)]
	[InlineData("<a x\"=\" <\"\"/>", "error 8: " + LessThanInValue)]
	[InlineData("<a x'y'=\"v\"/>", "x'y' x'y'=[v] v")]
	[InlineData("<a b='1' x'y'=\"v\" c=\"2\"/>", "b b=[1] 1", "x'y' x'y'=[v] v", "c c=[2] 2")]
	[InlineData("<a b='1' x\"='y'\"/>", "b b=[1] 1", "x\" x\"=[y] y", "error 15: " + NoNameOrValue)]
	[InlineData("<a b='1'c=\"2\"/>", "b b=[1] 1", "c c=[2] 2")]
	[InlineData("<a b='1' 'c'/>", "b b=[1] 1", "error 9: " + NoNameOrValue)]
	public async Task ReadsAMalformedTagWhoseQuotesPairOtherwiseFromItsBytes(string xml, params string[] expected)
	{
		// The tokenizer pairs quotes left to right, which in these tags isn't how a name, '=' and a value pair them: the
		// recorded quotes aren't the values', and the attributes read them from the bytes, as before.
		var bytes = Utf8(xml);

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(bytes, mode);
			Assert.True(await scanner.NextAsync(mode));
			Assert.True(scanner.IsStartTag("a"u8));
			var walked = Walk(bytes, 2, xml.Length - 4, out var qualified);
			Assert.Equal(expected, walked);
			Assert.Equal(expected, Read(scanner, qualified));
		});
	}

	/// <summary>Finds each error of an attribute, at its offset, after attributes that read, recorded or not.</summary>
	/// <param name="before">The attributes that read before the one in error.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(17)]
	[InlineData(30)]
	public async Task FindsAnErrorAfterAnyNumberOfAttributes(int before)
	{
		var good = string.Concat(Enumerable.Range(0, before).Select(i => $" a{i}=\"{i}\""));
		var cases = new (string Bad, int At, string Detail)[]
		{
			(" b c=\"1\"", 1, NoNameOrValue),
			(" =\"1\"", 1, NoNameOrValue),
			(" b\"1\"", 1, NoNameOrValue),
			(" b=1 c=\"2\"", 1, NoQuotes),
			(" b= c=\"2\"", 1, NoQuotes),
			(" x'=' <''", 6, LessThanInValue),
		};

		await XmlScan.InEveryModeAsync(async mode =>
		{
			foreach (var (bad, at, detail) in cases)
			{
				var xml = "<a" + good + bad + "/>";
				using var scanner = XmlScan.Open(Utf8(xml), mode);
				Assert.True(await scanner.NextAsync(mode));
				var attributes = scanner.Attributes;
				for (var i = 0; i < before; i++)
				{
					Assert.True(attributes.MoveNext());
					Assert.Equal($"{i}", Text(attributes.Value));
				}

				var e = Assert.Throws<InvalidDataException>(() => attributes.MoveNext());
				AssertError(e, 2 + good.Length + at, detail);
			}
		});
	}

	/// <summary>Malformed attributes of tags that are never enumerated read fine; enumerated, they throw.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task LeavesTheErrorsOfAttributesToTheirEnumeration()
	{
		var xml = Utf8("<r><a b c=1 d='2'/><a =\"x\" g/><v>1</v></r>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			// Read without the attributes: nothing is wrong.
			Assert.Equal(["<r>", "<a>", "</a>", "<a>", "</a>", "<v>", "1", "</v>", "</r>"], await TagsAsync(xml, mode));

			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var e = Assert.Throws<InvalidDataException>(() => ReadAll(scanner.Attributes));
			AssertError(e, 6, NoNameOrValue);
			await scanner.NextAsync(mode);
			e = Assert.Throws<InvalidDataException>(() => ReadAll(scanner.Attributes));
			AssertError(e, 22, NoNameOrValue);
		});
	}

	/// <summary>Attributes read after the scanner read the next start tag throw <see cref="InvalidOperationException"/>.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task AttributesReadAfterTheNextStartTagThrow()
	{
		var xml = Utf8("<r><a b=\"1\" c=\"2\">text</a><d e=\"3\"/></r>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			Assert.True(attributes.MoveNext());
			Assert.Equal("b", Text(attributes.LocalName));
			while (await scanner.NextAsync(mode) && !scanner.IsStartTag("d"u8))
			{
			}

			var e = Assert.Throws<InvalidOperationException>(() => attributes.MoveNext());
			Assert.Equal("The attributes of a start tag were read after the scanner read another start tag or refilled: they are gone.", e.Message);
			// The new tag's are read.
			Assert.Equal(["e=3"], ReadAll(scanner.Attributes));
		});
	}

	/// <summary>Text, CDATA and end tags leave a start tag's attributes valid; the next start tag doesn't.</summary>
	/// <returns>A task that completes when the modes have passed.</returns>
	[Fact]
	public async Task AttributesOutliveTextAndEndTagsUntilTheNextStartTag()
	{
		// Whole reads with the default window hold all of it after the first tag, so no refill comes between.
		foreach (var mode in ScanMode.All.Where(m => !m.Trickle && m.WindowSize == XmlWindow.DefaultSize))
		{
			using var scanner = XmlScan.Open(Utf8("<r><a b=\"1\" c=\"2\">text<![CDATA[x]]></a><d/></r>"), mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			Assert.True(attributes.MoveNext());
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.True(attributes.MoveNext());
			Assert.Equal("c=2", $"{Text(attributes.LocalName)}={Text(attributes.Value)}");
			Assert.False(attributes.MoveNext());
			await scanner.NextAsync(mode);
			Assert.Throws<InvalidOperationException>(() => attributes.MoveNext());
		}
	}

	/// <summary>Attributes read after a refill, which moves the window's bytes, throw <see cref="InvalidOperationException"/>.</summary>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Fact]
	public async Task AttributesReadAfterARefillThrow()
	{
		foreach (var mode in ScanMode.All)
		{
			using var scanner = XmlScan.Open(Utf8("<r><a b=\"1\" c=\"2\"><x/></a></r>"), mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			if (mode.Async)
			{
				await scanner.RefillAsync(CancellationToken.None);
			}
			else
			{
				scanner.Refill();
			}

			Assert.Throws<InvalidOperationException>(() => attributes.MoveNext());
		}
	}

	/// <summary>A raw skip that reads a nested start tag of the element's name ends the attributes; one that reads none leaves them.</summary>
	[Fact]
	public void AttributesReadAfterARawSkipReadANestedTagOfTheNameThrow()
	{
		var mode = ScanMode.All[0];

		// The skip reads the nested start tag of the name as a token.
		using (var scanner = XmlScan.Open(Utf8("<r><a b=\"1\"><a c=\"2\"/></a><z/></r>"), mode))
		{
			scanner.Read();
			scanner.Read();
			var attributes = scanner.Attributes;
			scanner.SkipRaw(scanner.QualifiedName.ToArray(), 1);
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.Throws<InvalidOperationException>(() => attributes.MoveNext());
		}

		// A skip that reads no start tag leaves them.
		using (var scanner = XmlScan.Open(Utf8("<r><a b=\"1\"><x c=\"2\"/></a><z/></r>"), mode))
		{
			scanner.Read();
			scanner.Read();
			var attributes = scanner.Attributes;
			scanner.SkipRaw(scanner.QualifiedName.ToArray(), 1);
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.Equal(["b=1"], ReadAll(attributes));
		}
	}

	/// <summary>A start tag the window cuts short leaves no recorded quotes, and the tag before it, still current, reads its attributes from the bytes.</summary>
	[Fact]
	public void AStartTagTheWindowCutsShortLeavesTheAttributesOfTheTagBefore()
	{
		// The stream holds the next tag's first values but not its end: reading it records over the quotes, and returns
		// false before the tag is read, which leaves the tag before current and its attributes valid.
		using var scanner = XmlScan.Open(Utf8("<a b=\"1\" c='2'><d e=\"3\" f=\"4"), ScanMode.All[0]);
		scanner.Read();
		var attributes = scanner.Attributes;
		Assert.Equal(4, scanner.QuoteCount);
		Assert.False(scanner.TryRead());
		Assert.Equal(0, scanner.QuoteCount);
		Assert.True(scanner.IsStartTag("a"u8));
		Assert.Equal(["b=1", "c=2"], ReadAll(attributes));
		Assert.Equal(["b=1", "c=2"], ReadAll(scanner.Attributes));

		// Part of the way through, too.
		using var again = XmlScan.Open(Utf8("<a b=\"1\" c='2'><d e=\"3\" f=\"4"), ScanMode.All[0]);
		again.Read();
		attributes = again.Attributes;
		Assert.True(attributes.MoveNext());
		Assert.False(again.TryRead());
		Assert.True(attributes.MoveNext());
		Assert.Equal("c=2", $"{Text(attributes.LocalName)}={Text(attributes.Value)}");
		Assert.False(attributes.MoveNext());
	}

	/// <summary>The values <see cref="GetInt32ReadsWhatUtf8ParserReads"/> reads: plain digits, and everything that isn't.</summary>
	public static TheoryData<string> Integers { get; } =
	[
		"0", "7", "007", " 1 ", "\t\r\n123456789\n ", "000000001", "0000000001", "000000000000001", "123456789", "999999999",
		"1000000000", "2147483647", "2147483648", "-1", "+1", "-0", "-2147483648", "-2147483649", "1234567890", "",
		" ", "\t", "1 2", "&#49;", "1&#32;", "&#32;1", "&#x31;&#x32;", "1a", "a1", "a", "0x1", "1.0", "1e3", "&bad;", "1&", "&amp;",
		((char)0x0661).ToString(), (char)0x00A0 + "1", "1" + (char)0x3000,
	];

	/// <summary><see cref="XmlAttributes.GetInt32"/> gives the answers and errors it gave before plain digits were parsed where they are.</summary>
	/// <param name="value">The value, as it is in the XML.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	[Theory]
	[MemberData(nameof(Integers))]
	public async Task GetInt32ReadsWhatUtf8ParserReads(string value)
	{
		var xml = Utf8("<a n=\"" + value + "\"/>");
		var expected = OldGetInt32(value, 6);

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			Assert.True(attributes.MoveNext());
			string actual;
			try
			{
				actual = attributes.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
			catch (InvalidDataException e)
			{
				actual = ErrorText(e);
			}

			Assert.Equal(expected, actual);
		});
	}

	/// <summary><see cref="XmlScanner.SetPosition"/> leaves no token, keeps the depth and the attributes, and the next read reads from the position.</summary>
	/// <returns>A task that completes when the modes have passed.</returns>
	[Fact]
	public async Task SetPositionMovesPastBytesTheCallerRead()
	{
		const string Xml = "<r><a y=\"2\">text</a><b x=\"1\"/>< c/></r>";
		var xml = Utf8(Xml);

		// Whole reads with the default window: the caller reads its bytes in the window, which holds all of it.
		foreach (var mode in ScanMode.All.Where(m => !m.Trickle && m.WindowSize == XmlWindow.DefaultSize))
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			var text = scanner.Position;

			// Past text: no token, the position the token's start, and the depth as it was.
			scanner.SetPosition(text + 4);
			Assert.Equal(XmlTokenKind.None, scanner.Kind);
			Assert.Equal(text + 4, scanner.Position);
			Assert.Equal(text + 4, scanner.TokenStart);
			Assert.Equal(2, scanner.Depth);
			Assert.Equal(Message(Xml.IndexOf("</a>", StringComparison.Ordinal), "x"), scanner.Error("x").Message);
			Assert.False(scanner.Attributes.MoveNext());

			// No start tag was read: the tag's attributes are still there.
			Assert.Equal(["y=2"], ReadAll(attributes));
			Assert.True(await scanner.NextAsync(mode));
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.Equal(1, scanner.Depth);

			// Past a whole element, and to where it is: the next read reads from there.
			var element = scanner.Position;
			scanner.SetPosition(element);
			Assert.Equal(element, scanner.Position);
			scanner.SetPosition(element + "<b x=\"1\"/>".Length);
			Assert.Equal(1, scanner.Depth);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.NextAsync(mode));
			AssertError(e, Xml.IndexOf("< c/>", StringComparison.Ordinal), "'<' starts no tag");
		}
	}

	/// <summary>Writes an attribute of <see cref="ReadsTheAttributesOfTagsOfAnyLength"/>, of a kind by its index.</summary>
	/// <param name="i">The index.</param>
	/// <returns>Its XML, with the white space before it; its names; and its value, as it is and decoded.</returns>
	private static (string Xml, string Qualified, string Local, string Raw, string Decoded) Attribute(int i)
	{
		var qualified = (i % 4) switch
		{
			1 => $"x:a{i}",
			3 => $"r:id{i}",
			_ => $"a{i}",
		};
		var local = qualified.Substring(qualified.IndexOf(':') + 1);
		var quote = i % 2 == 0 ? '"' : '\'';
		var other = quote == '"' ? '\'' : '"';
		var (raw, decoded) = (i % 5) switch
		{
			0 => ($"v{i}", $"v{i}"),
			1 => ($"{i}>{other}/", $"{i}>{other}/"),
			2 => ("", ""),
			3 => ($"&amp;{i}&#x41;", $"&{i}A"),
			_ => ($"a\tb{i}", $"a b{i}"),
		};
		var space = (i % 3) switch
		{
			0 => " ",
			1 => "\t",
			_ => "\r\n  ",
		};
		var equals = i % 7 == 6 ? " =\t" : "=";

		return ($"{space}{qualified}{equals}{quote}{raw}{quote}", qualified, local, raw, decoded);
	}

	/// <summary>Checks the quotes the scanner recorded for its start tag: as many as its values, up to the most it records, each at a quote.</summary>
	/// <param name="scanner">The scanner, at the tag.</param>
	/// <param name="values">The tag's values, namespace declarations' too.</param>
	private static void AssertRecorded(XmlScanner scanner, int values)
	{
		Assert.Equal(2 * Math.Min(values, XmlScanner.RecordedValues), scanner.QuoteCount);
		var attributesStart = scanner.TokenStart + 1 + scanner.QualifiedName.Length;
		for (var i = 0; i < scanner.QuoteCount; i += 2)
		{
			var open = scanner.Window.Buffer[attributesStart + scanner.Quotes[i]];
			Assert.True(open is (byte)'"' or (byte)'\'');
			Assert.Equal(open, scanner.Window.Buffer[attributesStart + scanner.Quotes[i + 1]]);
		}
	}

	/// <summary>
	/// Reads a tag's first token, and its attributes both ways: the scanner's, and the walk from the part's bytes over the
	/// same bytes. They must be the same.
	/// </summary>
	/// <param name="xml">The part: a start tag at its start.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="seed">The seed it was made from, for failures.</param>
	/// <returns>What the attributes came to, to count: <c>read</c>, <c>read past recorded</c>, an error's detail, <c>value not decoded</c> or <c>tag refused</c>.</returns>
	private static async Task<string> CompareAsync(byte[] xml, ScanMode mode, int seed)
	{
		using var scanner = XmlScan.Open(xml, mode);
		try
		{
			await scanner.NextAsync(mode);
		}
		catch (InvalidDataException)
		{
			// The tokenizer refuses the tag, as it did before it recorded quotes: there are no attributes to compare.
			return "tag refused";
		}

		Assert.Equal(XmlTokenKind.StartTag, scanner.Kind);
		var start = 1 + scanner.QualifiedName.Length;
		var end = (int)scanner.Window.Offset(scanner.Position) - (scanner.IsEmptyElement ? 2 : 1);
		var recorded = scanner.QuoteCount;
		var walked = Walk(xml, start, end - start, out var qualified);
		var read = Read(scanner, qualified);
		if (!walked.SequenceEqual(read))
		{
			Assert.Fail($"Seed {seed}, {Encoding.UTF8.GetString(xml)}: the walk from the bytes read{Environment.NewLine}{string.Join(Environment.NewLine, walked)}{Environment.NewLine}but the attributes read{Environment.NewLine}{string.Join(Environment.NewLine, read)}");
		}

		var last = walked.Count == 0 ? "" : walked[walked.Count - 1];
		foreach (var detail in new[] { NoNameOrValue, NoQuotes, LessThanInValue })
		{
			if (last.StartsWith("error ", StringComparison.Ordinal) && last.EndsWith(detail, StringComparison.Ordinal))
			{
				return detail;
			}
		}

		if (recorded == 2 * XmlScanner.RecordedValues && walked.Count > XmlScanner.RecordedValues)
		{
			return "read past recorded";
		}

		return walked.Any(a => a.IndexOf("] !", StringComparison.Ordinal) >= 0) ? "value not decoded" : "read";
	}

	/// <summary>
	/// The walk from the bytes alone that read every attribute before the tokenizer recorded quotes (<see cref="XmlAttributes.MoveNext"/>
	/// at 6a51d85d), over a part's bytes: each attribute's qualified and local names and value, as it is and decoded, and
	/// the error that ends the walk, as <see cref="Read"/> gives them.
	/// </summary>
	/// <param name="part">The part's bytes.</param>
	/// <param name="start">The offset of the attributes: after the tag's name.</param>
	/// <param name="length">Their length: to the tag's <c>&gt;</c> or <c>/&gt;</c>.</param>
	/// <param name="qualified">The attributes' qualified names.</param>
	/// <returns>The attributes, and the error.</returns>
	private static List<string> Walk(byte[] part, int start, int length, out List<string> qualified)
	{
		var walked = new List<string>();
		qualified = [];
		var span = part.AsSpan(start, length);
		var offset = 0;
		while (true)
		{
			while (offset < span.Length && XmlScanner.IsWhitespace(span[offset]))
			{
				offset++;
			}

			if (offset == span.Length)
			{
				return walked;
			}

			var name = offset;
			while (offset < span.Length && span[offset] != (byte)'=' && !XmlScanner.IsWhitespace(span[offset]))
			{
				offset++;
			}

			var nameEnd = offset;
			while (offset < span.Length && XmlScanner.IsWhitespace(span[offset]))
			{
				offset++;
			}

			if (nameEnd == name || offset == span.Length || span[offset] != (byte)'=')
			{
				walked.Add($"error {start + name}: {NoNameOrValue}");

				return walked;
			}

			offset++;
			while (offset < span.Length && XmlScanner.IsWhitespace(span[offset]))
			{
				offset++;
			}

			var quote = offset < span.Length ? span[offset] : (byte)0;
			var close = quote is (byte)'"' or (byte)'\'' ? span.Slice(offset + 1).IndexOfAny(quote, (byte)'<') : -1;
			if (close < 0)
			{
				walked.Add($"error {start + name}: {NoQuotes}");

				return walked;
			}

			if (span[offset + 1 + close] == (byte)'<')
			{
				walked.Add($"error {start + offset + 1 + close}: {LessThanInValue}");

				return walked;
			}

			var valueStart = offset + 1;
			var valueLength = close;
			offset += close + 2;
			var qualifiedName = span.Slice(name, nameEnd - name);
			if (qualifiedName.SequenceEqual("xmlns"u8) || qualifiedName.StartsWith("xmlns:"u8))
			{
				continue;
			}

			var colon = qualifiedName.IndexOf((byte)':');
			qualified.Add(Text(qualifiedName));
			walked.Add($"{Text(qualifiedName)} {Text(qualifiedName.Slice(colon + 1))}=[{Text(span.Slice(valueStart, valueLength))}] {Decoded(part, start + valueStart, valueLength)}");
		}
	}

	/// <summary>A value decoded, as <see cref="XmlAttributes.DecodeValue()"/> decodes it, or its error, from a part's bytes.</summary>
	/// <param name="part">The part's bytes.</param>
	/// <param name="valueStart">The value's offset.</param>
	/// <param name="valueLength">Its length.</param>
	/// <returns>The value, or <c>!offset: detail</c>.</returns>
	private static string Decoded(byte[] part, int valueStart, int valueLength)
	{
		var raw = part.AsSpan(valueStart, valueLength);
		var decoded = new byte[raw.Length];
		var written = XmlText.DecodeAttribute(raw, decoded);
		if (written < 0)
		{
			return $"!{valueStart + ~written}: {ReferenceDetail(raw.Slice(~written))}";
		}

		return Encoding.UTF8.GetString(decoded, 0, written);
	}

	/// <summary>What is wrong with a reference that can't be decoded, as the scanner says it.</summary>
	/// <param name="rest">The value from the reference's <c>&amp;</c>.</param>
	/// <returns>The detail.</returns>
	private static string ReferenceDetail(ReadOnlySpan<byte> rest)
	{
		rest = rest.Slice(0, Math.Min(rest.Length, 16));
		var semicolon = rest.IndexOf((byte)';');

		return $"'{Text(semicolon < 0 ? rest : rest.Slice(0, semicolon + 1))}' is neither a predefined entity nor a valid character reference";
	}

	/// <summary>Reads the attributes of the scanner's start tag as <see cref="Walk"/> gives them.</summary>
	/// <param name="scanner">The scanner, at the tag.</param>
	/// <param name="qualified">The qualified names the walk read, to check the attributes' against (they don't give theirs).</param>
	/// <returns>The attributes, and the error.</returns>
	private static List<string> Read(XmlScanner scanner, List<string> qualified)
	{
		var read = new List<string>();
		var attributes = scanner.Attributes;
		try
		{
			while (attributes.MoveNext())
			{
				var k = read.Count;
				var name = k < qualified.Count && attributes.IsQualifiedName(Utf8(qualified[k])) ? qualified[k] : "(another qualified name)";
				string decoded;
				try
				{
					decoded = attributes.DecodeValue();
				}
				catch (InvalidDataException e)
				{
					Assert.True(XmlError.TryGet(e, out var offset, out var detail));
					Assert.Equal(Message(offset, detail), e.Message);
					decoded = $"!{offset}: {detail}";
				}

				read.Add($"{name} {Text(attributes.LocalName)}=[{Text(attributes.Value)}] {decoded}");
			}
		}
		catch (InvalidDataException e)
		{
			read.Add(ErrorText(e));
		}

		return read;
	}

	/// <summary>Reads attributes as <c>name=value</c>, the values as they are.</summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The attributes.</returns>
	private static List<string> ReadAll(XmlAttributes attributes)
	{
		var read = new List<string>();
		while (attributes.MoveNext())
		{
			read.Add($"{Text(attributes.LocalName)}={Text(attributes.Value)}");
		}

		return read;
	}

	/// <summary>Reads a part's tags and text, without reading any attributes.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns><c>&lt;name&gt;</c> and <c>&lt;/name&gt;</c> for tags (both for an empty element), and the text inside.</returns>
	private static async Task<List<string>> TagsAsync(byte[] xml, ScanMode mode)
	{
		using var scanner = XmlScan.Open(xml, mode);
		var tags = new List<string>();
		while (await scanner.NextAsync(mode))
		{
			switch (scanner.Kind)
			{
				case XmlTokenKind.StartTag:
					tags.Add($"<{Text(scanner.LocalName)}>");
					if (scanner.IsEmptyElement)
					{
						tags.Add($"</{Text(scanner.LocalName)}>");
					}

					break;
				case XmlTokenKind.EndTag:
					tags.Add($"</{Text(scanner.LocalName)}>");

					break;
				case XmlTokenKind.Text:
					tags.Add(XmlScan.DecodeContent(scanner));

					break;
			}
		}

		return tags;
	}

	/// <summary>
	/// <see cref="XmlAttributes.GetInt32"/> as it was before plain digits were parsed where they are (6a51d85d): the value
	/// decoded when it has a reference, trimmed, and parsed by <see cref="Utf8Parser"/>.
	/// </summary>
	/// <param name="value">The value, as it is in the XML, of an attribute <c>n</c>.</param>
	/// <param name="valueOffset">The value's offset in the part.</param>
	/// <returns>The integer, or the error as <see cref="ErrorText"/> gives it.</returns>
	private static string OldGetInt32(string value, int valueOffset)
	{
		var raw = Utf8(value);
		var bytes = raw;
		if (Array.IndexOf(raw, (byte)'&') >= 0)
		{
			var decoded = new byte[raw.Length];
			var written = XmlText.DecodeAttribute(raw, decoded);
			if (written < 0)
			{
				return $"error {valueOffset + ~written}: {ReferenceDetail(raw.AsSpan(~written))}";
			}

			bytes = decoded.AsSpan(0, written).ToArray();
		}

		var start = 0;
		var end = bytes.Length;
		while (start < end && XmlScanner.IsWhitespace(bytes[start]))
		{
			start++;
		}

		while (end > start && XmlScanner.IsWhitespace(bytes[end - 1]))
		{
			end--;
		}

		var trimmed = bytes.AsSpan(start, end - start);
		if (!Utf8Parser.TryParse(trimmed, out int result, out var consumed) || consumed != trimmed.Length)
		{
			return $"error {valueOffset}: the attribute n=\"{value}\" is not an integer";
		}

		return result.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>Writes a random tag: attributes well formed or not, of any number, in an element empty or not.</summary>
	/// <param name="random">The source of the tag's choices.</param>
	/// <returns>The tag's bytes, as a part, ASCII.</returns>
	private static byte[] RandomTag(Random random)
	{
		var count = random.Next(5) switch
		{
			0 => random.Next(3),
			1 => random.Next(6),
			2 => 14 + random.Next(5),
			3 => 30 + random.Next(11),
			_ => random.Next(41),
		};
		// Some tags have an attribute without a name, and some an oddity (Oddities), anywhere.
		var emptyName = random.Next(8) == 0 ? random.Next(count) : -1;
		var oddity = random.Next(4) == 0 ? random.Next(count + 1) : -1;
		var builder = new StringBuilder();
		for (var i = 0; i <= count; i++)
		{
			if (i == oddity)
			{
				builder.Append(Pick(random, Oddities));
			}

			if (i == count)
			{
				break;
			}

			var quote = random.Next(2) == 0 ? '"' : '\'';
			var value = Pick(random, Values);
			if (random.Next(8) == 0)
			{
				value += quote == '"' ? "'" : "\"";
			}

			builder.Append(Pick(random, Spaces)).Append(i == emptyName ? "" : Pick(random, Names)).Append(Pick(random, Around)).Append('=').Append(Pick(random, Around));
			builder.Append(quote).Append(value).Append(quote);
		}

		// Many tags are mutated once or more: a byte deleted, one of the bytes that matter inserted, or one replaced.
		var text = builder.ToString();
		var mutations = random.Next(10) switch
		{
			< 4 => 0,
			< 7 => 1,
			< 8 => 2,
			_ => random.Next(6),
		};
		for (var i = 0; i < mutations; i++)
		{
			var at = random.Next(text.Length + 1);
			var operation = text.Length == 0 ? 1 : random.Next(3);
			if (operation == 0)
			{
				text = text.Remove(Math.Min(at, text.Length - 1), 1);
			}
			else if (operation == 1)
			{
				text = text.Insert(at, Inserted[random.Next(Inserted.Length)].ToString());
			}
			else
			{
				at = Math.Min(at, text.Length - 1);
				text = text.Substring(0, at) + Inserted[random.Next(Inserted.Length)] + text.Substring(at + 1);
			}
		}

		var end = random.Next(4) switch
		{
			0 => "/>",
			1 => "></a>",
			2 => " />",
			_ => "/ >",
		};

		return Utf8("<a" + text + end);
	}

	/// <summary>Picks one of some choices.</summary>
	/// <param name="random">The source of the choice.</param>
	/// <param name="choices">The choices.</param>
	/// <returns>The choice.</returns>
	private static string Pick(Random random, string[] choices) => choices[random.Next(choices.Length)];

	/// <summary>Checks an error of the tests' part: its offset and detail, and its message.</summary>
	/// <param name="exception">The error.</param>
	/// <param name="offset">The byte offset.</param>
	/// <param name="detail">What is wrong.</param>
	private static void AssertError(InvalidDataException exception, long offset, string detail)
	{
		Assert.True(XmlError.TryGet(exception, out var actualOffset, out var actualDetail), exception.Message);
		Assert.Equal(detail, actualDetail);
		Assert.Equal(offset, actualOffset);
		Assert.Equal(Message(offset, detail), exception.Message);
	}

	/// <summary>An error as the tests compare it: <c>error offset: detail</c>, once its message is checked.</summary>
	/// <param name="exception">The error.</param>
	/// <returns>The text.</returns>
	private static string ErrorText(InvalidDataException exception)
	{
		Assert.True(XmlError.TryGet(exception, out var offset, out var detail), exception.Message);
		Assert.Equal(Message(offset, detail), exception.Message);

		return $"error {offset}: {detail}";
	}

	/// <summary>The message of an error in the tests' part.</summary>
	/// <param name="offset">The byte offset.</param>
	/// <param name="detail">What is wrong.</param>
	/// <returns>The message.</returns>
	private static string Message(long offset, string detail) => $"The XML of {XmlScan.PartName} can't be read at byte {offset}: {detail}.";

	/// <summary>Some UTF-8 bytes as a string.</summary>
	/// <param name="utf8">The bytes.</param>
	/// <returns>The string.</returns>
	private static string Text(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8.ToArray());

	/// <summary>The UTF-8 bytes of a string.</summary>
	/// <param name="text">The string.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
