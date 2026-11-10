namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Columns' widths from text: the pixels of Calibri 11's characters at 96 DPI and Excel's padding, in 256ths of a digit,
/// against what Excel's auto-fit stored at 144 DPI, and no width for text that fits the default column.
/// </summary>
public class ColumnWidthsTests
{
	[Theory]
	// 8 digits of 7.5 pixels and 2 hyphens of 4.73: 69.47 pixels, 70, and 75 with the padding: 10.71 digits of 7.
	[InlineData("2024-12-31", 2743)]
	[InlineData("2024-12-31 23:59:59", 4791)]
	[InlineData("2024-12-31 23:59:59.999", 5779)]
	[InlineData("23:59:59.999", 3109)]
	[InlineData("1087354:30:00", 3511)]
	public void MeasuresNumberText(string text, int in256ths)
	{
		Assert.Equal(in256ths / 256.0, ColumnWidths.Of(text.AsSpan()));
	}

	[Theory]
	[InlineData("2024-12-31", 10.08984375)]
	[InlineData("2024-12-31 23:59:59", 17.81640625)]
	[InlineData("2024-12-31 23:59:59.999", 21.453125)]
	[InlineData("1087354:30:00", 13.0)]
	[InlineData("2400:00:00", 9.90625)]
	public void IsAFewPercentWiderThanExcelsAutoFitAt144Dpi(string text, double excel)
	{
		// Excel's own auto-fit widths of these number cells, stored at 144 DPI (checked on 2026-11-06). 96 DPI needs about
		// 22/21 of them, since a digit is 7.5 pixels of a 7-pixel unit there and 11.25 of 11 at 144 DPI.
		var width = ColumnWidths.Of(text.AsSpan());

		Assert.InRange(width / excel, 1.0, 1.08);
	}

	[Theory]
	[InlineData("")]
	[InlineData("23:59:59")]
	[InlineData("36:00:00")]
	[InlineData("7777777")]
	[InlineData("WWWW")]
	public void GivesNoWidthToTextThatFitsTheDefault(string text)
	{
		Assert.Equal(0, ColumnWidths.Of(text.AsSpan()));
	}

	[Fact]
	public void NeedsAWidthPastTheDefault()
	{
		// The default column is 61 pixels: 7 digits and an apostrophe fit with the padding (52.5 + 3.37 is 56, and 61),
		// and 7 digits and an exclamation mark don't (52.5 + 4.73 is 58, and 63: 9 digits of 7).
		Assert.Equal(0, ColumnWidths.Of("7777777'".AsSpan()));
		Assert.Equal(9.0, ColumnWidths.Of("7777777!".AsSpan()));
	}

