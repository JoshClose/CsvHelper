using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>
/// Reading the tokens of XML parts. Each test reads in every <see cref="ScanMode"/>: whole and one-byte reads, a 64 KB and
/// a 16-byte window, sync and async, so tokens are split between reads and refills at every position.
/// </summary>
public class XmlScannerTests
{
	[Fact]
	public async Task ReadsTokens()
	{
		var xml = Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n<x:worksheet xmlns:x=\"urn:x\"><x:row r=\"1\"><c r=\"A1\" t=\"s\"><v>12</v></c><c/></x:row>a &amp; b<![CDATA[<c>]]></x:worksheet>\r\n");
		var expected = new[]
		{
			"Text 0 '\r\n'",
			"StartTag 1 worksheet",
			"StartTag 2 row",
			"StartTag 3 c",
			"StartTag 4 v",
			"Text 4 '12'",
			"EndTag 3 v",
			"EndTag 2 c",
			"StartTag 2 c empty",
			"EndTag 1 row",
			"Text 1 'a &amp; b'",
			"CData 1 '<c>'",
			"EndTag 0 worksheet",
			"Text 0 '\r\n'",
		};

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			var tokens = new List<string>();
			while (await scanner.NextAsync(mode))
			{
				tokens.Add(Describe(scanner));
			}

