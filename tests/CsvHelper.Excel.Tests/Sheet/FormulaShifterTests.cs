using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// <see cref="FormulaShifter"/>: moving a shared formula's relative references to another cell of its group. The cases
/// marked "Excel" are Excel 365's answers, read through COM from hand-written workbooks (each with one shared formula
/// group) on 2026-11-03: Excel opened each without repairing it, and its <c>Range.Formula</c> on the group's other cells
/// is the expected text, less the <c>=</c>.
/// </summary>
public class FormulaShifterTests
{
	/// <summary>
	/// Excel's answers: a defining cell's formula, a cell's offset from it in rows and columns, and the formula Excel reads
	/// there. Excel shows references and function names in uppercase, numbers in its own form and newer functions without
	/// their <c>_xlfn.</c> prefix; those cases give the text as stored, with Excel's form in the comment.
	/// </summary>
	public static TheoryData<string, int, int, string> ExcelCases => new()
	{
		{ "A1+$A1+A$1+$A$1", 0, 1, "B1+$A1+B$1+$A$1" },
		{ "A1+$A1+A$1+$A$1", 1, 0, "A2+$A2+A$1+$A$1" },
		{ "A1+$A1+A$1+$A$1", 1, 1, "B2+$A2+B$1+$A$1" },
		{
			"SUM($A$1:A1)+A$1+$A1+SUM(A:A)+SUM(1:1)+SUM($A:$A)+SUM($1:1)+SUM(A1:$B$2)+SUM(A1:A2 A1:B1)", 0, 1,
			"SUM($A$1:B1)+B$1+$A1+SUM(B:B)+SUM(1:1)+SUM($A:$A)+SUM($1:1)+SUM(B1:$B$2)+SUM(B1:B2 B1:C1)"
		},
		{
			"SUM($A$1:A1)+A$1+$A1+SUM(A:A)+SUM(1:1)+SUM($A:$A)+SUM($1:1)+SUM(A1:$B$2)+SUM(A1:A2 A1:B1)", 1, 0,
			"SUM($A$1:A2)+A$1+$A2+SUM(A:A)+SUM(2:2)+SUM($A:$A)+SUM($1:2)+SUM(A2:$B$2)+SUM(A2:A3 A2:B2)"
		},
		{
			"SUM($A$1:A1)+A$1+$A1+SUM(A:A)+SUM(1:1)+SUM($A:$A)+SUM($1:1)+SUM(A1:$B$2)+SUM(A1:A2 A1:B1)", 1, 1,
			"SUM($A$1:B2)+B$1+$A2+SUM(B:B)+SUM(2:2)+SUM($A:$A)+SUM($1:2)+SUM(B2:$B$2)+SUM(B2:B3 B2:C2)"
		},
		{ "LOG10(A1)+MyName+LEN(\"A1\")+TRUE+SUM({1,2})+IFERROR(#N/A,1)", 1, 1, "LOG10(B2)+MyName+LEN(\"A1\")+TRUE+SUM({1,2})+IFERROR(#N/A,1)" },

		// Excel: 100000+F6+0.0015+0.5.
		{ "1E+5+E5+1.5E-3+.5", 1, 1, "1E+5+F6+1.5E-3+.5" },

		// Excel: XLOOKUP(B2,C2:C3,D2:D3).
		{ "_xlfn.XLOOKUP(A1,B1:B2,C1:C2)", 1, 1, "_xlfn.XLOOKUP(B2,C2:C3,D2:D3)" },

		// References are in capitals, moved or not. Excel: B1+$B$1+SUM(B:B)+C$2, and so on, function names in capitals too.
		{ "a1+$b$1+sum(a:a)+b$2", 0, 1, "B1+$B$1+sum(B:B)+C$2" },
		{ "a1+$b$1+sum(a:a)+b$2", 1, 1, "B2+$B$1+sum(B:B)+C$2" },
		{ "a1+$b$1+sum(a:a)+b$2", 1, 0, "A2+$B$1+sum(A:A)+B$2" },

		// Around the edges of the grid, references wrap.
		{ "A1+$A1+SUM(A:A)+B1", 1, -1, "XFD2+$A2+SUM(XFD:XFD)+A2" },
		{ "SUM(1:1)+SUM($A:B)", 1, -1, "SUM(2:2)+SUM($A:A)" },
		{ "XFD1+SUM(XFD:XFD)+SUM(B1:XFD1)+A1+SUM(XFC1:XFD1)", 0, 1, "A1+SUM(A:A)+SUM(C1:A1)+B1+SUM(XFD1:A1)" },
		{ "B1048576+B1", 1, 0, "B1+B2" },
		{ "SUM(1048576:1048576)+SUM(1:1048576)", 1, 0, "SUM(1:1)+SUM(2:1)" },

		// Excel shows the defining cell's C1:C1048576 as C:C.
		{ "SUM(B1048575:B1048576)+SUM(C1:C1048576)", 1, 0, "SUM(B1048576:B1)+SUM(C2:C1)" },
	};