	[Fact]
	public void MeasuresOtherCharacters()
	{
		// East Asian characters are 15 pixels, an emoji's pair is taken as one of them, and characters not measured are W's
		// width.
		var wide = new string((char)0x5E74, 4);
		var emoji = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F600), 4));
		var unknown = new string((char)0x0E2A, 5);

		Assert.Equal(Math.Ceiling((60 + 5) * 256.0 / 7) / 256, ColumnWidths.Of(wide.AsSpan()));
		Assert.Equal(ColumnWidths.Of(wide.AsSpan()), ColumnWidths.Of(emoji.AsSpan()));
		Assert.Equal(ColumnWidths.Of("WWWWW".AsSpan()), ColumnWidths.Of(unknown.AsSpan()));
		Assert.Equal(0, ColumnWidths.Of(new string((char)0x00E9, 7).AsSpan()));
		// Four per mille signs: 62.67 pixels, 63 and 68.
		Assert.Equal(Math.Ceiling(68 * 256.0 / 7) / 256, ColumnWidths.Of(new string((char)0x2030, 4).AsSpan()));
	}

	[Fact]
	public void HasTheWritersWidths()
	{
		Assert.Equal(ColumnWidths.Of("1900-01-01".AsSpan()), ColumnWidths.Date);
		Assert.Equal(ColumnWidths.Of("1900-01-01 00:00:00".AsSpan()), ColumnWidths.DateTime);
		Assert.Equal(ColumnWidths.Of("1900-01-01 00:00:00.000".AsSpan()), ColumnWidths.DateTimeMilliseconds);
		Assert.Equal(0, ColumnWidths.Time);
		Assert.Equal(ColumnWidths.Of("00:00:00.000".AsSpan()), ColumnWidths.TimeMilliseconds);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(0.5, 0.85546875)]
	[InlineData(1, 1.7109375)]
	[InlineData(8.43, 9.140625)]
	[InlineData(10, 10.7109375)]
	[InlineData(12.5, 13.2109375)]
	[InlineData(255, 255.7109375)]
	public void WritesCharactersAsExcelWritesThem(double characters, double width) => Assert.Equal(width, ColumnWidths.FromCharacters(characters));

	[Fact]
	public void HasExcelsDefaultWidth() => Assert.Equal(ColumnWidths.Default, ColumnWidths.FromCharacters(8.43));

	[Theory]
	// Bold Calibri 11's widths that differ from regular's (measured on 2026-11-08: 0.525 points more at 144 DPI, 0.7
	// pixels at 96), the Thai ko kai's 1.37 more, and East Asian characters as the bold year sign measured.
	[InlineData('"', 6.133, 6.800)]
	[InlineData('&', 10.233, 10.900)]
	[InlineData(',', 3.367, 4.067)]
	[InlineData('K', 7.500, 8.200)]
	[InlineData('P', 7.500, 8.200)]
	[InlineData('V', 8.200, 8.867)]
	[InlineData('X', 7.500, 8.200)]
	[InlineData('Z', 6.800, 7.500)]
	[InlineData('`', 4.067, 4.733)]
	[InlineData('j', 3.367, 4.067)]
	[InlineData('k', 6.800, 7.500)]
	[InlineData('t', 4.733, 5.433)]
	[InlineData('{', 4.733, 5.433)]
	[InlineData('}', 4.733, 5.433)]
	[InlineData((char)0x00B0, 4.733, 5.433)]
	[InlineData((char)0x2019, 3.367, 4.067)]
	[InlineData((char)0x03B1, 8.200, 8.867)]
	[InlineData((char)0x0436, 10.233, 10.900)]
	[InlineData((char)0x0E01, 8.867, 10.233)]
	[InlineData((char)0x5E74, 15.000, 15.667)]
	// The others are regular's.
	[InlineData('a', 7.500, 7.500)]
	[InlineData('W', 13.633, 13.633)]
	[InlineData('0', 7.500, 7.500)]
	[InlineData((char)0x00E9, 7.500, 7.500)]
	public void MeasuresBoldCalibri(char c, double regular, double bold)
	{
		Assert.Equal(regular, ColumnWidths.CharPixels(c, bold: false));
		Assert.Equal(bold, ColumnWidths.CharPixels(c, bold: true));
		Assert.Equal(bold * 3, ColumnWidths.TextPixels(new string(c, 3).AsSpan(), bold: true), 9);
	}

	[Fact]
	public void MeasuresLineFeedsAndTabs()
	{
		// Measured on 2026-11-09 in cells that don't wrap: a line feed 6 points at 144 DPI, a tab 5.5, a CR nothing.
		Assert.Equal(8.0, ColumnWidths.CharPixels('\n', bold: false));
		Assert.Equal(7.333, ColumnWidths.CharPixels('\t', bold: false));
		Assert.Equal(0, ColumnWidths.CharPixels('\r', bold: true));
	}

	[Theory]
	// A text's pixels, rounded up, and the padding, in 256ths of a digit, narrower than the default column too; Phase 4's
	// width only past 56 pixels, which the default column fits with room to spare (61 of its 64).
	[InlineData(7.5, 476)]
	[InlineData(19.067, 915)]
	[InlineData(56, 2231)]
	[InlineData(56.1, 2268)]
	[InlineData(0, 183)]
	public void FitsTextAsExcelsAutoFit(double pixels, int in256ths)
	{
		Assert.Equal(in256ths / 256.0, ColumnWidths.Fit(pixels));
		Assert.Equal(pixels > 56 ? ColumnWidths.Fit(pixels) : 0, ColumnWidths.OfPixels(pixels));
	}
}
