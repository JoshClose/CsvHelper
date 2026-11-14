namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// <see cref="ConversionNames"/>: sheets' names made file names on every system, and numbered when they're the same; and
/// CSV files' names made sheet names, as Excel makes them.
/// </summary>
public class ConversionNamesTests
{
	[Theory]
	[InlineData("Data", "Data")]
	[InlineData("Q1 Orders", "Q1 Orders")]
	[InlineData("a<b>c:d\"e/f\\g|h?i*j", "a_b_c_d_e_f_g_h_i_j")]
	[InlineData("../x", ".._x")]
	[InlineData("..\\..\\x", ".._.._x")]
	[InlineData("C:x", "C_x")]
	[InlineData("/etc/passwd", "_etc_passwd")]
	[InlineData("a\u0000b\u001Fc\u007Fd\u0085e", "a_b_c_d_e")]
	[InlineData("tab\tnew\nline", "tab_new_line")]
	[InlineData("pair 😀", "pair 😀")]
	[InlineData("dots...", "dots")]
	[InlineData("spaces   ", "spaces")]
	[InlineData("mixed . . ", "mixed")]
	[InlineData("  leading", "  leading")]
	[InlineData(".hidden", ".hidden")]
	[InlineData("a.b", "a.b")]
	[InlineData("Sales [2024]", "Sales [2024]")]
	[InlineData("O'Brien's", "O'Brien's")]
	[InlineData("年度", "年度")]
	public void MakesASheetsNameAFileName(string name, string expected) => Assert.Equal(expected, ConversionNames.FileStem(name, 1));

	[Fact]
	public void MakesALoneSurrogateAnUnderscore()
	{
		// Not theory data, which xunit's serialization makes U+FFFD.
		Assert.Equal("lone _ high", ConversionNames.FileStem("lone \uD800 high", 1));
		Assert.Equal("lone _ low", ConversionNames.FileStem("lone \uDC00 low", 1));
		Assert.Equal("reversed __", ConversionNames.FileStem("reversed \uDE00\uD83D", 1));
		Assert.Equal("end _", ConversionNames.FileStem("end \uD83D", 1));
	}

	[Theory]
	[InlineData("CON")]
	[InlineData("con")]
	[InlineData("Prn")]
	[InlineData("AUX")]
	[InlineData("NUL")]
	[InlineData("COM0")]
	[InlineData("COM1")]
	[InlineData("com9")]
	[InlineData("COM¹")]
	[InlineData("LPT1")]
	[InlineData("LPT9")]
	[InlineData("LPT³")]
	[InlineData("CON.txt")]
	[InlineData("nul.tar.gz")]
	[InlineData("CON .txt")]
	[InlineData("AUX.")]
	public void PutsAnUnderscoreBeforeADevicesName(string name) => Assert.Equal("_" + name.TrimEnd('.'), ConversionNames.FileStem(name, 1));

	[Theory]
	[InlineData("CONSOLE")]
	[InlineData("CON1")]
	[InlineData("COM10")]
	[InlineData("LPT")]
	[InlineData("xCON")]
	[InlineData("a.CON")]
	public void LeavesOtherNamesAlone(string name) => Assert.Equal(name, ConversionNames.FileStem(name, 1));

	[Theory]
	[InlineData("", 3, "Sheet3")]
	[InlineData(".", 1, "Sheet1")]
	[InlineData("..", 12, "Sheet12")]
	[InlineData(" . ", 2, "Sheet2")]
	public void NamesAnEmptyNameAfterTheSheetsNumber(string name, int number, string expected) => Assert.Equal(expected, ConversionNames.FileStem(name, number));

	[Fact]
	public void CutsALongNameWithoutSplittingAPair()
	{
		var name = new string('a', 100);
		Assert.Equal(new string('a', ConversionNames.MaxStemLength), ConversionNames.FileStem(name, 1));

		// A pair that would end past the limit is left out whole.
		var pairs = new string('a', ConversionNames.MaxStemLength - 1) + "😀b";
		Assert.Equal(new string('a', ConversionNames.MaxStemLength - 1), ConversionNames.FileStem(pairs, 1));
		var fits = new string('a', ConversionNames.MaxStemLength - 2) + "😀b";
		Assert.Equal(new string('a', ConversionNames.MaxStemLength - 2) + "😀", ConversionNames.FileStem(fits, 1));

		// Dots and spaces that the cut leaves at the end are dropped.
		Assert.Equal(new string('a', 10), ConversionNames.FileStem(new string('a', 10) + new string('.', 54) + "tail", 1));
	}

