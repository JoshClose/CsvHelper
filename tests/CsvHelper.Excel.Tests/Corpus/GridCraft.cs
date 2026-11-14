using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CsvHelper.Excel.Corpus;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// ArtCraft's GridCraft as an oracle: a spreadsheet application that reads workbooks as Excel does, but where the tests list
/// otherwise. Its command-line tool, <c>gridcraft-cli</c>, opens a workbook headless and runs its MCP's commands from a
/// script, a line of JSON out for each (<c>run --script</c>): <c>cell.get</c> gives a cell's type, value, text as shown,
/// formula, number format and link, and <c>sheet.read</c> a sheet's values. <c>info --json</c> gives each worksheet's
/// panes, filter, tables and merged ranges, and the workbook's names, and <c>convert</c> to JSON its model, with the hidden
/// rows and columns. GridCraft recalculates every formula when it opens a workbook. Its JSON is read as XML
/// (<see cref="JsonReaderWriterFactory"/>), which every target has. CI doesn't install it; elsewhere the tests are skipped
/// without it.
/// </summary>
internal static class GridCraft
{
	/// <summary>How long a command may take.</summary>
	private const int Timeout = 120_000;

	/// <summary>GridCraft's version, asked once.</summary>
	private static readonly Lazy<string> AskedVersion = new(() => Run("version").Trim());

	/// <summary>The written set's workbooks as this runtime writes them, in a folder made once for every test.</summary>
	private static readonly Lazy<string> WrittenFolder = new(() =>
	{
		var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.GridCraft." + Guid.NewGuid().ToString("N"))).FullName;
		foreach (var workbook in Written.Workbooks)
		{
			File.WriteAllBytes(Path.Combine(folder, workbook.Name), Written.Write(workbook));
		}

		return folder;
	});

	/// <summary>
	/// GridCraft's <c>gridcraft-cli</c>: <c>CSVHELPER_GRIDCRAFT</c>, or the default install's; <see langword="null"/> when
	/// there is none.
	/// </summary>
	public static string? Program { get; } = new[] { Environment.GetEnvironmentVariable("CSVHELPER_GRIDCRAFT"), @"C:\Program Files\GridCraft\gridcraft-cli.exe" }
		.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

	/// <summary>GridCraft's version, such as <c>gridcraft-cli 0.18.0</c>, for the tests' output and messages.</summary>
	public static string Version => AskedVersion.Value;

	/// <summary>A written workbook as this runtime writes it, saved for GridCraft.</summary>
	/// <param name="workbook">The workbook of the written set.</param>
	/// <returns>The file.</returns>
	public static string WrittenPath(WrittenWorkbook workbook) => Path.Combine(WrittenFolder.Value, workbook.Name);

	/// <summary>Opens a workbook: its worksheets, their panes, filters, tables and merged ranges, and its names.</summary>
	/// <param name="path">The workbook's file.</param>
	/// <returns>What GridCraft reads of it.</returns>
	public static GridCraftWorkbook Open(string path)
	{
		var info = Parse(Run("info", path, "--json"));
		var sheets = Property(info, "sheets")!.Elements().Select(s => new GridCraftSheet(
			Property(s, "name")!.Value,
			Property(s, "visibility")!.Value,
			IsNull(Property(s, "freeze")) ? null : Freeze(Property(s, "freeze")!),
			IsNull(Property(s, "autofilter")) ? null : Property(s, "autofilter")!.Value,
			[.. Property(s, "tables")!.Elements().Select(t => new GridCraftTable(
				Property(t, "name")!.Value,
				Property(t, "range")!.Value,
				Property(t, "style")!.Value,
				Property(t, "totals")!.Value == "true",
				[.. Property(t, "columns")!.Elements().Select(c => c.Value)]))],
			[.. Property(s, "merges")!.Elements().Select(m => m.Value)]))
			.ToList();
		var names = Property(info, "names")!.Elements().Select(n =>
		{
			var scope = Property(n, "scope")!;
			var name = Property(n, "name")!.Value;

			return new GridCraftName(
				IsNull(scope) ? name : SheetLocation.Quote(sheets[int.Parse(scope.Value, CultureInfo.InvariantCulture)].Name) + "!" + name,
				Property(n, "refersTo")!.Value);
		}).ToList();

		return new GridCraftWorkbook(path, sheets, names);
	}

