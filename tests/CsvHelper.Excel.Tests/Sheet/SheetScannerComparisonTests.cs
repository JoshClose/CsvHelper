using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// Generated sheets, read in every <see cref="SheetMode"/>, with empty rows skipped and kept, and compared with a
/// reference that applies the placement and value rules to an <see cref="XDocument"/> of the sheet.
/// </summary>
public class SheetScannerComparisonTests
{
	/// <summary>The shared strings of the generated sheets.</summary>
	private static readonly string[] SharedTexts = ["alpha", "", " spaced ", "東京", "x&y"];

	/// <summary>A row's attributes, in forms Excel hides and forms it doesn't.</summary>
	private static readonly string[] RowForms =
	[
		" hidden=\"1\"", " hidden=\"true\"", " hidden=\"&#49;\"", " hidden=\"0\"", " hidden=\" 1 \"", " ht=\"0\" customHeight=\"1\"", " ht=\"0\"",
		" ht=\"0.0\" customHeight=\"true\"", " ht=\"15\" customHeight=\"1\"", " ht=\"+0\" customHeight=\"1\"", " ht=\"\" customHeight=\"1\"",
	];

	/// <summary>The row attributes of <c>sheetFormatPr</c>: some hide the rows the file lacks.</summary>
	private static readonly string[] RowFormats =
	[
		" defaultRowHeight=\"15\"", " defaultRowHeight=\"15\" zeroHeight=\"1\"", " defaultRowHeight=\"15\" zeroHeight=\"0\"",
		" defaultRowHeight=\"0\" customHeight=\"1\"", " defaultRowHeight=\"0\"",
	];

	/// <summary>The column attribute of <c>sheetFormatPr</c>: some hide the columns no <c>col</c> covers.</summary>
	private static readonly string[] ColumnFormats = ["", "", " defaultColWidth=\"0\"", " defaultColWidth=\"9\"", " defaultColWidth=\" 0 \""];

	/// <summary>A <c>col</c>'s attributes, in forms Excel hides and forms it doesn't.</summary>
	private static readonly string[] ColumnForms =
	[
		" width=\"9\"", " width=\"9\"", " width=\"9\" hidden=\"1\"", " width=\"0\"", "", " width=\"9\" hidden=\"yes\"", " width=\"abc\"",
		" width=\"9\" hidden=\"true\"", " width=\"-1\"",
	];

	/// <summary>The fragments the text of a value is made of.</summary>
	private static readonly string[] Fragments =
	[
		"Name", "a b", "  ", "東京", "é", "&amp;", "&lt;", "&#233;", "&#x1F600;", "\r\n", "\t",
		"_x0041_", "_x005F_", "_xD83D__xDE00_", "_xD83D_", "_X0041_", "_x", "<![CDATA[<b>&]]>", "<!-- c -->",
	];

	[Fact]
	public async Task MatchesTheReferenceOnGeneratedSheets()
	{
		var strings = Sheets.Strings(SharedTexts);
		for (var seed = 0; seed < 100; seed++)
		{
			var xml = Generate(new Random(seed));
			foreach (var skipEmptyRows in new[] { true, false })
			{
				var expected = Reference(xml, skipEmptyRows);
				await Sheets.InEveryModeAsync(async mode =>
				{
					var actual = await Sheets.ReadAsync(xml, mode, skipEmptyRows, strings);

					Assert.True(expected.SequenceEqual(actual), $"Seed {seed}, skipping empty rows {skipEmptyRows}:\nexpected {string.Join("\n", expected)}\nactual {string.Join("\n", actual)}\n{xml}");
				});
			}
		}
	}

