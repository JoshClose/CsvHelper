using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>Decoding the text of XML parts: references, line ends, attribute values, CDATA and <c>_xHHHH_</c> escapes.</summary>
public class XmlTextTests
{
	private static readonly string Grinning = char.ConvertFromUtf32(0x1F600);
	private static readonly string MaxCodePoint = char.ConvertFromUtf32(0x10FFFF);
	private static readonly string Replacement = ((char)0xFFFD).ToString();

	[Theory]
	[InlineData("", "")]
	[InlineData("plain text", "plain text")]
	[InlineData("a &lt; b &gt; c &amp; d &quot;e&quot; &apos;f&apos;", "a < b > c & d \"e\" 'f'")]
	[InlineData("&amp;amp;", "&amp;")]
	[InlineData("&#65;&#x42;&#x0043;&#0000068;", "ABCD")]
	[InlineData("&#xe9;&#xE9;&#233;", "ééé")]
	[InlineData("&#x20AC;", "€")]
	[InlineData("&#9;&#10;&#xA;", "\t\n\n")]
	[InlineData("a\r\nb\rc\nd\r\r\ne", "a\nb\nc\nd\n\ne")]
	[InlineData("\r", "\n")]
	[InlineData("a&#13;&#10;b&#xD;c", "a\r\nb\rc")]
	[InlineData("&#13;\r\n", "\r\n")]
	[InlineData("_x0041_", "_x0041_")]
	[InlineData("é日本", "é日本")]
	public void DecodesText(string xml, string expected)
	{
		Assert.Equal(expected, DecodeText(xml));
	}

	[Fact]
	public void DecodesReferencesToSupplementaryCharacters()
	{
		Assert.Equal(Grinning + Grinning, DecodeText("&#x1F600;&#128512;"));
		Assert.Equal(MaxCodePoint, DecodeText("&#x10FFFF;"));
		Assert.Equal(((char)0xD7FF).ToString() + (char)0xE000 + (char)0xFFFD + char.ConvertFromUtf32(0x10000), DecodeText("&#xD7FF;&#xE000;&#xFFFD;&#x10000;"));
	}

	[Theory]
	[InlineData("&foo;", 0)]
	[InlineData("a&nbsp;b", 1)]
	[InlineData("&amp", 0)]
	[InlineData("& amp;", 0)]
	[InlineData("&;", 0)]
	[InlineData("&", 0)]
	[InlineData("ok &AMP;", 3)]
	[InlineData("&#;", 0)]
	[InlineData("&#x;", 0)]
	[InlineData("&#X41;", 0)]
	[InlineData("&#12a;", 0)]
	[InlineData("&#65", 0)]
	[InlineData("&#0;", 0)]
	[InlineData("&#1;", 0)]
	[InlineData("&#x1F;", 0)]
	[InlineData("&#xD800;", 0)]
	[InlineData("&#xDFFF;", 0)]
	[InlineData("&#xFFFE;", 0)]
	[InlineData("&#xFFFF;", 0)]
	[InlineData("&#x110000;", 0)]
	[InlineData("&#99999999999999999999999;", 0)]
	[InlineData("&#xFFFFFFFFFFFFFFFF41;", 0)]
	[InlineData("ab&amp;cd&bad;", 9)]
	public void RejectsReferencesItCantDecode(string xml, int index)
	{
		var source = Encoding.UTF8.GetBytes(xml);
		var written = XmlText.DecodeText(source, new byte[source.Length]);

		Assert.True(written < 0);
		Assert.Equal(index, ~written);
	}

	[Theory]
	[InlineData("plain", "plain")]
	[InlineData("a\tb\nc\rd\r\ne", "a b c d e")]
	[InlineData("\r\n\r\n", "  ")]
	[InlineData("a&#9;b&#10;c&#13;d", "a\tb\nc\rd")]
	[InlineData("&quot;q&quot; &apos;", "\"q\" '")]
	[InlineData("x>y", "x>y")]
	public void DecodesAttributeValues(string xml, string expected)
	{
		var source = Encoding.UTF8.GetBytes(xml);
		var destination = new byte[source.Length];
		var written = XmlText.DecodeAttribute(source, destination);

		Assert.Equal(expected, Encoding.UTF8.GetString(destination, 0, written));
	}

	[Fact]
	public void RejectsAttributeReferencesItCantDecode()
	{
		var source = "a\tb&lt&gt;"u8.ToArray();

		Assert.Equal(3, ~XmlText.DecodeAttribute(source, new byte[source.Length]));
	}

	[Theory]
	[InlineData("", "")]
	[InlineData("&amp; <b> &#65; ]] ]", "&amp; <b> &#65; ]] ]")]
	[InlineData("a\r\nb\rc\r\r\n", "a\nb\nc\n\n")]
	public void DecodesCData(string xml, string expected)
	{
		var source = Encoding.UTF8.GetBytes(xml);
		var destination = new byte[source.Length];
		var written = XmlText.DecodeCData(source, destination);

		Assert.Equal(expected, Encoding.UTF8.GetString(destination, 0, written));
	}

