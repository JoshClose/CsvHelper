using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A written sheet's validations, its columns' default styles' number formats and their widths, by each column's header
/// name, as the workbook's XML has them, for tests of <see cref="ExcelSheetOptions.Validate"/> and of columns' lists and
/// prompts.
/// </summary>
internal static class SheetValidations
{
	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The built-in number formats the writer uses, by id.</summary>
	private static readonly Dictionary<int, string> BuiltIn = new()
	{
		[0] = "General",
		[1] = "0",
		[2] = "0.00",
		[3] = "#,##0",
		[4] = "#,##0.00",
		[46] = "[h]:mm:ss",
		[49] = "@",
	};

	/// <summary>
	/// Each column's validation and default style, by its header name: <c>type [operator] [formula1] [formula2]
	/// [title=…] [prompt=…] | format</c>, such as <c>whole -128 127 | General</c>, <c>list "Yes,No" | @</c> or <c>none | @</c>.
	/// A validation's cells must be its column's from the row below the header to Excel's last.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="headerRow">The header's row.</param>
	/// <returns>The descriptions, by header name.</returns>
	public static Dictionary<string, string> Describe(byte[] workbook, int sheet = 1, int headerRow = 1)
	{
		var validations = Of(workbook, sheet, headerRow + 1);
		var formats = Formats(workbook, sheet);
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var (column, name) in Header(workbook, sheet, headerRow))
		{
			var validation = validations.TryGetValue(column, out var v) ? v : "none";
			var format = formats.TryGetValue(column, out var f) ? f : "General";
			result[name] = validation + " | " + format;
		}

		return result;
	}

	/// <summary>Each validation of a sheet, by its column's letters, checked to cover the column from a row to Excel's last.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="firstRow">The row each validation starts at.</param>
	/// <returns>Each validation as <c>type [operator] [formula1] [formula2] [title=…] [prompt=…]</c>, with its attributes' text as written.</returns>
	public static Dictionary<string, string> Of(byte[] workbook, int sheet = 1, int firstRow = 2)
	{
		var xml = XDocument.Parse(Written.Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		var element = xml.Root!.Element(Main + "dataValidations");
		if (element is null)
		{
			return result;
		}

		Assert.Equal(element.Elements().Count(), (int)element.Attribute("count")!);
		foreach (var validation in element.Elements(Main + "dataValidation"))
		{
			var sqref = (string)validation.Attribute("sqref")!;
			var column = Regex.Match(sqref, "^[A-Z]+").Value;
			Assert.Equal($"{column}{firstRow}:{column}1048576", sqref);
			Assert.Equal(("1", "1", "1", null), ((string?)validation.Attribute("allowBlank"), (string?)validation.Attribute("showInputMessage"), (string?)validation.Attribute("showErrorMessage"), (string?)validation.Attribute("errorStyle")));
			var parts = new List<string> { (string?)validation.Attribute("type") ?? "any" };
			if ((string?)validation.Attribute("operator") is { } op)
			{
				parts.Add(op);
			}

			if (validation.Element(Main + "formula1") is { } formula1)
			{
				parts.Add(formula1.Value);
			}

			if (validation.Element(Main + "formula2") is { } formula2)
			{
				parts.Add(formula2.Value);
			}

			if ((string?)validation.Attribute("promptTitle") is { } title)
			{
				parts.Add("title=" + title);
			}

			if ((string?)validation.Attribute("prompt") is { } prompt)
			{
				parts.Add("prompt=" + prompt);
			}

			result.Add(column, string.Join(" ", parts));
		}

		return result;
	}

	/// <summary>Each column's default style's number format, by its letters, for the columns with a style.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <returns>The formats' codes.</returns>
	public static Dictionary<string, string> Formats(byte[] workbook, int sheet = 1)
	{
		var styles = XDocument.Parse(Written.Entry(workbook, "xl/styles.xml"));
		var custom = styles.Descendants(Main + "numFmt").ToDictionary(f => (int)f.Attribute("numFmtId")!, f => (string)f.Attribute("formatCode")!);
		var xfs = styles.Root!.Element(Main + "cellXfs")!.Elements(Main + "xf").ToList();
		var xml = XDocument.Parse(Written.Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var col in xml.Descendants(Main + "col"))
		{
			if ((int?)col.Attribute("style") is not { } style)
			{
				continue;
			}

			var id = (int)xfs[style].Attribute("numFmtId")!;
			var code = custom.TryGetValue(id, out var c) ? c : BuiltIn[id];
			for (var i = (int)col.Attribute("min")!; i <= (int)col.Attribute("max")!; i++)
			{
				result[Letters(i - 1)] = code;
			}
		}

		return result;
	}

	/// <summary>Each column's width, by its header name, or 0 for a column without one.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="headerRow">The header's row.</param>
	/// <returns>The widths.</returns>
	public static Dictionary<string, double> Widths(byte[] workbook, int sheet = 1, int headerRow = 1)
	{
		var xml = XDocument.Parse(Written.Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));
		var widths = new Dictionary<string, double>(StringComparer.Ordinal);
		foreach (var col in xml.Descendants(Main + "col"))
		{
			if ((string?)col.Attribute("customWidth") == "1")
			{
				widths[Letters((int)col.Attribute("min")! - 1)] = double.Parse((string)col.Attribute("width")!, CultureInfo.InvariantCulture);
			}
		}

		return Header(workbook, sheet, headerRow).ToDictionary(h => h.Name, h => widths.TryGetValue(h.Column, out var width) ? width : 0);
	}

	/// <summary>The header's cells: each one's column letters and its text.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="row">The header's row.</param>
	/// <returns>The cells, in order.</returns>
	public static List<(string Column, string Name)> Header(byte[] workbook, int sheet = 1, int row = 1)
	{
		var xml = XDocument.Parse(Written.Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));
		var header = xml.Descendants(Main + "row").Single(r => (int)r.Attribute("r")! == row);

		return [.. header.Elements(Main + "c").Select(c => (Regex.Match((string)c.Attribute("r")!, "^[A-Z]+").Value, string.Concat(c.Descendants(Main + "t").Select(t => t.Value))))];
	}

	/// <summary>A column's letters.</summary>
	/// <param name="column">The 0-based column.</param>
	/// <returns>The letters.</returns>
	private static string Letters(int column)
	{
		var letters = "";
		for (var n = column + 1; n > 0; n = (n - 1) / 26)
		{
			letters = (char)('A' + (n - 1) % 26) + letters;
		}

		return letters;
	}
}