	[Fact]
	public async Task MatchesTheReferenceInGeneratedAreas()
	{
		var strings = Sheets.Strings(SharedTexts);
		for (var seed = 0; seed < 100; seed++)
		{
			var random = new Random(seed);
			var xml = Generate(random);

			// Areas that start and end anywhere in the generated rows and columns, or past them.
			var firstRow = random.Next(1, 12);
			var lastRow = random.Next(4) == 0 ? CellReference.MaxRow : firstRow + random.Next(0, 10);
			var firstColumn = random.Next(0, 6);
			var lastColumn = random.Next(4) == 0 ? CellReference.MaxColumns - 1 : firstColumn + random.Next(0, 5);
			var layout = new SheetLayout(firstRow, lastRow, firstColumn, lastColumn);
			foreach (var skipEmptyRows in new[] { true, false })
			{
				var expected = Reference(xml, skipEmptyRows, layout);
				await Sheets.InEveryModeAsync(async mode =>
				{
					var actual = await Sheets.ReadAsync(xml, mode, skipEmptyRows, strings, reading: new CellReading(layout: layout));

					Assert.True(
						expected.SequenceEqual(actual),
						$"Seed {seed}, area rows {firstRow}-{lastRow} columns {firstColumn}-{lastColumn}, skipping empty rows {skipEmptyRows}:\nexpected {string.Join("\n", expected)}\nactual {string.Join("\n", actual)}\n{xml}");
				});
			}
		}
	}

	[Fact]
	public async Task MatchesTheReferenceWithHiddenCells()
	{
		var strings = Sheets.Strings(SharedTexts);
		for (var seed = 0; seed < 100; seed++)
		{
			var random = new Random(seed);
			var xml = Generate(random, hidden: true);
			var (rows, columns) = random.Next(3) switch
			{
				0 => (true, false),
				1 => (false, true),
				_ => (true, true),
			};
			SheetLayout? layout = null;
			if (random.Next(2) == 0)
			{
				var firstRow = random.Next(1, 8);
				var firstColumn = random.Next(0, 4);
				layout = new SheetLayout(firstRow, random.Next(3) == 0 ? CellReference.MaxRow : firstRow + random.Next(0, 10), firstColumn, random.Next(3) == 0 ? CellReference.MaxColumns - 1 : firstColumn + random.Next(0, 6));
			}

			foreach (var skipEmptyRows in new[] { true, false })
			{
				var expected = Reference(xml, skipEmptyRows, layout, rows, columns);
				await Sheets.InEveryModeAsync(async mode =>
				{
					var reading = new CellReading(layout: layout, skipHiddenRows: rows, skipHiddenColumns: columns);
					var actual = await Sheets.ReadAsync(xml, mode, skipEmptyRows, strings, reading: reading);

					Assert.True(
						expected.SequenceEqual(actual),
						$"Seed {seed}, hidden rows {rows}, columns {columns}, area {(layout is null ? "none" : $"rows {layout.FirstRow}-{layout.LastRow} columns {layout.FirstColumn}-{layout.LastColumn}")}, skipping empty rows {skipEmptyRows}:\nexpected {string.Join("\n", expected)}\nactual {string.Join("\n", actual)}\n{xml}");
				});
			}
		}
	}

