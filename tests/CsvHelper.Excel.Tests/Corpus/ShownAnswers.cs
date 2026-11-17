using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>A case of the display set as Excel 365 shows it: an item of <c>Files/excel-display.json</c>.</summary>
/// <param name="row">Its row in its sheet: its value is in column C.</param>
/// <param name="code">Its number format's code, or <see langword="null"/> for a built-in id.</param>
/// <param name="builtIn">Its built-in id, or −1 for a code.</param>
/// <param name="kind">Its value's kind: <c>n</c> a number, <c>s</c> text, <c>b</c> a boolean, <c>e</c> an error.</param>
/// <param name="value">Its value's stored text.</param>
/// <param name="text">Excel's <c>Range.Text</c>, in a column 255 wide.</param>
/// <param name="numberFormat">Excel's <c>NumberFormat</c> of the cell.</param>
/// <param name="export">The cell's text in Excel's CSV export.</param>
internal sealed class DisplayCell(long row, string? code, int builtIn, char kind, string value, string text, string numberFormat, string export)
{
	/// <summary>Its row in its sheet.</summary>
	public long Row => row;

	/// <summary>Its number format's code, or <see langword="null"/> for a built-in id.</summary>
	public string? Code => code;

	/// <summary>Its built-in id, or −1 for a code.</summary>
	public int BuiltIn => builtIn;

	/// <summary>Its value's kind: <c>n</c>, <c>s</c>, <c>b</c> or <c>e</c>.</summary>
	public char Kind => kind;

	/// <summary>Its value's stored text.</summary>
	public string Value => value;

	/// <summary>Excel's <c>Range.Text</c>, in a column 255 wide.</summary>
	public string Text => text;

	/// <summary>Excel's <c>NumberFormat</c> of the cell.</summary>
	public string NumberFormat => numberFormat;

	/// <summary>The cell's text in Excel's CSV export.</summary>
	public string Export => export;

	/// <summary>The text CsvHelper.Excel shows, by Phase 8's decision 17, or <see langword="null"/> where Excel can't show the value.</summary>
	public string? Expected => ExcelShown.Expected(text, export, numberFormat);

	/// <inheritdoc/>
	public override string ToString() => $"C{row} {code ?? "id " + builtIn.ToString(CultureInfo.InvariantCulture)} {kind}:{CellValue.Show(value)}";
}

/// <summary>A workbook of the display set and Excel's answers about its cases.</summary>
/// <param name="file">Its file, in <c>Files/display</c>.</param>
/// <param name="date1904">Whether it uses the 1904 date system.</param>
/// <param name="cells">Its cases, by row.</param>
internal sealed class DisplayWorkbook(string file, bool date1904, IReadOnlyList<DisplayCell> cells)
{
	/// <summary>Its file, in <c>Files/display</c>.</summary>
	public string File => file;

	/// <summary>Whether it uses the 1904 date system.</summary>
	public bool Date1904 => date1904;

	/// <summary>Its cases, by row.</summary>
	public IReadOnlyList<DisplayCell> Cells => cells;
}

/// <summary>
/// Excel's answers about the display set (<c>Files/excel-display.json</c>, from the corpus tool's <c>display</c> command):
/// Excel's version and separators, and each workbook's cases.
/// </summary>
internal sealed class DisplayAnswers
{
	/// <summary>The answers, read once.</summary>
	private static readonly Lazy<DisplayAnswers> Loaded = new(Read);

	/// <summary>Creates the answers.</summary>
	/// <param name="excel">Excel's version and build, such as <c>16.0 20527</c>.</param>
	/// <param name="culture">The culture Excel ran in.</param>
	/// <param name="separators">Excel's decimal and thousands separators.</param>
	/// <param name="workbooks">The workbooks, in the order the tool wrote them.</param>
	private DisplayAnswers(string excel, string culture, string separators, IReadOnlyList<DisplayWorkbook> workbooks)
	{
		Excel = excel;
		Culture = culture;
		Separators = separators;
		Workbooks = workbooks;
	}

	/// <summary>The answers.</summary>
	public static DisplayAnswers Instance => Loaded.Value;

	/// <summary>Excel's version and build, such as <c>16.0 20527</c>.</summary>
	public string Excel { get; }

	/// <summary>The culture Excel ran in.</summary>
	public string Culture { get; }

	/// <summary>Excel's decimal and thousands separators, such as <c>.,</c>.</summary>
	public string Separators { get; }

	/// <summary>The workbooks, in the order the tool wrote them.</summary>
	public IReadOnlyList<DisplayWorkbook> Workbooks { get; }

