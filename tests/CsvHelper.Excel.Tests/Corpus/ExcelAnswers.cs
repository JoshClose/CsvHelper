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
/// Excel's answers about a corpus workbook (<c>Files/excel-oracle/&lt;file&gt;.json</c>, from the corpus tool's
/// <c>oracle</c> command): each worksheet's cells with a value or a formula, by row and 0-based column.
/// </summary>
internal sealed class ExcelAnswers
{
	/// <summary>Creates the answers.</summary>
	/// <param name="date1904">Whether Excel says the workbook uses the 1904 date system.</param>
	/// <param name="repaired">What Excel repaired to open the workbook; empty when it opened as it is.</param>
	/// <param name="sheets">The cells of each worksheet, by name.</param>
	private ExcelAnswers(bool date1904, IReadOnlyList<string> repaired, Dictionary<string, Dictionary<(long Row, int Column), ExcelCell>> sheets)
	{
		Date1904 = date1904;
		Repaired = repaired;
		Sheets = sheets;
	}

	/// <summary>Whether Excel says the workbook uses the 1904 date system.</summary>
	public bool Date1904 { get; }

	/// <summary>What Excel repaired to open the workbook, as its log says; empty when it opened as it is.</summary>
	public IReadOnlyList<string> Repaired { get; }

	/// <summary>The cells of each worksheet, by name.</summary>
	public Dictionary<string, Dictionary<(long Row, int Column), ExcelCell>> Sheets { get; }

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
		foreach (var sheet in root.Element("sheets")!.Elements("item"))
		{
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

		return new ExcelAnswers(Text(root, "date1904") == "true", repaired, sheets);
	}

	/// <summary>A property of an object of the answers, or <see langword="null"/> when it is missing or null.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement item, string name) =>
		item.Element(name) is { } element && (string?)element.Attribute("type") != "null" ? element.Value : null;
}