	/// <summary>
	/// Generates a sheet: rows and cells with and without references, every cell type, valueless cells and noise; with
	/// <paramref name="hidden"/>, also <c>sheetFormatPr</c>, <c>cols</c> and rows in forms that Excel hides and forms it doesn't.
	/// </summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="hidden">Whether to hide rows and columns; without it, the choices are those of the sheets before hidden cells.</param>
	/// <returns>The part's XML.</returns>
	private static string Generate(Random random, bool hidden = false)
	{
		var p = random.Next(4) == 0 ? "x:" : "";
		var ns = random.Next(5) == 0 ? TestPackage.StrictMainNamespace : TestPackage.MainNamespace;
		var indent = random.Next(3) == 0 ? "\r\n  " : "";
		var xml = new StringBuilder(TestPackage.Declaration);
		xml.Append(p.Length > 0 ? $"<x:worksheet xmlns:x=\"{ns}\">" : $"<worksheet xmlns=\"{ns}\">");
		xml.Append($"<{p}dimension ref=\"A1:C3\"/><{p}sheetViews><{p}sheetView workbookViewId=\"0\"/></{p}sheetViews>");
		if (hidden)
		{
			xml.Append($"<{p}sheetFormatPr{Pick(random, RowFormats)}{Pick(random, ColumnFormats)}/><{p}cols>");
			for (var cols = random.Next(0, 5); cols >= 0; cols--)
			{
				var min = random.Next(1, 9);
				var max = random.Next(6) == 0 ? CellReference.MaxColumns + random.Next(2) : min + random.Next(0, 4);
				xml.Append($"<{p}col min=\"{min}\" max=\"{max}\"{Pick(random, ColumnForms)}/>");
			}

			xml.Append($"</{p}cols>");
		}
		else
		{
			xml.Append($"<{p}cols><{p}col min=\"1\" max=\"2\" width=\"9\"/></{p}cols>");
		}

		xml.Append($"<{p}sheetData>");
		long lastRow = 0;
		for (var rows = random.Next(0, 12); rows > 0; rows--)
		{
			var row = lastRow + (random.Next(4) == 0 ? random.Next(2, 5) : 1);
			var rowReference = row != lastRow + 1 ? random.Next(3) != 0 : random.Next(2) == 0;

			// Without a reference, a row that skips rows takes its number from its first cell's.
			var cellCount = random.Next(rowReference || row == lastRow + 1 ? 0 : 1, 7);
			xml.Append(indent);
			if (random.Next(10) == 0)
			{
				xml.Append($"<{p}unknown><{p}row r=\"999\"><{p}c><{p}v>9</{p}v></{p}c></{p}row></{p}unknown>");
			}

			var spans = random.Next(3) == 0 ? " spans=\"1:3\"" : "";
			var rowSpace = random.Next(8) == 0 ? " xml:space=\"preserve\"" : "";
			var rowHidden = hidden && random.Next(2) == 0 ? Pick(random, RowForms) : "";
			xml.Append(rowReference ? $"<{p}row r=\"{row}\"{spans}{rowSpace}{rowHidden}" : $"<{p}row{spans}{rowSpace}{rowHidden}");
			if (cellCount == 0 && random.Next(2) == 0)
			{
				xml.Append("/>");
				lastRow = row;

				continue;
			}

			xml.Append('>');
			var column = -1;
			for (var i = 0; i < cellCount; i++)
			{
				var next = column + (random.Next(5) == 0 ? random.Next(2, 6) : 1);
				var reference = next != column + 1 || (i == 0 && !rowReference && row != lastRow + 1) || random.Next(2) == 0;
				column = next;
				if (random.Next(12) == 0)
				{
					xml.Append($"<{p}extLst><{p}ext uri=\"x\"><{p}c r=\"ZZ{row}\"><{p}v>no</{p}v></{p}c></{p}ext></{p}extLst>");
				}

				var name = CellReference.ColumnName(column);
				var at = reference ? $" r=\"{(random.Next(4) == 0 ? name.ToLowerInvariant() : name)}{row}\"" : "";
				xml.Append(indent).Append(Cell(random, p, at));
			}

			xml.Append(indent).Append($"</{p}row>");
			lastRow = row;
		}

		xml.Append(indent).Append($"</{p}sheetData><{p}pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></{p}worksheet>");

		return xml.ToString();
	}

	/// <summary>One of some choices.</summary>
	/// <param name="random">The source of the choice.</param>
	/// <param name="choices">The choices.</param>
	/// <returns>The choice.</returns>
	private static string Pick(Random random, string[] choices) => choices[random.Next(choices.Length)];

