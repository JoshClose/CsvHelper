using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CsvHelper.Excel.Corpus;

/// <summary>What a cell of the basic workbook holds, and so how a producer writes it.</summary>
internal enum CellType
{
	/// <summary>Text.</summary>
	Text,

	/// <summary>A number.</summary>
	Number,

	/// <summary>A boolean.</summary>
	Bool,

	/// <summary>A formula, written with its cached result where the producer can.</summary>
	Formula,

	/// <summary>A date or time, with a number format.</summary>
	Date,

	/// <summary>Rich text: runs, some bold.</summary>
	Rich,

	/// <summary>Japanese text with a phonetic reading (furigana).</summary>
	Phonetic,

	/// <summary>An error value as a constant, such as <c>#N/A</c>, not a formula's result.</summary>
	Error,
}

/// <summary>A cell of the basic workbook.</summary>
internal sealed class CellSpec
{
	/// <summary>The cell's column letters.</summary>
	[JsonPropertyName("column")]
	public string Column { get; init; } = "";

	/// <summary>What the cell holds.</summary>
	[JsonPropertyName("type")]
	[JsonConverter(typeof(JsonStringEnumConverter<CellType>))]
	public CellType Type { get; init; }

	/// <summary>The text of a text, rich or phonetic cell.</summary>
	[JsonPropertyName("text")]
	public string? Text { get; init; }

	/// <summary>The value of a number cell.</summary>
	[JsonPropertyName("number")]
	public double? Number { get; init; }

	/// <summary>The value of a boolean cell.</summary>
	[JsonPropertyName("bool")]
	public bool? Bool { get; init; }

	/// <summary>A formula, in English and without its <c>=</c>.</summary>
	[JsonPropertyName("formula")]
	public string? Formula { get; init; }

	/// <summary>The value of a date cell, ISO 8601; <see langword="null"/> for a time alone, which is its <see cref="Number"/>.</summary>
	[JsonPropertyName("date")]
	public DateTime? Date { get; init; }

	/// <summary>The number format of a date cell, as Excel writes it.</summary>
	[JsonPropertyName("format")]
	public string? Format { get; init; }

	/// <summary>
	/// The built-in number format id of a date cell, in place of <see cref="Format"/>, for the producers that set ids
	/// (feature <c>formatId</c>). Excel only takes codes.
	/// </summary>
	[JsonPropertyName("formatId")]
	public int? FormatId { get; init; }

	/// <summary>
	/// A formula's result as Excel computes it, for the producers that write the result they're given: a number, a text,
	/// a boolean, or an error's text (<c>#N/A</c>).
	/// </summary>
	[JsonPropertyName("result")]
	public object? Result { get; init; }

	/// <summary>The range of a legacy array formula (Ctrl+Shift+Enter), such as <c>G14:G16</c>, whose first cell this is.</summary>
	[JsonPropertyName("array")]
	public string? Array { get; init; }

	/// <summary>Whether the formula is a dynamic array formula, which spills.</summary>
	[JsonPropertyName("spill")]
	public bool? Spill { get; init; }

	/// <summary>The runs of a rich text cell: their text, and whether each is bold.</summary>
	[JsonPropertyName("runs")]
	public RunSpec[]? Runs { get; init; }

	/// <summary>The phonetic reading of a phonetic cell, in katakana.</summary>
	[JsonPropertyName("phonetic")]
	public string? Phonetic { get; init; }

	/// <summary>
	/// What the cell needs from a producer beyond plain values, so that one that lacks it leaves the cell out:
	/// <c>control</c> (a control character), <c>long</c> (32,767 characters), <c>formula</c>, <c>rich</c>, <c>phonetic</c> or
	/// <c>date</c>; in the typed workbook also <c>formatId</c>, <c>error</c> (a constant error), <c>array</c> (a legacy array
	/// formula), <c>spill</c> (a dynamic array formula), <c>name</c> (a formula with a defined name), <c>table</c> (a
	/// structured reference) and <c>newer</c> (a function newer than Excel 2010).
	/// </summary>
	[JsonPropertyName("feature")]
	public string? Feature { get; init; }

	/// <summary>The cell's reference in its row.</summary>
	/// <param name="row">The row.</param>
	/// <returns>The reference, such as <c>B3</c>.</returns>
	public string Reference(int row) => Column + row;
}

/// <summary>A run of a rich text cell.</summary>
internal sealed class RunSpec
{
	/// <summary>The run's text.</summary>
	[JsonPropertyName("text")]
	public string Text { get; init; } = "";

	/// <summary>Whether the run is bold.</summary>
	[JsonPropertyName("bold")]
	public bool Bold { get; init; }
}

/// <summary>A row of the basic workbook.</summary>
internal sealed class RowSpec
{
	/// <summary>The row's number.</summary>
	[JsonPropertyName("row")]
	public int Row { get; init; }

	/// <summary>The row's cells, in column order.</summary>
	[JsonPropertyName("cells")]
	public CellSpec[] Cells { get; init; } = [];
}

