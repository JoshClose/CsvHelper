using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>
/// What segment scanning needs of the window and the XML scanner: a window over a segment's bytes at their offset in the
/// part (<see cref="XmlWindow.ForSegments"/>, <see cref="XmlWindow.Segment"/>), which never reads, and the part's window
/// restarted over another stream at an offset (<see cref="XmlWindow.Restart"/>); the scanner started inside the root
/// (<see cref="XmlScanner.ResetTo"/>); and whether a window transcodes UTF-16.
/// </summary>
public class XmlSegmentWindowTests
{
	[Fact]
	public void ReadsASegmentsTokensAtItsOffsetInThePart()
	{
		// The buffer is longer than the segment, and its bytes past it would read as tokens.
		var bytes = Utf8("<row r=\"7\"><c><v>1</v></c></row>\n<row/><x/></sheetData>");
		var length = Array.IndexOf(bytes, (byte)'\n') + 1;
		using var scanner = new XmlScanner(XmlWindow.ForSegments("sheet.xml", 1 << 20));
		scanner.Window.Segment(bytes, length, 1000);
		scanner.ResetTo(2);

		var tokens = new List<string>();
		while (scanner.TryRead())
		{
			tokens.Add($"{scanner.Kind} {scanner.Depth} at {scanner.Window.Offset(scanner.TokenStart)}");
		}

		// The text at the end isn't a token: more could follow it. It is skipped to the segment's end.
		Assert.Equal(["StartTag 3 at 1000", "StartTag 4 at 1011", "StartTag 5 at 1014", "Text 5 at 1017", "EndTag 4 at 1018", "EndTag 3 at 1022", "EndTag 2 at 1026"], tokens);
		Assert.Equal(length - 1, scanner.Position);
		Assert.False(scanner.TrySkipText());
		Assert.Equal(length, scanner.Position);
		Assert.False(scanner.Window.IsEof);
		Assert.True(scanner.Window.HoldsSegments);
		Assert.Same(bytes, scanner.Window.Buffer);
		Assert.Equal(length, scanner.Window.End);
	}

	[Fact]
	public void AsksForMoreAtATokenTheSegmentCutsShort()
	{
		using var scanner = new XmlScanner(XmlWindow.ForSegments("sheet.xml", 1 << 20));
		foreach (var cut in new[] { "<row r=\"1\"><c r=\"A", "<row r=\"1\"></ro", "<row r=\"1\"><!-- a", "<row r=\"1\"><![CDATA[", "<row r=\"1\"><?pi" })
		{
			var bytes = Utf8(cut);
			scanner.Window.Segment(bytes, bytes.Length, 50);
			scanner.ResetTo(2);
			Assert.True(scanner.TryRead());
			Assert.False(scanner.TryRead());
			Assert.Equal("<row r=\"1\">".Length, scanner.Position);
		}

		// Text that runs to the segment's end is skipped to it, and is never the part's end.
		var text = Utf8("</row>\n  ");
		scanner.Window.Segment(text, text.Length, 0);
		scanner.ResetTo(3);
		Assert.True(scanner.TryRead());
		Assert.False(scanner.TrySkipText());
		Assert.Equal(text.Length, scanner.Position);
	}

	[Fact]
	public void ReportsErrorsAtTheirOffsetInThePart()
	{
		var bytes = Utf8("<row r=\"1\" a=\"<\"></row>");
		using var scanner = new XmlScanner(XmlWindow.ForSegments("xl/worksheets/sheet1.xml", 1 << 20));
		scanner.Window.Segment(bytes, bytes.Length, 4096);
		scanner.ResetTo(2);

		var e = Assert.Throws<InvalidDataException>(() => scanner.TryRead());
		Assert.Equal($"The XML of xl/worksheets/sheet1.xml can't be read at byte {4096 + 14}: an attribute value has '<', which XML doesn't allow.", e.Message);
	}

