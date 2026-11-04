using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// The content of every producer's <c>typed</c> workbook, defined once: number formats and the serials around their
/// limits on "Dates", numbers, booleans and errors on "Numbers", and formulas of every kind on "Formulas", which
/// references "Other Sheet" and the defined name <c>Rate</c>. Each producer writes what it can of it.
/// </summary>
internal static class Typed
{
	/// <summary>The serial every format shows: 2024-01-15 12:00 in the 1900 date system.</summary>
	private const double Noon = 45306.5;

	/// <summary>A day and a half, which an elapsed-time format shows as 36 hours.</summary>
	private const double DayAndAHalf = 1.5;

	/// <summary>The format the serials around the limits show in, to the millisecond.</summary>
	private const string Milliseconds = "yyyy-mm-dd hh:mm:ss.000";

	/// <summary>The built-in number format ids of dates, times and durations, and some of numbers, to set by id.</summary>
	private static readonly int[] FormatIds =
	[
		14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58,
		71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 0, 1, 2, 10, 11, 37, 49, 59, 63,
	];

	/// <summary>Format codes of dates, times, durations, and numbers with literals that look like date parts.</summary>
	private static readonly string[] Codes =
	[
		"yyyy-mm-dd", "dd/mm/yyyy", "d-mmm-yy", "mmmm d, yyyy", "dddd", "mmm-yy", "yyyy-mm-dd hh:mm:ss", "yyyy-mm-ddThh:mm:ss.000",
		"h:mm AM/PM", "hh:mm:ss.000", "mm:ss", "[$-409]mmmm d, yyyy;@", "[$-F800]dddd, mmmm dd, yyyy", "yyyy\"年\"m\"月\"d\"日\"", "e",
		"yyyy-mm-dd [h]:mm", "0.0 \"d\"", "0 \"days\"", "#,##0 \"h\"", "0\\m", "\"yyyy\"0", "[Red]0.00", "0;yyyy", "General", "@",
		"0.00E+00", "# ?/?", "$#,##0.00", "0%",
	];

	/// <summary>Elapsed-time format codes, each set on a day and a half.</summary>
	private static readonly string[] Elapsed = ["[h]:mm:ss", "[mm]:ss", "[s]", "[h]:mm:ss.000"];

	/// <summary>Serials around the limits of the date systems, and around a day's end and a millisecond.</summary>
	private static readonly double[] Serials =
	[
		0, 0.5, 1, 59, 60, 60.5, 61, 2958465, 2958465.99999999, 2958466, -1, 0.999999999, 45306.000005787037, 45306.000000005,
		45306.99999999, 1E-10,
	];

	/// <summary>Durations, in days, around zero and past a day.</summary>
	private static readonly double[] Durations = [DayAndAHalf, 1.0 / 24, 100, -0.5, 0.000011574074074074073];

	/// <summary>The defined names: <c>Rate</c>, a constant that a formula multiplies by.</summary>
	public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string> { ["Rate"] = "0.5" };

	/// <summary>The sheets, in tab order.</summary>
	public static IReadOnlyList<SheetSpec> Sheets { get; } = [DatesSheet(), NumbersSheet(), FormulasSheet(), OtherSheet()];

