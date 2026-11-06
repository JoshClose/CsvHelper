using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// The content of every producer's <c>messy</c> workbook, defined once: a report laid out for people ("Report"), a
/// chartsheet, tables of each shape ("Tables"), and a filtered sheet that hides every row and column past its data
/// ("Filtered"), with defined names of every kind. Each producer writes what it can of it.
/// </summary>
internal static class Messy
{
	/// <summary>The sheets, in tab order: the chartsheet before "Tables", so that sheets' indexes count it.</summary>
	public static IReadOnlyList<SheetSpec> Sheets { get; } =
	[
		ReportSheet(),
		new() { Name = "Chart", Kind = "chartsheet", Chart = "Report!E4:E7" },
		TablesSheet(),
		FilteredSheet(),
	];

	/// <summary>
	/// The defined names: the workbook's <c>Data</c>; <c>Local</c> as the own name of two sheets; a relative name, a union,
	/// a constant, a formula, a dynamic range, a 3-D reference, whole columns and whole rows, a name to a deleted sheet, a
	/// hidden name, a table reference, and Report's print area and print titles. The filter adds Filtered's
	/// <c>_FilterDatabase</c> where the producer writes one.
	/// </summary>
	public static IReadOnlyList<NameSpec> Names { get; } =
	[
		new() { Name = "Data", RefersTo = "Report!$B$4:$G$15" },
		new() { Name = "Local", Sheet = "Report", RefersTo = "Report!$B$5:$B$7" },
		new() { Name = "Local", Sheet = "Tables", RefersTo = "Tables!$A$1:$C$4" },
		new() { Name = "Relative", RefersTo = "Report!B5" },
		new() { Name = "Union", RefersTo = "Report!$B$5,Report!$B$8" },
		new() { Name = "Constant", RefersTo = "42" },
		new() { Name = "Formula", RefersTo = "SUM(Report!$E$5:$E$14)" },
		new() { Name = "Dynamic", RefersTo = "OFFSET(Report!$B$5,0,0,3,1)" },
		new() { Name = "ThreeD", RefersTo = "Tables:Filtered!$A$1" },
		new() { Name = "Columns", RefersTo = "Report!$B:$G" },
		new() { Name = "Rows", RefersTo = "Report!$4:$15" },
		new() { Name = "Deleted", RefersTo = "#REF!", DeletedSheet = "Gone" },
		new() { Name = "Secret", RefersTo = "Report!$B$2", Hidden = true },
		new() { Name = "TableRef", RefersTo = "Sales[Qty]" },
		new() { Name = "Print_Area", Sheet = "Report", RefersTo = "Report!$B$1:$G$15" },
		new() { Name = "Print_Titles", Sheet = "Report", RefersTo = "Report!$4:$4" },
	];

