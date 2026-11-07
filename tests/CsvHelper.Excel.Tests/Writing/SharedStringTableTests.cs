using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>The shared strings of a written workbook: each distinct string once, by first use.</summary>
public class SharedStringTableTests
{
	[Fact]
	public void IndexesStringsInTheOrderFirstSeen()
	{
		var table = new SharedStringTable();

		Assert.Equal(0, table.IndexOf("<t>b</t>"u8));
		Assert.Equal(1, table.IndexOf("<t>a</t>"u8));
		Assert.Equal(0, table.IndexOf("<t>b</t>"u8));
		Assert.Equal(2, table.IndexOf("<t>ab</t>"u8));
		Assert.Equal(3, table.IndexOf(""u8));
		Assert.Equal(3, table.IndexOf(""u8));
		Assert.Equal(4, table.Count);
		Assert.Equal("<t>b</t>|<t>a</t>|<t>ab</t>|", string.Join("|", Enumerable.Range(0, table.Count).Select(i => Encoding.UTF8.GetString(table[i].ToArray()))));
	}

	[Fact]
	public void KeepsEveryStringAsItGrows()
	{
		var table = new SharedStringTable();
		for (var i = 0; i < 100000; i++)
		{
			Assert.Equal(i, table.IndexOf(Encoding.UTF8.GetBytes("s" + i)));
		}

		for (var i = 0; i < 100000; i += 7)
		{
			Assert.Equal(i, table.IndexOf(Encoding.UTF8.GetBytes("s" + i)));
			Assert.Equal("s" + i, Encoding.UTF8.GetString(table[i].ToArray()));
		}

		Assert.Equal(100000, table.Count);
		Assert.True(table.MemorySize < 4 << 20, $"{table.MemorySize} bytes");
	}
}