	[Theory]
	[InlineData("", "")]
	[InlineData("no escapes", "no escapes")]
	[InlineData("_x0041_", "A")]
	[InlineData("a_x0041_b_x0042_c", "aAbBc")]
	[InlineData("_x00e9__x00E9_", "éé")]
	[InlineData("_x65e5__x672C_", "日本")]
	[InlineData("_x005F_", "_")]
	[InlineData("_x005F_x0041_", "_x0041_")]
	[InlineData("_x005f_x005F_", "_x005F_")]
	[InlineData("__x0041_", "_A")]
	[InlineData("_x0041__", "A_")]
	[InlineData("___", "___")]
	[InlineData("_x0041", "_x0041")]
	[InlineData("_x004_", "_x004_")]
	[InlineData("_x00G1_", "_x00G1_")]
	[InlineData("_X0041_", "_X0041_")]
	[InlineData("_y0041_", "_y0041_")]
	[InlineData("_x00411_", "_x00411_")]
	[InlineData("a_b_c", "a_b_c")]
	[InlineData("_x000D__x000A__x0009_", "\r\n\t")]
	[InlineData("_x0000_", "\0")]
	public void DecodesEscapes(string text, string expected)
	{
		Assert.Equal(expected, DecodeEscapes(text));
	}

	[Fact]
	public void DecodesEscapedSurrogatePairsAsOneCharacter()
	{
		Assert.Equal(Grinning, DecodeEscapes("_xD83D__xDE00_"));
		Assert.Equal("a" + Grinning + "b", DecodeEscapes("a_xd83d__xde00_b"));
		Assert.Equal(MaxCodePoint, DecodeEscapes("_xDBFF__xDFFF_"));
	}

	[Theory]
	[InlineData("_xD83D_", "R")]
	[InlineData("_xDE00_", "R")]
	[InlineData("_xD83D_x", "Rx")]
	[InlineData("_xD83D__x0041_", "RA")]
	[InlineData("_xD83D__xD83D__xDE00_", "RG")]
	[InlineData("_xDE00__xD83D_", "RR")]
	public void ReplacesLoneEscapedSurrogates(string text, string expected)
	{
		// R stands for U+FFFD, and G for U+1F600.
		Assert.Equal(expected.Replace("R", Replacement).Replace("G", Grinning), DecodeEscapes(text));
	}

	[Fact]
	public void DecodesEscapesInPlace()
	{
		var text = Encoding.UTF8.GetBytes("x_x0041__xD83D__xDE00__x00E9_y");
		var length = XmlText.DecodeEscapes(text);

		Assert.Equal("xA" + Grinning + "éy", Encoding.UTF8.GetString(text, 0, length));
	}

	[Fact]
	public void DecodedTextIsNeverLongerThanItsSource()
	{
		var pieces = new[]
		{
			"a", "é", "日", Grinning, "&amp;", "&lt;", "&#65;", "&#xE9;", "&#x20AC;", "&#x1F600;", "&#128512;", "&#x10FFFF;",
			"&#9;", "\r", "\n", "\r\n", "\t", "_x0041_", "_xD83D__xDE00_", "_xD83D_", "_x005F_", "_", "&bad;", "&#0;", "&",
		};
		var random = new Random(1);
		for (var i = 0; i < 2000; i++)
		{
			var builder = new StringBuilder();
			var count = random.Next(1, 12);
			for (var j = 0; j < count; j++)
			{
				builder.Append(pieces[random.Next(pieces.Length)]);
			}

			var source = Encoding.UTF8.GetBytes(builder.ToString());
			// Exactly the source's length: a decoder that needed more would throw.
			var text = XmlText.DecodeText(source, new byte[source.Length]);
			var attribute = XmlText.DecodeAttribute(source, new byte[source.Length]);
			var cdata = XmlText.DecodeCData(source, new byte[source.Length]);
			var escapes = XmlText.DecodeEscapes(source.ToArray());

			Assert.True(text <= source.Length && attribute <= source.Length && cdata <= source.Length && escapes <= source.Length);
		}
	}

	[Theory]
	[InlineData(0x9, true)]
	[InlineData(0xA, true)]
	[InlineData(0xD, true)]
	[InlineData(0x20, true)]
	[InlineData(0xD7FF, true)]
	[InlineData(0xE000, true)]
	[InlineData(0xFFFD, true)]
	[InlineData(0x10000, true)]
	[InlineData(0x10FFFF, true)]
	[InlineData(0x0, false)]
	[InlineData(0x8, false)]
	[InlineData(0xB, false)]
	[InlineData(0x1F, false)]
	[InlineData(0xD800, false)]
	[InlineData(0xDFFF, false)]
	[InlineData(0xFFFE, false)]
	[InlineData(0xFFFF, false)]
	[InlineData(0x110000, false)]
	[InlineData(-1, false)]
	public void KnowsXmlCharacters(int codePoint, bool expected)
	{
		Assert.Equal(expected, XmlText.IsXmlChar(codePoint));
	}

	/// <summary>Decodes character data with <see cref="XmlText.DecodeText"/>, into exactly as many bytes as it has.</summary>
	/// <param name="xml">The character data.</param>
	/// <returns>The decoded text.</returns>
	private static string DecodeText(string xml)
	{
		var source = Encoding.UTF8.GetBytes(xml);
		var destination = new byte[source.Length];
		var written = XmlText.DecodeText(source, destination);
		Assert.True(written >= 0, $"Can't decode at {~written}.");

		return Encoding.UTF8.GetString(destination, 0, written);
	}

	/// <summary>Decodes the escapes of a text with <see cref="XmlText.DecodeEscapes"/>.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The decoded text.</returns>
	private static string DecodeEscapes(string text)
	{
		var bytes = Encoding.UTF8.GetBytes(text);
		var length = XmlText.DecodeEscapes(bytes);

		return Encoding.UTF8.GetString(bytes, 0, length);
	}
}
