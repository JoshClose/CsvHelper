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

	/// <summary>Generates a sheet: rows and cells with and without references, every cell type, valueless cells and noise.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <returns>The part's XML.</returns>
	private static string Generate(Random random)
	{
		var p = random.Next(4) == 0 ? "x:" : "";
		var ns = random.Next(5) == 0 ? TestPackage.StrictMainNamespace : TestPackage.MainNamespace;
		var indent = random.Next(3) == 0 ? "\r\n  " : "";
		var xml = new StringBuilder(TestPackage.Declaration);
		xml.Append(p.Length > 0 ? $"<x:worksheet xmlns:x=\"{ns}\">" : $"<worksheet xmlns=\"{ns}\">");
		xml.Append($"<{p}dimension ref=\"A1:C3\"/><{p}sheetViews><{p}sheetView workbookViewId=\"0\"/></{p}sheetViews><{p}cols><{p}col min=\"1\" max=\"2\" width=\"9\"/></{p}cols>");
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
			xml.Append(rowReference ? $"<{p}row r=\"{row}\"{spans}{rowSpace}" : $"<{p}row{spans}{rowSpace}");
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
	/// rows without a value dropped, or with empty rows kept, a record of one empty field, as each missing row is.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <returns>The records, described as the scanner's are.</returns>
	private static List<string> Reference(string xml, bool skipEmptyRows)
	{
		var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
		var sheetData = document.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "sheetData");
		var records = new List<CapturedRecord>();
		if (sheetData is null)
		{
			return [];
		}

		long lastRow = 0;
		var width = -1;
		foreach (var rowElement in sheetData.Elements().Where(e => e.Name.LocalName == "row"))
		{
			var cells = rowElement.Elements().Where(e => e.Name.LocalName == "c").ToList();
			var row = Attribute(rowElement, "r") is { } r ? long.Parse(r)
				: cells.Count > 0 && Attribute(cells[0], "r") is { } first ? RowOf(first)
				: lastRow + 1;
			if (!skipEmptyRows)
			{
				for (var gap = lastRow + 1; gap < row; gap++)
				{
					records.Add(new CapturedRecord(gap, [("", SheetCellKind.Empty)]));
				}
			}

			var fields = new List<(string Text, SheetCellKind Kind)>();
			var column = -1;
			foreach (var cell in cells)
			{
				column = Attribute(cell, "r") is { } reference ? ColumnOf(reference) : column + 1;
				if (ValueOf(cell) is not { } value)
				{
					continue;
				}

				while (fields.Count < column)
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
