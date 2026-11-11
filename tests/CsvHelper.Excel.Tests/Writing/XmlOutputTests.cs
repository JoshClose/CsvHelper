using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The writer's XML text: escapes as Excel writes them, checked by reading them back with the reader's own decoding, UTF-8
/// validation against .NET's strict decoder, and the length Excel counts a cell's text in.
/// </summary>
public class XmlOutputTests
{
	/// <summary>The cases of <see cref="EscapesAsExcelWrites"/>: the text, as code points and strings, and its XML.</summary>
	public static IEnumerable<object[]> Escapes()
	{
		yield return ["plain", "plain"];
		yield return ["a&b<c>d", "a&amp;b&lt;c&gt;d"];
		yield return ["a\"b'c", "a\"b'c"];
		yield return ["tab\tand\nlf", "tab\tand\nlf"];
		yield return ["cr\rlf", "cr_x000D_lf"];
		yield return ["\r\n", "_x000D_\n"];
		yield return [Text(0xFFFE), "_xFFFE_"];
		yield return [Text(0xFFFF), "_xFFFF_"];
		yield return ["a" + Text(0xFFFD) + Text(0xFF21) + Text(0xEFFF), "a" + Text(0xFFFD) + Text(0xFF21) + Text(0xEFFF)];
		yield return ["_x0041_", "_x005F_x0041_"];
		yield return ["_x00e9_", "_x005F_x00e9_"];
		yield return ["__x0041_", "__x005F_x0041_"];
		yield return ["_x0041_x0042_", "_x005F_x0041_x005F_x0042_"];
		yield return ["_x005F_", "_x005F_x005F_"];
		yield return ["_X0041_", "_X0041_"];
		yield return ["_x0041\r", "_x005F_x0041_x000D_"];
		yield return ["_x00e9" + Text(1) + "b", "_x005F_x00e9_x0001_b"];
		yield return ["_x0041" + Text(0xFFFE), "_x005F_x0041_xFFFE_"];
		yield return ["a_x0041" + Text(0xFFFF), "a_x005F_x0041_xFFFF_"];
		yield return ["_x0041\t", "_x0041\t"];
		yield return ["_x0041\n", "_x0041\n"];
		yield return ["_x004\r", "_x004_x000D_"];
		yield return ["_X0041\r", "_X0041_x000D_"];
		yield return ["_x0041" + Text(0xFFFD), "_x0041" + Text(0xFFFD)];
		yield return ["_x0041", "_x0041"];
		yield return ["_x0041a", "_x0041a"];
		yield return ["_x0041a_", "_x0041a_"];
		yield return ["_x41_", "_x41_"];
		yield return ["_x004G_", "_x004G_"];
		yield return ["a_b_c", "a_b_c"];
		yield return [Text(0x1F600), Text(0x1F600)];
	}

	[Theory]
	[MemberData(nameof(Escapes))]
	public void EscapesAsExcelWrites(string text, string xml)
	{
		Assert.Equal(xml, Escaped(text, attribute: false));
		Assert.Equal(text, ReadBack(xml));
	}

	[Fact]
	public void EscapesEveryControlCharacterButTabAndLf()
	{
		for (var c = 0; c < 0x20; c++)
		{
			var text = "a" + (char)c + "b";
			var expected = c is '\t' or '\n' ? text : $"a_x{c:X4}_b";

			Assert.Equal(expected, Escaped(text, attribute: false));
			Assert.Equal(text, ReadBack(expected));
		}
	}

	[Fact]
	public void WritesTheOtherControlCharactersAsTheyAre()
	{
		// XML allows U+007F to U+009F, and Excel reads them as they are.
		var text = Text(0x7F) + Text(0x85) + Text(0x9F);

		Assert.Equal(text, Escaped(text, attribute: false));
	}