	/// <summary>
	/// A workbook's hidden rows and columns, from its model: rows from 1 and columns from 0, by worksheet. GridCraft's model
	/// holds the rows and columns the file describes.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>Each worksheet's hidden rows and columns, by name.</returns>
	public static Dictionary<string, (IReadOnlyList<long> Rows, IReadOnlyList<int> Columns)> Hidden(GridCraftWorkbook workbook)
	{
		var model = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.GridCraft." + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			Run("convert", workbook.Path, model);
			var root = Parse(File.ReadAllText(model, Encoding.UTF8));
			var hidden = new Dictionary<string, (IReadOnlyList<long>, IReadOnlyList<int>)>(StringComparer.Ordinal);
			foreach (var sheet in Property(root, "sheets")!.Elements())
			{
				static IEnumerable<long> Indexes(XElement? lines) =>
					lines is null || IsNull(lines) ? Enumerable.Empty<long>() : lines.Elements().Where(l => Property(l, "hidden")?.Value == "true").Select(l => long.Parse(NameOf(l), CultureInfo.InvariantCulture));

				hidden[Property(sheet, "name")!.Value] = ([.. Indexes(Property(sheet, "rows")).Select(r => r + 1).OrderBy(r => r)], [.. Indexes(Property(sheet, "cols")).Select(c => (int)c).OrderBy(c => c)]);
			}

			return hidden;
		}
		finally
		{
			File.Delete(model);
		}
	}

	/// <summary>
	/// Reads cells of a workbook: on each worksheet, the cells asked about and every other cell with a value, each through
	/// <c>cell.get</c>.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="asked">The cells asked about on each worksheet, by GridCraft's name of it, by row from 1 and column from 0.</param>
	/// <returns>The cells, by worksheet and then by row and column.</returns>
	public static Dictionary<string, Dictionary<(long Row, int Column), GridCraftCell>> Cells(GridCraftWorkbook workbook, IReadOnlyDictionary<string, IEnumerable<(long Row, int Column)>> asked)
	{
		// First each worksheet's values, for the cells with a value that weren't asked about.
		var values = RunScript(workbook.Path, workbook.Sheets.Select(s => "sheet.read={\"sheet\":" + Json(s.Name) + "}"));
		var wanted = new List<(string Sheet, (long Row, int Column) Key)>();
		foreach (var (sheet, read) in workbook.Sheets.Zip(values, (s, v) => (s, Property(v, "result")!)))
		{
			var cells = new HashSet<(long, int)>(asked.TryGetValue(sheet.Name, out var keys) ? keys : Enumerable.Empty<(long, int)>());
			if (Property(read, "range") is { } range && !IsNull(range))
			{
				var (top, left) = Position(range.Value.Split(':')[0]);
				foreach (var (row, r) in Property(read, "values")!.Elements().Select((row, r) => (row, r)))
				{
					foreach (var (value, c) in row.Elements().Select((value, c) => (value, c)))
					{
						if (!IsNull(value) && !(Type(value) == "string" && value.Value.Length == 0))
						{
							cells.Add((top + r, left + c));
						}
					}
				}
			}

			wanted.AddRange(cells.OrderBy(k => k.Item1).ThenBy(k => k.Item2).Select(k => (sheet.Name, k)));
		}

		var results = RunScript(workbook.Path, wanted.Select(w => "cell.get={\"cell\":" + Json(CellReference.ColumnName(w.Key.Column) + w.Key.Row.ToString(CultureInfo.InvariantCulture)) + ",\"sheet\":" + Json(w.Sheet) + "}"));
		var found = workbook.Sheets.ToDictionary(s => s.Name, _ => new Dictionary<(long Row, int Column), GridCraftCell>(), StringComparer.Ordinal);
		foreach (var (want, result) in wanted.Zip(results, (w, r) => (w, Property(r, "result")!)))
		{
			found[want.Sheet][want.Key] = Cell(result);
		}

		return found;
	}

	/// <summary>
	/// A workbook's first worksheet's cells with a value, row by row, each as its reference, type and text as
	/// <c>Written.ClosedXmlValues</c> gives them: <c>B2=Number:1.5</c>, <c>C2=DateTime:2024-01-15T10:30:00.000</c> for a
	/// number in a format with a year, <c>D2=Boolean:TRUE</c>, <c>E2=Text:a</c>, and <c>F2=Error:#N/A</c>.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The cells' values.</returns>
	public static List<string> Values(byte[] workbook)
	{
		var path = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.GridCraft." + Guid.NewGuid().ToString("N") + ".xlsx");
		try
		{
			File.WriteAllBytes(path, workbook);
			var book = Open(path);
			var cells = Cells(book, new Dictionary<string, IEnumerable<(long Row, int Column)>>())[book.Sheets[0].Name];

			return [.. cells.OrderBy(c => c.Key.Row).ThenBy(c => c.Key.Column).Select(c => $"{CellReference.ColumnName(c.Key.Column)}{c.Key.Row.ToString(CultureInfo.InvariantCulture)}={Value(c.Value)}")];
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>A cell's type and text, as <see cref="Values"/> gives them.</summary>
	/// <param name="cell">The cell.</param>
	/// <returns>Its type and text.</returns>
	private static string Value(GridCraftCell cell) => cell.Type switch
	{
		"number" when cell.NumberFormat.Contains("yy") =>
			"DateTime:" + DateTime.FromOADate(double.Parse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
		"number" => "Number:" + double.Parse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
		"boolean" => "Boolean:" + (cell.Value == "true" ? "TRUE" : "FALSE"),
		"error" => "Error:" + cell.Value,
		"text" => "Text:" + cell.Value,
		_ => cell.Type + ":" + cell.Value,
	};

	/// <summary>A cell from <c>cell.get</c>'s result.</summary>
	/// <param name="result">The result.</param>
	/// <returns>The cell.</returns>
	private static GridCraftCell Cell(XElement result)
	{
		var type = Property(result, "type")!.Value;
		var value = Property(result, "value")!;
		var formula = Property(result, "formula")!;
		var link = Property(result, "hyperlink")!;

		return new GridCraftCell(
			type,
			IsNull(value) ? "" : type == "error" ? Property(value, "error")!.Value : value.Value,
			Property(result, "text")!.Value,
			Property(Property(result, "style")!, "num_fmt")!.Value,
			IsNull(formula) ? null : formula.Value,
			IsNull(link) ? null : Property(link, "target")!.Value,
			!IsNull(Property(result, "spilledFrom")));
	}

	/// <summary>A pane's frozen rows and columns, from <c>info</c>'s <c>[rows, columns]</c>.</summary>
	/// <param name="freeze">The array.</param>
	/// <returns>The rows and columns.</returns>
	private static (int Rows, int Columns) Freeze(XElement freeze)
	{
		var counts = freeze.Elements().Select(e => int.Parse(e.Value, CultureInfo.InvariantCulture)).ToList();

		return (counts[0], counts[1]);
	}

	/// <summary>Runs commands on a workbook from a script, a line each, and gives each command's line of JSON out.</summary>
	/// <param name="path">The workbook's file.</param>
	/// <param name="commands">The commands, each <c>id={json}</c>.</param>
	/// <returns>Each command's output, as XML.</returns>
	private static List<XElement> RunScript(string path, IEnumerable<string> commands)
	{
		var lines = commands.ToList();
		if (lines.Count == 0)
		{
			return [];
		}

		var script = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.GridCraft." + Guid.NewGuid().ToString("N") + ".jsonl");
		try
		{
			File.WriteAllText(script, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
			var output = Run("run", "--in", path, "--script", script).Split('\n').Where(l => l.Trim().Length > 0).Select(Parse).ToList();
			Assert.True(output.Count == lines.Count, $"{Version} answered {output.Count} of {lines.Count} commands on {path}.");

			return output;
		}
		finally
		{
			File.Delete(script);
		}
	}

	/// <summary>Runs <c>gridcraft-cli</c>, failing unless it exits with 0.</summary>
	/// <param name="arguments">Its arguments, none of which holds a quote.</param>
	/// <returns>What it wrote to standard output.</returns>
	private static string Run(params string[] arguments)
	{
		// Arguments as one string, which net48 needs; standard error has GridCraft's warnings.
		var info = new ProcessStartInfo(Program!, string.Join(" ", arguments.Select(a => '"' + a + '"')))
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
		};

		using var process = Process.Start(info)!;
		var stdout = process.StandardOutput.ReadToEndAsync();
		var stderr = process.StandardError.ReadToEndAsync();
		if (!process.WaitForExit(Timeout))
		{
			process.Kill();

			throw new TimeoutException($"GridCraft didn't finish {arguments[0]} within {Timeout / 1000} seconds.");
		}

		Assert.True(process.ExitCode == 0, $"GridCraft's {arguments[0]} exited with {process.ExitCode}: {stderr.Result}");

		return stdout.Result;
	}

	/// <summary>
	/// A JSON document as XML. GridCraft writes U+FFFE and U+FFFF as they are, which the reader refuses, so they are escaped
	/// first, as Excel's answers have them.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <returns>Its root.</returns>
	private static XElement Parse(string json)
	{
		var escaped = json.Replace("\uFFFE", "\\uFFFE").Replace("\uFFFF", "\\uFFFF");
		using var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(escaped), XmlDictionaryReaderQuotas.Max);

		return XElement.Load(reader);
	}

	/// <summary>A property of an object, or <see langword="null"/> when it has none of that name.</summary>
	/// <param name="item">The object.</param>
	/// <param name="name">The property's name.</param>
	/// <returns>The property.</returns>
	private static XElement? Property(XElement item, string name) => item.Elements().FirstOrDefault(e => NameOf(e) == name);

	/// <summary>A property's name: its element's, or its <c>item</c> attribute's for a name that isn't an XML name, such as <c>9</c>.</summary>
	/// <param name="property">The property.</param>
	/// <returns>Its name.</returns>
	private static string NameOf(XElement property) => (string?)property.Attribute("item") ?? property.Name.LocalName;

	/// <summary>A JSON value's type: <c>string</c>, <c>number</c>, <c>boolean</c>, <c>null</c>, <c>object</c> or <c>array</c>.</summary>
	/// <param name="value">The value.</param>
	/// <returns>Its type.</returns>
	private static string Type(XElement value) => (string?)value.Attribute("type") ?? "string";

	/// <summary>Whether a value is missing or JSON's <c>null</c>.</summary>
	/// <param name="value">The value.</param>
	/// <returns>Whether it is null.</returns>
	private static bool IsNull(XElement? value) => value is null || Type(value) == "null";

	/// <summary>A text as a JSON string, every character outside printable ASCII escaped.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The string, quotes around it.</returns>
	private static string Json(string text)
	{
		var json = new StringBuilder("\"");
		foreach (var c in text)
		{
			json.Append(c switch
			{
				'"' => "\\\"",
				'\\' => "\\\\",
				< ' ' or > '~' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
				_ => c.ToString(),
			});
		}

		return json.Append('"').ToString();
	}

	/// <summary>A reference's row and 0-based column.</summary>
	/// <param name="reference">The reference, such as <c>B12</c>.</param>
	/// <returns>The row and column.</returns>
	public static (long Row, int Column) Position(string reference)
	{
		var letters = new string([.. reference.TakeWhile(char.IsLetter)]);

		return (long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture), letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1)) - 1);
	}
}

