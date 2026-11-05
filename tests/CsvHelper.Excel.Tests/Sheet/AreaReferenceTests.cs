using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// <see cref="AreaReference"/>: blocks with a sheet's name in front, as ranges and defined names write them, in the forms
/// Excel reads (checked through COM on 2026-11-04), and what is not one block.
/// </summary>
public class AreaReferenceTests
{
	[Theory]
	[InlineData("Orders!B5:F200", "Orders", "B5:F200")]
	[InlineData("Messy!$B$4:$F$14", "Messy", "B4:F14")]
	[InlineData("'Q1 Orders'!$B$5", "Q1 Orders", "B5")]
	[InlineData("'Bob''s'!A1", "Bob's", "A1")]
	[InlineData("'''Quoted'''!A1", "'Quoted'", "A1")]
	[InlineData("'Hi!'!A1:B2", "Hi!", "A1:B2")]
	[InlineData("'Hidden All'!$A$1:$A$6", "Hidden All", "A1:A6")]
	[InlineData(" S!$A$1 ", "S", "A1")]
	[InlineData("s!$a$1:$b$2", "s", "A1:B2")]
	[InlineData("S!$B$2:$A$1", "S", "A1:B2")]
	[InlineData("S!$A1:B$2", "S", "A1:B2")]
	[InlineData("S!A:A", "S", "A:A")]
	[InlineData("Messy!$B:$C", "Messy", "B:C")]
	[InlineData("Messy!$4:$4", "Messy", "4:4")]
	[InlineData("Messy!B2", "Messy", "B2")]
	[InlineData("S!XFD1048576", "S", "XFD1048576")]
	[InlineData("日本語!A1", "日本語", "A1")]
	[InlineData("Sheet.1!A1", "Sheet.1", "A1")]
	[InlineData("_x!A1", "_x", "A1")]
	[InlineData("B5:F200", null, "B5:F200")]
	[InlineData("$B$5", null, "B5")]
	public void ReadsABlockWithItsSheet(string text, string? sheet, string area)
	{
		Assert.True(AreaReference.TryParse(text.AsSpan(), out var name, out var range));

		Assert.Equal(sheet, name);
		Assert.Equal(area, range.ToString());
	}

	[Theory]
	[InlineData("Messy!$B$5,Messy!$C$6")]
	[InlineData("S!$A$1:$B$2 S!$B$1:$B$3")]
	[InlineData("(S!$A$1:$B$2)")]
	[InlineData("Messy:Tables!$A$1")]
	[InlineData("[1]Sheet1!A1")]
	[InlineData("'[1]Sheet1'!A1:B2!C3")]
	[InlineData("#REF!")]
	[InlineData("#REF!$A$1:$B$2")]
	[InlineData("Sheet1!#REF!")]
	[InlineData("0.05")]
	[InlineData("OFFSET(Messy!$B$4,0,0,COUNTA(Messy!$B:$B),5)")]
	[InlineData("Sales[Qty]")]
	[InlineData("=S!A1")]
	[InlineData("''!A1")]
	[InlineData("'S!A1")]
	[InlineData("'S'A1")]
	[InlineData("'S'")]
	[InlineData("!A1")]
	[InlineData("Q1 Orders!A1")]
	[InlineData("S!")]
	[InlineData("S!A1!B2")]
	[InlineData("S!A0")]
	[InlineData("\"S\"!A1")]
	[InlineData("")]
	public void RejectsWhatIsNotOneBlock(string text)
	{
		Assert.False(AreaReference.TryParse(text.AsSpan(), out _, out _));
	}

	[Theory]
	[InlineData("A1:D10", "A1:D10")]
	[InlineData("  A1  ", "A1")]
	[InlineData("a20:d23", "A20:D23")]
	public void ReadsUtf8(string text, string area)
	{
		Assert.True(AreaReference.TryParseArea(Encoding.UTF8.GetBytes(text), out var range));
		Assert.Equal(area, range.ToString());
	}

	[Fact]
	public void ReadsLongUtf8OnlyWhenItIsWhiteSpaceAround()
	{
		var padded = new string(' ', 100) + "B2:C3" + new string('\n', 100);
		Assert.True(AreaReference.TryParseArea(Encoding.UTF8.GetBytes(padded), out var range));
		Assert.Equal("B2:C3", range.ToString());

		Assert.False(AreaReference.TryParseArea(Encoding.UTF8.GetBytes("A" + new string('0', 100) + "1"), out _));
		Assert.False(AreaReference.TryParseArea(Encoding.UTF8.GetBytes("Ä1"), out _));
		Assert.False(AreaReference.TryParseArea(Encoding.UTF8.GetBytes("S!A1"), out _));
	}
}
