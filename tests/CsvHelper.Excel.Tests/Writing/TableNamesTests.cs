namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The table names Excel 365 opened and refused through COM on 2026-11-08 (<c>Csv\_bench\results\phase5\design</c>,
/// <c>answers.txt</c> and <c>answers2.txt</c>), and the forms the rules refuse because they weren't checked.
/// </summary>
public class TableNamesTests
{
	/// <summary>Names Excel opened.</summary>
	public static TheoryData<string> Opened => new()
	{
		"Orders",
		"_Orders",
		"\\Orders",
		"Q1.Orders",
		"Bestellungen" + (char)0xFC,
		"Print_Area",
		"Probe",
		"XFE1",
		"ABCD1",
		"A0",
		"XFD",
		"SUM",
		"Table1",
		"_1",
		"a?b",
		char.ConvertFromUtf32(0x65E5) + char.ConvertFromUtf32(0x672C),
		"a" + char.ConvertFromUtf32(0x1F600),
		new string('T', 255),
	};

	/// <summary>Names Excel refused, and what the rules say of each.</summary>
	public static TheoryData<string, string> Refused => new()
	{
		{ "My Orders", "which has U+0020" },
		{ "a-b", "which has -" },
		{ "1Orders", "which starts with 1" },
		{ "123", "which starts with 1" },
		{ ".a", "which starts with ." },
		{ new string('T', 256), "a name of 256 characters" },
		{ "A1", "a cell reference" },
		{ "a1", "a cell reference" },
		{ "XFD1048576", "a cell reference" },
		{ "R1C1", "a row or column reference" },
		// Columns R and C, row 1.
		{ "R1", "a cell reference" },
		{ "C1", "a cell reference" },
		{ "R1C", "a row or column reference" },
		// Column RC, row 1.
		{ "RC1", "a cell reference" },
		{ "R2D2", "a row or column reference" },
		{ "RC", "a row or column reference" },
		{ "R", "a row or column reference" },
		{ "r", "a row or column reference" },
		{ "C", "a row or column reference" },
		{ "TRUE", "a boolean" },
	};

	/// <summary>Names the rules refuse though Excel wasn't asked: the stricter forms.</summary>
	public static TheoryData<string> Stricter => new()
	{
		"",
		"FALSE",
		"false",
		"A01",
		"A1048577",
		"_xlnm.Orders",
		"_XLNM._FilterDatabase",
		char.ConvertFromUtf32(0x1F600),
		"a" + (char)0x2212 + "b",
		"a" + (char)0xA0 + "b",
		"a" + (char)0x301,
		"a" + (char)0x663,
		"a" + (char)0xD800,
		"a\tb",
	};

	[Theory]
	[MemberData(nameof(Opened))]
	public void PassesNamesExcelOpens(string name) => Assert.Null(TableNames.Problem(name));

	[Theory]
	[MemberData(nameof(Refused))]
	public void RefusesNamesExcelRefuses(string name, string reason)
	{
		var problem = TableNames.Problem(name);

		Assert.NotNull(problem);
		Assert.Contains(reason, problem);
	}

	[Theory]
	[MemberData(nameof(Stricter))]
	public void RefusesFormsThatWerentChecked(string name) => Assert.NotNull(TableNames.Problem(name));

	[Fact]
	public void TakesNamesThatOnlyStartLikeReferences()
	{
		// Letters after R or C, a row past a dot, and a column past XFD aren't references.
		Assert.Null(TableNames.Problem("Rates"));
		Assert.Null(TableNames.Problem("Customers"));
		Assert.Null(TableNames.Problem("RCa"));
		Assert.Null(TableNames.Problem("A1.x"));
		Assert.Null(TableNames.Problem("A1_"));
		Assert.Null(TableNames.Problem("XFE1048576"));
		Assert.Null(TableNames.Problem("TRUE1"));
	}

	[Theory]
	[InlineData("Orders", "Orders")]
	[InlineData("Q1 Orders", "Q1_Orders")]
	[InlineData("2024", "_2024")]
	[InlineData("A1", "_A1")]
	[InlineData("R2D2", "_R2D2")]
	[InlineData("r", "_r")]
	[InlineData("TRUE", "_TRUE")]
	[InlineData("Orders (2)", "Orders__2_")]
	[InlineData(".x", "_.x")]
	[InlineData("_xlnm.x", "__xlnm.x")]
	[InlineData("Bob's-sheet", "Bob_s_sheet")]
	public void MakesANameFromTheSheets(string sheet, string name)
	{
		// The table names made in the step's workbooks opened in Excel (Csv\_bench\results\phase5\step4, answers.txt).
		Assert.Equal(name, TableNames.FromSheetName(sheet));
		Assert.Null(TableNames.Problem(name));
	}

	[Fact]
	public void PutsAnUnderscoreFirstBeforeACharacterPastThePlane()
	{
		var emoji = char.ConvertFromUtf32(0x1F600);

		Assert.Equal("_" + emoji + "x", TableNames.FromSheetName(emoji + "x"));

		// A letter past ASCII stays; a symbol doesn't.
		Assert.Equal("Bestellungen" + (char)0xFC + "_", TableNames.FromSheetName("Bestellungen" + (char)0xFC + (char)0x20AC));
	}

	[Theory]
	[InlineData("Orders", 2, "Orders2")]
	[InlineData("Q1.Orders", 10, "Q1.Orders10")]
	[InlineData("AB", 2, "AB_2")]
	[InlineData("XFD", 1048576, "XFD_1048576")]
	[InlineData("R", 3, "R_3")]
	[InlineData("RC", 3, "RC_3")]
	[InlineData("ABCD", 2, "ABCD2")]
	public void NumbersANameAsExcelDoesACopysTable(string name, long number, string numbered)
	{
		// Excel named the copies of AB's sheet AB_3 and AB_4, and R's R__3 (rename.txt); the rules need one _.
		Assert.Equal(numbered, TableNames.Numbered(name, number));
		Assert.Null(TableNames.Problem(numbered));
	}

	[Fact]
	public void CutsANameToNumberIt()
	{
		var name = new string('T', 255);

		Assert.Equal(new string('T', 254) + "2", TableNames.Numbered(name, 2));
		Assert.Equal(new string('T', 252) + "100", TableNames.Numbered(name, 100));
	}

	[Fact]
	public void KeepsAWorkbooksNamesIgnoringCase()
	{
		var names = new TableNames();
		names.Add("Orders");
		names.Add("Orders2");

		Assert.Equal("Orders", names.Existing("ORDERS"));
		Assert.Null(names.Existing("Orders3"));
		Assert.Equal("orders3", names.Unique("orders"));
		Assert.Equal("Sales", names.Unique("Sales"));
		Assert.Equal("Orders3", names.Next("Orders"));
		Assert.Equal("Sales2", names.Next("Sales"));
	}

	[Fact]
	public void SaysWhatIsWrong()
	{
		Assert.Equal("has the name \"a-b\", which has -: Excel's table names have letters, digits, _, ., ? and \\.", TableNames.Problem("a-b"));
		Assert.Equal("has the name \"1Orders\", which starts with 1: Excel's table names start with a letter, _ or \\.", TableNames.Problem("1Orders"));
		Assert.Equal("has the name \"A1\", which is a cell reference: Excel opens no table of that name.", TableNames.Problem("A1"));
		Assert.Equal("has a name of 0 characters, and Excel's table names have 1 to 255.", TableNames.Problem(""));
	}
}
