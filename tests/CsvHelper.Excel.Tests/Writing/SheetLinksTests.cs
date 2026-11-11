using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A sheet's links (<see cref="SheetLinks"/>): which URIs each policy makes links, their targets as Excel opens them
/// (checked through COM: <c>Csv\_bench\results\phase5\step5</c>), the relationships numbered in the order of first use, a
/// discarded record's links dropped, and Excel's limits.
/// </summary>
public class SheetLinksTests
{
	/// <summary>A letter past ASCII: e with an acute accent.</summary>
	private static readonly string E = ((char)0xE9).ToString();

	/// <summary>A backslash.</summary>
	private static readonly string Backslash = ((char)92).ToString();

	/// <summary>Texts, a policy, and the target the text is, or <see langword="null"/> for none.</summary>
	public static TheoryData<string, ExcelLinks, string?> Targets => new()
	{
		{ "https://example.com/a?b=1&c=2#frag", ExcelLinks.Web, "https://example.com/a?b=1&c=2#frag" },
		{ "HTTP://Example.COM", ExcelLinks.Web, "http://example.com/" },
		{ "mailto:someone@example.com?subject=Hi there", ExcelLinks.Web, "mailto:someone@example.com?subject=Hi%20there" },
		{ "  https://example.com/  ", ExcelLinks.Web, "https://example.com/" },
		{ "https://example.com/a%zz", ExcelLinks.Web, "https://example.com/a%25zz" },
		{ "https://example.com/a%2Fb%23c", ExcelLinks.Web, "https://example.com/a%2Fb%23c" },
		{ "file:///C:/Temp/report.txt", ExcelLinks.Web, null },
		{ "file:///C:/Temp/report.txt", ExcelLinks.All, "file:///C:/Temp/report.txt" },
		{ Backslash + Backslash + "server" + Backslash + "share" + Backslash + "a b.txt", ExcelLinks.All, "file://server/share/a%20b.txt" },
		{ "javascript:alert(1)", ExcelLinks.Web, null },
		{ "javascript:alert(1)", ExcelLinks.All, "javascript:alert(1)" },
		{ "ftp://example.com/x", ExcelLinks.Web, null },
		{ "docs/readme.txt", ExcelLinks.Web, null },
		{ "docs/readme.txt", ExcelLinks.All, "docs/readme.txt" },
		{ "docs" + Backslash + "a b.txt", ExcelLinks.All, "docs" + Backslash + "a b.txt" },
		{ "#Sheet2!A1", ExcelLinks.All, "#Sheet2!A1" },
		{ "https://example.com/", ExcelLinks.None, null },
		{ "", ExcelLinks.All, null },
		{ "   ", ExcelLinks.All, null },
	};

	[Theory]
	[MemberData(nameof(Targets))]
	public void TakesTheUrisItsPolicyTakes(string text, ExcelLinks policy, string? target)
	{
		Assert.Equal(target, SheetLinks.Target(text, policy));
	}

	[Fact]
	public void WritesEscapesOfLettersPastAsciiAsTheLetters()
	{
		// Excel reads a file link's %C3%A9 as two characters, and keeps a relative one's as written.
		var emoji = char.ConvertFromUtf32(0x1F600);
		var resume = "https://example.com/r" + E + "sum" + E + "?q=" + E + "#" + E;
		Assert.Equal(resume, SheetLinks.Target(resume, ExcelLinks.Web));
		Assert.Equal("file:///C:/Temp/r" + E + "port.txt", SheetLinks.Target("file:///C:/Temp/r%C3%A9port.txt", ExcelLinks.All));
		Assert.Equal("file:///C:/Temp/r" + E + "port.txt", SheetLinks.Target("C:" + Backslash + "Temp" + Backslash + "r" + E + "port.txt", ExcelLinks.All));
		Assert.Equal("mailto:jos" + E + "@example.com", SheetLinks.Target("mailto:jos" + E + "@example.com", ExcelLinks.Web));
		Assert.Equal("a" + E + "b" + emoji, SheetLinks.Target("a%C3%a9b%F0%9F%98%80", ExcelLinks.All));

		// ASCII's escapes stay, and so do bytes that aren't a character's UTF-8 (cut short, a bad second byte, an overlong
		// form, a surrogate, past U+10FFFF), and characters that don't show as themselves (a direction's mark, a C1 control,
		// a line separator, non-characters, private use).
		foreach (var kept in new[] { "a%20b", "a%2Fb", "a%C3", "a%C3%A", "a%C3%28", "a%C0%AF", "a%ED%A0%80", "a%F4%90%80%80", "a%E2%80%8E", "a%C2%85", "a%E2%80%A8", "a%EF%BF%BE", "a%EF%B7%90", "a%EE%80%80", "a%" })
		{
			Assert.Equal(kept, SheetLinks.Target(kept, ExcelLinks.All));
		}
	}

