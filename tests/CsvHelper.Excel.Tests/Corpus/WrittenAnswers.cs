using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>A worksheet of a written workbook as Excel 365 reads it through COM.</summary>
/// <param name="name">Its name.</param>
/// <param name="widths">Each column's <c>ColumnWidth</c>, from A to the used range's last.</param>
/// <param name="cells">Its cells with a value or a formula, by row and 0-based column.</param>
/// <param name="look">Its look.</param>
/// <param name="autoFit">Each column's <c>ColumnWidth</c> at Excel's own AutoFit, from A; <see langword="null"/> for a hidden column.</param>
/// <param name="used">Its used range's last row and last 0-based column, whose cells and the row below them its look covers.</param>
internal sealed class WrittenAnswerSheet(
	string name, IReadOnlyList<double> widths, Dictionary<(long Row, int Column), ExcelCell> cells, SheetLook look, IReadOnlyList<double?> autoFit, (long Row, int Column) used)
{
	/// <summary>Its name.</summary>
	public string Name => name;

	/// <summary>Its used range's last row and last 0-based column, whose cells and the row below them its look covers.</summary>
	public (long Row, int Column) Used => used;

	/// <summary>Each column's <c>ColumnWidth</c>, from A to the used range's last.</summary>
	public IReadOnlyList<double> Widths => widths;

	/// <summary>Its cells with a value or a formula, by row and 0-based column.</summary>
	public Dictionary<(long Row, int Column), ExcelCell> Cells => cells;

	/// <summary>Its look: panes, filter, tables, merged ranges, links, hidden columns, tall rows and cells' looks.</summary>
	public SheetLook Look => look;

	/// <summary>Each column's <c>ColumnWidth</c> at Excel's own AutoFit, from A; <see langword="null"/> for a hidden column.</summary>
	public IReadOnlyList<double?> AutoFit => autoFit;
}

/// <summary>A table as Excel reads it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Range">Its range, such as <c>A1:C4</c>.</param>
/// <param name="Style">Its style's name, or <see langword="null"/> for none.</param>
/// <param name="Stripes">Whether its rows are banded.</param>
/// <param name="Buttons">Whether its header has filter buttons.</param>
/// <param name="Columns">Its columns' names.</param>
internal sealed record SheetTable(string Name, string Range, string? Style, bool Stripes, bool Buttons, IReadOnlyList<string> Columns)
{
	/// <inheritdoc/>
	public override string ToString() => $"table {Name} {Range} style {Style ?? "none"}{(Stripes ? " stripes" : "")}{(Buttons ? " buttons" : "")} [{string.Join("|", Columns.Select(CellValue.Show))}]";
}

/// <summary>A link as Excel reads it.</summary>
/// <param name="Cell">Its cell, such as <c>B2</c>.</param>
/// <param name="Address">Its <c>Address</c>, or <see langword="null"/> for a jump within the workbook.</param>
/// <param name="SubAddress">Its <c>SubAddress</c>: the target's fragment, or <see langword="null"/>.</param>
internal sealed record SheetLink(string Cell, string? Address, string? SubAddress)
{
	/// <inheritdoc/>
	public override string ToString() => $"link {Cell} {CellValue.Show(Address ?? "-")}{(SubAddress is null ? "" : " # " + CellValue.Show(SubAddress))}";
}

/// <summary>
/// A worksheet's look, as Excel reads it or as the written set should show: what the comparisons of
/// <c>WrittenAnswersTests</c> describe in lines, each starting with what it is about.
/// </summary>
internal sealed class SheetLook
{
	/// <summary>The rows and columns frozen, or <see langword="null"/> for no frozen pane.</summary>
	public (int Rows, int Columns)? Freeze { get; init; }

	/// <summary>Its filter's range, or <see langword="null"/>.</summary>
	public string? Filter { get; init; }

	/// <summary>Its tables.</summary>
	public IReadOnlyList<SheetTable> Tables { get; init; } = [];

	/// <summary>Its merged ranges.</summary>
	public IReadOnlyList<string> Merged { get; init; } = [];

	/// <summary>Its links.</summary>
	public IReadOnlyList<SheetLink> Links { get; init; } = [];

	/// <summary>Its hidden columns' letters.</summary>
	public IReadOnlyList<string> HiddenColumns { get; init; } = [];

	/// <summary>The rows Excel sizes taller than its standard height.</summary>
	public IReadOnlyList<long> TallRows { get; init; } = [];