			Assert.Equal(expected, tokens);
			Assert.Equal(XmlTokenKind.End, scanner.Kind);
			Assert.Equal(0, scanner.Depth);
			Assert.False(await scanner.NextAsync(mode));
		});
	}

	[Fact]
	public async Task SkipsCommentsAndProcessingInstructions()
	{
		var xml = Utf8("<!-- before --><?pi before?><a><!----><?x?><b/><!-- - -><x> --><?pi <x> ?></a><!-- after --><?pi after?>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["<a>", "<b>", "</b>", "</a>"], await XmlScan.EventsAsync(xml, mode));
		});
	}

	[Fact]
	public async Task MatchesLocalNames()
	{
		var xml = Utf8("<x:sheetData xmlns:x=\"urn:x\"><x:c></x:c><c></c></x:sheetData>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("sheetData"u8));
			Assert.False(scanner.IsStartTag("x:sheetData"u8));
			Assert.False(scanner.IsEndTag("sheetData"u8));

			for (var i = 0; i < 2; i++)
			{
				await scanner.NextAsync(mode);
				Assert.True(scanner.IsStartTag("c"u8));
				await scanner.NextAsync(mode);
				Assert.True(scanner.IsEndTag("c"u8));
				Assert.False(scanner.IsStartTag("c"u8));
			}

			await scanner.NextAsync(mode);
			Assert.True(scanner.IsEndTag("sheetData"u8));
		});
	}

	[Fact]
	public async Task ReadsAttributes()
	{
		var xml = Utf8(
			"<x:sheet xmlns:x=\"urn:x\" xmlns=\"urn:main\" name=\"A &amp; B\" sheetId = '1'\r\n\tr:id=\"rId1\" xmlns:r=\"urn:r\" " +
			"state=\"x>y\" quote='say \"hi\"' ws=\"a\tb\r\nc&#10;d\" empty=\"\"/>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["<sheet name=\"A & B\" sheetId=\"1\" id=\"rId1\" state=\"x>y\" quote=\"say \"hi\"\" ws=\"a b c\nd\" empty=\"\">", "</sheet>"],
				await XmlScan.EventsAsync(xml, mode));
		});
	}

	[Fact]
	public async Task ReadsRawAttributeValuesAndMatchesNames()
	{
		var xml = Utf8("<c r=\"B7\" s=\"3\" t=\"inlineStr\"/>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			var found = new List<string>();
			while (attributes.MoveNext())
			{
				if (attributes.IsName("r"u8) || attributes.IsName("t"u8))
				{
					found.Add(Encoding.UTF8.GetString(attributes.Value.ToArray()));
				}
			}

			Assert.Equal(["B7", "inlineStr"], found);
		});
	}

	[Fact]
	public async Task OtherTokensHaveNoAttributesNameOrContent()
	{
		var xml = Utf8("<a b=\"1\">text</a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			Assert.False(scanner.Attributes.MoveNext());
			await scanner.NextAsync(mode);
			Assert.True(scanner.Content.IsEmpty);
			await scanner.NextAsync(mode);
			Assert.Equal(XmlTokenKind.Text, scanner.Kind);
			Assert.True(scanner.LocalName.IsEmpty);
			Assert.False(scanner.Attributes.MoveNext());
			await scanner.NextAsync(mode);
			Assert.True(scanner.Content.IsEmpty);
			Assert.False(scanner.Attributes.MoveNext());
		});
	}

	[Theory]
	[InlineData("<a b/>", 3, "an attribute has no name, or no value")]
	[InlineData("<a b c=\"1\"/>", 3, "an attribute has no name, or no value")]
	[InlineData("<a =\"1\"/>", 3, "an attribute has no name, or no value")]
	[InlineData("<a b=1/>", 3, "an attribute value has no quotes")]
	[InlineData("<a b=/>", 3, "an attribute value has no quotes")]
	[InlineData("<a x=\"1\" b=c\"d\"/>", 9, "an attribute value has no quotes")]
	public async Task RejectsAttributesItCantRead(string xml, int offset, string detail)
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(Utf8(xml), mode);
			await scanner.NextAsync(mode);
			var e = Assert.Throws<InvalidDataException>(() =>
			{
				var attributes = scanner.Attributes;
				while (attributes.MoveNext())
				{
				}
			});
			Assert.Equal(Message(offset, detail), e.Message);
		});
	}

	[Theory]
	[InlineData("<a>x&foo;y</a>", 4, "'&foo;'")]
	[InlineData("<a>x&#0;</a>", 4, "'&#0;'")]
	[InlineData("<a>&#xD800;</a>", 3, "'&#xD800;'")]
	[InlineData("<a>&#x110000;</a>", 3, "'&#x110000;'")]
	[InlineData("<a>&amp</a>", 3, "'&amp'")]
	[InlineData("<a>&#1234567890123456789;</a>", 3, "'&#12345678901234'")]
	public async Task RejectsReferencesInTextItCantDecode(string xml, int offset, string reference)
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(Utf8(xml), mode));
			Assert.Equal(Message(offset, $"{reference} is neither a predefined entity nor a valid character reference"), e.Message);
		});
	}

	[Fact]
	public async Task RejectsReferencesInAttributesItCantDecode()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(Utf8("<a b=\"ok\" c=\"x&nbsp;\"/>"), mode));
			Assert.Equal(Message(14, "'&nbsp;' is neither a predefined entity nor a valid character reference"), e.Message);
		});
	}

	[Fact]
	public async Task LeavesReferencesInCDataAndUndecodedTextAlone()
	{
		var xml = Utf8("<a><![CDATA[&foo; &#0;]]><b>&bad;</b></a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.Equal("&foo; &#0;", XmlScan.DecodeContent(scanner));
			await scanner.NextAsync(mode);
			await scanner.SkipAsync(1, mode);
			Assert.True(scanner.IsEndTag("b"u8));
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsEndTag("a"u8));
		});
	}

	[Theory]
	[InlineData("", 0, "it has no root element")]
	[InlineData("  \r\n ", 5, "it has no root element")]
	[InlineData("<?xml version=\"1.0\"?><!-- no root -->", 37, "it has no root element")]
	[InlineData("<a>", 3, "it ends inside an element")]
	[InlineData("<a><b/>text", 11, "it ends inside an element")]
	[InlineData("</a>", 0, "an end tag has no start tag")]
	[InlineData("<a/></a>", 4, "an end tag has no start tag")]
	[InlineData("<a></a></a>", 7, "an end tag has no start tag")]
	[InlineData("<a/><b/>", 4, "it has a second root element")]
	[InlineData("<a></a>text<b></b>", 11, "it has a second root element")]
	[InlineData("<a", 0, "it ends inside a tag")]
	[InlineData("<a><", 3, "it ends inside a tag")]
	[InlineData("<a b=\"1", 0, "it ends inside a tag")]
	[InlineData("<a b='1\">", 0, "it ends inside a tag")]
	[InlineData("<a></a", 3, "it ends inside a tag")]
	[InlineData("<a><!-- x", 3, "it ends inside a comment")]
	[InlineData("<a><!-- x --", 3, "it ends inside a comment")]
	[InlineData("<a><![CDATA[ x ]]", 3, "it ends inside a CDATA section")]
	[InlineData("<a><?pi x", 3, "it ends inside a processing instruction")]
	[InlineData("<a><!", 3, "it ends inside markup")]
	[InlineData("<a><!-", 3, "it ends inside markup")]
	[InlineData("<a><![CDAT", 3, "it ends inside markup")]
	[InlineData("<!DOCTY", 0, "it ends inside markup")]
	[InlineData("< a/>", 0, "'<' starts no tag")]
	[InlineData("<a><></a>", 3, "'<' starts no tag")]
	[InlineData("<a></></a>", 3, "an end tag has no name")]
	[InlineData("<a></ a></a>", 3, "an end tag has no name")]
	[InlineData("<a></a b></a>", 3, "an end tag has more than a name")]
	[InlineData("<a><!ELEMENT x></a>", 3, "'<!' starts neither a comment nor a CDATA section")]
	[InlineData("<a><!doctype a></a>", 3, "'<!' starts neither a comment nor a CDATA section")]
	public async Task RejectsXmlItCantRead(string xml, int offset, string detail)
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(Utf8(xml), mode));
			Assert.Equal(Message(offset, detail), e.Message);
		});
	}

	[Theory]
	[InlineData("<!DOCTYPE a><a/>")]
	[InlineData("<?xml version=\"1.0\"?>\n<!DOCTYPE lolz [<!ENTITY lol \"lol\"><!ENTITY lol2 \"&lol;&lol;&lol;&lol;\">]><lolz>&lol2;</lolz>")]
	[InlineData("<?xml version=\"1.0\"?><!DOCTYPE foo [<!ENTITY xxe SYSTEM \"file:///c:/windows/win.ini\">]><foo>&xxe;</foo>")]
	[InlineData("<!-- a comment first --><!DOCTYPE a SYSTEM \"http://example.com/a.dtd\"><a/>")]
	[InlineData("<a><!DOCTYPE a></a>")]
	public async Task RejectsADoctypeBeforeReadingPastIt(string xml)
	{
		var bytes = Utf8(xml);
		var start = xml.IndexOf("<!DOCTYPE", StringComparison.Ordinal);

		await XmlScan.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(bytes, mode));
			Assert.Equal(Message(start, "it has a DOCTYPE, which Office Open XML parts must not have"), e.Message);
		});

		foreach (var async in new[] { false, true })
		{
			var stream = new TrickleStream(bytes);
			using var scanner = new XmlScanner(new XmlWindow(stream, XmlScan.PartName, 1 << 20, 16));
			await Assert.ThrowsAsync<InvalidDataException>(async () =>
			{
				while (async ? await scanner.ReadAsync(CancellationToken.None) : scanner.Read())
				{
				}
			});
			Assert.Equal(start + "<!DOCTYPE".Length, stream.Delivered);
		}
	}

	[Fact]
	public async Task DropsAUtf8ByteOrderMark()
	{
		var bom = new byte[] { 0xEF, 0xBB, 0xBF };

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["<a>", "</a>"], await XmlScan.EventsAsync([.. bom, .. Utf8("<a/>")], mode));
			// Offsets count the byte order mark: the end is byte 6.
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync([.. bom, .. Utf8("<a>")], mode));
			Assert.Equal(Message(6, "it ends inside an element"), e.Message);
			e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(bom, mode));
			Assert.Equal(Message(3, "it has no root element"), e.Message);
		});
	}

	[Fact]
	public async Task ReadsWhatOnlyStartsLikeAByteOrderMarkAsUtf8()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			// EF BB with no BF: two bytes of text before the root (not valid UTF-8, but nothing reads it).
			Assert.Equal(["<a>", "</a>"], await XmlScan.EventsAsync([0xEF, 0xBB, .. Utf8("<a/>")], mode));
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync([0xEF, 0xBB], mode));
			Assert.Equal(Message(2, "it has no root element"), e.Message);
			e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync([0xEF], mode));
			Assert.Equal(Message(1, "it has no root element"), e.Message);
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsUtf16(bool bigEndian)
	{
		var text = "<?xml version=\"1.0\" encoding=\"UTF-16\"?><a b=\"" + char.ConvertFromUtf32(0x1F600) + "\">é日本" + char.ConvertFromUtf32(0x10FFFF) + "</a>";
		var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
		byte[] xml = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(XmlScan.ReferenceEvents(xml), await XmlScan.EventsAsync(xml, mode));
			Assert.Equal(["<a b=\"" + char.ConvertFromUtf32(0x1F600) + "\">", "é日本" + char.ConvertFromUtf32(0x10FFFF), "</a>"], await XmlScan.EventsAsync(xml, mode));
		});
	}

	[Fact]
	public async Task GrowsTheWindowForATokenThatDoesntFit()
	{
		var xml = Utf8("<a b=\"" + new string('x', 1000) + "\"/>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			var attributes = scanner.Attributes;
			Assert.True(attributes.MoveNext());
			Assert.Equal(1000, attributes.Value.Length);
			Assert.Equal(mode.WindowSize == 16 ? 1024 : XmlWindow.DefaultSize, scanner.Window.Capacity);
		});
	}

	[Fact]
	public async Task ThrowsForATokenPastTheSizeLimit()
	{
		// The start tag is exactly 64 bytes: it fits a window of the limit, and one more byte doesn't.
		var fits = Utf8("<a b=\"" + new string('x', 56) + "\"></a>");
		var tooLong = Utf8("<a b=\"" + new string('x', 57) + "\"></a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["<a b=\"" + new string('x', 56) + "\">", "</a>"], await XmlScan.EventsAsync(fits, mode, 64));
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(tooLong, mode, 64));
			Assert.Equal(Message(0, "the token or cell there is larger than ExcelReaderOptions.MaxPartSize (64 bytes)"), e.Message);
		});
	}

	[Fact]
	public async Task ReadsPartsMuchLargerThanTheWindow()
	{
		var builder = new StringBuilder("<a>");
		for (var i = 0; i < 2000; i++)
		{
			builder.Append("<b r=\"").Append(i).Append("\">t").Append(i).Append("</b>");
		}

		var xml = Utf8(builder.Append("</a>").ToString());

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode, 64);
			var count = 0;
			while (await scanner.NextAsync(mode))
			{
				if (scanner.Kind == XmlTokenKind.Text)
				{
					Assert.Equal($"t{count++}", XmlScan.DecodeContent(scanner));
				}
			}

			Assert.Equal(2000, count);
			Assert.True(scanner.Window.Capacity <= 64);
		});
	}

	[Fact]
	public async Task SkipsElementsOfAnyDepth()
	{
		var xml = Utf8("<a><b x=\"1\"><c>t<d/><![CDATA[x]]><!-- c --></c><c><e></e></c></b><f/></a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("b"u8));
			Assert.Equal(2, scanner.Depth);
			await scanner.SkipAsync(1, mode);
			Assert.True(scanner.IsEndTag("b"u8));
			Assert.Equal(1, scanner.Depth);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("f"u8));
			// Already at the depth: nothing to skip.
			await scanner.SkipAsync(1, mode);
			Assert.True(scanner.IsStartTag("f"u8));
			await scanner.SkipAsync(0, mode);
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.False(await scanner.NextAsync(mode));
		});
	}

	[Fact]
	public async Task SkipsTextThatDoesntFitTheWindow()
	{
		var text = new string('t', 10_000);
		var xml = Utf8("<a><b>" + text + "</b><c/></a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode, 64);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			await scanner.SkipAsync(1, mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("c"u8));
			Assert.True(scanner.Window.Capacity <= 64);

			// Read as a token, the same text is past the limit.
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => XmlScan.EventsAsync(xml, mode, 64));
			Assert.Equal(Message(6, "the token or cell there is larger than ExcelReaderOptions.MaxPartSize (64 bytes)"), e.Message);
		});
	}

	[Fact]
	public async Task SkipsTextAsItComes()
	{
		// Text up to the next markup, across refills, without growing the window; markup stays a token, and at markup
		// there is nothing to skip.
		var text = new string(' ', 10_000);
		var xml = Utf8("<a>" + text + "<![CDATA[x]]>" + text + "<b/></a>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode, 64);
			await scanner.NextAsync(mode);
			await scanner.SkipTextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.Equal(XmlTokenKind.CData, scanner.Kind);
			await scanner.SkipTextAsync(mode);
			await scanner.SkipTextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("b"u8));
			await scanner.SkipTextAsync(mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsEndTag("a"u8));
			Assert.False(await scanner.NextAsync(mode));
			Assert.True(scanner.Window.Capacity <= 64);
		});
	}

	[Fact]
	public async Task SkippingTextThatEndsInsideAnElementLeavesTheErrorToTheNextRead()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(Utf8("<a>" + new string(' ', 1000)), mode, 64);
			await scanner.NextAsync(mode);
			await scanner.SkipTextAsync(mode);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.NextAsync(mode));
			Assert.Equal(Message(1003, "it ends inside an element"), e.Message);
		});
	}

	[Fact]
	public async Task SkipThatEndsInsideAnElementThrows()
	{
		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(Utf8("<a><b><c>text"), mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => scanner.SkipAsync(1, mode));
			Assert.Equal(Message(13, "it ends inside an element"), e.Message);
		});
	}

	[Fact]
	public void SkipToANegativeDepthThrows()
	{
		using var scanner = XmlScan.Open(Utf8("<a/>"), ScanMode.All[0]);

		Assert.Throws<ArgumentOutOfRangeException>(() => scanner.TrySkipTo(-1));
	}

	[Fact]
	public async Task NestsDeeplyWithoutRecursion()
	{
		const int Depth = 100_000;
		var xml = Utf8("<r>" + string.Concat(Enumerable.Repeat("<x>", Depth)) + "<v>1</v>" + string.Concat(Enumerable.Repeat("</x>", Depth)) + "<after/></r>");

		foreach (var mode in new[] { ScanMode.All[0], ScanMode.All[1], ScanMode.All[3] })
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			await scanner.NextAsync(mode);
			await scanner.SkipAsync(1, mode);
			await scanner.NextAsync(mode);
			Assert.True(scanner.IsStartTag("after"u8));

			using var reader = XmlScan.Open(xml, mode);
			var maxDepth = 0;
			while (await reader.NextAsync(mode))
			{
				maxDepth = Math.Max(maxDepth, reader.Depth);
			}

			Assert.Equal(Depth + 2, maxDepth);
		}
	}

	[Fact]
	public async Task RestoresASavedState()
	{
		var xml = Utf8("<a><b x=\"1\">t</b><c/></a>");

		// Whole reads with the default window hold all of it after one refill, so the state outlives the reads.
		foreach (var mode in ScanMode.All.Where(m => !m.Trickle && m.WindowSize == XmlWindow.DefaultSize))
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			var state = scanner.Save();
			var first = new List<string>();
			for (var i = 0; i < 4; i++)
			{
				await scanner.NextAsync(mode);
				first.Add(Describe(scanner));
			}

			scanner.Restore(state);
			Assert.Equal(XmlTokenKind.None, scanner.Kind);
			Assert.Equal(1, scanner.Depth);
			var again = new List<string>();
			for (var i = 0; i < 4; i++)
			{
				await scanner.NextAsync(mode);
				again.Add(Describe(scanner));
			}

			Assert.Equal(["StartTag 2 b", "Text 2 't'", "EndTag 1 b", "StartTag 1 c empty"], first);
			Assert.Equal(first, again);
		}
	}

	[Fact]
	public async Task RestoringAStateFromBeforeARefillThrows()
	{
		using var scanner = XmlScan.Open(Utf8("<a><b/></a>"), ScanMode.All[0]);
		await scanner.NextAsync(ScanMode.All[0]);
		var state = scanner.Save();
		scanner.Refill();

		Assert.Throws<InvalidOperationException>(() => scanner.Restore(state));
	}

	[Fact]
	public async Task RewindsToACellAndRefillsFromIt()
	{
		// The sheet scanner's pattern: save before a cell's '<c', read the whole cell if the window holds it, and otherwise
		// go back to the '<c' and refill, which keeps the cell's bytes and reads more.
		var xml = Utf8("<row><c r=\"A1\"><v>" + new string('9', 40) + "</v></c><c r=\"B1\"><v>2</v></c></row>");

		await XmlScan.InEveryModeAsync(async mode =>
		{
			using var scanner = XmlScan.Open(xml, mode);
			await scanner.NextAsync(mode);
			var cells = new List<string>();
			while (true)
			{
				var cell = scanner.Save();
				var result = TryReadCell(scanner, out var value);
				if (result < 0)
				{
					break;
				}

				if (result > 0)
				{
					cells.Add(value!);

					continue;
				}

				scanner.Restore(cell);
				if (mode.Async)
				{
					await scanner.RefillAsync(CancellationToken.None);
				}
				else
				{
					scanner.Refill();
				}
			}

			Assert.Equal(["A1=" + new string('9', 40), "B1=2"], cells);
		});
	}

	[Fact]
	public async Task RetriesARefillThatWasCanceled()
	{
		var xml = Utf8("<a><b>text</b></a>");
		using var scanner = new XmlScanner(new XmlWindow(new TrickleStream(xml, 4), XmlScan.PartName, 1 << 20, 16));
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();

		Assert.False(scanner.TryRead());
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.RefillAsync(canceled.Token).AsTask());
		var tokens = new List<string>();
		while (await scanner.ReadAsync(CancellationToken.None))
		{
			tokens.Add(Describe(scanner));
		}

		Assert.Equal(["StartTag 1 a", "StartTag 2 b", "Text 2 'text'", "EndTag 1 b", "EndTag 0 a"], tokens);
	}

	[Fact]
	public async Task DisposingTheScannerDisposesTheStream()
	{
		var stream = new TrickleStream(Utf8("<a/>"));
		var scanner = new XmlScanner(new XmlWindow(stream, XmlScan.PartName, 1 << 20));
		scanner.Dispose();
		scanner.Dispose();

		Assert.True(stream.Disposed);
		Assert.Throws<ObjectDisposedException>(() => scanner.Refill());

		var asyncStream = new TrickleStream(Utf8("<a/>"));
		var asyncScanner = new XmlScanner(new XmlWindow(asyncStream, XmlScan.PartName, 1 << 20));
		await asyncScanner.DisposeAsync();
		await asyncScanner.DisposeAsync();
		Assert.True(asyncStream.Disposed);
	}

	[Fact]
	public void RefillingAfterTheEndThrows()
	{
		using var scanner = XmlScan.Open(Utf8("<a/>"), ScanMode.All[0]);
		while (scanner.Read())
		{
		}

		Assert.Throws<InvalidOperationException>(() => scanner.Refill());
	}

	[Fact]
	public void WindowChecksItsArguments()
	{
		Assert.Throws<ArgumentNullException>(() => new XmlWindow(null!, "p", 1));
		Assert.Throws<ArgumentNullException>(() => new XmlWindow(new MemoryStream(), null!, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new XmlWindow(new MemoryStream(), "p", 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => new XmlWindow(new MemoryStream(), "p", 1, 3));
		Assert.Throws<ArgumentNullException>(() => new XmlScanner(null!));
		using var window = new XmlWindow(new MemoryStream(), "p", 1);
		Assert.Throws<ArgumentOutOfRangeException>(() => window.Compact(1));
	}

	/// <summary>Reads a cell, from its start tag to its end tag, if the window holds all of it: the sheet scanner's pattern.</summary>
	/// <param name="scanner">The scanner, before the cell's start tag (or the row's end tag).</param>
	/// <param name="cell">The cell's reference and value, such as <c>A1=2</c>.</param>
	/// <returns>1 when a cell is read, 0 when the window ends first, and -1 at the end of the row.</returns>
	private static int TryReadCell(XmlScanner scanner, out string? cell)
	{
		cell = null;
		if (!scanner.TryRead())
		{
			return 0;
		}

		if (scanner.IsEndTag("row"u8))
		{
			return -1;
		}

		Assert.True(scanner.IsStartTag("c"u8));
		var attributes = scanner.Attributes;
		Assert.True(attributes.MoveNext());
		var reference = attributes.DecodeValue();
		while (scanner.TryRead())
		{
			if (scanner.Kind == XmlTokenKind.Text)
			{
				cell = reference + "=" + XmlScan.DecodeContent(scanner);
			}
			else if (scanner.IsEndTag("c"u8))
			{
				return 1;
			}
		}

		cell = null;

		return 0;
	}

	/// <summary>Describes the scanner's token: its kind, the depth after it, and its name or raw content.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <returns>The description.</returns>
	private static string Describe(XmlScanner scanner) => scanner.Kind switch
	{
		XmlTokenKind.StartTag => $"StartTag {scanner.Depth} {Encoding.UTF8.GetString(scanner.LocalName.ToArray())}{(scanner.IsEmptyElement ? " empty" : "")}",
		XmlTokenKind.EndTag => $"EndTag {scanner.Depth} {Encoding.UTF8.GetString(scanner.LocalName.ToArray())}",
		_ => $"{scanner.Kind} {scanner.Depth} '{Encoding.UTF8.GetString(scanner.Content.ToArray())}'",
	};

	/// <summary>The message of an error in the tests' part.</summary>
	/// <param name="offset">The byte offset.</param>
	/// <param name="detail">What is wrong.</param>
	/// <returns>The message.</returns>
	private static string Message(long offset, string detail) => $"The XML of {XmlScan.PartName} can't be read at byte {offset}: {detail}.";

	/// <summary>The UTF-8 bytes of a string.</summary>
	/// <param name="text">The string.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