/// <summary>A sheet of the basic workbook.</summary>
internal sealed class SheetSpec
{
	/// <summary>The sheet's name.</summary>
	[JsonPropertyName("name")]
	public string Name { get; init; } = "";

	/// <summary><c>worksheet</c>, or <c>chartsheet</c> (a column chart of Q1 Orders' quantities).</summary>
	[JsonPropertyName("kind")]
	public string Kind { get; init; } = "worksheet";

	/// <summary><c>visible</c>, <c>hidden</c> or <c>veryHidden</c>.</summary>
	[JsonPropertyName("state")]
	public string State { get; init; } = "visible";

	/// <summary>The sheet's rows; none for a chartsheet.</summary>
	[JsonPropertyName("rows")]
	public RowSpec[] Rows { get; init; } = [];

	/// <summary>
	/// Ranges Excel fills from their first cell, down or right, as a user fills them, so that it writes shared formulas.
	/// The rows have each cell's formula as the fill gives it, which the other producers write.
	/// </summary>
	[JsonPropertyName("fills")]
	public FillSpec[] Fills { get; init; } = [];

	/// <summary>Cells Excel copies to another, as a user copies them, so that it moves their references; the target's formula is in the rows.</summary>
	[JsonPropertyName("copies")]
	public CopySpec[] Copies { get; init; } = [];

	/// <summary>The sheet's tables; their cells are in the rows.</summary>
	[JsonPropertyName("tables")]
	public TableSpec[] Tables { get; init; } = [];
}

/// <summary>A range Excel fills from its first cell.</summary>
internal sealed class FillSpec
{
	/// <summary>The range, such as <c>A8:A12</c>.</summary>
	[JsonPropertyName("range")]
	public string Range { get; init; } = "";

	/// <summary><c>down</c> or <c>right</c>.</summary>
	[JsonPropertyName("direction")]
	public string Direction { get; init; } = "down";
}

/// <summary>A cell Excel copies to another.</summary>
internal sealed class CopySpec
{
	/// <summary>The cell copied.</summary>
	[JsonPropertyName("from")]
	public string From { get; init; } = "";

	/// <summary>The cell it is copied to.</summary>
	[JsonPropertyName("to")]
	public string To { get; init; } = "";
}

/// <summary>A table (a list object): its name and range, the first row its headers.</summary>
internal sealed class TableSpec
{
	/// <summary>The table's name.</summary>
	[JsonPropertyName("name")]
	public string Name { get; init; } = "";

	/// <summary>The table's range, headers included, such as <c>A20:D23</c>.</summary>
	[JsonPropertyName("range")]
	public string Range { get; init; } = "";
}

/// <summary>
/// The content of every producer's <c>basic</c> workbook, defined once: sheets of each kind and state, and on "Data" a
/// row per kind of value, with the edge cases a reader must get right. Each producer writes what it can of it.
/// </summary>
internal static class Basic
{
	/// <summary>The length of the longest text a cell can hold: 32,767 characters.</summary>
	public const int MaxCellText = 32767;