	/// <summary>
	/// What moves and what doesn't, beyond Excel's answers: a formula, an offset in rows and columns, and the moved formula.
	/// </summary>
	public static TheoryData<string, int, int, string> TokenCases => new()
	{
		// Other sheets' references move, as Excel's fills move them (Excel reads no shared formula that has one).
		{ "'My Sheet'!A1+Sheet3!A1:B2+SUM(Sheet1:Sheet3!A1)", 1, 0, "'My Sheet'!A2+Sheet3!A2:B3+SUM(Sheet1:Sheet3!A2)" },
		{ "'Bob''s'!A1+'Sheet 1:Sheet 3'!B2+Sheet1!1:1+Sheet1!A:A", 1, 1, "'Bob''s'!B2+'Sheet 1:Sheet 3'!C3+Sheet1!2:2+Sheet1!B:B" },
		{ "[1]Sheet1!A1+'[1]My Sheet'!A1+[1]!MyName", 1, 0, "[1]Sheet1!A2+'[1]My Sheet'!A2+[1]!MyName" },
		{ "ABC:ABD!A1+Données!A1", 1, 0, "ABC:ABD!A2+Données!A2" },

		// String literals and quoted names, with doubled quotes.
		{ "\"A1\"&\"say \"\"B2\"\"\"&A1", 1, 0, "\"A1\"&\"say \"\"B2\"\"\"&A2" },
		{ "\"\"&A1&\"\"", 1, 0, "\"\"&A2&\"\"" },

		// Structured references, workbook indexes, array constants.
		{ "Table1[[#This Row],[Col]]*A1+Table1[@Col]+Table1['#Col]+[@[Col 1]]+Table1[[#Headers],[A1]]", 1, 0, "Table1[[#This Row],[Col]]*A2+Table1[@Col]+Table1['#Col]+[@[Col 1]]+Table1[[#Headers],[A1]]" },
		{ "Table1['[A1']]+A1", 1, 0, "Table1['[A1']]+A2" },

		// A column name with a bracket of its own, escaped: the brackets end where the unescaped ones balance.
		{ "Table1[x'[A1]+B1+Table1[y']A1]+B1", 1, 0, "Table1[x'[A1]+B2+Table1[y']A1]+B2" },
		{ "SUM({1,2;3,4})+COUNTA({\"A1\",\"}\",TRUE,#N/A})+A1", 1, 0, "SUM({1,2;3,4})+COUNTA({\"A1\",\"}\",TRUE,#N/A})+A2" },

		// Error literals, and the spill operator.
		{ "#REF!+#N/A+#DIV/0!+#NAME?+#NULL!+#NUM!+#VALUE!+#GETTING_DATA+#SPILL!+#CALC!+Sheet1!#REF!", 1, 1, "#REF!+#N/A+#DIV/0!+#NAME?+#NULL!+#NUM!+#VALUE!+#GETTING_DATA+#SPILL!+#CALC!+Sheet1!#REF!" },
		{ "SUM(A1#)+A1#*2", 1, 0, "SUM(A2#)+A2#*2" },

		// Functions, however they look.
		{ "LOG10(A1)+ATAN2(A1,B1)+DAYS360(A1,B1)+_xlfn.SEQUENCE(3)+_xlfn._xlws.SORT(A1:A3)+STDEV.S(A1:A3)", 1, 0, "LOG10(A2)+ATAN2(A2,B2)+DAYS360(A2,B2)+_xlfn.SEQUENCE(3)+_xlfn._xlws.SORT(A2:A4)+STDEV.S(A2:A4)" },
		{ "_xlfn.LAMBDA(_xlpm.x,_xlpm.x+A1)(B1)", 1, 0, "_xlfn.LAMBDA(_xlpm.x,_xlpm.x+A2)(B2)" },

		// Names, and words that look like references but aren't: past the grid, row 0, R1C1, four letters.
		{ "TRUE+FALSE+MyName+_xlnm.Print_Area+XFE1+A0+A1048577+A12345678+R1C1+ABCD1+A1B+My.Name1+Name?+\\Name+données", 1, 1, "TRUE+FALSE+MyName+_xlnm.Print_Area+XFE1+A0+A1048577+A12345678+R1C1+ABCD1+A1B+My.Name1+Name?+\\Name+données" },
		{ "A:A1+A:Sheet1!B1+A:B(1)", 0, 1, "A:B1+A:Sheet1!C1+A:B(1)" },

		// Numbers, and words after them.
		{ "1E5+1e-5+.5+10.25+1.5E+3+2E+E2+1.", 1, 0, "1E5+1e-5+.5+10.25+1.5E+3+2E+E3+1." },

		// Operators, white space and line ends.
		{ "A1 + B1&C1%-D1^2<>@E1\n+(F1)", 1, 0, "A2 + B2&C2%-D2^2<>@E2\n+(F2)" },

		// Leading zeros: a part that moves loses them, one that doesn't keeps them.
		{ "A01+A$01+$A01", 1, 1, "B2+B$01+$A2" },
		{ "01:02+$01:2+A01", 0, 1, "01:02+$01:2+B01" },

		// A $ that starts nothing.
		{ "$+$$A1+A$", 1, 0, "$+$$A2+A$" },

		// No move, or a move by the whole grid.
		{ "SUM(A1:B2)+'S'!C3", 0, 0, "SUM(A1:B2)+'S'!C3" },
		{ "SUM(A1:B2)+1:1+A:A", CellReference.MaxRow, CellReference.MaxColumns, "SUM(A1:B2)+1:1+A:A" },
		{ "A1+1:1+A:A", -CellReference.MaxRow - 1, -CellReference.MaxColumns - 1, "XFD1048576+1048576:1048576+XFD:XFD" },

		// Unclosed strings, quotes, brackets and arrays run to the end.
		{ "A1&\"B1", 1, 0, "A2&\"B1" },
		{ "A1&'B1", 1, 0, "A2&'B1" },
		{ "A1&[B1", 1, 0, "A2&[B1" },
		{ "A1&{B1", 1, 0, "A2&{B1" },
	};