/// <summary>A cell as GridCraft's <c>cell.get</c> gives it.</summary>
/// <param name="Type">Its type: <c>text</c>, <c>number</c>, <c>boolean</c>, <c>error</c>, or <c>empty</c> for none.</param>
/// <param name="Value">
/// Its value as text: a text, a number as GridCraft writes it in JSON (<c>42.0</c>, <c>1E-05</c>), <c>true</c> or
/// <c>false</c>, an error's text, or empty.
/// </param>
/// <param name="Text">The text it shows, which doesn't depend on its column's width.</param>
/// <param name="NumberFormat">Its number format's code as the file gives it, empty for an xf without one.</param>
/// <param name="Formula">Its formula, <c>=</c> first, or <see langword="null"/>.</param>
/// <param name="Link">Its link's target, its fragment after a <c>#</c>, or <see langword="null"/>.</param>
/// <param name="Spilled">Whether a dynamic array's formula in another cell gives its value.</param>
internal sealed record GridCraftCell(string Type, string Value, string Text, string NumberFormat, string? Formula, string? Link, bool Spilled);

/// <summary>A table as GridCraft's <c>info</c> gives it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Range">Its range, such as <c>A1:C5</c>.</param>
/// <param name="Style">Its style's name, empty for none.</param>
/// <param name="Totals">Whether it has a totals row.</param>
/// <param name="Columns">Its columns' names.</param>
internal sealed record GridCraftTable(string Name, string Range, string Style, bool Totals, IReadOnlyList<string> Columns);

