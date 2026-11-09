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

	[Fact]
	public void SaysWhatIsWrong()
	{
		Assert.Equal("has the name \"a-b\", which has -: Excel's table names have letters, digits, _, ., ? and \\.", TableNames.Problem("a-b"));
		Assert.Equal("has the name \"1Orders\", which starts with 1: Excel's table names start with a letter, _ or \\.", TableNames.Problem("1Orders"));
		Assert.Equal("has the name \"A1\", which is a cell reference: Excel opens no table of that name.", TableNames.Problem("A1"));
		Assert.Equal("has a name of 0 characters, and Excel's table names have 1 to 255.", TableNames.Problem(""));
	}
}
