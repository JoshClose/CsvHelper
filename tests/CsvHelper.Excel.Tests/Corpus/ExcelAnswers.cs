using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>A cell as Excel 365 reads it through COM: an item of <c>Files/excel-oracle</c>.</summary>
/// <param name="value2">
/// <c>Value2</c> as text: a text, a number's shortest round-trip form, <c>true</c> or <c>false</c>, or an error's text.
/// </param>
/// <param name="type">
/// The type of <c>Value</c>: <c>text</c>, <c>number</c>, <c>date</c>, <c>currency</c>, <c>boolean</c>, <c>error</c>, or
/// <c>empty</c> for a formula without a value.
/// </param>
/// <param name="text">The text Excel shows.</param>
/// <param name="numberFormat">The cell's number format.</param>
/// <param name="category">A number's <c>CELL("format")</c>, or <see langword="null"/>.</param>
/// <param name="elapsed">A number's format's text of a day and a half, or <see langword="null"/>.</param>
/// <param name="formula">The formula, <c>=</c> first, as Excel shows it, or <see langword="null"/>.</param>
/// <param name="hasArray">Whether the cell is in a legacy array formula.</param>
internal sealed class ExcelCell(string value2, string type, string text, string numberFormat, string? category, string? elapsed, string? formula, bool hasArray)
{
	/// <summary><c>Value2</c> as text.</summary>
	public string Value2 => value2;

	/// <summary>The type of <c>Value</c>.</summary>
	public string Type => type;

	/// <summary>The text Excel shows.</summary>
	public string Text => text;

	/// <summary>The cell's number format.</summary>
	public string NumberFormat => numberFormat;

	/// <summary>A number's <c>CELL("format")</c>.</summary>
	public string? Category => category;

	/// <summary>A number's format's text of a day and a half.</summary>
	public string? Elapsed => elapsed;

	/// <summary>The formula as Excel shows it.</summary>
	public string? Formula => formula;

	/// <summary>Whether the cell is in a legacy array formula.</summary>
	public bool HasArray => hasArray;
}

/// <summary>
/// A worksheet's rows and columns that Excel 365 hides, over the whole grid, and its merged ranges, as the oracle asks
/// through COM (<c>SpecialCells(xlCellTypeVisible)</c> and each cell's <c>MergeArea</c>).
/// </summary>
/// <param name="hiddenRows">The runs of hidden rows, 1-based.</param>
/// <param name="hiddenColumns">The runs of hidden columns, 1-based.</param>
/// <param name="merged">The merged ranges, such as <c>B1:G1</c>, in the order of their top-left cells.</param>
internal sealed class ExcelSheetStructure(IReadOnlyList<(long First, long Last)> hiddenRows, IReadOnlyList<(int First, int Last)> hiddenColumns, IReadOnlyList<string> merged)
{
	/// <summary>The runs of hidden rows, 1-based.</summary>
	public IReadOnlyList<(long First, long Last)> HiddenRows => hiddenRows;

	/// <summary>The runs of hidden columns, 1-based.</summary>
	public IReadOnlyList<(int First, int Last)> HiddenColumns => hiddenColumns;

	/// <summary>The merged ranges, such as <c>B1:G1</c>, in the order of their top-left cells.</summary>
	public IReadOnlyList<string> Merged => merged;

	/// <summary>Whether Excel hides a row.</summary>
	/// <param name="row">The row, from 1.</param>
	/// <returns>Whether it is hidden.</returns>
	public bool IsRowHidden(long row) => hiddenRows.Any(r => row >= r.First && row <= r.Last);

	/// <summary>Whether Excel hides a column.</summary>
	/// <param name="column">The column, from 0.</param>
	/// <returns>Whether it is hidden.</returns>
	public bool IsColumnHidden(int column) => hiddenColumns.Any(c => column + 1 >= c.First && column + 1 <= c.Last);
}

/// <summary>A defined name as Excel 365 answers through COM's <c>Names</c>.</summary>
/// <param name="name">Its <c>Name</c>: <c>Data</c>, or <c>Report!Local</c> for a sheet's own name.</param>
/// <param name="refersTo">Its <c>RefersTo</c>, <c>=</c> first.</param>
/// <param name="sheet">The sheet of its <c>RefersToRange</c>, or <see langword="null"/> when Excel has no range for it.</param>
/// <param name="range">The address of its <c>RefersToRange</c>, such as <c>B5:B7</c> or <c>B5,B8</c>, or <see langword="null"/>.</param>
/// <param name="visible">Its <c>Visible</c>.</param>
internal sealed class ExcelNameAnswer(string name, string refersTo, string? sheet, string? range, bool visible)
{
	/// <summary>Its <c>Name</c>: <c>Data</c>, or <c>Report!Local</c> for a sheet's own name.</summary>
	public string Name => name;