/// <summary>A worksheet as GridCraft's <c>info</c> gives it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Visibility">Its visibility: <c>Visible</c>, <c>Hidden</c> or <c>VeryHidden</c>.</param>
/// <param name="Freeze">The rows and columns frozen, or <see langword="null"/> for no frozen pane.</param>
/// <param name="Filter">Its filter's range, or <see langword="null"/>.</param>
/// <param name="Tables">Its tables.</param>
/// <param name="Merged">Its merged ranges.</param>
internal sealed record GridCraftSheet(string Name, string Visibility, (int Rows, int Columns)? Freeze, string? Filter, IReadOnlyList<GridCraftTable> Tables, IReadOnlyList<string> Merged);

/// <summary>A defined name as GridCraft's <c>info</c> gives it.</summary>
/// <param name="Name">Its name, <c>Sheet!Name</c> for a sheet's own, the sheet quoted as Excel quotes it.</param>
/// <param name="RefersTo">What it refers to, <c>=</c> first.</param>
internal sealed record GridCraftName(string Name, string RefersTo);

/// <summary>A workbook as GridCraft's <c>info</c> gives it.</summary>
/// <param name="Path">Its file.</param>
/// <param name="Sheets">Its worksheets, in tab order.</param>
/// <param name="Names">Its defined names.</param>
internal sealed record GridCraftWorkbook(string Path, IReadOnlyList<GridCraftSheet> Sheets, IReadOnlyList<GridCraftName> Names);

/// <summary>A theory that runs only where GridCraft is installed (<see cref="GridCraft.Program"/>).</summary>
public sealed class GridCraftTheoryAttribute : TheoryAttribute
{
	/// <summary>Creates the attribute, skipping the theory when GridCraft is missing.</summary>
	public GridCraftTheoryAttribute()
	{
		if (GridCraft.Program is null)
		{
			Skip = "GridCraft is not installed (set CSVHELPER_GRIDCRAFT to its gridcraft-cli.exe).";
		}
	}
}

/// <summary>A fact that runs only where GridCraft is installed (<see cref="GridCraft.Program"/>).</summary>
public sealed class GridCraftFactAttribute : FactAttribute
{
	/// <summary>Creates the attribute, skipping the fact when GridCraft is missing.</summary>
	public GridCraftFactAttribute()
	{
		if (GridCraft.Program is null)
		{
			Skip = "GridCraft is not installed (set CSVHELPER_GRIDCRAFT to its gridcraft-cli.exe).";
		}
	}
}
