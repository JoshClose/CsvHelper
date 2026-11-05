namespace CsvHelper.Excel.Tests.Api;

/// <summary><see cref="ExcelRange"/>: its forms, parsing, text, checks and equality.</summary>
public class ExcelRangeTests
{
	[Theory]
	[InlineData("B5:F200", 5, 2, 200, 6, "B5:F200")]
	[InlineData("$B$5:$F$200", 5, 2, 200, 6, "B5:F200")]
	[InlineData("b5:f200", 5, 2, 200, 6, "B5:F200")]
	[InlineData("F200:B5", 5, 2, 200, 6, "B5:F200")]
	[InlineData("F5:B200", 5, 2, 200, 6, "B5:F200")]
	[InlineData("$B5:F$200", 5, 2, 200, 6, "B5:F200")]
	[InlineData(" \tB5:F200\r\n", 5, 2, 200, 6, "B5:F200")]
	[InlineData("B5", 5, 2, 5, 2, "B5")]
	[InlineData("$B$5", 5, 2, 5, 2, "B5")]
	[InlineData("B5:B5", 5, 2, 5, 2, "B5")]
	[InlineData("A01", 1, 1, 1, 1, "A1")]
	[InlineData("XFD1048576", 1048576, 16384, 1048576, 16384, "XFD1048576")]
	[InlineData("B:F", 1, 2, 1048576, 6, "B:F")]
	[InlineData("$B:$F", 1, 2, 1048576, 6, "B:F")]
	[InlineData("F:B", 1, 2, 1048576, 6, "B:F")]
	[InlineData("A:A", 1, 1, 1048576, 1, "A:A")]
	[InlineData("B1:F1048576", 1, 2, 1048576, 6, "B:F")]
	[InlineData("5:200", 5, 1, 200, 16384, "5:200")]
	[InlineData("$5:$200", 5, 1, 200, 16384, "5:200")]
	[InlineData("200:5", 5, 1, 200, 16384, "5:200")]
	[InlineData("4:4", 4, 1, 4, 16384, "4:4")]
	[InlineData("A4:XFD4", 4, 1, 4, 16384, "4:4")]
	[InlineData("A:XFD", 1, 1, 1048576, 16384, "1:1048576")]
	[InlineData("A1:XFD1048576", 1, 1, 1048576, 16384, "1:1048576")]
	public void ParsesEveryForm(string text, int firstRow, int firstColumn, int lastRow, int lastColumn, string shown)
	{
		var range = ExcelRange.Parse(text);

		Assert.Equal((firstRow, firstColumn, lastRow, lastColumn), (range.FirstRow, range.FirstColumn, range.LastRow, range.LastColumn));
		Assert.Equal((lastRow - firstRow + 1, lastColumn - firstColumn + 1), (range.RowCount, range.ColumnCount));
		Assert.Equal(shown, range.ToString());
		Assert.True(ExcelRange.TryParse(text, out var parsed));
		Assert.Equal(range, parsed);
		Assert.Equal(range, ExcelRange.Parse(shown));
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData("B")]
	[InlineData("5")]
	[InlineData("$")]
	[InlineData("$$B5")]
	[InlineData("B$$5")]
	[InlineData("A$")]
	[InlineData("A$:B$")]
	[InlineData("B5:")]
	[InlineData(":B5")]
	[InlineData("B5:F")]
	[InlineData("B:5")]
	[InlineData("5:B")]
	[InlineData("A0")]
	[InlineData("A1048577")]
	[InlineData("A99999999999")]
	[InlineData("XFE1")]
	[InlineData("AAAA1")]
	[InlineData("XFE:XFE")]
	[InlineData("0:1")]
	[InlineData("A1:B2:C3")]
	[InlineData("A1,B2")]
	[InlineData("A1 B2")]
	[InlineData("A 1")]
	[InlineData("1A")]
	[InlineData("A1.5")]
	[InlineData("A-1")]
	[InlineData("=A1")]
	[InlineData("(A1)")]
	[InlineData("#REF!")]
	[InlineData("Sheet1!A1")]
	[InlineData("R1C1")]
	[InlineData("Ä1")]
	public void RejectsWhatIsNotABlock(string text)
	{
		Assert.False(ExcelRange.TryParse(text, out var range));
		Assert.Equal(default, range);

		var e = Assert.Throws<FormatException>(() => ExcelRange.Parse(text));
		Assert.Equal($"\"{text}\" is not a range of cells, such as B5:F200, B:F or 5:200.", e.Message);
	}