	/// <summary>Its <c>RefersTo</c>, <c>=</c> first.</summary>
	public string RefersTo => refersTo;

	/// <summary>The sheet of its <c>RefersToRange</c>, or <see langword="null"/> when Excel has no range for it.</summary>
	public string? Sheet => sheet;

	/// <summary>The address of its <c>RefersToRange</c>, or <see langword="null"/>.</summary>
	public string? Range => range;

	/// <summary>Its <c>Visible</c>.</summary>
	public bool Visible => visible;
}

/// <summary>A table as Excel 365 answers through COM's <c>ListObjects</c>.</summary>
/// <param name="sheet">Its worksheet's name.</param>
/// <param name="name">Its <c>Name</c>: the part's <c>name</c>.</param>
/// <param name="displayName">Its <c>DisplayName</c>, which formulas use.</param>
/// <param name="range">The address of its <c>Range</c>: its header row (unless hidden), data and totals row.</param>
/// <param name="showHeaders">Its <c>ShowHeaders</c>.</param>
/// <param name="showTotals">Its <c>ShowTotals</c>.</param>
/// <param name="columns">Its columns' names.</param>
internal sealed class ExcelTableAnswer(string sheet, string name, string displayName, string range, bool showHeaders, bool showTotals, IReadOnlyList<string> columns)
{
	/// <summary>Its worksheet's name.</summary>
	public string Sheet => sheet;

	/// <summary>Its <c>Name</c>: the part's <c>name</c>.</summary>
	public string Name => name;

	/// <summary>Its <c>DisplayName</c>, which formulas use.</summary>
	public string DisplayName => displayName;

	/// <summary>The address of its <c>Range</c>.</summary>
	public string Range => range;

	/// <summary>Its <c>ShowHeaders</c>.</summary>
	public bool ShowHeaders => showHeaders;

	/// <summary>Its <c>ShowTotals</c>.</summary>
	public bool ShowTotals => showTotals;

	/// <summary>Its columns' names.</summary>
	public IReadOnlyList<string> Columns => columns;
}

/// <summary>
/// Excel's answers about a corpus workbook (<c>Files/excel-oracle/&lt;file&gt;.json</c>, from the corpus tool's
/// <c>oracle</c> command): each worksheet's cells with a value or a formula, by row and 0-based column; its hidden rows
/// and columns and merged ranges; and the workbook's defined names and tables. A workbook Excel can't open, even by
/// repairing it, is <see cref="Refused"/>, with no answers.
/// </summary>
internal sealed class ExcelAnswers
{
	/// <summary>Creates the answers.</summary>
	/// <param name="refused">Whether Excel can't open the workbook, even by repairing it.</param>
	/// <param name="date1904">Whether Excel says the workbook uses the 1904 date system.</param>
	/// <param name="repaired">What Excel repaired to open the workbook; empty when it opened as it is.</param>
	/// <param name="sheets">The cells of each worksheet, by name.</param>
	/// <param name="structures">The hidden rows and columns and the merged ranges of each worksheet, by name.</param>
	/// <param name="names">The defined names.</param>
	/// <param name="tables">The tables, by worksheet.</param>
	private ExcelAnswers(
		bool refused, bool date1904, IReadOnlyList<string> repaired, Dictionary<string, Dictionary<(long Row, int Column), ExcelCell>> sheets,
		Dictionary<string, ExcelSheetStructure> structures, IReadOnlyList<ExcelNameAnswer> names, IReadOnlyList<ExcelTableAnswer> tables)
	{
		Refused = refused;
		Date1904 = date1904;
		Repaired = repaired;
		Sheets = sheets;
		Structures = structures;
		Names = names;
		Tables = tables;
	}

	/// <summary>Whether Excel can't open the workbook, even by repairing it; it then has no other answers.</summary>
	public bool Refused { get; }

	/// <summary>Whether Excel says the workbook uses the 1904 date system.</summary>
	public bool Date1904 { get; }