	/// <summary>
	/// Each cell's look where it isn't Normal's, by reference: <c>bold</c>, <c>size 14</c>, <c>Hyperlink</c> (the cell style,
	/// underlined in its colour), <c>wrap</c>, and for a cell without a value <c>format</c> and its code, separated by commas.
	/// </summary>
	public IReadOnlyDictionary<string, string> Cells { get; init; } = new Dictionary<string, string>();

	/// <summary>
	/// The look as entries to compare, by what each is about: <c>freeze</c>, <c>filter</c>, <c>table</c> and the table's
	/// name, <c>merged</c> and the range, <c>link</c> and its cell, <c>hidden</c> and the column, <c>row</c> and a tall
	/// row's number, and each cell's reference for its look.
	/// </summary>
	/// <returns>The entries.</returns>
	public Dictionary<string, string> Entries()
	{
		var entries = new Dictionary<string, string>(StringComparer.Ordinal);
		if (Freeze is { } freeze)
		{
			entries["freeze"] = $"{freeze.Rows} rows and {freeze.Columns} columns frozen";
		}

		if (Filter is not null)
		{
			entries["filter"] = "a filter over " + Filter;
		}

		foreach (var table in Tables)
		{
			entries["table " + table.Name] = table.ToString();
		}

		foreach (var range in Merged)
		{
			entries["merged " + range] = "merged";
		}

		foreach (var link in Links)
		{
			entries["link " + link.Cell] = link.ToString();
		}

		foreach (var column in HiddenColumns)
		{
			entries["hidden " + column] = "hidden";
		}

		foreach (var row in TallRows)
		{
			entries["row " + row.ToString(CultureInfo.InvariantCulture)] = "taller than the standard height";
		}

		foreach (var cell in Cells)
		{
			entries[cell.Key] = cell.Value;
		}

		return entries;
	}
}