	[Fact]
	public void ChecksNull()
	{
		Assert.False(ExcelRange.TryParse(null, out _));
		Assert.Throws<ArgumentNullException>(() => ExcelRange.Parse(null!));
	}

	[Fact]
	public void ChecksTheConstructor()
	{
		var range = new ExcelRange(5, 2, 200, 6);
		Assert.Equal("B5:F200", range.ToString());
		Assert.Equal("XFD1048576", new ExcelRange(1048576, 16384, 1048576, 16384).ToString());

		var cases = new (Func<ExcelRange> Create, string Parameter)[]
		{
			(() => new ExcelRange(0, 1, 1, 1), "firstRow"),
			(() => new ExcelRange(1048577, 1, 1048577, 1), "firstRow"),
			(() => new ExcelRange(1, 0, 1, 1), "firstColumn"),
			(() => new ExcelRange(1, 16385, 1, 16385), "firstColumn"),
			(() => new ExcelRange(5, 1, 4, 1), "lastRow"),
			(() => new ExcelRange(1, 1, 1048577, 1), "lastRow"),
			(() => new ExcelRange(1, 5, 1, 4), "lastColumn"),
			(() => new ExcelRange(1, 1, 1, 16385), "lastColumn"),
		};
		foreach (var (create, parameter) in cases)
		{
			var e = Assert.Throws<ArgumentOutOfRangeException>(() => create());
			Assert.Equal(parameter, e.ParamName);
		}
	}

	[Fact]
	public void TheDefaultIsA1()
	{
		var range = default(ExcelRange);

		Assert.Equal((1, 1, 1, 1), (range.FirstRow, range.FirstColumn, range.LastRow, range.LastColumn));
		Assert.Equal("A1", range.ToString());
		Assert.Equal(ExcelRange.Parse("A1"), range);
	}

	[Fact]
	public void ComparesByCells()
	{
		var range = ExcelRange.Parse("B5:F200");
		var same = new ExcelRange(5, 2, 200, 6);
		var other = ExcelRange.Parse("B5:F201");

		Assert.True(range == same);
		Assert.False(range != same);
		Assert.True(range.Equals(same));
		Assert.True(range.Equals((object)same));
		Assert.Equal(range.GetHashCode(), same.GetHashCode());
		Assert.True(range != other);
		Assert.False(range.Equals(other));
		Assert.False(range.Equals("B5:F200"));
		Assert.False(range.Equals(null));
	}

	[Fact]
	public void RoundTripsRandomRanges()
	{
		var random = new Random(3);
		for (var i = 0; i < 5000; i++)
		{
			var firstRow = random.Next(1, 1048577);
			var firstColumn = random.Next(1, 16385);
			var lastRow = random.Next(4) == 0 ? 1048576 : random.Next(firstRow, 1048577);
			var lastColumn = random.Next(4) == 0 ? 16384 : random.Next(firstColumn, 16385);
			if (random.Next(8) == 0)
			{
				firstRow = 1;
				lastRow = 1048576;
			}

			if (random.Next(8) == 0)
			{
				firstColumn = 1;
				lastColumn = 16384;
			}

			var range = new ExcelRange(firstRow, firstColumn, lastRow, lastColumn);

			Assert.Equal(range, ExcelRange.Parse(range.ToString()));
			Assert.Equal(range, ExcelRange.Parse(range.ToString().ToLowerInvariant()));
		}
	}
}
