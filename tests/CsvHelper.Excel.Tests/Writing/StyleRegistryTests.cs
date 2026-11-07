using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The cell formats: indexes in the order of first use, built-in ids for Excel's codes, custom ids from 164, the XML of
/// the styles part, and the writer's own formats, which Phase 2's reader must take for dates, times and durations.
/// </summary>
public class StyleRegistryTests
{
	[Fact]
	public void HandsOutIndexesInTheOrderOfFirstUse()
	{
		var styles = new StyleRegistry();

		Assert.Equal(0, styles.IndexOf("General"));
		Assert.Equal(0, styles.IndexOf("general"));
		Assert.Equal(0, styles.IndexOf(""));
		Assert.Equal(1, styles.IndexOf("yyyy-mm-dd"));
		Assert.Equal(2, styles.IndexOf("0.00"));
		Assert.Equal(1, styles.IndexOf("yyyy-mm-dd"));
		Assert.Equal(3, styles.IndexOf("YYYY-MM-DD"));
		Assert.Equal(4, styles.Count);
	}

	[Fact]
	public void WritesBuiltInIdsAndCustomOnes()
	{
		var styles = new StyleRegistry();
		foreach (var code in new[] { "0", "0.00", "#,##0", "#,##0.00", "0%", "0.00%", "0.00E+00", "# ?/?", "# ??/??", "[h]:mm:ss", "##0.0E+0", "@", "yyyy-mm-dd", "m/d/yyyy", "\"a&b\"<0>" })
		{
			styles.IndexOf(code);
		}

		var (numberFormats, cellFormats) = Xml(styles);

		Assert.Equal("<numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd\"/><numFmt numFmtId=\"165\" formatCode=\"m/d/yyyy\"/><numFmt numFmtId=\"166\" formatCode=\"&quot;a&amp;b&quot;&lt;0&gt;\"/></numFmts>", numberFormats);
		var ids = string.Join(",", System.Text.RegularExpressions.Regex.Matches(cellFormats, "numFmtId=\"(\\d+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value));
		Assert.Equal("0,1,2,3,4,9,10,11,12,13,46,48,49,164,165,166", ids);
		Assert.StartsWith("<cellXfs count=\"16\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>", cellFormats);
	}

	[Fact]
	public void WritesNoNumberFormatsWithoutCustomOnes()
	{
		var styles = new StyleRegistry();
		styles.IndexOf(FixedFormat.Duration);

		var (numberFormats, cellFormats) = Xml(styles);

		Assert.Equal("", numberFormats);
		Assert.Equal("<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"46\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>", cellFormats);
	}

	[Fact]
	public void GivesEachFixedFormatOneIndex()
	{
		var styles = new StyleRegistry();

		Assert.Equal(1, styles.IndexOf(FixedFormat.Time));
		Assert.Equal(2, styles.IndexOf(FixedFormat.Date));
		Assert.Equal(1, styles.IndexOf(FixedFormat.Time));
		Assert.Equal(2, styles.IndexOf("yyyy-mm-dd"));
		Assert.Equal(3, styles.Count);
	}

	[Theory]
	[InlineData("Date", "yyyy-mm-dd", "DateNumber")]
	[InlineData("DateTime", "yyyy-mm-dd hh:mm:ss", "DateNumber")]
	[InlineData("DateTimeMilliseconds", "yyyy-mm-dd hh:mm:ss.000", "DateNumber")]
	[InlineData("Time", "hh:mm:ss", "DateNumber")]
	[InlineData("TimeMilliseconds", "hh:mm:ss.000", "DateNumber")]
	[InlineData("Duration", "[h]:mm:ss", "Duration")]
	[InlineData("DurationMilliseconds", "[h]:mm:ss.000", "Duration")]
	public void WritesFormatsTheReaderTakesForDatesAndDurations(string name, string code, string kindName)
	{
		var format = (FixedFormat)Enum.Parse(typeof(FixedFormat), name);
		var kind = (SheetCellKind)Enum.Parse(typeof(SheetCellKind), kindName);
		var styles = new StyleRegistry();
		styles.IndexOf(format);
		var (numberFormats, cellFormats) = Xml(styles);

		Assert.Equal(kind, NumberFormats.Classify(code));
		if (format == FixedFormat.Duration)
		{
			Assert.Equal(SheetCellKind.Duration, NumberFormats.BuiltIn(46));
			Assert.Contains("numFmtId=\"46\"", cellFormats);
		}
		else
		{
			Assert.Contains($"formatCode=\"{code}\"", numberFormats);
		}
	}

	/// <summary>The styles part's XML that the registry writes.</summary>
	/// <param name="styles">The registry.</param>
	/// <returns>Its number formats and its cell formats.</returns>
	private static (string NumberFormats, string CellFormats) Xml(StyleRegistry styles)
	{
		using var numberFormats = new XmlOutput(64);
		styles.WriteNumberFormats(numberFormats);
		using var cellFormats = new XmlOutput(64);
		styles.WriteCellFormats(cellFormats);

		return (Encoding.UTF8.GetString(numberFormats.Written.ToArray()), Encoding.UTF8.GetString(cellFormats.Written.ToArray()));
	}
}