	/// <summary>The content as JSON, for the producers outside .NET: <c>{"sheets": [...], "names": {...}}</c>.</summary>
	/// <returns>The JSON.</returns>
	public static string ToJson() => JsonSerializer.Serialize(
		new { sheets = Sheets, names = Names },
		new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

	/// <summary>"Dates": built-in format ids, format codes, durations, the limits' serials, and dates written as dates.</summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec DatesSheet()
	{
		var rows = new List<RowSpec> { Row(1, Text("A", "Case"), Text("B", "Value")) };
		foreach (var id in FormatIds)
		{
			var value = id is 46 or 80 ? DayAndAHalf : Noon;
			rows.Add(Row(rows.Count + 1, Text("A", $"id {id}"), new CellSpec { Column = "B", Type = CellType.Date, Number = value, FormatId = id, Feature = "formatId" }));
		}

		foreach (var code in Codes)
		{
			rows.Add(Row(rows.Count + 1, Text("A", code), Serial("B", code is "0 \"days\"" ? 3 : Noon, code)));
		}

		foreach (var code in Elapsed)
		{
			rows.Add(Row(rows.Count + 1, Text("A", code), Serial("B", DayAndAHalf, code)));
		}

		foreach (var duration in Durations)
		{
			rows.Add(Row(rows.Count + 1, Text("A", "duration " + Show(duration)), Serial("B", duration, "[h]:mm:ss.000")));
		}

		foreach (var serial in Serials)
		{
			rows.Add(Row(rows.Count + 1, Text("A", "serial " + Show(serial)), Serial("B", serial, Milliseconds)));
		}

		// Dates written as dates, which openpyxl's iso_dates writes as t="d", as Excel's Strict files do.
		var dates = new (string Label, DateTime Date, string Format)[]
		{
			("date and time", new DateTime(2024, 1, 15, 12, 0, 0), "yyyy-mm-dd hh:mm"),
			("milliseconds", new DateTime(2024, 1, 15, 12, 34, 56, 789), Milliseconds),
			("day", new DateTime(2024, 1, 15), "yyyy-mm-dd"),
			("first day", new DateTime(1900, 1, 1), "yyyy-mm-dd"),
			("after the leap day", new DateTime(1900, 3, 1), "yyyy-mm-dd"),
			("last day", new DateTime(9999, 12, 31), "yyyy-mm-dd"),
		};
		foreach (var (label, date, format) in dates)
		{
			rows.Add(Row(rows.Count + 1, Text("A", label), new CellSpec { Column = "B", Type = CellType.Date, Date = date, Format = format, Feature = "date" }));
		}

		return new SheetSpec { Name = "Dates", Rows = [.. rows] };
	}

	/// <summary>"Numbers": integers, 17-digit doubles, exponents, booleans, errors as constants and as results, and texts that look like values.</summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec NumbersSheet()
	{
		var rows = new List<RowSpec> { Row(1, Text("A", "Case"), Text("B", "Value")) };
		var numbers = new double[]
		{
			0, 1, -1, 42, 9007199254740992, 1E15, 12345678901234567, 9223372036854775808, -2147483649, 4294967296,
			0.1 + 0.2, 1.0 / 3, 2.0 / 3, 3 + 4.440892098500626E-16, 1.0000000000000002, 0.1, 100.00000000000001, 1234567.8901234567,
			1E-300, double.MaxValue, 2.2250738585072014E-308, 1E21, 1E-07, 1.5E20, 6.02214076E23, -1E-05, 1.23456E12,
		};
		foreach (var number in numbers)
		{
			rows.Add(Row(rows.Count + 1, Text("A", "number " + Show(number)), Number("B", number)));
		}

		rows.Add(Row(rows.Count + 1, Text("A", "true"), new CellSpec { Column = "B", Type = CellType.Bool, Bool = true }));
		rows.Add(Row(rows.Count + 1, Text("A", "false"), new CellSpec { Column = "B", Type = CellType.Bool, Bool = false }));
		foreach (var error in new[] { "#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A" })
		{
			rows.Add(Row(rows.Count + 1, Text("A", "error " + error), new CellSpec { Column = "B", Type = CellType.Error, Text = error, Feature = "error" }));
		}

		var results = new (string Formula, string Result, string? Feature)[]
		{
			("NA()", "#N/A", null), ("1/0", "#DIV/0!", null), ("\"a\"+1", "#VALUE!", null), ("SQRT(-1)", "#NUM!", null),
			("INDEX(B2:B3,3)", "#REF!", null), ("B2:B3 C2:C3", "#NULL!", null), ("NOSUCHNAME", "#NAME?", "name"),
			("FILTER(B2:B3,B2:B3>1E+300)", "#CALC!", "spill"),
		};
		foreach (var (formula, result, feature) in results)
		{
			rows.Add(Row(rows.Count + 1, Text("A", "formula " + formula), Formula("B", formula, result, feature)));
		}

		foreach (var text in new[] { "00123", "1E5", "TRUE", "#N/A", "2024-01-15", " 42 ", "1,234" })
		{
			rows.Add(Row(rows.Count + 1, Text("A", "text " + text.Trim()), Text("B", text)));
		}

		return new SheetSpec { Name = "Numbers", Rows = [.. rows] };
	}

	/// <summary>
	/// "Formulas": data in A1:E5, K1:K5 and L1:L5, formulas filled down rows 8 to 12 and right rows 14 to 16, a legacy
	/// array, a spill, a newer function, a reference moved off the grid, line breaks, and a table with structured references.
	/// </summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec FormulasSheet()
	{
		var columns = new[] { "A", "B", "C", "D", "E" };
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

		// The data: column c of row s is s times 10 to the c; K is 2s, and L is 1.
		for (var s = 1; s <= 5; s++)
		{
			for (var c = 0; c < columns.Length; c++)
			{
				Add(s, Number(columns[c], s * Math.Pow(10, c)));
			}

			Add(s, Number("K", 2 * s));
			Add(s, Number("L", 1));
		}

		// Filled down from row 8: each cell's formula, and its result, from the row s it refers to. A row's sum is
		// 11,111s of A to E, 2s of K and 1 of L.
		var down = new (string Column, Func<int, string> Formula, Func<int, object> Result, string? Feature)[]
		{
			("A", s => $"A{s}*2", s => 2.0 * s, null),
			("B", s => $"$A$1+A{s}", s => 1.0 + s, null),
			("C", s => $"A$1*$B{s}", s => 10.0 * s, null),
			("D", s => $"SUM($A$1:A{s})", s => s * (s + 1) / 2.0, null),
			("E", s => $"SUM({s}:{s})", s => 11113.0 * s + 1, null),
			("F", s => $"SUM(K:K)+A{s}", s => 30.0 + s, null),
			("G", s => $"LOG10(B{s})", s => Math.Log10(10.0 * s), null),
			("H", s => $"IF(A{s}>2,\"A1 big\",\"A1 small\")", s => s > 2 ? "A1 big" : "A1 small", null),
			("I", s => $"'Other Sheet'!A{s}*1", s => 6.0 + s, null),
			("J", s => $"A{s}*Rate", s => 0.5 * s, "name"),
			("K", s => $"A{s}>2", s => s > 2, null),
		};
		var fills = new List<FillSpec>();
		foreach (var (column, formula, result, feature) in down)
		{
			for (var s = 1; s <= 5; s++)
			{
				Add(7 + s, Formula(column, formula(s), result(s), feature));
			}

			fills.Add(new FillSpec { Range = $"{column}8:{column}12", Direction = "down" });
		}

		// Filled right along rows 14 to 16, from column A: column c refers to column c, and to K plus c for whole columns.
		var sums = new[] { 30.0, 5, 0, 0, 0 };
		for (var c = 0; c < columns.Length; c++)
		{
			var x = columns[c];
			var whole = ((char)('K' + c)).ToString();
			Add(14, Formula(x, $"{x}1+{x}$2", 3 * Math.Pow(10, c), null));
			Add(15, Formula(x, $"SUM({whole}:{whole})", sums[c], null));
			Add(16, Formula(x, $"$A1*{x}$1", Math.Pow(10, c), null));
		}

		fills.Add(new FillSpec { Range = "A14:E14", Direction = "right" });
		fills.Add(new FillSpec { Range = "A15:E15", Direction = "right" });
		fills.Add(new FillSpec { Range = "A16:E16", Direction = "right" });

		Add(14, new CellSpec { Column = "G", Type = CellType.Formula, Formula = "A1:A3*2", Result = 2.0, Array = "G14:G16", Feature = "array" });
		Add(14, new CellSpec { Column = "H", Type = CellType.Formula, Formula = "SEQUENCE(3)", Result = 1.0, Spill = true, Feature = "spill" });
		Add(14, Formula("I", "XLOOKUP(3,A1:A5,B1:B5)", 30.0, "newer"));
		Add(14, new CellSpec { Column = "J", Type = CellType.Formula, Formula = "SUM(H14#)", Result = 6.0, Spill = true, Feature = "spill" });

		// B18's A1 copied a column left is off the grid, which Excel stores as #REF!; line breaks in a formula and its result.
		Add(18, Formula("A", "#REF!*2", "#REF!", null));
		Add(18, Formula("B", "A1*2", 2.0, null));
		Add(18, Formula("C", "1+\n2", 3.0, null));
		Add(18, Formula("D", "\"a\"&CHAR(10)&\"b\"", "a\nb", null));
		Add(18, Formula("E", "TRUE()", true, null));

		// The table Orders, with a calculated column, and a formula over one of its columns.
		var orders = new (string Item, double Quantity, double Price)[] { ("apple", 3, 1.5), ("pear", 10, 0.25), ("plum", 7, 2) };
		Add(20, Text("A", "Item"));
		Add(20, Text("B", "Qty"));
		Add(20, Text("C", "Price"));
		Add(20, Text("D", "Total"));
		for (var i = 0; i < orders.Length; i++)
		{
			var (item, quantity, price) = orders[i];
			Add(21 + i, Text("A", item));
			Add(21 + i, Number("B", quantity));
			Add(21 + i, Number("C", price));
			Add(21 + i, Formula("D", "Orders[[#This Row],[Qty]]*Orders[[#This Row],[Price]]", quantity * price, "table"));
		}

		Add(21, Formula("F", "SUM(Orders[Total])", orders.Sum(o => o.Quantity * o.Price), "table"));

		return new SheetSpec
		{
			Name = "Formulas",
			Rows = [.. rows.OrderBy(r => r.Key).Select(r => Row(r.Key, [.. r.Value.OrderBy(c => Basic.ColumnIndex(c.Column))]))],
			Fills = [.. fills],
			Copies = [new CopySpec { From = "B18", To = "A18" }],
			Tables = [new TableSpec { Name = "Orders", Range = "A20:D23" }],
		};
	}

	/// <summary>"Other Sheet": A1:A5, which "Formulas" refers to by its quoted name.</summary>
	/// <returns>The sheet.</returns>
	private static SheetSpec OtherSheet() => new()
	{
		Name = "Other Sheet",
		Rows = [.. Enumerable.Range(1, 5).Select(s => Row(s, Number("A", 6 + s)))],
	};

	/// <summary>A number as a label shows it: its shortest round-trip text.</summary>
	/// <param name="number">The number.</param>
	/// <returns>The text.</returns>
	private static string Show(double number) => number.ToString("R", CultureInfo.InvariantCulture);

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

	/// <summary>A number cell with a format code: a serial with a date or time format, or a number with another.</summary>
	/// <param name="column">The column.</param>
	/// <param name="number">The number.</param>
	/// <param name="format">The format code.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Serial(string column, double number, string format) => new() { Column = column, Type = CellType.Date, Number = number, Format = format, Feature = "date" };

	/// <summary>A formula cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="formula">The formula, in English and without its <c>=</c>.</param>
	/// <param name="result">Its result as Excel computes it.</param>
	/// <param name="feature">What else it needs beyond a formula, or <see langword="null"/>.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Formula(string column, string formula, object result, string? feature) =>
		new() { Column = column, Type = CellType.Formula, Formula = formula, Result = result, Feature = feature ?? "formula" };
}