	/// <summary>The content as JSON, for the producers outside .NET: <c>{"sheets": [...], "names": [...]}</c>.</summary>
	/// <returns>The JSON.</returns>
	public static string ToJson() => JsonSerializer.Serialize(
		new { sheets = Sheets, names = Names },
		new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

	/// <summary>
	/// "Report": a title merged across B1:G1, a note, an empty row, the header at B4 with column D (Code) hidden, regions
	/// merged down column B, a block merged over C11:D12 (into the hidden column), row 8 hidden (the top of South's region),
	/// rows 13 and 14 in a collapsed group, and a totals line.
	/// </summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec ReportSheet()
	{
		var rows = new List<RowSpec>
		{
			Row(1, Text("B", "Sales report")),
			Row(2, Text("B", "Units in boxes, prices in dollars")),
			Row(4, Text("B", "Region"), Text("C", "Product"), Text("D", "Code"), Text("E", "Units"), Text("F", "Price"), Text("G", "Total")),
		};
		var lines = new (int Row, string? Region, string? Product, string? Code, double Units, double Price)[]
		{
			(5, "North", "apple", "A-1", 10, 1.5),
			(6, null, "pear", "P-2", 20, 0.25),
			(7, null, "plum", "M-3", 30, 2),
			(8, "South", "apple", "A-1", 5, 1.5),
			(9, null, "pear", "P-2", 6, 0.25),
			(10, null, "plum", "M-3", 7, 2),
			(11, "West", "mixed", null, 8, 1),
			(12, null, null, null, 9, 1),
			(13, "East", "apple", "A-1", 1, 1.5),
			(14, null, "pear", "P-2", 2, 0.25),
		};
		foreach (var (row, region, product, code, units, price) in lines)
		{
			var cells = new List<CellSpec>();
			if (region is not null)
			{
				cells.Add(Text("B", region));
			}

			if (product is not null)
			{
				cells.Add(Text("C", product));
			}

			if (code is not null)
			{
				cells.Add(Text("D", code));
			}

			cells.Add(Number("E", units));
			cells.Add(Number("F", price));
			cells.Add(Formula("G", $"E{row}*F{row}", units * price));
			rows.Add(Row(row, [.. cells]));
		}

		rows.Add(Row(
			15,
			Text("B", "Total"),
			Formula("E", "SUM(E5:E14)", lines.Sum(l => l.Units)),
			Formula("G", "SUM(G5:G14)", lines.Sum(l => l.Units * l.Price))));

		return new SheetSpec
		{
			Name = "Report",
			Rows = [.. rows],
			Merges = ["B1:G1", "B5:B7", "B8:B10", "B11:B12", "C11:D12", "B13:B14"],
			HiddenRows = [8],
			HiddenColumns = ["D"],
			Groups = [new GroupSpec { First = 13, Last = 14 }],
		};
	}

	/// <summary>
	/// "Tables": Sales with a totals row, NoHeader with its header row hidden, Wrapped with a line break in a column name,
	/// and Away, away from A1.
	/// </summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec TablesSheet()
	{
		var rows = new Dictionary<int, List<CellSpec>>();
		void Add(int row, CellSpec cell)
		{
			if (!rows.TryGetValue(row, out var cells))
			{
				cells = [];
				rows[row] = cells;
			}

			cells.Add(cell);
		}

		// Sales, A1:C5: its totals row sums Qty.
		Add(1, Text("A", "Item"));
		Add(1, Text("B", "Qty"));
		Add(1, Text("C", "Price"));
		var sales = new (string Item, double Quantity, double Price)[] { ("apple", 3, 1.5), ("pear", 10, 0.25), ("plum", 7, 2) };
		for (var i = 0; i < sales.Length; i++)
		{
			Add(2 + i, Text("A", sales[i].Item));
			Add(2 + i, Number("B", sales[i].Quantity));
			Add(2 + i, Number("C", sales[i].Price));
		}

		Add(5, Text("A", "Total"));
		Add(5, new CellSpec { Column = "B", Type = CellType.Formula, Formula = "SUBTOTAL(109,Sales[Qty])", Result = sales.Sum(s => s.Quantity), Feature = "table" });

		// NoHeader, E3:F4, its names Name and Score in no cell.
		Add(3, Text("E", "ann"));
		Add(3, Number("F", 7));
		Add(4, Text("E", "bob"));
		Add(4, Number("F", 9));

		// Wrapped, H1:I3, its first name on two lines.
		Add(1, Text("H", "First\nline"));
		Add(1, Text("I", "Value"));
		Add(2, Text("H", "x"));
		Add(2, Number("I", 1));
		Add(3, Text("H", "y"));
		Add(3, Number("I", 2));

		// Away, C8:D10.
		Add(8, Text("C", "Code"));
		Add(8, Text("D", "Count"));
		Add(9, Text("C", "k1"));
		Add(9, Number("D", 4));
		Add(10, Text("C", "k2"));
		Add(10, Number("D", 5));

		return new SheetSpec
		{
			Name = "Tables",
			Rows = [.. rows.OrderBy(r => r.Key).Select(r => Row(r.Key, [.. r.Value.OrderBy(c => Basic.ColumnIndex(c.Column))]))],
			Tables =
			[
				new TableSpec { Name = "Sales", Range = "A1:C5", TotalsLabel = "Total", TotalsSum = "Qty" },
				new TableSpec { Name = "NoHeader", Range = "E3:F4", HideHeader = true, Columns = ["Name", "Score"] },
				new TableSpec { Name = "Wrapped", Range = "H1:I3" },
				new TableSpec { Name = "Away", Range = "C8:D10" },
			],
		};
	}

	/// <summary>
	/// "Filtered": A1:C10 under a filter that hides kind b, with row 6 empty, every row from 11 hidden (<c>zeroHeight</c>),
	/// and every column from D. Excel writes the empty row 6, which it shows; a producer that leaves it out hides it, since
	/// <c>zeroHeight</c> hides the rows a sheet lacks.
	/// </summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec FilteredSheet()
	{
		var rows = new List<RowSpec> { Row(1, Text("A", "Kind"), Text("B", "Name"), Text("C", "Amount")) };
		var names = new[] { "one", "two", "three", "four", "five", "six", "seven", "eight" };
		var hidden = new List<int>();
		for (var i = 0; i < names.Length; i++)
		{
			var row = i < 4 ? 2 + i : 3 + i;
			var kind = i % 2 == 0 ? "a" : "b";
			rows.Add(Row(row, Text("A", kind), Text("B", names[i]), Number("C", i + 1)));
			if (kind == "b")
			{
				hidden.Add(row);
			}
		}

		return new SheetSpec
		{
			Name = "Filtered",
			Rows = [.. rows],
			Filter = new FilterSpec { Range = "A1:C10", Column = 1, Hides = "b", HiddenRows = [.. hidden] },
			HideRowsFrom = 11,
			HideColumnsFrom = "D",
		};
	}

	/// <summary>A block's rows and 1-based columns, from its text such as <c>A1:C5</c>.</summary>
	/// <param name="range">The block.</param>
	/// <returns>Its corners.</returns>
	internal static (int FirstRow, int FirstColumn, int LastRow, int LastColumn) Corners(string range)
	{
		var parts = range.Split(':');
		var (firstColumn, firstRow) = Cell(parts[0]);
		var (lastColumn, lastRow) = Cell(parts[^1]);

		return (firstRow, firstColumn, lastRow, lastColumn);

		static (int Column, int Row) Cell(string cell)
		{
			var letters = new string([.. cell.TakeWhile(char.IsLetter)]);

			return (Basic.ColumnIndex(letters), int.Parse(cell.AsSpan(letters.Length), System.Globalization.CultureInfo.InvariantCulture));
		}
	}

	/// <summary>Whether a cell is in a table's range: its header row (shown or not), data rows or totals row.</summary>
	/// <param name="table">The table.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="column">The cell's column letters.</param>
	/// <returns>Whether the table covers it.</returns>
	internal static bool InTable(TableSpec table, int row, string column)
	{
		var corners = Corners(table.Range);
		var first = table.HideHeader == true ? corners.FirstRow - 1 : corners.FirstRow;

		return row >= first && row <= corners.LastRow && InColumns(table.Range, column);
	}

	/// <summary>Whether a column is one of a block's.</summary>
	/// <param name="range">The block.</param>
	/// <param name="column">The column's letters.</param>
	/// <returns>Whether the block covers it.</returns>
	internal static bool InColumns(string range, string column)
	{
		var corners = Corners(range);
		var index = Basic.ColumnIndex(column);

		return index >= corners.FirstColumn && index <= corners.LastColumn;
	}

	/// <summary>A row.</summary>
	/// <param name="row">The row's number.</param>
	/// <param name="cells">The cells.</param>
	/// <returns>The row.</returns>
	private static RowSpec Row(int row, params CellSpec[] cells) => new() { Row = row, Cells = cells };

	/// <summary>A text cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Text(string column, string text) => new() { Column = column, Type = CellType.Text, Text = text };

	/// <summary>A number cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="number">The number.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Number(string column, double number) => new() { Column = column, Type = CellType.Number, Number = number };

	/// <summary>A formula cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="formula">The formula, without its <c>=</c>.</param>
	/// <param name="result">Its result as Excel computes it.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Formula(string column, string formula, object result) =>
		new() { Column = column, Type = CellType.Formula, Formula = formula, Result = result, Feature = "formula" };
}