	/// <summary>What Excel repaired to open the workbook, as its log says; empty when it opened as it is.</summary>
	public IReadOnlyList<string> Repaired { get; }

	/// <summary>The cells of each worksheet, by name.</summary>
	public Dictionary<string, Dictionary<(long Row, int Column), ExcelCell>> Sheets { get; }

	/// <summary>The hidden rows and columns and the merged ranges of each worksheet, by name.</summary>
	public Dictionary<string, ExcelSheetStructure> Structures { get; }

	/// <summary>The defined names, as COM's <c>Names</c> lists them.</summary>
	public IReadOnlyList<ExcelNameAnswer> Names { get; }

	/// <summary>The tables, by worksheet in tab order and then as COM's <c>ListObjects</c> lists them.</summary>
	public IReadOnlyList<ExcelTableAnswer> Tables { get; }

	/// <summary>The answers about a workbook, read as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The answers, or <see langword="null"/> for a workbook the oracle leaves out.</returns>
	public static ExcelAnswers? Load(string file)
	{
		var path = CorpusFiles.PathOf("excel-oracle/" + file + ".json");
		if (!File.Exists(path))
		{
			return null;
		}

		var bytes = File.ReadAllBytes(path);
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);
		var root = XElement.Load(reader);
		var sheets = new Dictionary<string, Dictionary<(long, int), ExcelCell>>();
		var structures = new Dictionary<string, ExcelSheetStructure>();
		if (Text(root, "refused") == "true")
		{
			return new ExcelAnswers(true, false, [], sheets, structures, [], []);
		}

		foreach (var sheet in root.Element("sheets")!.Elements("item"))
		{
			var rows = Texts(sheet, "hiddenRows").Select(r => r.Split(':')).Select(r => (long.Parse(r[0], CultureInfo.InvariantCulture), long.Parse(r[1], CultureInfo.InvariantCulture))).ToList();
			var columns = Texts(sheet, "hiddenColumns").Select(c => c.Split(':')).Select(c => (ColumnNumber(c[0]), ColumnNumber(c[1]))).ToList();
			structures[Text(sheet, "name")!] = new ExcelSheetStructure(rows, columns, Texts(sheet, "merged"));
			var cells = new Dictionary<(long, int), ExcelCell>();
			foreach (var item in sheet.Element("cells")!.Elements("item"))
			{
				var reference = Text(item, "cell")!;
				var letters = new string([.. reference.TakeWhile(char.IsLetter)]);
				var column = letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1)) - 1;
				var row = long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture);
				var value2 = Text(item, "value2") ?? "";
				cells[(row, column)] = new ExcelCell(
					value2, Text(item, "type")!, Text(item, "text") ?? value2, Text(item, "numberFormat")!, Text(item, "category"), Text(item, "elapsed"),
					Text(item, "formula"), Text(item, "hasArray") == "true");
			}

			sheets[Text(sheet, "name")!] = cells;
		}

		var repaired = root.Element("repaired")?.Elements("item").Select(e => e.Value).ToList() ?? [];
		var names = root.Element("names")!.Elements("item")
			.Select(n => new ExcelNameAnswer(Text(n, "name")!, Text(n, "refersTo")!, Text(n, "sheet"), Text(n, "range"), Text(n, "visible") == "true"))
			.ToList();
		var tables = root.Element("tables")!.Elements("item")
			.Select(t => new ExcelTableAnswer(
				Text(t, "sheet")!, Text(t, "name")!, Text(t, "displayName")!, Text(t, "range")!, Text(t, "showHeaders") == "true", Text(t, "showTotals") == "true",
				Texts(t, "columns")))
			.ToList();

		return new ExcelAnswers(false, Text(root, "date1904") == "true", repaired, sheets, structures, names, tables);
	}

	/// <summary>The texts of an array property of an object of the answers.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its items' texts.</returns>
	private static List<string> Texts(XElement item, string name) => [.. item.Element(name)!.Elements("item").Select(e => e.Value)];

	/// <summary>The 1-based number of a column's letters.</summary>
	/// <param name="letters">The letters, such as <c>XFD</c>.</param>
	/// <returns>The number.</returns>
	private static int ColumnNumber(string letters) => letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1));

	/// <summary>A property of an object of the answers, or <see langword="null"/> when it is missing or null.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement item, string name) =>
		item.Element(name) is { } element && (string?)element.Attribute("type") != "null" ? element.Value : null;
}