	[Theory]
	[MemberData(nameof(ExcelCases))]
	public void MovesAsExcelReadsSharedFormulas(string formula, int rows, int columns, string expected)
	{
		Assert.Equal(expected, Shift(formula, rows, columns));
	}

	[Theory]
	[MemberData(nameof(TokenCases))]
	public void MovesOnlyReferences(string formula, int rows, int columns, string expected)
	{
		Assert.Equal(expected, Shift(formula, rows, columns));
	}

	[Fact]
	public void GrowsAtMostFiveTimes()
	{
		// A1 to XFD1048576, and 1:1 to 1048576:1048576, are the most a reference grows.
		Assert.Equal("XFD1048576", Shift("A1", CellReference.MaxRow - 1, CellReference.MaxColumns - 1));
		Assert.Equal("1048576:1048576", Shift("1:1", CellReference.MaxRow - 1, 0));
		Assert.Equal("XFD:XFD", Shift("A:A", 0, CellReference.MaxColumns - 1));

		var formula = Encoding.UTF8.GetBytes("1:1");
		Assert.True(FormulaShifter.TryShift(formula, CellReference.MaxRow - 1, 0, new byte[FormulaShifter.MaxGrowth * formula.Length], out var written));
		Assert.Equal(15, written);
		Assert.False(FormulaShifter.TryShift(formula, CellReference.MaxRow - 1, 0, new byte[(FormulaShifter.MaxGrowth * formula.Length) - 1], out written));
		Assert.Equal(0, written);
		Assert.False(FormulaShifter.TryShift(formula, 0, 0, new byte[2], out written));
		Assert.Equal(0, written);

		// Every reference of a long formula grows, in room five times its length.
		var many = Encoding.UTF8.GetBytes(string.Join(",", Enumerable.Repeat("A1", 10_000)));
		var destination = new byte[FormulaShifter.MaxGrowth * many.Length];
		Assert.True(FormulaShifter.TryShift(many, CellReference.MaxRow - 1, CellReference.MaxColumns - 1, destination, out written));
		Assert.Equal(string.Join(",", Enumerable.Repeat("XFD1048576", 10_000)), Encoding.UTF8.GetString(destination, 0, written));
	}

	[Fact]
	public void ReadsDeepNestingWithoutRecursion()
	{
		const int Depth = 1_000_000;
		var formula = new string('(', Depth) + "A1" + new string(')', Depth) + new string('[', Depth) + "A1" + new string(']', Depth) + "+B1";

		Assert.Equal(new string('(', Depth) + "A2" + new string(')', Depth) + new string('[', Depth) + "A1" + new string(']', Depth) + "+B2", Shift(formula, 1, 0));
	}

	/// <summary>Moves a formula, in room for its most growth.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="rows">The rows to move by.</param>
	/// <param name="columns">The columns to move by.</param>
	/// <returns>The moved formula.</returns>
	private static string Shift(string formula, int rows, int columns)
	{
		var bytes = Encoding.UTF8.GetBytes(formula);
		var destination = new byte[FormulaShifter.MaxGrowth * bytes.Length];
		Assert.True(FormulaShifter.TryShift(bytes, rows, columns, destination, out var written));

		return Encoding.UTF8.GetString(destination, 0, written);
	}
}
