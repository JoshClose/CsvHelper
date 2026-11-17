using System.Globalization;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// The built-in number format ids' codes (<see cref="BuiltInFormats"/>): Excel en-US's, as Excel 365 (16.0 build 20527)
/// reported each through COM on 2026-11-16 (<c>NumberFormat</c> of a cell in a style of the id, which the styles part
/// doesn't define; <c>Csv\_bench\results\phase8\design</c>), and the ids that follow another culture.
/// </summary>
public class BuiltInFormatsTests
{
	/// <summary>Each id Excel was asked about and the code it reported.</summary>
	private const string ExcelCodes = """
		0	General
		1	0
		2	0.00
		3	#,##0
		4	#,##0.00
		5	$#,##0_);($#,##0)
		6	$#,##0_);[Red]($#,##0)
		7	$#,##0.00_);($#,##0.00)
		8	$#,##0.00_);[Red]($#,##0.00)
		9	0%
		10	0.00%
		11	0.00E+00
		12	# ?/?
		13	# ??/??
		14	m/d/yyyy
		15	d-mmm-yy
		16	d-mmm
		17	mmm-yy
		18	h:mm AM/PM
		19	h:mm:ss AM/PM
		20	h:mm
		21	h:mm:ss
		22	m/d/yyyy h:mm
		23	General
		24	General
		25	General
		26	General
		27	m/d/yyyy
		28	m/d/yyyy
		29	m/d/yyyy
		30	m/d/yyyy
		31	m/d/yyyy
		32	h:mm:ss
		33	h:mm:ss
		34	h:mm:ss
		35	h:mm:ss
		36	m/d/yyyy
		37	#,##0_);(#,##0)
		38	#,##0_);[Red](#,##0)
		39	#,##0.00_);(#,##0.00)
		40	#,##0.00_);[Red](#,##0.00)
		41	_(* #,##0_);_(* (#,##0);_(* "-"_);_(@_)
		42	_($* #,##0_);_($* (#,##0);_($* "-"_);_(@_)
		43	_(* #,##0.00_);_(* (#,##0.00);_(* "-"??_);_(@_)
		44	_($* #,##0.00_);_($* (#,##0.00);_($* "-"??_);_(@_)
		45	mm:ss
		46	[h]:mm:ss
		47	mm:ss.0
		48	##0.0E+0
		49	@
		50	m/d/yyyy
		51	m/d/yyyy
		52	m/d/yyyy
		53	m/d/yyyy
		54	m/d/yyyy
		55	m/d/yyyy
		56	m/d/yyyy
		57	m/d/yyyy
		58	m/d/yyyy
		59	0
		60	0.00
		61	#,##0
		62	#,##0.00
		63	$#,##0_);($#,##0)
		64	$#,##0_);[Red]($#,##0)
		65	$#,##0.00_);($#,##0.00)
		66	$#,##0.00_);[Red]($#,##0.00)
		67	0%
		68	0.00%
		69	# ?/?
		70	# ??/??
		71	m/d/yyyy
		72	m/d/yyyy
		73	d-mmm-yy
		74	d-mmm
		75	mmm-yy
		76	h:mm
		77	h:mm:ss
		78	m/d/yyyy h:mm
		79	mm:ss
		80	[h]:mm:ss
		81	mm:ss.0
		100	General
		163	General
		""";

	/// <summary>In Excel en-US, every id has the code Excel reported, and ids past the table General.</summary>
	[Fact]
	public void MatchesExcelEnglish()
	{
		var display = DisplayCulture.Invariant;
		foreach (var line in ExcelCodes.Split('\n'))
		{
			var parts = line.TrimEnd('\r').Split('\t');
			var id = int.Parse(parts[0], CultureInfo.InvariantCulture);
			Assert.Equal(parts[1], display.BuiltIn(id));
			Assert.Equal(parts[1], BuiltInFormats.English(id));
		}

		for (var id = 82; id < BuiltInFormats.FirstCustom; id++)
		{
			Assert.Equal("General", display.BuiltIn(id));
		}

		Assert.Equal("General", display.BuiltIn(164));
		Assert.Equal("General", display.BuiltIn(-1));
		Assert.Equal("General", BuiltInFormats.English(int.MaxValue));
	}

	/// <summary>
	/// In German, 14 and 22 are its short date, and the currency ids show the euro after the number and a minus sign
	/// first, as Excel's own German formats do; the other ids stay Excel en-US's.
	/// </summary>
	[Fact]
	public void FollowsGerman()
	{
		var display = DisplayCulture.For(CultureInfo.GetCultureInfo("de-DE"));
		Assert.Equal(display.ShortDate, display.BuiltIn(14));
		Assert.Equal(display.ShortDate + " h:mm", display.BuiltIn(22));
		Assert.Equal("#,##0 \"€\";-#,##0 \"€\"", display.BuiltIn(5));
		Assert.Equal("#,##0 \"€\";[Red]-#,##0 \"€\"", display.BuiltIn(6));
		Assert.Equal("#,##0.00 \"€\";-#,##0.00 \"€\"", display.BuiltIn(7));
		Assert.Equal("#,##0.00 \"€\";[Red]-#,##0.00 \"€\"", display.BuiltIn(8));
		Assert.Equal("_-* #,##0_-;-* #,##0_-;_-* \"-\"_-;_-@_-", display.BuiltIn(41));
		Assert.Equal("_-* #,##0 \"€\"_-;-* #,##0 \"€\"_-;_-* \"-\" \"€\"_-;_-@_-", display.BuiltIn(42));
		Assert.Equal("_-* #,##0.00_-;-* #,##0.00_-;_-* \"-\"??_-;_-@_-", display.BuiltIn(43));
		Assert.Equal("_-* #,##0.00 \"€\"_-;-* #,##0.00 \"€\"_-;_-* \"-\"?? \"€\"_-;_-@_-", display.BuiltIn(44));
		for (var i = 0; i < 4; i++)
		{
			Assert.Equal(display.BuiltIn(5 + i), display.BuiltIn(63 + i));
		}

		Assert.Equal("#,##0_);(#,##0)", display.BuiltIn(37));
		Assert.Equal("m/d/yyyy", display.BuiltIn(27));
		Assert.Equal("m/d/yyyy", display.BuiltIn(71));
		Assert.Equal("m/d/yyyy h:mm", display.BuiltIn(78));
	}