	[Fact]
	public void NumbersNamesThatAreTheSame()
	{
		var names = ConversionNames.FileNames([("Data", 1), ("data", 2), ("DATA", 3), ("Other", 4)]);
		Assert.Equal(["Data.csv", "data (2).csv", "DATA (3).csv", "Other.csv"], names);

		// A sheet's own name is kept for it, and a number skips it.
		names = ConversionNames.FileNames([("Data", 1), ("data", 2), ("Data (2)", 3)]);
		Assert.Equal(["Data.csv", "data (3).csv", "Data (2).csv"], names);

		// Names made the same: by their characters, by Unicode's forms, by an empty name's number, by a device's underscore.
		names = ConversionNames.FileNames([("a/b", 1), ("a:b", 2), ("é", 3), ("é", 4), ("", 5), ("Sheet5", 6), ("CON", 7), ("_con", 8)]);
		Assert.Equal(["a_b.csv", "a_b (2).csv", "é.csv", "é (2).csv", "Sheet5.csv", "Sheet5 (2).csv", "_CON.csv", "_con (2).csv"], names);

		Assert.Empty(ConversionNames.FileNames([]));
	}

	[Theory]
	[InlineData("orders.csv", "orders")]
	[InlineData("Q1 Orders.csv", "Q1 Orders")]
	[InlineData("data.backup.csv", "data.backup")]
	[InlineData("noextension", "noextension")]
	[InlineData("brackets [x] and #.csv", "brackets (x) and #")]
	[InlineData("quote's.csv", "quote's")]
	[InlineData("'quoted'.csv", "quoted")]
	[InlineData("''.csv", "Sheet1")]
	[InlineData(".csv", "Sheet1")]
	[InlineData("a-very-long-file-name-for-the-sheet-name-check-of-excel.csv", "a-very-long-file-name-for-the-s")]
	[InlineData("pair 😀.csv", "pair 😀")]
	[InlineData("History.csv", "History")]
	[InlineData("=sum + 1!.csv", "=sum + 1!")]
	public void NamesACsvFilesSheetAsExcelDoes(string file, string expected)
	{
		// Excel 365 names the sheets of the fifth, sixth and tenth so (Csv\_bench\results\phase6\design\excel-sheet-names.txt).
		Assert.Equal(expected, ConversionNames.SheetName(Path.Combine(Path.GetTempPath(), file)));
		SheetNames.Check(expected, "name");
	}

	[Fact]
	public void MakesEveryFileNameASheetName()
	{
#if NET
		// Characters a file name can have off Windows, control characters, and a lone surrogate, which no file system gives.
		// (.NET Framework's paths can't have control characters, and take a colon for a drive's.)
		Assert.Equal("a_b_c_d_e_f", ConversionNames.SheetName("a?b*c:d\u0001e\uD800f.csv"));
		Assert.Equal("x_y", ConversionNames.SheetName("x\ty.csv"));
#endif

		// Cut to 31 without splitting a pair, then without an apostrophe at the end that the cut leaves.
		Assert.Equal(new string('a', 30), ConversionNames.SheetName(new string('a', 30) + "😀.csv"));
		Assert.Equal(new string('a', 29) + "😀", ConversionNames.SheetName(new string('a', 29) + "😀b.csv"));
		Assert.Equal(new string('a', 30), ConversionNames.SheetName(new string('a', 30) + "'b.csv"));

		// Escapes take more as the workbook writes them: _x0041_ is written _x005F_x0041_, so the name is cut to fit.
		var escapes = ConversionNames.SheetName("_x0041__x0042__x0043__x0044_.csv");
		Assert.True(SheetNames.WrittenLength(escapes) <= SheetNames.MaxLength);
		Assert.StartsWith(escapes, "_x0041__x0042__x0043__x0044_", StringComparison.Ordinal);
		SheetNames.Check(escapes, "name");
	}
}