	[Fact]
	public void EscapesTheControlsOfARelativeTarget()
	{
		Assert.Equal("a%09b%7Fc%EF%BF%BEd", SheetLinks.Target("a\tb" + (char)0x7F + "c" + (char)0xFFFE + "d", ExcelLinks.All));
	}

	[Fact]
	public void LeavesOutATargetLongerThanExcelOpens()
	{
		var longest = "https://example.com/" + new string('a', SheetLinks.MaxTargetLength - 20);
		Assert.Equal(longest, SheetLinks.Target(longest, ExcelLinks.Web));
		Assert.Null(SheetLinks.Target(longest + "b", ExcelLinks.Web));

		// A fragment counts, and an escape as written.
		Assert.Null(SheetLinks.Target(longest.Substring(0, longest.Length - 2) + "#ab", ExcelLinks.Web));
		Assert.Null(SheetLinks.Target(longest.Substring(0, longest.Length - 2) + "%20", ExcelLinks.Web));

		// A letter past ASCII counts once, as the target has it, though AbsoluteUri escapes it in six characters.
		var letters = "https://example.com/" + string.Concat(Enumerable.Repeat(E, SheetLinks.MaxTargetLength - 20));
		Assert.Equal(letters, SheetLinks.Target(letters, ExcelLinks.Web));
		Assert.Null(SheetLinks.Target(letters + E, ExcelLinks.Web));
	}

	[Fact]
	public void NumbersTheRelationshipsByFirstUse()
	{
		var links = new SheetLinks();
		links.Add(2, 1, "https://a.example/");
		links.Add(3, 1, "https://b.example/");
		links.Add(3, 27, "https://a.example/");
		links.EndRecord();
		using var xml = new XmlOutput(256);

		links.WriteHyperlinks(xml, 2);

		Assert.Equal("<hyperlinks><hyperlink ref=\"B2\" r:id=\"rId2\"/><hyperlink ref=\"B3\" r:id=\"rId3\"/><hyperlink ref=\"AB3\" r:id=\"rId2\"/></hyperlinks>", Text(xml));
		Assert.Equal(2, links.Relationships);
		xml.Truncate(0);
		links.AppendRelationship(xml, 0);
		links.AppendRelationship(xml, 1);
		Assert.Equal(Relationship("rId2", "https://a.example/") + Relationship("rId3", "https://b.example/"), Text(xml));
	}

	[Fact]
	public void DropsTheLinksOfADiscardedRecord()
	{
		var links = new SheetLinks();
		links.Add(1, 0, "https://kept.example/");
		links.EndRecord();
		links.Add(2, 0, "https://dropped.example/");
		links.Add(2, 1, "https://kept.example/");
		links.DiscardRecord();
		links.Add(2, 0, "https://later.example/");
		links.EndRecord();
		using var xml = new XmlOutput(256);

		links.WriteHyperlinks(xml, 1);

		// The dropped target has no relationship.
		Assert.Equal(2, links.Count);
		Assert.Equal("<hyperlinks><hyperlink ref=\"A1\" r:id=\"rId1\"/><hyperlink ref=\"A2\" r:id=\"rId2\"/></hyperlinks>", Text(xml));
		Assert.Equal(2, links.Relationships);
		xml.Truncate(0);
		links.AppendRelationship(xml, 1);
		Assert.Equal(Relationship("rId2", "https://later.example/"), Text(xml));
	}

	[Fact]
	public void WritesATargetAsXmlText()
	{
		// A part of relationships reads no _xHHHH_ escapes, so a target's _ stays; XML's own characters are escaped.
		var links = new SheetLinks();
		links.Add(1, 0, SheetLinks.Target("a&b<c>\"d_x0041_", ExcelLinks.All)!);
		using var xml = new XmlOutput(256);
		links.WriteHyperlinks(xml, 1);
		xml.Truncate(0);

		links.AppendRelationship(xml, 0);

		Assert.Equal(Relationship("rId1", "a&amp;b&lt;c&gt;&quot;d_x0041_"), Text(xml));
	}

	[Fact]
	public void IsFullAtExcelsLinks()
	{
		var links = new SheetLinks();
		for (var i = 1; i < SheetLinks.MaxLinks; i++)
		{
			links.Add(i, 0, "https://example.com/");
		}

		Assert.False(links.IsFull);
		links.Add(SheetLinks.MaxLinks, 0, "https://example.com/");
		Assert.True(links.IsFull);
		Assert.Equal(65_530, SheetLinks.MaxLinks);
	}

	/// <summary>A link's relationship, as written.</summary>
	/// <param name="id">Its id.</param>
	/// <param name="target">Its target, as XML.</param>
	/// <returns>The XML.</returns>
	private static string Relationship(string id, string target) =>
		$"<Relationship Id=\"{id}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{target}\" TargetMode=\"External\"/>";

	/// <summary>A buffer's text.</summary>
	/// <param name="xml">The buffer.</param>
	/// <returns>The text.</returns>
	private static string Text(XmlOutput xml) => Encoding.UTF8.GetString(xml.Written.ToArray());
}