	/// <summary>The workbooks' files, as theory data.</summary>
	public static IEnumerable<object[]> Files => Instance.Workbooks.Select(w => new object[] { w.File });

	/// <summary>The full path of a workbook of the set.</summary>
	/// <param name="file">The workbook's file.</param>
	/// <returns>The path.</returns>
	public static string PathOf(string file) => CorpusFiles.PathOf("display/" + file);

	/// <summary>Reads the answers, as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <returns>The answers.</returns>
	private static DisplayAnswers Read()
	{
		var bytes = System.IO.File.ReadAllBytes(CorpusFiles.PathOf("excel-display.json"));
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);
		var root = XElement.Load(reader);
		var workbooks = root.Element("workbooks")!.Elements("item").Select(w => new DisplayWorkbook(
			w.Element("file")!.Value,
			w.Element("date1904")!.Value == "true",
			[.. w.Element("cells")!.Elements("item").Select(c =>
			{
				var text = c.Element("text")!.Value;
				var code = c.Element("code")?.Value;

				return new DisplayCell(
					long.Parse(c.Element("row")!.Value, CultureInfo.InvariantCulture),
					code,
					c.Element("builtin") is { } id ? int.Parse(id.Value, CultureInfo.InvariantCulture) : -1,
					c.Element("kind")!.Value[0],
					c.Element("value")!.Value,
					text,
					c.Element("numberFormat")?.Value ?? code!,
					c.Element("export")?.Value ?? text);
			})]))
			.ToList();

		return new DisplayAnswers(
			root.Element("excel")!.Value + " " + root.Element("build")!.Value,
			root.Element("culture")!.Value,
			root.Element("decimalSeparator")!.Value + root.Element("thousandsSeparator")!.Value,
			workbooks);
	}
}

/// <summary>A worksheet as Excel's own CSV export writes it.</summary>
/// <param name="name">Its name.</param>
/// <param name="used">Its used range, such as <c>A1:F12</c>: the export starts at its top-left cell.</param>
/// <param name="visible">Its <c>Visible</c> as the workbook has it: −1 visible, 0 hidden, 2 very hidden.</param>
/// <param name="exported">Whether Excel exported it: it doesn't show some hidden sheets.</param>
/// <param name="bom">Whether the file began with a byte-order mark.</param>
/// <param name="csv">The CSV's text after the byte-order mark.</param>
internal sealed class ExcelExportSheet(string name, string used, int visible, bool exported, bool bom, string csv)
{
	/// <summary>The records, read once.</summary>
	private readonly Lazy<List<string[]>> records = new(() => Parse(csv));

	/// <summary>Its name.</summary>
	public string Name => name;

	/// <summary>Its used range, such as <c>A1:F12</c>.</summary>
	public string Used => used;

	/// <summary>Its <c>Visible</c> as the workbook has it.</summary>
	public int Visible => visible;

	/// <summary>Whether Excel exported it.</summary>
	public bool Exported => exported;

	/// <summary>Whether the file began with a byte-order mark.</summary>
	public bool Bom => bom;

	/// <summary>The CSV's text after the byte-order mark.</summary>
	public string Csv => csv;

	/// <summary>The used range's top-left cell: the export's first record's row, and its first field's column, from 0.</summary>
	public (long Row, int Column) Start => WrittenAnswers.Position(used.Split(':')[0]);

	/// <summary>The export's records, each field the text of a cell from <see cref="Start"/>.</summary>
	public List<string[]> Records => records.Value;

	/// <summary>A cell's text in the export, or <see langword="null"/> outside it.</summary>
	/// <param name="row">The cell's row, from 1.</param>
	/// <param name="column">Its column, from 0.</param>
	/// <returns>The text.</returns>
	public string? Text(long row, int column)
	{
		var (top, left) = Start;
		var r = row - top;
		var c = column - left;
		if (r < 0 || c < 0 || r >= Records.Count)
		{
			return null;
		}

		var record = Records[(int)r];

		return c < record.Length ? record[c] : "";
	}

	/// <summary>Parses a CSV export.</summary>
	/// <param name="csv">Its text.</param>
	/// <returns>Its records.</returns>
	private static List<string[]> Parse(string csv)
	{
		using var reader = CsvReader.FromText(csv, new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false });
		var records = new List<string[]>();
		while (reader.Read())
		{
			records.Add([.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)]);
		}

		return records;
	}
}

