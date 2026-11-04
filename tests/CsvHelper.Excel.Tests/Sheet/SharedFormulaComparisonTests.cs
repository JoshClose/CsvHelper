using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Shared formulas read as text, compared with ClosedXML's <c>FormulaA1</c>, which moves a shared formula's references
/// with a formula parser of its own: generated groups of every kind of reference, literal and function, whose references
/// stay within the grid when moved (around its edges, Excel wraps them; see <see cref="FormulaShifterTests"/>).
/// </summary>
public class SharedFormulaComparisonTests
{
	/// <summary>The number of groups.</summary>
	private const int Groups = 60;

	/// <summary>The rows of a group below its defining cell.</summary>
	private const int GroupRows = 8;

	/// <summary>The columns of a group right of its defining cell.</summary>
	private const int GroupColumns = 4;

	[Fact]
	public async Task MovesAsClosedXmlDoes()
	{
		var random = new Random(20261103);
		var cells = new SortedDictionary<int, SortedDictionary<int, string>>();
		var formulas = new List<(int Row, int Column, string Formula)>();
		for (var g = 0; g < Groups; g++)
		{
			var row = 2 + (g / 6 * (GroupRows + 2));
			var column = 1 + (g % 6 * (GroupColumns + 2));
			var formula = Expression(random, 0);
			formulas.Add((row, column, formula));
			var reference = $"{CellReference.ColumnName(column)}{row}:{CellReference.ColumnName(column + GroupColumns)}{row + GroupRows}";
			Put(cells, row, column, $"<f t=\"shared\" ref=\"{reference}\" si=\"{g}\">{Sheets.Escape(formula)}</f><v>0</v>");
			for (var i = 0; i < 12; i++)
			{
				Put(cells, row + random.Next(GroupRows + 1), column + random.Next(GroupColumns + 1), $"<f t=\"shared\" si=\"{g}\"/><v>0</v>");
			}
		}

		var rows = new StringBuilder();
		foreach (var row in cells)
		{
			rows.Append($"<row r=\"{row.Key}\">");
			foreach (var cell in row.Value)
			{
				rows.Append($"<c r=\"{CellReference.ColumnName(cell.Key)}{row.Key}\">{cell.Value}</c>");
			}

			rows.Append("</row>");
		}

		var xml = Sheets.Worksheet(rows.ToString());
		var package = TestPackage.Workbook("Data", "Other Sheet").Set(Sheets.PartName, xml);
		var ours = new Dictionary<(long Row, int Column), string>();
		foreach (var record in await Sheets.ReadRecordsAsync(xml, SheetMode.All[0], reading: CellReading.FormulaText))
		{
			for (var column = 0; column < record.Fields.Count; column++)
			{
				if (record.Fields[column].Kind == SheetCellKind.Text)
				{
					ours[(record.Row, column)] = record.Fields[column].Text;
				}
			}
		}

		using var workbook = new XLWorkbook(package.ToStream());
		var differences = new List<string>();
		var compared = 0;
		foreach (var cell in workbook.Worksheet(1).CellsUsed())
		{
			var key = ((long)cell.Address.RowNumber, cell.Address.ColumnNumber - 1);
			var theirs = "=" + cell.FormulaA1;
			ours.TryGetValue(key, out var mine);
			compared++;
			if (mine != theirs)
			{
				differences.Add($"{cell.Address}: ours {mine}, ClosedXML's {theirs}");
			}
		}

		Assert.Equal(cells.Sum(r => r.Value.Count), compared);
		Assert.True(differences.Count == 0, string.Join("\n", differences.Take(20)));

		// The other cells' formulas are moved, not the defining cells' copied.
		Assert.True(ours.Values.Distinct().Count() > 6 * Groups, $"{ours.Values.Distinct().Count()} distinct formulas");
	}

