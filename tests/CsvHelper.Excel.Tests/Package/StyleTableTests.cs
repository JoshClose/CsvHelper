namespace CsvHelper.Excel.Tests.Package;

/// <summary>The styles part: the number format of each cell format, the custom formats, and the number kind of each style.</summary>
public class StyleTableTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsTheNumberFormatOfEachCellFormat(bool async)
	{
		var styles = await TestPackage.Workbook("Data").LoadStylesAsync(async);

		// cellXfs: General, 14 (a built-in date) and 164; the cellStyleXfs' xf is not a cell format.
		Assert.Equal(3, styles.Count);
		Assert.Equal([0, 14, 164], Enumerable.Range(0, 3).Select(styles.NumberFormatId));
		Assert.Equal("yyyy\\-mm\\-dd", styles.FormatCode(164));
		Assert.Null(styles.FormatCode(14));
		Assert.Null(styles.FormatCode(165));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesAStyleOutOfRangeTheFirstCellFormat(bool async)
	{
		var styles = await TestPackage.Workbook("Data")
			.Replace("xl/styles.xml", "<xf numFmtId=\"0\" fontId=\"0\" xfId=\"0\"/>", "<xf numFmtId=\"2\"/>")
			.LoadStylesAsync(async);

		Assert.Equal(2, styles.NumberFormatId(0));
		Assert.Equal(2, styles.NumberFormatId(3));
		Assert.Equal(2, styles.NumberFormatId(-1));
		Assert.Equal(2, styles.NumberFormatId(int.MaxValue));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesEveryStyleTheNumberKindForNow(bool async)
	{
		var styles = await TestPackage.Workbook("Data").LoadStylesAsync(async);

		Assert.All(new[] { 0, 1, 2, 3, -1 }, s => Assert.Equal(SheetCellKind.Number, styles.NumberKind(s)));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task HasOnlyGeneralWithoutAStylesPart(bool async)
	{
		var none = await TestPackage.Workbook("Data")
			.Remove("xl/styles.xml")
			.Replace("xl/_rels/workbook.xml.rels", TestPackage.RelationshipType("styles"), TestPackage.RelationshipType("theme"))
			.LoadStylesAsync(async);
		var missing = await TestPackage.Workbook("Data").Remove("xl/styles.xml").LoadStylesAsync(async);

		foreach (var styles in new[] { none, missing })
		{
			Assert.Same(StyleTable.Empty, styles);
			Assert.Equal(0, styles.Count);
			Assert.Equal(0, styles.NumberFormatId(0));
			Assert.Equal(0, styles.NumberFormatId(5));
			Assert.Equal(SheetCellKind.Number, styles.NumberKind(0));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsFormatCodesAndDefaults(bool async)
	{
		var styles = await TestPackage.Workbook("Data")
			.Set(
				"xl/styles.xml",
				$"<x:styleSheet xmlns:x=\"{TestPackage.MainNamespace}\">" +
				"<x:numFmts><x:numFmt numFmtId=\"164\" formatCode=\"&quot;$&quot;#,##0.00\"/><x:numFmt numFmtId=\"164\" formatCode=\"second\"/>" +
				"<x:numFmt formatCode=\"no id\"/><x:numFmt numFmtId=\" 165 \" formatCode=\"[Red]0%\"/></x:numFmts>" +
				"<x:cellXfs><x:xf/><x:xf numFmtId=\"165\"><x:alignment horizontal=\"left\"/></x:xf><x:xf applyNumberFormat=\"1\"></x:xf></x:cellXfs>" +
				"<x:extLst><x:ext><x:xf numFmtId=\"9\"/></x:ext></x:extLst></x:styleSheet>")
			.LoadStylesAsync(async);

		Assert.Equal("\"$\"#,##0.00", styles.FormatCode(164));
		Assert.Equal("[Red]0%", styles.FormatCode(165));
		Assert.Equal([0, 165, 0], Enumerable.Range(0, styles.Count).Select(styles.NumberFormatId));
	}

	[Theory]
	[InlineData("abc", "an integer")]
	[InlineData("-1", "a number format id (0 or more)")]
	[InlineData("99999999999", "an integer")]
	[InlineData("", "an integer")]
	public async Task RejectsNumberFormatIdsThatAreNotNumbers(string id, string what)
	{
		var package = TestPackage.Workbook("Data").Replace("xl/styles.xml", "<xf numFmtId=\"14\"", $"<xf numFmtId=\"{id}\"");

		foreach (var async in new[] { false, true })
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadStylesAsync(async));
			Assert.Matches($@"^The XML of xl/styles\.xml can't be read at byte \d+: the attribute numFmtId=""{System.Text.RegularExpressions.Regex.Escape(id)}"" is not {System.Text.RegularExpressions.Regex.Escape(what)}\.$", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsAStylesPartWithTheWrongRootOrADoctype(bool async)
	{
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => TestPackage.Workbook("Data").Set("xl/styles.xml", "<styles/>").LoadStylesAsync(async));
		Assert.Equal("The XML of xl/styles.xml can't be read at byte 0: its root element is not styleSheet.", e.Message);

		var doctype = TestPackage.Workbook("Data").Edit("xl/styles.xml", xml => xml.Replace(TestPackage.Declaration, TestPackage.Declaration + "<!DOCTYPE s>"));
		e = await Assert.ThrowsAsync<InvalidDataException>(() => doctype.LoadStylesAsync(async));
		Assert.Equal($"The XML of xl/styles.xml can't be read at byte {TestPackage.Declaration.Length}: it has a DOCTYPE, which Office Open XML parts must not have.", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheStylesPartAgainstTheLimit(bool async)
	{
		var package = TestPackage.Workbook("Data").Replace("xl/styles.xml", "</styleSheet>", "<!--" + new string('x', 5000) + "--></styleSheet>");

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadStylesAsync(async, new PartLimits(4000, 1L << 30)));
		Assert.StartsWith("xl/styles.xml is larger than ExcelReaderOptions.MaxPartSize (4000 bytes uncompressed)", e.Message);
	}
}