/// <summary>
/// Excel's own CSV UTF-8 export of each worksheet of a workbook (<c>Files/excel-csv/&lt;file&gt;.json</c>, from the corpus
/// tool's <c>exports</c> command), for the corpus workbooks with Excel's answers and the written set.
/// </summary>
internal sealed class ExcelExport
{
	/// <summary>Creates the exports.</summary>
	/// <param name="refused">Whether Excel can't open the workbook, even by repairing it.</param>
	/// <param name="repaired">Whether Excel opened it by repairing it.</param>
	/// <param name="sha256">A written workbook's <c>Written.PartsHash</c>, or <see langword="null"/>.</param>
	/// <param name="cultureData">A written workbook's fingerprint of the culture data it read, or <see langword="null"/>.</param>
	/// <param name="sheets">Its worksheets' exports, in tab order.</param>
	private ExcelExport(bool refused, bool repaired, string? sha256, string? cultureData, IReadOnlyList<ExcelExportSheet> sheets)
	{
		Refused = refused;
		Repaired = repaired;
		Sha256 = sha256;
		CultureData = cultureData;
		Sheets = sheets;
	}

	/// <summary>Whether Excel can't open the workbook, even by repairing it.</summary>
	public bool Refused { get; }

	/// <summary>Whether Excel opened it by repairing it.</summary>
	public bool Repaired { get; }

	/// <summary>A written workbook's <c>Written.PartsHash</c>, or <see langword="null"/>.</summary>
	public string? Sha256 { get; }

	/// <summary>A written workbook's fingerprint of the culture data it read, or <see langword="null"/>.</summary>
	public string? CultureData { get; }

	/// <summary>Its worksheets' exports, in tab order.</summary>
	public IReadOnlyList<ExcelExportSheet> Sheets { get; }

	/// <summary>The exports of a workbook, read as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <param name="file">The workbook, relative to the corpus folder: <c>excel/basic.xlsx</c>, or <c>excel-written/types.xlsx</c>.</param>
	/// <returns>The exports, or <see langword="null"/> for a workbook without them.</returns>
	public static ExcelExport? Load(string file)
	{
		var path = CorpusFiles.PathOf("excel-csv/" + file + ".json");
		if (!System.IO.File.Exists(path))
		{
			return null;
		}

		var bytes = System.IO.File.ReadAllBytes(path);
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);
		var root = XElement.Load(reader);
		var sheets = root.Element("sheets")?.Elements("item").Select(s => new ExcelExportSheet(
			s.Element("name")!.Value,
			s.Element("used")!.Value,
			s.Element("visible") is { } visible ? int.Parse(visible.Value, CultureInfo.InvariantCulture) : -1,
			s.Element("exported")?.Value != "false",
			s.Element("bom")?.Value == "true",
			s.Element("csv")?.Value ?? ""))
			.ToList() ?? [];

		return new ExcelExport(root.Element("refused")?.Value == "true", root.Element("repaired")?.Value == "true", root.Element("sha256")?.Value, root.Element("cultureData")?.Value, sheets);
	}
}

/// <summary>Which of Excel's texts of a cell CsvHelper.Excel's <see cref="ExcelText.Formatted"/> shows (Phase 8's decision 17).</summary>
internal static class ExcelShown
{
	/// <summary>
	/// The text CsvHelper.Excel shows of a cell, from Excel's two: <c>Range.Text</c>, but Excel's CSV export where the
	/// column's width changes <c>Range.Text</c> (a <c>*</c> fill repeated to it, General shortened to it, <c>#</c> where the
	/// text doesn't fit it, a text cut at 8,221 characters); or <see langword="null"/> where Excel can't show the value at any
	/// width (<c>#</c> in both).
	/// </summary>
	/// <param name="text">Excel's <c>Range.Text</c>.</param>
	/// <param name="export">The cell's text in Excel's CSV export.</param>
	/// <param name="numberFormat">The cell's number format, as Excel gives it.</param>
	/// <returns>The text, or <see langword="null"/>.</returns>
	public static string? Expected(string text, string export, string numberFormat)
	{
		var shown = text;
		if (export != text && (Hashes(text) || numberFormat.Contains('*') || numberFormat == "General" || (text.Length >= 8221 && export.StartsWith(text, StringComparison.Ordinal))))
		{
			shown = export;
		}

		return Hashes(shown) ? null : shown;
	}

	/// <summary>Whether a text is a run of <c>#</c>, Excel's sign that it can't show a value in its column.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether it is.</returns>
	public static bool Hashes(string text) => text.Length > 0 && text.All(c => c == '#');
}