/// <summary>A defined name as Excel reads it.</summary>
/// <param name="Name">Its name, <c>Sheet!Name</c> for a sheet's own.</param>
/// <param name="RefersTo">What it refers to, <c>=</c> first.</param>
/// <param name="Visible">Whether Excel shows it.</param>
internal sealed record WrittenName(string Name, string RefersTo, bool Visible);

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
	/// <param name="names">Its defined names.</param>
	/// <param name="sheets">Its worksheets, in tab order.</param>
	private WrittenAnswers(
		string sha256, IReadOnlyList<string> cultures, string? cultureData, int dpi, string os, bool refused, IReadOnlyList<string> repaired, IReadOnlyList<WrittenName> names,
		IReadOnlyList<WrittenAnswerSheet> sheets)
	{
		Sha256 = sha256;
		Cultures = cultures;
		CultureData = cultureData;
		Dpi = dpi;
		Os = os;
		Refused = refused;
		Repaired = repaired;
		Names = names;
		Sheets = sheets;
	}

	/// <summary>The <c>Color</c> of Excel's <c>Hyperlink</c> cell style's font, <c>#0563C1</c> as Excel's BGR.</summary>
	public const double HyperlinkColor = 0xC16305;

	/// <summary><c>xlUnderlineStyleSingle</c>, the <c>Hyperlink</c> cell style's underline.</summary>
	public const int SingleUnderline = 2;

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

	/// <summary>Its defined names, Excel's own (<c>_xlfn.</c>) among them.</summary>
	public IReadOnlyList<WrittenName> Names { get; }

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
			return new WrittenAnswers(sha256, cultures, cultureData, dpi, os, true, [], [], []);
		}

		var names = root.Element("names")!.Elements("item").Select(n => new WrittenName(Text(n, "name")!, Text(n, "refersTo")!, Text(n, "visible") == "true")).ToList();
		var sheets = new List<WrittenAnswerSheet>();
		foreach (var sheet in root.Element("sheets")!.Elements("item"))
		{
			var widths = sheet.Element("widths")!.Elements("item").Select(e => double.Parse(e.Value, CultureInfo.InvariantCulture)).ToList();
			var cells = new Dictionary<(long, int), ExcelCell>();
			foreach (var item in sheet.Element("cells")!.Elements("item"))
			{
				var (row, column) = Reference(Text(item, "cell")!);
				var value2 = Text(item, "value2") ?? "";
				cells[(row, column)] = new ExcelCell(
					value2, Text(item, "type")!, Text(item, "text") ?? value2, Text(item, "numberFormat")!, Text(item, "category"), Text(item, "elapsed"),
					Text(item, "formula"), Text(item, "hasArray") == "true");
			}

			var autoFit = sheet.Element("autoFit")!.Elements("item").Select(e => Text(e) is { } w ? double.Parse(w, CultureInfo.InvariantCulture) : (double?)null).ToList();
			var used = Text(sheet, "used")!;
			sheets.Add(new WrittenAnswerSheet(Text(sheet, "name")!, widths, cells, Look(sheet), autoFit, Reference(used.Substring(used.IndexOf(':') + 1))));
		}

		var repaired = root.Element("repaired")?.Elements("item").Select(e => e.Value).ToList() ?? [];

		return new WrittenAnswers(sha256, cultures, cultureData, dpi, os, false, repaired, names, sheets);
	}

	/// <summary>A cell reference's row and 0-based column.</summary>
	/// <param name="reference">The reference, such as <c>B12</c>.</param>
	/// <returns>The row and column.</returns>
	private static (long Row, int Column) Reference(string reference)
	{
		var letters = new string([.. reference.TakeWhile(char.IsLetter)]);

		return (long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture), letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1)) - 1);
	}

	/// <summary>A sheet's look as Excel answered it.</summary>
	/// <param name="sheet">The sheet's answers.</param>
	/// <returns>The look.</returns>
	private static SheetLook Look(XElement sheet)
	{
		var freeze = sheet.Element("freeze") is { } pane && (string?)pane.Attribute("type") != "null"
			? (int.Parse(Text(pane, "rows")!, CultureInfo.InvariantCulture), int.Parse(Text(pane, "columns")!, CultureInfo.InvariantCulture))
			: ((int, int)?)null;
		var standard = double.Parse(Text(sheet, "standardHeight")!, CultureInfo.InvariantCulture);
		var cells = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var item in sheet.Element("looks")!.Elements("item"))
		{
			var parts = new List<string>();
			if (Text(item, "bold") == "true")
			{
				parts.Add("bold");
			}

			if (Text(item, "size") is { } size)
			{
				parts.Add("size " + size);
			}

			if (Text(item, "font") is { } font)
			{
				parts.Add("font " + font);
			}

			var style = Text(item, "style");
			var underline = Text(item, "underline");
			var color = Text(item, "color");
			if (style == "Hyperlink"
				&& underline == SingleUnderline.ToString(CultureInfo.InvariantCulture)
				&& color is not null && double.Parse(color, CultureInfo.InvariantCulture) == HyperlinkColor)
			{
				parts.Add("Hyperlink");
			}
			else
			{
				parts.AddRange(new[] { ("style ", style), ("underline ", underline), ("color ", color) }.Where(p => p.Item2 is not null).Select(p => p.Item1 + p.Item2));
			}

			if (Text(item, "wrap") == "true")
			{
				parts.Add("wrap");
			}

			if (Text(item, "numberFormat") is { } format)
			{
				parts.Add("format " + format);
			}

			cells[Text(item, "cell")!] = string.Join(", ", parts);
		}

		return new SheetLook
		{
			Freeze = freeze,
			Filter = Text(sheet, "filter"),
			Tables =
			[
				.. sheet.Element("tables")!.Elements("item").Select(t => new SheetTable(
					Text(t, "name")!, Text(t, "range")!, Text(t, "style"), Text(t, "stripes") == "true", Text(t, "buttons") == "true",
					[.. t.Element("columns")!.Elements("item").Select(c => c.Value)])),
			],
			Merged = [.. sheet.Element("merged")!.Elements("item").Select(m => m.Value)],
			Links = [.. sheet.Element("links")!.Elements("item").Select(l => new SheetLink(Text(l, "cell")!, Text(l, "address"), Text(l, "subAddress")))],
			HiddenColumns = [.. sheet.Element("hiddenColumns")!.Elements("item").Select(c => c.Value)],
			TallRows =
			[
				.. sheet.Element("heights")!.Elements("item")
					.Where(h => double.Parse(Text(h, "height")!, CultureInfo.InvariantCulture) > standard)
					.Select(h => long.Parse(Text(h, "row")!, CultureInfo.InvariantCulture)),
			],
			Cells = cells,
		};
	}

	/// <summary>A property of an object of the answers, or <see langword="null"/> when it is missing or null.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement item, string name) => item.Element(name) is { } element ? Text(element) : null;

	/// <summary>A value of the answers, or <see langword="null"/> when it is null.</summary>
	/// <param name="element">The value.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement element) => (string?)element.Attribute("type") != "null" ? element.Value : null;
}
