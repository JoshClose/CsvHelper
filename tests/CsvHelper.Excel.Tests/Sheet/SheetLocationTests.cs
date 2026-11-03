namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>Cell references, and locations with sheet names quoted as Excel quotes them in formulas.</summary>
public class SheetLocationTests
{
	/// <summary>
	/// Names Excel 365 (build 20527) leaves unquoted in a formula, checked through COM on 2026-11-02: a formula
	/// <c>='name'!A1</c> reads back as <c>=name!A1</c>.
	/// </summary>
	private static readonly string[] Unquoted =
	[
		"Data", "XFE1", "AAAA1", "A0", "RX", "CX", "true1", "abc1x", "_x", "a.b", "a_b", "日本", "日本1", "Données", "é1", "ZZZ9",
		"XFD1048577", "A1048577", "A1B", "NA", "N", "RCx", "RC_", "R_", "R.1", "C.1", "R1.", "Rabc", "Cabc", "R1048577", "tRue1",
		"R0", "C0", "RC0", "x.1", "a.", "_1", "_", "A_1", "A1_", "A1.", "AB.1", "XFD0", "TRUEX", "FALSE1", "NULL", "Name1", "RR",
		"CC", "RCR", "C1048577", "C1.", "R0x", "C0x", "R1048577x", "C1048577x", "FALSEx", "C16385x",
	];

	/// <summary>Names Excel quotes, checked as <see cref="Unquoted"/> was.</summary>
	private static readonly string[] Quoted =
	[
		"Q1 Orders", "Bob's", "A1", "XFD1048576", "A01", "R", "C", "R1", "C1", "R1C1", "RC", "R1C", "RC1", "R1C1x", "TRUE", "FALSE",
		"1abc", ".a", "Sheet-1", "a b", "ab12", "x1", "Q1", "R1x", "C1x", "R1C1_", "R1Cx", "Rx1", "C1C", "R2D2", "C3PO", "R1C1R",
		"RR1", "CC1", "C16385", "R1C16385", "True", "R01", "R1C1C1", "C16384", "C99999", "C100000", "C1048576", "R1048576", "R1_",
		"C1_", "R1x1", "R1C1.", "R1C16385x", "R1C0", "R1C1048577", "RC1x", "RC1_", "R1Cx1", "tRUE", "R1C99999",
	];

	[Theory]
	[InlineData(0, "A")]
	[InlineData(25, "Z")]
	[InlineData(26, "AA")]
	[InlineData(51, "AZ")]
	[InlineData(52, "BA")]
	[InlineData(701, "ZZ")]
	[InlineData(702, "AAA")]
	[InlineData(16383, "XFD")]
	public void NamesColumns(int column, string name)
	{
		Assert.Equal(name, CellReference.ColumnName(column));
	}

	[Fact]
	public void NamesOnlyTheSheetsColumns()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => CellReference.ColumnName(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => CellReference.ColumnName(CellReference.MaxColumns));
	}

	[Theory]
	[InlineData("A1", 0, 1)]
	[InlineData("a1", 0, 1)]
	[InlineData("xFd1048576", 16383, 1048576)]
	[InlineData("AA10", 26, 10)]
	[InlineData("A01", 0, 1)]
	public void ParsesReferences(string text, int column, int row)
	{
		Assert.True(CellReference.TryParse(System.Text.Encoding.UTF8.GetBytes(text), out var parsedColumn, out var parsedRow));
		Assert.Equal((column, row), (parsedColumn, parsedRow));
	}

	[Theory]
	[InlineData("")]
	[InlineData("A")]
	[InlineData("1")]
	[InlineData("A0")]
	[InlineData("XFE1")]
	[InlineData("AAAA1")]
	[InlineData("A1048577")]
	[InlineData("A12345678")]
	[InlineData("$A$1")]
	[InlineData(" A1")]
	[InlineData("A1 ")]
	[InlineData("A-1")]
	[InlineData("A1B")]
	[InlineData("é1")]
	[InlineData("A1:B2")]
	public void RejectsWhatIsNotAReference(string text)
	{
		Assert.False(CellReference.TryParse(System.Text.Encoding.UTF8.GetBytes(text), out _, out _));
	}

	[Fact]
	public void ParsesEveryColumnItNames()
	{
		for (var column = 0; column < CellReference.MaxColumns; column++)
		{
			Assert.True(CellReference.TryParse(System.Text.Encoding.UTF8.GetBytes(CellReference.ColumnName(column) + "7"), out var parsed, out var row));
			Assert.Equal((column, 7), (parsed, row));
		}
	}

	[Fact]
	public void QuotesSheetNamesAsExcelDoes()
	{
		Assert.All(Unquoted, name => Assert.Equal(name, SheetLocation.Quote(name)));
		Assert.All(Quoted, name => Assert.Equal("'" + name.Replace("'", "''") + "'", SheetLocation.Quote(name)));
		Assert.Equal("'Bob''s'", SheetLocation.Quote("Bob's"));
		Assert.Equal("'it''s ''quoted'''", SheetLocation.Quote("it's 'quoted'"));

		// Sheet names compare ignoring case, so Excel can't hold a1 beside A1; a formula reads either as a reference.
		Assert.Equal("'a1'", SheetLocation.Quote("a1"));
		Assert.Equal("'r'", SheetLocation.Quote("r"));
		Assert.Equal("'rc1x'", SheetLocation.Quote("rc1x"));
	}

	[Fact]
	public void FormatsCellsAndRows()
	{
		var orders = new SheetLocation("Q1 Orders");
		Assert.Equal("Q1 Orders", orders.Name);
		Assert.Equal("'Q1 Orders'", orders.QuotedName);
		Assert.Equal("'Q1 Orders'!C14", orders.Cell(2, 14));
		Assert.Equal("'Q1 Orders'!14:14", orders.Row(14));

		var data = new SheetLocation("Data");
		Assert.Equal("Data!XFD1048576", data.Cell(16383, 1048576));
		Assert.Equal("Data!A1", data.Cell(0, 1));
		Assert.Equal("Data!1048576:1048576", data.Row(1048576));
	}
}