	/// <summary>The sheets, in tab order.</summary>
	public static IReadOnlyList<SheetSpec> Sheets { get; } =
	[
		new()
		{
			Name = "Data",
			Rows =
			[
				Row(1, Text("A", "Kind"), Text("B", "Value"), Text("C", "More")),
				Row(2, Text("A", "text"), Text("B", "plain text"), Text("C", "x")),
				Row(3, Text("A", "integer"), Number("B", 42)),
				Row(4, Text("A", "decimal"), Number("B", 3.25)),
				Row(5, Text("A", "negative"), Number("B", -17.5)),
				Row(6, Text("A", "small"), Number("B", 1E-05)),
				Row(7, Text("A", "precise"), Number("B", 0.1 + 0.2)),
				Row(8, Text("A", "boolean true"), Bool("B", true)),
				Row(9, Text("A", "boolean false"), Bool("B", false)),
				Row(10, Text("A", "not available"), Formula("B", "NA()")),
				Row(11, Text("A", "divide by zero"), Formula("B", "1/0")),
				Row(12, Text("A", "gap"), Text("B", "a"), Text("D", "d")),
				Row(13, Text("A", "short")),
				Row(15, Text("C", "block"), Number("D", 1), Number("E", 2)),
				Row(16, Text("A", "emoji"), Text("B", char.ConvertFromUtf32(0x1F600) + char.ConvertFromUtf32(0x1F44D) + char.ConvertFromUtf32(0x1F3FD))),
				Row(17, Text("A", "spaces"), Text("B", "  leading and trailing  ")),
				Row(18, Text("A", "line break"), Text("B", "line one\nline two")),
				Row(19, Text("A", "control"), new CellSpec { Column = "B", Type = CellType.Text, Text = "a" + (char)1 + "b", Feature = "control" }),
				Row(20, Text("A", "escape"), Text("B", "_x0041_")),
				Row(21, Text("A", "markup"), Text("B", "&<>\"'")),
				Row(22, Text("A", "long"), new CellSpec { Column = "B", Type = CellType.Text, Text = new string('x', MaxCellText), Feature = "long" }),
				Row(23, Text("A", "rich"), new CellSpec { Column = "B", Type = CellType.Rich, Runs = [new() { Text = "bold", Bold = true }, new() { Text = " and plain" }], Feature = "rich" }),
				Row(24, Text("A", "phonetic"), new CellSpec { Column = "B", Type = CellType.Phonetic, Text = "東京", Phonetic = "トウキョウ", Feature = "phonetic" }),
				Row(25, Text("A", "formula number"), Formula("B", "B3*2")),
				Row(26, Text("A", "formula text"), Formula("B", "A2&\"!\"")),
				Row(27, Text("A", "formula error"), Formula("B", "B2+1")),
				Row(28, Text("A", "shared formula"), Formula("B", "ROW()*10")),
				Row(29, Text("A", "shared formula"), Formula("B", "ROW()*10")),
				Row(30, Text("A", "shared formula"), Formula("B", "ROW()*10")),
				Row(31, Text("A", "date"), Date("B", new DateTime(2024, 1, 2), "m/d/yyyy")),
				Row(32, Text("A", "date and time"), Date("B", new DateTime(2024, 1, 2, 3, 4, 5), "yyyy-mm-dd hh:mm:ss")),
				Row(33, Text("A", "time"), Time("B", new TimeSpan(3, 4, 5), "h:mm:ss")),
				Row(34, Text("A", "custom date"), Date("B", new DateTime(2024, 12, 31), "dd mmm yyyy")),
			],
		},
		new() { Name = "Chart", Kind = "chartsheet" },
		new() { Name = "Hidden", State = "hidden", Rows = [Row(1, Text("A", "hidden"))] },
		new() { Name = "VeryHidden", State = "veryHidden", Rows = [Row(1, Text("A", "very hidden"))] },
		new()
		{
			Name = "Q1 Orders",
			Rows =
			[
				Row(1, Text("A", "Name"), Text("B", "Quantity"), Text("C", "Price"), Text("D", "Paid"), Text("E", "Ordered")),
				Row(2, Text("A", "apple"), Number("B", 3), Number("C", 1.5), Bool("D", true), Date("E", new DateTime(2024, 1, 2), "yyyy-mm-dd")),
				Row(3, Text("A", "pear"), Number("B", 10), Number("C", 0.25), Bool("D", false), Date("E", new DateTime(2024, 2, 3), "yyyy-mm-dd")),
				Row(4, Text("A", "plum"), Number("B", 7), Number("C", 2), Bool("D", true), Date("E", new DateTime(2024, 3, 4), "yyyy-mm-dd")),
			],
		},
		new() { Name = "Bob's", Rows = [Row(1, Text("A", "bob"))] },
		new() { Name = "日本語", Rows = [Row(1, Text("A", "日本語"), Number("B", 1))] },
	];

	/// <summary>The content as JSON, for the producers outside .NET.</summary>
	/// <returns>The JSON.</returns>
	public static string ToJson() => JsonSerializer.Serialize(Sheets, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

	/// <summary>The cells of a sheet that a producer supports.</summary>
	/// <param name="sheet">The sheet.</param>
	/// <param name="features">The features the producer supports.</param>
	/// <returns>Each row with the cells it can write.</returns>
	public static IEnumerable<(int Row, CellSpec[] Cells)> Rows(SheetSpec sheet, params string[] features) =>
		sheet.Rows.Select(r => (r.Row, r.Cells.Where(c => c.Feature is null || features.Contains(c.Feature)).ToArray()));

	/// <summary>The 1-based index of a column's letters.</summary>
	/// <param name="column">The letters.</param>
	/// <returns>The index: 1 for <c>A</c>.</returns>
	public static int ColumnIndex(string column) => column.Aggregate(0, (index, c) => index * 26 + (c - 'A' + 1));

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

	/// <summary>A boolean cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="value">The value.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Bool(string column, bool value) => new() { Column = column, Type = CellType.Bool, Bool = value };

	/// <summary>A formula cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="formula">The formula, without its <c>=</c>.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Formula(string column, string formula) => new() { Column = column, Type = CellType.Formula, Formula = formula, Feature = "formula" };

	/// <summary>
	/// A cell of a time alone: its serial (the fraction of a day) with a time format. A date on the epoch would be a day
	/// before Excel's first, which producers that count from Excel's epoch write as a negative serial.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="time">The time.</param>
	/// <param name="format">The number format.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Time(string column, TimeSpan time, string format) => new() { Column = column, Type = CellType.Date, Number = time.TotalDays, Format = format, Feature = "date" };

	/// <summary>A date cell.</summary>
	/// <param name="column">The column.</param>
	/// <param name="date">The date.</param>
	/// <param name="format">The number format.</param>
	/// <returns>The cell.</returns>
	private static CellSpec Date(string column, DateTime date, string format) => new() { Column = column, Type = CellType.Date, Date = date, Format = format, Feature = "date" };
}