	[Fact]
	public void ASegmentsWindowNeverReadsOrWritesItsBytes()
	{
		var bytes = Utf8("<row/><row/>");
		var copy = bytes.ToArray();
		var scanner = new XmlScanner(XmlWindow.ForSegments("sheet.xml", 64));
		scanner.Window.Segment(bytes, 6, 0);
		scanner.ResetTo(2);
		Assert.True(scanner.TryRead());

		Assert.Throws<InvalidOperationException>(() => scanner.Refill());
		Assert.Throws<InvalidOperationException>(() => scanner.Window.Compact(0));
		Assert.Throws<InvalidOperationException>(() => scanner.Window.Read());
		Assert.Throws<InvalidOperationException>(() => scanner.Window.Restart(new MemoryStream(), 0));
		Assert.Equal(copy, bytes);

		// Disposing it returns nothing to the pool: the bytes are the pipeline's.
		scanner.Dispose();
		scanner.Dispose();
		Assert.Equal(copy, bytes);
		Assert.Throws<ObjectDisposedException>(() => scanner.Window.Segment(bytes, 6, 0));
	}

	[Fact]
	public void SegmentChecksItsArguments()
	{
		Assert.Throws<ArgumentNullException>(() => XmlWindow.ForSegments(null!, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => XmlWindow.ForSegments("p", 0));
		using var window = XmlWindow.ForSegments("p", 1);
		Assert.Throws<ArgumentNullException>(() => window.Segment(null!, 0, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => window.Segment(new byte[4], 5, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => window.Segment(new byte[4], -1, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => window.Segment(new byte[4], 4, -1));

		// A part's window has its own bytes.
		using var part = new XmlWindow(new MemoryStream(), "p", 1);
		Assert.Throws<InvalidOperationException>(() => part.Segment(new byte[4], 4, 0));
		Assert.False(part.HoldsSegments);
	}

	[Fact]
	public void RestartsOverAnotherStreamAtAnOffset()
	{
		var first = new TrickleStream(Utf8("<worksheet><sheetData><row r=\"1\"/>"));
		var scanner = new XmlScanner(new XmlWindow(first, "sheet.xml", 1 << 20, 16));
		Assert.True(scanner.Read());
		Assert.True(scanner.Read());
		Assert.True(scanner.Read());

		// The rest of the part from byte 30, which starts with what a byte order mark starts with: not looked for again.
		var rest = new TrickleStream([0xEF, 0xBB, 0xBF, .. Utf8("<row r=\"2\"/></sheetData></worksheet>")]);
		scanner.Window.Restart(rest, 30);
		scanner.ResetTo(2);
		Assert.Equal(0, scanner.Window.End);
		Assert.False(scanner.Window.IsEof);
		Assert.Equal(30, scanner.Window.Offset(0));

		var tokens = new List<string>();
		while (scanner.Read())
		{
			tokens.Add($"{scanner.Kind} {scanner.Depth} at {scanner.Window.Offset(scanner.TokenStart)}");
		}

		Assert.Equal(["Text 2 at 30", "StartTag 2 at 33", "EndTag 1 at 45", "EndTag 0 at 57"], tokens);

		// The old stream is the caller's; the window owns the new one.
		scanner.Dispose();
		Assert.False(first.Disposed);
		Assert.True(rest.Disposed);
	}

	[Fact]
	public void DoesntLookForAByteOrderMarkAfterARestart()
	{
		// Even at the part's first byte, for a window that never read: what follows is the part's from that offset.
		var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'<', (byte)'a', (byte)'/', (byte)'>' };
		using var scanner = new XmlScanner(new XmlWindow(new MemoryStream(), "p", 1 << 20));
		scanner.Window.Restart(new MemoryStream(bytes), 0);
		scanner.ResetTo(1);
		Assert.True(scanner.Read());
		Assert.Equal(XmlTokenKind.Text, scanner.Kind);
		Assert.Equal(0, scanner.Window.Offset(scanner.TokenStart));
		Assert.Equal(3, scanner.Content.Length);
	}

	[Fact]
	public void RestartChecksItsArguments()
	{
		using var window = new XmlWindow(new MemoryStream(), "p", 1);
		Assert.Throws<ArgumentNullException>(() => window.Restart(null!, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => window.Restart(new MemoryStream(), -1));
		window.Dispose();
		Assert.Throws<ObjectDisposedException>(() => window.Restart(new MemoryStream(), 0));
	}

	[Fact]
	public void ResetToStartsInsideTheRootWithNothingFromBefore()
	{
		var xml = Utf8("<worksheet><sheetData><row r=\"1\" a=\"b\"/>");
		using var scanner = new XmlScanner(new XmlWindow(new MemoryStream(xml), "sheet.xml", 1 << 20));
		scanner.Read();
		scanner.Read();
		scanner.Read();
		var attributes = scanner.Attributes;
		var saved = scanner.Save();

		// The rest of the part, from the row's end: the root is open, and ends once.
		scanner.Window.Restart(new MemoryStream(Utf8("</sheetData></worksheet>")), xml.Length);
		scanner.ResetTo(2);
		Assert.Equal(0, scanner.Position);
		Assert.Equal(2, scanner.Depth);
		Assert.Equal(XmlTokenKind.None, scanner.Kind);
		Assert.Equal(0, scanner.QuoteCount);
		Assert.Throws<InvalidOperationException>(() => attributes.MoveNext());
		Assert.Throws<InvalidOperationException>(() => scanner.Restore(saved));
		Assert.Throws<ArgumentOutOfRangeException>(() => scanner.ResetTo(0));

		var tokens = new List<string>();
		while (scanner.Read())
		{
			tokens.Add($"{scanner.Kind} {scanner.Depth}");
		}

		Assert.Equal(["EndTag 1", "EndTag 0"], tokens);
		Assert.Equal(XmlTokenKind.End, scanner.Kind);

		// After the root's end, another root is a second one.
		var second = Utf8("</sheetData></worksheet><x/>");
		scanner.Window.Restart(new MemoryStream(second), 0);
		scanner.ResetTo(2);
		Assert.True(scanner.Read());
		Assert.True(scanner.Read());
		var e = Assert.Throws<InvalidDataException>(() => scanner.Read());
		Assert.EndsWith("it has a second root element.", e.Message);
	}

	[Fact]
	public void ResetToForgetsARawSkipInProgress()
	{
		using var scanner = new XmlScanner(XmlWindow.ForSegments("sheet.xml", 1 << 20));

		// A raw skip of a row, cut short inside a comment: it would search for the comment's end after a refill.
		var cut = Utf8("<row r=\"1\"><!-- a <row");
		scanner.Window.Segment(cut, cut.Length, 0);
		scanner.ResetTo(2);
		Assert.True(scanner.TryRead());
		Assert.False(scanner.TrySkipRaw("row"u8, 2));

		var next = Utf8("<row r=\"2\"><c/></row>");
		scanner.Window.Segment(next, next.Length, 100);
		scanner.ResetTo(2);
		Assert.True(scanner.TryRead());
		Assert.True(scanner.TrySkipRaw("row"u8, 2));
		Assert.Equal(2, scanner.Depth);
		Assert.Equal(next.Length, scanner.Position);
	}

	[Fact]
	public void TellsAWindowThatTranscodesUtf16()
	{
		var text = "<a>é</a>";
		using var utf16 = new XmlWindow(new MemoryStream([0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)]), "p", 1 << 20);
		Assert.False(utf16.Transcodes);
		utf16.Read();
		utf16.Read();
		Assert.True(utf16.Transcodes);

		using var utf8 = new XmlWindow(new MemoryStream(Utf8(text)), "p", 1 << 20);
		utf8.Read();
		utf8.Read();
		Assert.False(utf8.Transcodes);
		using var segments = XmlWindow.ForSegments("p", 1);
		Assert.False(segments.Transcodes);
	}

	/// <summary>The UTF-8 bytes of text.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
