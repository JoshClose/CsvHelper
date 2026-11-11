using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A written table (<see cref="TableWriter"/>): its columns' names against the names Excel 365 gave the same headers in
/// <c>ListObjects.Add</c> through COM on 2026-11-10 (<c>Csv\_bench\results\phase5\step4</c>, <c>rename.txt</c> and
/// <c>rename2.txt</c>), and its part's XML.
/// </summary>
public class TableWriterTests
{
	/// <summary>Headers, with <c>|</c> between fields, and the names Excel gave their columns.</summary>
	public static TheoryData<string, string> ExcelNames => new()
	{
		{ "Name|Name|Name", "Name|Name2|Name3" },
		{ "Name|Name|Name2", "Name|Name3|Name2" },
		{ "Name2|Name|Name", "Name2|Name|Name3" },
		{ "Name|name|NAME", "Name|name2|NAME3" },
		{ "Name|Name2|Name", "Name|Name2|Name3" },
		{ "||A", "Column1|Column2|A" },
		{ "A||Column2", "A|Column1|Column2" },
		{ "Column1||", "Column1|Column2|Column3" },
		{ "|Column1|", "Column2|Column1|Column3" },
		{ "A||B|", "A|Column1|B|Column2" },
		{ "Name| Name|Name ", "Name| Name|Name " },
		{ "a\nb|a\nb", "a\nb|a\nb2" },
		{ "Name1|Name|Name", "Name1|Name|Name2" },
		{ "x|x|x2|x2", "x|x3|x2|x24" },
		{ "a|a|b|b", "a|a2|b|b3" },
		{ "a|a||", "a|a2|Column3|Column4" },
		{ "||a|a", "Column1|Column2|a|a3" },
		{ "a|a|a2|a|a", "a|a3|a2|a4|a5" },
		{ "Column1|Column1|", "Column1|Column12|Column3" },
		{ "||Column1", "Column2|Column3|Column1" },
		{ "a|A2|a", "a|A2|a3" },
		{ "b|a|a|b", "b|a|a2|b3" },
		{ "Column|", "Column|Column1" },
		{ " | ", " | 2" },
		{ "2024|2024|2024", "2024|20242|20243" },
		{ "Name|Name|Name|Name|Name|Name|Name|Name|Name|Name|Name|Name", "Name|Name2|Name3|Name4|Name5|Name6|Name7|Name8|Name9|Name10|Name11|Name12" },
	};

	[Theory]
	[MemberData(nameof(ExcelNames))]
	public void NamesColumnsAsExcelDoes(string header, string names) =>
		Assert.Equal(names.Split('|'), TableWriter.ColumnNames(header.Split('|')));

	[Theory]
	[InlineData(251, 252)]
	[InlineData(252, 253)]
	[InlineData(253, 253)]
	[InlineData(254, 253)]
	[InlineData(255, 253)]
	public void CutsALongNameToGiveItANumberAsExcelDoes(int length, int numbered)
	{
		// Excel gives a name it makes with a number 253 characters at most.
		var name = new string('H', length);

		var names = TableWriter.ColumnNames([name, name, name]);

		Assert.Equal(name, names[0]);
		Assert.Equal(new string('H', numbered - 1) + "2", names[1]);
		Assert.Equal(new string('H', numbered - 1) + "3", names[2]);
	}

	[Fact]
	public void TakesNamesThatSheetNamesTakeToBeTheSameAsRepeats()
	{
		// The writer's comparison (SheetNames.Key) folds more than case, so that it never leaves two names Excel takes to be one.
		var names = TableWriter.ColumnNames(["Stra" + (char)0xDF + "e", "STRASSE", "A", "" + (char)0xFF21]);

		Assert.Equal(["Stra" + (char)0xDF + "e", "STRASSE2", "A", (char)0xFF21 + "3"], names);
	}

	[Fact]
	public void DoesntCutASurrogatePair()
	{
		// The cut at 252 characters, before the number, falls between the emoji's halves, so the emoji goes.
		var name = new string('H', 251) + char.ConvertFromUtf32(0x1F600) + "x";

		var names = TableWriter.ColumnNames([name, name]);

		Assert.Equal(new string('H', 251) + "2", names[1]);
	}

	[Fact]
	public void WritesTheTablesPart()
	{
		using var xml = new XmlOutput(256);

		TableWriter.Write(xml, 3, "Q1.Orders", ExcelRange.Parse("A2:C5"), ["Name", "Two\nLines", "a\"b&c"], new ExcelTableOptions());

		Assert.Equal(
			"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
			"<table xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" id=\"3\" name=\"Q1.Orders\" displayName=\"Q1.Orders\" ref=\"A2:C5\" totalsRowShown=\"0\">" +
			"<autoFilter ref=\"A2:C5\"/>" +
			"<tableColumns count=\"3\"><tableColumn id=\"1\" name=\"Name\"/><tableColumn id=\"2\" name=\"Two_x000A_Lines\"/><tableColumn id=\"3\" name=\"a&quot;b&amp;c\"/></tableColumns>" +
			"<tableStyleInfo name=\"TableStyleMedium2\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/></table>",
			Encoding.UTF8.GetString(xml.Written.ToArray()));
	}

	[Fact]
	public void WritesATablesOptions()
	{
		using var xml = new XmlOutput(256);
		using var plain = new XmlOutput(256);

		TableWriter.Write(xml, 1, "Sales", ExcelRange.Parse("A1:XFD1048576"), ["A"], new ExcelTableOptions { Style = "tablestylelight9", BandedRows = false, FilterButtons = false });
		TableWriter.Write(plain, 1, "Sales", ExcelRange.Parse("B1:B2"), ["A"], new ExcelTableOptions { Style = null });

		// The style as Excel writes it, no buttons, no stripes, and a range's corners whatever its size.
		var text = Encoding.UTF8.GetString(xml.Written.ToArray());
		Assert.Contains("ref=\"A1:XFD1048576\" totalsRowShown=\"0\"><tableColumns", text);
		Assert.Contains("<tableStyleInfo name=\"TableStyleLight9\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"0\" showColumnStripes=\"0\"/>", text);
		Assert.Contains("<tableStyleInfo showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/>", Encoding.UTF8.GetString(plain.Written.ToArray()));
	}

	[Fact]
	public void WritesTheSheetsRelationshipToItsTable()
	{
		using var xml = new XmlOutput(256);

		TableWriter.AppendRelationship(xml, 12);

		Assert.Equal(
			"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table12.xml\"/>",
			Encoding.UTF8.GetString(xml.Written.ToArray()));
		Assert.Equal("xl/tables/table12.xml", TableWriter.PartName(12));
	}
}