	/// <summary>Puts a cell's content in the sheet, unless the cell has some.</summary>
	/// <param name="cells">The cells, by row and column.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="column">The cell's column, from 0.</param>
	/// <param name="content">The cell's content.</param>
	private static void Put(SortedDictionary<int, SortedDictionary<int, string>> cells, int row, int column, string content)
	{
		if (!cells.TryGetValue(row, out var columns))
		{
			columns = [];
			cells[row] = columns;
		}

		if (!columns.ContainsKey(column))
		{
			columns[column] = content;
		}
	}

	/// <summary>A generated expression: terms joined by operators.</summary>
	/// <param name="random">The generator.</param>
	/// <param name="depth">How deep in functions and brackets it is.</param>
	/// <returns>The expression.</returns>
	private static string Expression(Random random, int depth)
	{
		var text = new StringBuilder(Term(random, depth));
		var terms = random.Next(1, 4);
		for (var i = 0; i < terms; i++)
		{
			text.Append(Pick(random, "+", "-", "*", "&", "/", "^", "=", "<>"));
			text.Append(Term(random, depth));
		}

		return text.ToString();
	}

	/// <summary>A generated term: a reference, a literal, a function call, or an expression in brackets.</summary>
	/// <param name="random">The generator.</param>
	/// <param name="depth">How deep in functions and brackets it is.</param>
	/// <returns>The term.</returns>
	private static string Term(Random random, int depth)
	{
		var kind = random.Next(depth < 2 ? 14 : 10);

		return kind switch
		{
			0 or 1 or 2 => Cell(random),
			3 => $"{Cell(random)}:{Cell(random)}",
			4 => $"SUM({Column(random)}:{Column(random)})",
			5 => $"SUM({Row(random)}:{Row(random)})",
			6 => Pick(random, "'Other Sheet'!", "Data!") + Cell(random),
			7 => Pick(random, "\"A1\"", "\"say \"\"B2\"\"\"", "1.5E+3", "2", ".5", "TRUE", "#N/A", "{1,2;3,4}"),
			8 => $"SUM({Cell(random)}:{Cell(random)} {Cell(random)}:{Cell(random)})",
			9 => "LOG10(" + Cell(random) + ")",
			10 => $"SUM({Expression(random, depth + 1)},{Cell(random)})",
			11 => $"IF({Cell(random)}>1,{Expression(random, depth + 1)},\"A1\")",
			12 => $"({Expression(random, depth + 1)})",
			_ => $"_xlfn.XLOOKUP({Cell(random)},{Cell(random)}:{Cell(random)},{Cell(random)}:{Cell(random)})",
		};
	}

	/// <summary>A generated cell reference, relative, absolute or mixed, far enough from the grid's edges.</summary>
	/// <param name="random">The generator.</param>
	/// <returns>The reference.</returns>
	private static string Cell(Random random) => Column(random) + Row(random);

	/// <summary>
	/// A generated column, relative or absolute, in capitals or not: near <c>A</c> or near <c>XFD</c>, but not past it when
	/// moved.
	/// </summary>
	/// <param name="random">The generator.</param>
	/// <returns>The column's letters.</returns>
	private static string Column(Random random)
	{
		var column = random.Next(2) == 0 ? random.Next(30) : CellReference.MaxColumns - GroupColumns - 1 - random.Next(800);
		var letters = CellReference.ColumnName(column);

		return (random.Next(3) == 0 ? "$" : "") + (random.Next(8) == 0 ? letters.ToLowerInvariant() : letters);
	}

	/// <summary>A generated row, relative or absolute: near 1 or near the last row, but not past it when moved.</summary>
	/// <param name="random">The generator.</param>
	/// <returns>The row's digits.</returns>
	private static string Row(Random random)
	{
		var row = random.Next(2) == 0 ? 1 + random.Next(100) : CellReference.MaxRow - GroupRows - random.Next(1000);

		return (random.Next(3) == 0 ? "$" : "") + row.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>One of some choices.</summary>
	/// <param name="random">The generator.</param>
	/// <param name="choices">The choices.</param>
	/// <returns>The choice.</returns>
	private static string Pick(Random random, params string[] choices) => choices[random.Next(choices.Length)];
}