	[Theory]
	[InlineData("abc", 3, 3)]
	[InlineData("_x0041_", 13, 13)]
	[InlineData("_x0041", 6, 6)]
	[InlineData("_x0041\r", 19, 19)]
	[InlineData("a&b<c>\"d", 8, 8)]
	[InlineData("\t\n", 2, 14)]
	[InlineData("_x0041\n", 7, 19)]
	[InlineData("a\rb", 9, 9)]
	public void CountsTextAsWritten(string text, int length, int attributeLength)
	{
		// The text as AppendText writes it, once XML's entities are read: in an attribute, tab and LF are escapes.
		Assert.Equal(length, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes(text)));
		Assert.Equal(length, System.Net.WebUtility.HtmlDecode(Escaped(text, attribute: false)).Length);
		Assert.Equal(attributeLength, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes(text), attribute: true));
		Assert.Equal(attributeLength, System.Net.WebUtility.HtmlDecode(Escaped(text, attribute: true)).Length);
	}

	[Fact]
	public void EscapesTabsAndLineBreaksInAttributes()
	{
		// XML reads them back as spaces in an attribute: a table's column name with a line break must keep it, and an _
		// before one, which would start an escape with the line break's, is escaped itself.
		Assert.Equal("a_x0009_b_x000A_c", Escaped("a\tb\nc", attribute: true));
		Assert.Equal("a\tb\nc", Escaped("a\tb\nc", attribute: false));
		Assert.Equal("_x005F_x0041_x000A_", Escaped("_x0041\n", attribute: true));
		Assert.Equal("_x0041\n", Escaped("_x0041\n", attribute: false));
	}

	[Fact]
	public void CountsNonCharactersAndPairsAsWritten()
	{
		Assert.Equal(7, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes(Text(0xFFFE))));
		Assert.Equal(15, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes("a" + Text(0xFFFF) + Text(0xFFFE))));
		Assert.Equal(2, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes(Text(0x1F600))));
		Assert.Equal(20, XmlOutput.WrittenLength(Encoding.UTF8.GetBytes("_x0041" + Text(0xFFFF) + "z")));
	}

	[Fact]
	public void QuotesQuotesInAttributes()
	{
		Assert.Equal("a&quot;b&amp;c_x005F_x0041_", Escaped("a\"b&c_x0041_", attribute: true));
	}

	[Fact]
	public void ReadsBackRandomTextAsWritten()
	{
		// Text made of what the escapes are about: underscores, x, hexadecimal digits, the start of an escape, controls and the rest.
		var pieces = new[] { "_", "x", "X", "0", "4", "1", "F", "f", "e", "G", "_x0041", "_x00e9", "&", "<", ">", "\r", "\n", "\t", " ", Text(1), Text(0x1F), Text(0xFFFE), Text(0xFFFF), Text(0xE9), Text(0x1F600) };
		var random = new Random(4);
		for (var n = 0; n < 20000; n++)
		{
			var text = new StringBuilder();
			for (var i = random.Next(1, 16); i > 0; i--)
			{
				text.Append(pieces[random.Next(pieces.Length)]);
			}

			var value = text.ToString();

			Assert.Equal(value, ReadBack(Escaped(value, attribute: false)));
		}
	}

	[Theory]
	[InlineData(new byte[] { 0x80 })]
	[InlineData(new byte[] { 0xC0, 0x80 })]
	[InlineData(new byte[] { 0xC1, 0xBF })]
	[InlineData(new byte[] { 0xE0, 0x80, 0x80 })]
	[InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
	[InlineData(new byte[] { 0xED, 0xBF, 0xBF })]
	[InlineData(new byte[] { 0xF0, 0x80, 0x80, 0x80 })]
	[InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 })]
	[InlineData(new byte[] { 0xF5, 0x80, 0x80, 0x80 })]
	[InlineData(new byte[] { 0xE2, 0x82 })]
	[InlineData(new byte[] { 0x61, 0xFF })]
	[InlineData(new byte[] { 0xC3 })]
	[InlineData(new byte[] { 0xE2, 0x41, 0x41 })]
	public void FindsInvalidUtf8(byte[] bytes)
	{
		Assert.False(XmlOutput.IsValidUtf8(bytes));
		Assert.True(XmlOutput.IsValidUtf8(XmlOutput.ReplaceInvalidUtf8(bytes)));
		Assert.Contains(Text(0xFFFD), Encoding.UTF8.GetString(XmlOutput.ReplaceInvalidUtf8(bytes)));
	}

	[Fact]
	public void ValidatesUtf8AsDotNetDoes()
	{
		// Random bytes, mostly around the edges of the well-formed sequences, against .NET's strict decoder.
		var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
		var edges = new byte[] { 0x00, 0x41, 0x7F, 0x80, 0x8F, 0x90, 0x9F, 0xA0, 0xBF, 0xC0, 0xC1, 0xC2, 0xDF, 0xE0, 0xE1, 0xEC, 0xED, 0xEE, 0xEF, 0xF0, 0xF1, 0xF3, 0xF4, 0xF5, 0xFF };
		var random = new Random(8);
		for (var n = 0; n < 50000; n++)
		{
			var bytes = new byte[random.Next(1, 7)];
			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] = random.Next(4) == 0 ? (byte)random.Next(256) : edges[random.Next(edges.Length)];
			}

			bool valid;
			try
			{
				strict.GetString(bytes);
				valid = true;
			}
			catch (DecoderFallbackException)
			{
				valid = false;
			}

			Assert.True(valid == XmlOutput.IsValidUtf8(bytes), BitConverter.ToString(bytes));
			if (valid)
			{
				Assert.Equal(Encoding.UTF8.GetCharCount(bytes), XmlOutput.Utf16Length(bytes));
			}
		}
	}

	[Theory]
	[InlineData("abc", 3)]
	[InlineData("é", 1)]
	[InlineData("日本", 2)]
	[InlineData("", 0)]
	public void CountsUtf16CodeUnits(string text, int units)
	{
		Assert.Equal(units, XmlOutput.Utf16Length(Encoding.UTF8.GetBytes(text)));
		Assert.Equal(2, XmlOutput.Utf16Length(Encoding.UTF8.GetBytes(Text(0x1F600))));
	}

	[Theory]
	[InlineData(" a", true)]
	[InlineData("a ", true)]
	[InlineData("\ta", true)]
	[InlineData("a\n", true)]
	[InlineData("\ra", false)]
	[InlineData("a b", false)]
	[InlineData("a", false)]
	public void PreservesWhatExcelWouldTrim(string text, bool preserve)
	{
		Assert.Equal(preserve, XmlOutput.NeedsPreserve(Encoding.UTF8.GetBytes(text)));
	}

	[Fact]
	public void GrowsPastItsCapacity()
	{
		using var output = new XmlOutput(16);
		var text = new string('a', 100000) + "&" + new string('b', 100000);

		output.AppendText(Encoding.UTF8.GetBytes(text));

		Assert.Equal(text.Replace("&", "&amp;"), Encoding.UTF8.GetString(output.Written.ToArray()));
	}

	/// <summary>A code point as a string.</summary>
	/// <param name="codePoint">The code point.</param>
	/// <returns>The string.</returns>
	private static string Text(int codePoint) => codePoint < 0x10000 ? ((char)codePoint).ToString() : char.ConvertFromUtf32(codePoint);

	/// <summary>Text as <see cref="XmlOutput.AppendText"/> escapes it.</summary>
	/// <param name="text">The text.</param>
	/// <param name="attribute">Whether it is an attribute value.</param>
	/// <returns>The XML.</returns>
	private static string Escaped(string text, bool attribute)
	{
		using var output = new XmlOutput(16);
		output.AppendText(Encoding.UTF8.GetBytes(text), attribute);

		return Encoding.UTF8.GetString(output.Written.ToArray());
	}

	/// <summary>XML text read back as CsvHelper.Excel's reader reads it: its references, then its escapes.</summary>
	/// <param name="xml">The XML text.</param>
	/// <returns>The text.</returns>
	private static string ReadBack(string xml)
	{
		var source = Encoding.UTF8.GetBytes(xml);
		var decoded = new byte[source.Length];
		var length = XmlText.DecodeText(source, decoded);
		length = XmlText.DecodeEscapes(decoded.AsSpan(0, length));

		return Encoding.UTF8.GetString(decoded, 0, length);
	}
}
