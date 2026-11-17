using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes the display set (Phase 8's step 7): <c>display/</c>, the workbooks of Phase 8's probes of how Excel shows values
/// in number formats, which <c>python/display.py</c> writes, a sheet each; and <c>excel-display.json</c>, Excel's answers
/// about them through COM. Excel opens each workbook read-only and widens column C, where the cases are, to 255
/// characters, so that a text is <c>#</c> only where Excel can't show the value. For each case it gives the cell's
/// <c>Range.Text</c> and <c>NumberFormat</c>. Then it saves the workbook as "CSV UTF-8", as its own export writes it, and
/// the export's text of a case is kept where it isn't <c>Range.Text</c>: a <c>*</c> fill, which <c>Range.Text</c> repeats to
/// the column's width and the export drops; Buddhist years, Hijri and Thai dates, which the export writes as Gregorian
/// ones; and a text of 255 characters that a column of 255 shows as <c>#</c> by its pixels.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelDisplay
{
	/// <summary><c>xlCSVUTF8</c>: Excel's "CSV UTF-8".</summary>
	private const int CsvUtf8 = 62;

	/// <summary>The widest a column can be.</summary>
	private const double WidestColumn = 255;

	/// <summary>The cases' column: C.</summary>
	private const int CaseColumn = 3;

	/// <summary>The JSON's options: compact, with text past ASCII as it is.</summary>
	private static readonly JsonWriterOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	/// <summary>Writes the display set's workbooks and Excel's answers about them.</summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="scripts">The folder of <c>display.py</c>.</param>
	public static void Write(string files, string scripts)
	{
		var folder = Path.Combine(files, "display");
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, true);
		}

		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		try
		{
			var casesPath = Path.Combine(temp, "cases.json");
			Program.Run(Program.FindPython(), "-I", Path.Combine(scripts, "display.py"), folder, casesPath);
			var workbooks = Cases(casesPath);
			var answers = new List<string>();
			string? head = null;
			ExcelProducer.Run(excel =>
			{
				head = Head(excel);
				foreach (var (file, date1904, cases) in workbooks)
				{
					answers.Add(Answer(excel, Path.Combine(folder, file), temp, file, date1904, cases));
					Console.WriteLine($"  {Path.Combine(folder, file)}");
				}
			});

			var json = new StringBuilder();
			json.Append(head).Append(",\n\t\"workbooks\": [\n").Append(string.Join(",\n", answers)).Append("\n\t]\n}\n");
			var target = Path.Combine(files, "excel-display.json");
			File.WriteAllText(target, json.ToString(), new UTF8Encoding(false));
			Console.WriteLine($"  {target}");
		}
		finally
		{
			Directory.Delete(temp, true);
		}
	}

	/// <summary>The cases <c>display.py</c> wrote, by workbook in its order.</summary>
	/// <param name="path">The cases' JSON.</param>
	/// <returns>Each workbook's file, date system and cases.</returns>
	private static List<(string File, bool Date1904, List<JsonElement> Cases)> Cases(string path)
	{
		using var document = JsonDocument.Parse(File.ReadAllBytes(path));
		var workbooks = new List<(string File, bool Date1904, List<JsonElement> Cases)>();
		foreach (var item in document.RootElement.EnumerateArray())
		{
			var file = item.GetProperty("file").GetString()!;
			if (workbooks.Count == 0 || workbooks[^1].File != file)
			{
				workbooks.Add((file, item.GetProperty("date1904").GetBoolean(), []));
			}

			workbooks[^1].Cases.Add(item.Clone());
		}

		return workbooks;
	}

	/// <summary>The answers' head: Excel's version and the settings its text depends on.</summary>
	/// <param name="excel">Excel.</param>
	/// <returns>The JSON's first lines, without the closing brace.</returns>
	private static string Head(dynamic excel)
	{
		var head = new StringBuilder();
		head.Append("{\n");
		head.Append("\t\"excel\": ").Append(JsonSerializer.Serialize((string)excel.Version)).Append(",\n");
		head.Append("\t\"build\": ").Append(JsonSerializer.Serialize(Convert.ToString(excel.Build, CultureInfo.InvariantCulture))).Append(",\n");
		head.Append("\t\"culture\": ").Append(JsonSerializer.Serialize(CultureInfo.CurrentCulture.Name)).Append(",\n");
		head.Append("\t\"decimalSeparator\": ").Append(JsonSerializer.Serialize((string)excel.DecimalSeparator)).Append(",\n");
		head.Append("\t\"thousandsSeparator\": ").Append(JsonSerializer.Serialize((string)excel.ThousandsSeparator)).Append(",\n");
		head.Append("\t\"useSystemSeparators\": ").Append((bool)excel.UseSystemSeparators ? "true" : "false");

		return head.ToString();
	}

	/// <summary>Excel's answers about one workbook of the set, as a JSON object of a line per case.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="path">The workbook's file.</param>
	/// <param name="temp">A folder for its CSV export.</param>
	/// <param name="file">Its name.</param>
	/// <param name="date1904">Whether it uses the 1904 date system.</param>
	/// <param name="cases">Its cases.</param>
	/// <returns>The JSON.</returns>
	private static string Answer(dynamic excel, string path, string temp, string file, bool date1904, List<JsonElement> cases)
	{
		var texts = new List<(string Text, string NumberFormat)>();
		var csv = Path.Combine(temp, Path.ChangeExtension(file, ".csv"));
		dynamic workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false);
		try
		{
			var sheet = workbook.Worksheets[1];
			sheet.Columns[CaseColumn].ColumnWidth = WidestColumn;
			foreach (var item in cases)
			{
				var cell = sheet.Cells[item.GetProperty("row").GetInt32(), CaseColumn];
				texts.Add(((string)cell.Text, (string)cell.NumberFormat));
			}

			workbook.SaveAs(Filename: csv, FileFormat: CsvUtf8);
		}
		finally
		{
			workbook.Close(false);
		}

		var exports = Exports(csv);
		var lines = new List<string>();
		for (var i = 0; i < cases.Count; i++)
		{
			var item = cases[i];
			var id = item.GetProperty("id").GetString()!;
			var (text, numberFormat) = texts[i];
			var export = exports.TryGetValue(id, out var exported) ? exported : throw new InvalidOperationException($"Excel's export of {file} has no {id}.");
			lines.Add("\t\t\t" + CellJson(item, text, numberFormat, export));
		}

		return $"\t\t{{\"file\": {JsonSerializer.Serialize(file)}, \"date1904\": {(date1904 ? "true" : "false")}, \"cells\": [\n{string.Join(",\n", lines)}\n\t\t]}}";
	}

	/// <summary>The text Excel's CSV export wrote for each case, by its id in column A.</summary>
	/// <param name="csv">The export.</param>
	/// <returns>Each case's column C, by id.</returns>
	private static Dictionary<string, string> Exports(string csv)
	{
		var exports = new Dictionary<string, string>(StringComparer.Ordinal);
		using var reader = CsvReader.FromFile(csv, new CsvReaderOptions { HasHeader = false });
		while (reader.Read())
		{
			if (reader.FieldCount > 0)
			{
				exports[reader.GetString(0)] = reader.FieldCount > 2 ? reader.GetString(2) : "";
			}
		}

		return exports;
	}

	/// <summary>
	/// A case's answers as compact JSON: its row, its code or built-in id, the kind and stored text of its value, the text
	/// Excel shows, Excel's <c>NumberFormat</c> where it isn't the code written, and the export's text where it isn't the
	/// shown text.
	/// </summary>
	/// <param name="item">The case.</param>
	/// <param name="text">Its <c>Range.Text</c>.</param>
	/// <param name="numberFormat">Its <c>NumberFormat</c>.</param>
	/// <param name="export">Its text in Excel's CSV export.</param>
	/// <returns>The JSON.</returns>
	private static string CellJson(JsonElement item, string text, string numberFormat, string export)
	{
		using var stream = new MemoryStream();
		using (var json = new Utf8JsonWriter(stream, Compact))
		{
			json.WriteStartObject();
			json.WriteNumber("row", item.GetProperty("row").GetInt32());
			var code = item.GetProperty("code");
			if (code.ValueKind == JsonValueKind.String)
			{
				json.WriteString("code", code.GetString());
			}
			else
			{
				json.WriteNumber("builtin", item.GetProperty("builtin").GetInt32());
			}

			json.WriteString("kind", item.GetProperty("kind").GetString());
			json.WriteString("value", item.GetProperty("value").GetString());
			json.WriteString("text", text);
			if (code.ValueKind != JsonValueKind.String || numberFormat != code.GetString())
			{
				json.WriteString("numberFormat", numberFormat);
			}

			if (export != text)
			{
				json.WriteString("export", export);
			}

			json.WriteEndObject();
		}

		return Encoding.UTF8.GetString(stream.ToArray());
	}
}