	/// <summary>Generates a cell of a random type, or a valueless one.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="p">The prefix of the elements.</param>
	/// <param name="at">The cell's <c>r</c> attribute, with a space before it, or nothing.</param>
	/// <returns>The cell's XML.</returns>
	private static string Cell(Random random, string p, string at)
	{
		string Value(string text) => random.Next(4) switch
		{
			0 => $"<{p}v xml:space=\"preserve\">{text}</{p}v>",
			1 => $"<{p}v xml:space=\"default\">{text}</{p}v>",
			_ => $"<{p}v>{text}</{p}v>",
		};

		string Number() => (random.Next(4) == 0 ? " " : "") + (random.Next(2) == 0 ? random.Next(-100, 100).ToString(System.Globalization.CultureInfo.InvariantCulture) : "1.5E-05") + (random.Next(4) == 0 ? "\r\n" : "");

		return random.Next(14) switch
		{
			0 => $"<{p}c{at}/>",
			1 => $"<{p}c{at} s=\"1\"></{p}c>",
			2 => $"<{p}c{at}><{p}f>A1+1</{p}f></{p}c>",
			3 => $"<{p}c{at}{(random.Next(2) == 0 ? " t=\"n\"" : "")}>{Value(Number())}</{p}c>",
			4 => $"<{p}c{at} s=\"2\"><{p}f>A1*2</{p}f>{Value(Number())}</{p}c>",
			5 => $"<{p}c{at} t=\"s\">{Value((random.Next(3) == 0 ? " " : "") + random.Next(SharedTexts.Length))}</{p}c>",
			6 => $"<{p}c{at} t=\"str\">{Value(Text(random))}</{p}c>",
			7 => $"<{p}c{at} t=\"inlineStr\">{Inline(random, p)}</{p}c>",
			8 => $"<{p}c{at} t=\"inlineStr\">{Value(Text(random))}</{p}c>",
			9 => $"<{p}c{at} t=\"b\">{Value(new[] { "1", "0", "true", "FALSE", " 1 ", "yes", "" }[random.Next(7)])}</{p}c>",
			10 => $"<{p}c{at} t=\"e\">{Value(new[] { "#N/A", "#DIV/0!", "#VALUE!" }[random.Next(3)])}</{p}c>",
			11 => $"<{p}c{at} t=\"d\">{Value("2024-01-02T03:04:05")}</{p}c>",
			12 => $"<{p}c{at} t=\"q\">{Value(Text(random))}</{p}c>",
			_ => $"<{p}c{at}><{p}unknown><{p}v>no</{p}v></{p}unknown><!-- c -->{Value(Number())}<{p}extLst/></{p}c>",
		};
	}

	/// <summary>Generates an <c>is</c> element: a <c>t</c>, runs, and a phonetic run.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="p">The prefix of the elements.</param>
	/// <returns>The element's XML.</returns>
	private static string Inline(Random random, string p)
	{
		var xml = new StringBuilder($"<{p}is>");
		if (random.Next(2) == 0)
		{
			xml.Append($"<{p}t>{Text(random)}</{p}t>");
		}

		for (var runs = random.Next(0, 3); runs > 0; runs--)
		{
			xml.Append($"<{p}r><{p}rPr><{p}b/></{p}rPr><{p}t xml:space=\"preserve\">{Text(random)}</{p}t></{p}r>");
		}

		if (random.Next(3) == 0)
		{
			xml.Append($"<{p}rPh sb=\"0\" eb=\"1\"><{p}t>no</{p}t></{p}rPh>");
		}

		return xml.Append($"</{p}is>").ToString();
	}

	/// <summary>Generates the XML of a text: nothing, or fragments of plain text, references, line ends, escapes, CDATA and comments.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <returns>The XML.</returns>
	private static string Text(Random random)
	{
		var text = new StringBuilder();
		for (var fragments = random.Next(0, 5); fragments > 0; fragments--)
		{
			text.Append(Fragments[random.Next(Fragments.Length)]);
		}

		return text.ToString();
	}

	/// <summary>
	/// The records of a sheet, by the rules: rows numbered by <c>r</c>, else their first cell's, else the row before's
	/// plus one; cells placed by their references; valueless cells left out; rows padded to the first record with a value;
	/// rows without a value dropped, or with empty rows kept, a record of one empty field, as each missing row is. In an
	/// area, rows above it are left out, the rows end at the first row below it, cells outside its columns are left out, and
	/// field 0 is its first column. Skipping hidden rows, a row Excel hides is left out after the missing rows before it,
	/// and missing rows are left out where the sheet hides them; skipping hidden columns, the fields are the area's shown
	/// columns. What Excel hides is read with .NET's number parsing, apart from the scanner's.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="area">The area read, or <see langword="null"/> for the whole sheet.</param>
	/// <param name="skipHiddenRows">Whether hidden rows are left out.</param>
	/// <param name="skipHiddenColumns">Whether hidden columns are left out.</param>
	/// <returns>The records, described as the scanner's are.</returns>
	private static List<string> Reference(string xml, bool skipEmptyRows, SheetLayout? area = null, bool skipHiddenRows = false, bool skipHiddenColumns = false)
	{
		area ??= new SheetLayout(1, CellReference.MaxRow, 0, CellReference.MaxColumns - 1);
		var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
		var sheetData = document.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "sheetData");
		var records = new List<CapturedRecord>();
		if (sheetData is null)
		{
			return [];
		}

		// Excel's rules: a boolean is 1 or true; a height or width is a number without white space or a plus sign, empty
		// being 0; a col's width that is no such number is 0.
		static bool IsTrue(string? value) => value is "1" or "true";
		static bool? Zero(string? value) =>
			value is null ? null
			: value.Length == 0 ? true
			: value.Trim() != value || value.StartsWith("+", StringComparison.Ordinal) ? null
			: double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number) ? number == 0
			: null;

		var format = document.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "sheetFormatPr");
		var missingHidden = skipHiddenRows && format is not null
			&& (IsTrue(Attribute(format, "zeroHeight")) || (IsTrue(Attribute(format, "customHeight")) && Zero(Attribute(format, "defaultRowHeight")) == true));
		var hidden = new bool[CellReference.MaxColumns];
		if (skipHiddenColumns)
		{
			var hidesUncovered = format is not null && Zero(Attribute(format, "defaultColWidth")) == true;
			for (var column = 0; column < hidden.Length; column++)
			{
				hidden[column] = hidesUncovered;
			}

			var cols = document.Root!.Elements().Where(e => e.Name.LocalName == "cols").SelectMany(e => e.Elements().Where(c => c.Name.LocalName == "col"));
			foreach (var col in cols)
			{
				var hides = IsTrue(Attribute(col, "hidden")) || Zero(Attribute(col, "width")) != false;
				for (var column = int.Parse(Attribute(col, "min")!) - 1; column < Math.Min(int.Parse(Attribute(col, "max")!), CellReference.MaxColumns); column++)
				{
					hidden[column] = hides;
				}
			}
		}

		// The field of each shown column of the area.
		var fieldOf = new Dictionary<int, int>();
		for (var column = area.FirstColumn; column <= area.LastColumn; column++)
		{
			if (!hidden[column])
			{
				fieldOf[column] = fieldOf.Count;
			}
		}

		long lastRow = 0;
		var width = -1;
		foreach (var rowElement in sheetData.Elements().Where(e => e.Name.LocalName == "row"))
		{
			var cells = rowElement.Elements().Where(e => e.Name.LocalName == "c").ToList();
			var row = Attribute(rowElement, "r") is { } r ? long.Parse(r)
				: cells.Count > 0 && Attribute(cells[0], "r") is { } first ? RowOf(first)
				: lastRow + 1;
			if (row > area.LastRow)
			{
				break;
			}

			if (row < area.FirstRow)
			{
				lastRow = row;

				continue;
			}

			if (!skipEmptyRows && !missingHidden)
			{
				for (var gap = Math.Max(lastRow + 1, area.FirstRow); gap < row; gap++)
				{
					records.Add(new CapturedRecord(gap, [("", SheetCellKind.Empty)]));
				}
			}

			if (skipHiddenRows && (IsTrue(Attribute(rowElement, "hidden")) || (IsTrue(Attribute(rowElement, "customHeight")) && Zero(Attribute(rowElement, "ht")) == true)))
			{
				lastRow = row;

				continue;
			}

			var fields = new List<(string Text, SheetCellKind Kind)>();
			var column = -1;
			foreach (var cell in cells)
			{
				column = Attribute(cell, "r") is { } reference ? ColumnOf(reference) : column + 1;
				if (!fieldOf.TryGetValue(column, out var field) || ValueOf(cell) is not { } value)
				{
					continue;
				}

				while (fields.Count < field)
				{
					fields.Add(("", SheetCellKind.Empty));
				}

				fields.Add(value);
			}

			if (fields.Count > 0)
			{
				width = width < 0 ? fields.Count : width;
				while (fields.Count < width)
				{
					fields.Add(("", SheetCellKind.Empty));
				}

				records.Add(new CapturedRecord(row, fields));
			}
			else if (!skipEmptyRows)
			{
				records.Add(new CapturedRecord(row, [("", SheetCellKind.Empty)]));
			}

			lastRow = row;
		}

		return [.. records.Select(r => r.ToString())];
	}

	/// <summary>A cell's value and kind by its type, or <see langword="null"/> when it is valueless.</summary>
	/// <param name="cell">The cell.</param>
	/// <returns>The value and kind.</returns>
	private static (string Text, SheetCellKind Kind)? ValueOf(XElement cell)
	{
		var type = Attribute(cell, "t")?.Trim() ?? "n";
		var element = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v" || (type == "inlineStr" && e.Name.LocalName == "is"));
		if (element is null)
		{
			return null;
		}

		var text = element.Name.LocalName == "v"
			? Decode(Trimmed(element))
			: string.Concat(element.Elements()
				.SelectMany(e => e.Name.LocalName == "t" ? new[] { e } : e.Name.LocalName == "r" ? [.. e.Elements().Where(t => t.Name.LocalName == "t")] : Array.Empty<XElement>())
				.Select(t => Decode(Trimmed(t))));
		var trimmed = text.Trim(' ', '\t', '\r', '\n');
		switch (type)
		{
			case "s":
				var shared = trimmed.Length == 0 ? "" : SharedTexts[int.Parse(trimmed)];

				return shared.Length == 0 ? null : (shared, SheetCellKind.Text);
			case "n":
				return trimmed.Length == 0 ? null : (trimmed, SheetCellKind.Number);
			case "b":
				return trimmed.Length == 0 ? null
					: trimmed is "1" or "0" ? (trimmed, SheetCellKind.Bool)
					: trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ? ("1", SheetCellKind.Bool)
					: trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ? ("0", SheetCellKind.Bool)
					: (trimmed, SheetCellKind.Text);
			case "e":
				return text.Length == 0 ? null : (text, SheetCellKind.Error);
			// A t="d" cell takes its style's kind, as a number does: with no styles, a number.
			case "d" when Corpus.IsoSerials.Of(trimmed, false) is { } serial:
				return (serial, SheetCellKind.Number);
			default:
				return text.Length == 0 ? null : (text, SheetCellKind.Text);
		}
	}

	/// <summary>The text and CDATA directly in an element, joined: not its elements', comments or processing instructions.</summary>
	/// <param name="element">The element.</param>
	/// <returns>The text.</returns>
	private static string DirectText(XElement element) => string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));

	/// <summary>
	/// The direct text of a <c>v</c> or <c>t</c>, trimmed of the XML white space around it unless the nearest
	/// <c>xml:space</c>, on it or an ancestor, is <c>preserve</c>: as Excel reads it.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The text.</returns>
	private static string Trimmed(XElement element)
	{
		var space = element.AncestorsAndSelf().Select(e => e.Attribute(XNamespace.Xml + "space")?.Value).FirstOrDefault(v => v is not null);
		var text = DirectText(element);

		return space == "preserve" ? text : text.Trim(' ', '\t', '\r', '\n');
	}

	/// <summary>Decodes a text's escapes with a regular expression, then as UTF-8, where a lone surrogate becomes U+FFFD.</summary>
	/// <param name="text">The text, with its XML decoded.</param>
	/// <returns>The decoded text.</returns>
	private static string Decode(string text)
	{
		var decoded = Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

		return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(decoded));
	}

	/// <summary>An attribute's value by its local name, or <see langword="null"/>.</summary>
	/// <param name="element">The element.</param>
	/// <param name="name">The attribute's local name.</param>
	/// <returns>The value.</returns>
	private static string? Attribute(XElement element, string name) => element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;

	/// <summary>The 0-based column of a reference such as <c>C14</c>.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The column.</returns>
	private static int ColumnOf(string reference) => reference.TakeWhile(char.IsLetter).Aggregate(0, (column, c) => column * 26 + (char.ToUpperInvariant(c) - 'A' + 1)) - 1;

	/// <summary>The row of a reference such as <c>C14</c>.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The row.</returns>
	private static long RowOf(string reference) => long.Parse(new string([.. reference.SkipWhile(char.IsLetter)]));
}