	/// <summary>A culture whose negative currency is in parentheses keeps Excel en-US's shapes, with its own symbol.</summary>
	[Fact]
	public void KeepsParenthesesShapes()
	{
		var display = DisplayCulture.For(WithCurrency("$", 0, 0));
		Assert.Equal("\"$\"#,##0_);(\"$\"#,##0)", display.BuiltIn(5));
		Assert.Equal("\"$\"#,##0.00_);[Red](\"$\"#,##0.00)", display.BuiltIn(8));
		Assert.Equal("_(* #,##0_);_(* (#,##0);_(* \"-\"_);_(@_)", display.BuiltIn(41));
		Assert.Equal("_(\"$\"* #,##0_);_(\"$\"* (#,##0);_(\"$\"* \"-\"_);_(@_)", display.BuiltIn(42));
		Assert.Equal("_(\"$\"* #,##0.00_);_(\"$\"* (#,##0.00);_(\"$\"* \"-\"??_);_(@_)", display.BuiltIn(44));

		display = DisplayCulture.For(WithCurrency("kr", 3, 15));
		Assert.Equal("#,##0 \"kr\"_);(#,##0 \"kr\")", display.BuiltIn(5));
		Assert.Equal("_(* #,##0 \"kr\"_);_(* (#,##0) \"kr\";_(* \"-\" \"kr\"_);_(@_)", display.BuiltIn(42));
	}

	/// <summary>Each of .NET's negative patterns places the minus sign and the symbol as it says (16 is .NET's alone).</summary>
	/// <param name="negative">The negative pattern.</param>
	/// <param name="expected">Id 5's negative section.</param>
	[Theory]
	[InlineData(1, "-\"¤\"#,##0")]
	[InlineData(2, "\"¤\"-#,##0")]
	[InlineData(3, "\"¤\"#,##0-")]
	[InlineData(4, "(#,##0\"¤\")")]
	[InlineData(5, "-#,##0\"¤\"")]
	[InlineData(6, "#,##0-\"¤\"")]
	[InlineData(7, "#,##0\"¤\"-")]
	[InlineData(8, "-#,##0 \"¤\"")]
	[InlineData(9, "-\"¤\" #,##0")]
	[InlineData(10, "#,##0 \"¤\"-")]
	[InlineData(11, "\"¤\" #,##0-")]
	[InlineData(12, "\"¤\" -#,##0")]
	[InlineData(13, "#,##0- \"¤\"")]
	[InlineData(14, "(\"¤\" #,##0)")]
	[InlineData(15, "(#,##0 \"¤\")")]
#if NET
	[InlineData(16, "\"¤\"- #,##0")]
#endif
	public void PlacesNegatives(int negative, string expected)
	{
		var display = DisplayCulture.For(WithCurrency("¤", 0, negative));
		var code = display.BuiltIn(5);
		Assert.Equal(expected, code.Substring(code.IndexOf(';') + 1));
		Assert.True(FormatCode.Compile(code).IsReadable);
		Assert.True(FormatCode.Compile(display.BuiltIn(42)).IsReadable);
		Assert.True(FormatCode.Compile(display.BuiltIn(41)).IsReadable);
	}

	/// <summary>A symbol with a quote in it is escaped.</summary>
	[Fact]
	public void EscapesQuotesInSymbols()
	{
		var display = DisplayCulture.For(WithCurrency("a\"b", 0, 1));
		Assert.Equal("\"a\"\\\"\"b\"#,##0;-\"a\"\\\"\"b\"#,##0", display.BuiltIn(5));
		Assert.True(FormatCode.Compile(display.BuiltIn(5)).IsReadable);
	}

	/// <summary>A culture with a currency of its own: en-US's data with another symbol and patterns.</summary>
	/// <param name="symbol">The symbol.</param>
	/// <param name="positive">The positive pattern.</param>
	/// <param name="negative">The negative pattern.</param>
	/// <returns>The culture.</returns>
	private static CultureInfo WithCurrency(string symbol, int positive, int negative)
	{
		var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
		culture.NumberFormat.CurrencySymbol = symbol;
		culture.NumberFormat.CurrencyPositivePattern = positive;
		culture.NumberFormat.CurrencyNegativePattern = negative;

		return culture;
	}
}
