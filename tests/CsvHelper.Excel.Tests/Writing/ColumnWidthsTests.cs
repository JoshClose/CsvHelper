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
}
