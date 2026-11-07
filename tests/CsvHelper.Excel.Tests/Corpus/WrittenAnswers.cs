using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>A worksheet of a written workbook as Excel 365 reads it through COM.</summary>
/// <param name="name">Its name.</param>
/// <param name="widths">Each column's <c>ColumnWidth</c>, from A to the used range's last.</param>
/// <param name="cells">Its cells with a value or a formula, by row and 0-based column.</param>
internal sealed class WrittenAnswerSheet(string name, IReadOnlyList<double> widths, Dictionary<(long Row, int Column), ExcelCell> cells)
{
	/// <summary>Its name.</summary>
	public string Name => name;

	/// <summary>Each column's <c>ColumnWidth</c>, from A to the used range's last.</summary>
	public IReadOnlyList<double> Widths => widths;

	/// <summary>Its cells with a value or a formula, by row and 0-based column.</summary>
	public Dictionary<(long Row, int Column), ExcelCell> Cells => cells;
}

/// <summary>
/// Excel's answers about a workbook of the written set (<c>Files/excel-written/&lt;name&gt;.json</c>, from the corpus
/// tool's <c>written</c> command): the hash of the workbook Excel read, the culture data it was written with, the screen's
/// DPI, whether Excel opened it as it is, and each worksheet's widths and cells.
/// </summary>
internal sealed class WrittenAnswers
{
	/// <summary>Creates the answers.</summary>
	/// <param name="sha256">The SHA-256 of the workbook's parts.</param>
	/// <param name="cultures">The cultures its formats read, other than the invariant one.</param>
	/// <param name="cultureData">The fingerprint of their data, or <see langword="null"/> when it reads none.</param>
	/// <param name="dpi">The screen's DPI when Excel answered.</param>
	/// <param name="os">The operating system the workbook was written on.</param>
	/// <param name="refused">Whether Excel can't open it, even by repairing it.</param>
	/// <param name="repaired">What Excel repaired to open it; empty when it opened as it is.</param>
	/// <param name="sheets">Its worksheets, in tab order.</param>
	private WrittenAnswers(
		string sha256, IReadOnlyList<string> cultures, string? cultureData, int dpi, string os, bool refused, IReadOnlyList<string> repaired, IReadOnlyList<WrittenAnswerSheet> sheets)
	{
		Sha256 = sha256;
		Cultures = cultures;
		CultureData = cultureData;
		Dpi = dpi;
		Os = os;
		Refused = refused;
		Repaired = repaired;
		Sheets = sheets;
	}

	/// <summary>The SHA-256 of the workbook's parts (<c>Written.PartsHash</c>).</summary>
	public string Sha256 { get; }

	/// <summary>The cultures its formats read, other than the invariant one.</summary>
	public IReadOnlyList<string> Cultures { get; }

	/// <summary>The fingerprint of their data (<c>Written.CultureData</c>), or <see langword="null"/> when it reads none.</summary>
	public string? CultureData { get; }

	/// <summary>The screen's DPI when Excel answered, which its column widths depend on.</summary>
	public int Dpi { get; }

	/// <summary>
	/// The operating system the workbook was written on (<see cref="System.Runtime.InteropServices.RuntimeInformation.OSDescription"/>),
	/// whose ICU .NET reads culture data from on Windows.
	/// </summary>
	public string Os { get; }

	/// <summary>Whether Excel can't open the workbook, even by repairing it; it then has no sheets.</summary>
	public bool Refused { get; }

	/// <summary>What Excel repaired to open the workbook; empty when it opened as it is.</summary>
	public IReadOnlyList<string> Repaired { get; }

	/// <summary>Its worksheets, in tab order.</summary>
	public IReadOnlyList<WrittenAnswerSheet> Sheets { get; }

	/// <summary>The answers' files, by the workbook's name.</summary>
	public static IReadOnlyList<string> Files { get; } =
		[.. Directory.EnumerateFiles(CorpusFiles.PathOf("excel-written"), "*.json").Select(p => Path.GetFileNameWithoutExtension(p)).OrderBy(n => n, StringComparer.Ordinal)];

	/// <summary>The answers about a workbook, read as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <param name="name">The workbook's name, such as <c>types.xlsx</c>.</param>
	/// <returns>The answers.</returns>
	public static WrittenAnswers Load(string name)
	{
		var bytes = File.ReadAllBytes(CorpusFiles.PathOf("excel-written/" + name + ".json"));
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);
		var root = XElement.Load(reader);
		var sha256 = Text(root, "sha256")!;
		var cultures = root.Element("cultures")?.Elements("item").Select(e => e.Value).ToList() ?? [];
		var cultureData = Text(root, "cultureData");
		var dpi = int.Parse(Text(root, "dpi")!, CultureInfo.InvariantCulture);
		var os = Text(root, "os")!;
		if (Text(root, "refused") == "true")
		{
			return new WrittenAnswers(sha256, cultures, cultureData, dpi, os, true, [], []);
		}

		var sheets = new List<WrittenAnswerSheet>();
		foreach (var sheet in root.Element("sheets")!.Elements("item"))
		{
			var widths = sheet.Element("widths")!.Elements("item").Select(e => double.Parse(e.Value, CultureInfo.InvariantCulture)).ToList();
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

			sheets.Add(new WrittenAnswerSheet(Text(sheet, "name")!, widths, cells));
		}

		var repaired = root.Element("repaired")?.Elements("item").Select(e => e.Value).ToList() ?? [];

		return new WrittenAnswers(sha256, cultures, cultureData, dpi, os, false, repaired, sheets);
	}

	/// <summary>A property of an object of the answers, or <see langword="null"/> when it is missing or null.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement item, string name) =>
		item.Element(name) is { } element && (string?)element.Attribute("type") != "null" ? element.Value : null;
}
