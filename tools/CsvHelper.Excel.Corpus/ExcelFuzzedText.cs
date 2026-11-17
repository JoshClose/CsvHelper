using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Has Excel judge the text CsvHelper.Excel shows in the workbooks the writer's code fuzzer saved (its
/// <c>CSVHELPER_EXCEL_FUZZ_SAVE</c> folder), as Phase 8's "Testing" asks on demand: Phase 5's random codes over numbers,
/// dates, durations, booleans and text, a column each. Excel opens each workbook read-only through COM and widens its used
/// columns to 255 characters, so that a text is <c>#</c> only where Excel can't show the value; for each cell it gives
/// <c>Range.Text</c> and <c>NumberFormat</c>, and the workbook is saved as "CSV UTF-8", as Excel's own export writes it.
/// Excel's text of a cell is chosen as the tests' <c>ExcelShown.Expected</c> chooses it: <c>Range.Text</c>, but the export
/// where the column's width changed it (a run of <c>#</c>, a <c>*</c> fill, General, a long text), a lone surrogate as
/// U+FFFD. The reader reads the
/// same workbook under <see cref="ExcelText.Formatted"/> and <see cref="ExcelText.Raw"/>, in the invariant culture: a cell
/// agrees when its formatted text is Excel's, or, where Excel can't show the value, when it is Raw's (Phase 8's decision
/// 5). Writes the report, each difference with its code, value and texts, to standard output and to <c>fuzzed-text.txt</c>
/// in the folder, and the differences as JSON lines to <c>fuzzed-text.jsonl</c>. The tool fails when a cell differs. Nothing is committed: the fuzzer's workbooks change with its seeds.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelFuzzedText
{
	/// <summary><c>xlCSVUTF8</c>: Excel's "CSV UTF-8".</summary>
	private const int CsvUtf8 = 62;

	/// <summary>The widest a column can be.</summary>
	private const double WidestColumn = 255;

	/// <summary>The workbooks opened before Excel is started again, so that one session doesn't grow through thousands.</summary>
	private const int PerSession = 200;

	/// <summary>Judges each workbook of a folder, and reports.</summary>
	/// <param name="folder">The folder of the fuzzer's workbooks.</param>
	/// <returns>0 when every cell agrees, else 1.</returns>
	public static int Judge(string folder)
	{
		var files = Directory.GetFiles(folder, "*.xlsx").OrderBy(f => f, StringComparer.Ordinal).ToList();
		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		var differences = new List<(string Code, string Line)>();
		var json = new List<string>();
		var (cells, agreed, cantShow) = (0, 0, 0);
		try
		{
			for (var start = 0; start < files.Count; start += PerSession)
			{
				var session = files.Skip(start).Take(PerSession).ToList();
				ExcelProducer.Run(excel =>
				{
					foreach (var file in session)
					{
						// Typed, since a call with a dynamic argument is dynamic, and its tuple's names are gone.
						List<(int Row, int Column, string Value, string NumberFormat, string Text, string Export)> answers = Answers(excel, file, temp);
						var formatted = Fields(file, ExcelText.Formatted);
						var raw = Fields(file, ExcelText.Raw);
						foreach (var answer in answers)
						{
							cells++;
							var ours = Field(formatted, answer.Row, answer.Column);
							var rawText = Field(raw, answer.Row, answer.Column);
							var expected = Expected(answer.Text, answer.Export, answer.NumberFormat);
							if (expected is null ? ours == rawText : ours == expected)
							{
								agreed++;
								cantShow += expected is null ? 1 : 0;

								continue;
							}

							var reference = ExcelOracle.ColumnName(answer.Column + 1) + (answer.Row + 1).ToString(CultureInfo.InvariantCulture);
							differences.Add((answer.NumberFormat, $"{Path.GetFileName(file)} {reference}: {Show(answer.NumberFormat)} of {Show(answer.Value)}: Excel {Show(answer.Text)}" +
								(answer.Export != answer.Text ? $" (export {Show(answer.Export)})" : "") + $", ours {Show(ours)}" + (ours == rawText ? " (Raw's)" : $", Raw's {Show(rawText)}")));
							json.Add(JsonSerializer.Serialize(new Dictionary<string, string?>
							{
								["file"] = Path.GetFileName(file), ["cell"] = reference, ["code"] = answer.NumberFormat, ["value"] = answer.Value, ["text"] = answer.Text,
								["export"] = answer.Export, ["expected"] = expected, ["ours"] = ours, ["raw"] = rawText,
							}));
						}
					}
				});
				Console.WriteLine($"  {Math.Min(start + PerSession, files.Count)} of {files.Count} judged");
			}
		}
		finally
		{
			Directory.Delete(temp, true);
		}

		var codes = differences.Select(d => d.Code).Distinct(StringComparer.Ordinal).Count();
		var report = new List<string>
		{
			$"{files.Count} workbooks in {folder}: {cells} cells, {agreed} agree ({cantShow} of them where Excel can't show the value), {differences.Count} differ, in {codes} codes.",
		};
		report.AddRange(differences.OrderBy(d => d.Code, StringComparer.Ordinal).Select(d => d.Line));
		File.WriteAllLines(Path.Combine(folder, "fuzzed-text.txt"), report, new UTF8Encoding(false));
		File.WriteAllLines(Path.Combine(folder, "fuzzed-text.jsonl"), json, new UTF8Encoding(false));
		foreach (var line in report)
		{
			Console.WriteLine(line);
		}

		return differences.Count == 0 ? 0 : 1;
	}

	/// <summary>Excel's answers about a workbook's first worksheet: each cell of its used range with a value.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="path">The workbook's file.</param>
	/// <param name="temp">A folder for its CSV export.</param>
	/// <returns>Each cell's 0-based row and column, value, <c>NumberFormat</c>, text and export.</returns>
	private static List<(int Row, int Column, string Value, string NumberFormat, string Text, string Export)> Answers(dynamic excel, string path, string temp)
	{
		var cells = new List<(int Row, int Column, string Value, string NumberFormat, string Text)>();
		var csv = Path.Combine(temp, Path.GetFileNameWithoutExtension(path) + ".csv");
		dynamic workbook = ExcelOracle.WhenReady(() => excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false));
		int top;
		int left;
		try
		{
			var sheet = workbook.Worksheets[1];
			var used = sheet.UsedRange;
			int rows = used.Rows.Count;
			int columns = used.Columns.Count;
			top = used.Row;
			left = used.Column;
			used.EntireColumn.ColumnWidth = WidestColumn;
			var values = ExcelOracle.Grid(used.Value2, rows, columns);
			for (var r = 0; r < rows; r++)
			{
				for (var c = 0; c < columns; c++)
				{
					if (values[r, c] is not { } value)
					{
						continue;
					}

					var cell = sheet.Cells[top + r, left + c];
					cells.Add((top + r - 1, left + c - 1, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", (string)cell.NumberFormat, (string)cell.Text));
				}
			}

			workbook.SaveAs(Filename: csv, FileFormat: CsvUtf8);
		}
		finally
		{
			workbook.Close(false);
		}

		// The export starts at the used range's top-left cell.
		var exported = new List<string[]>();
		using (var reader = CsvReader.FromFile(csv, new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false }))
		{
			while (reader.Read())
			{
				var fields = new string[reader.FieldCount];
				for (var i = 0; i < fields.Length; i++)
				{
					fields[i] = reader.GetString(i);
				}

				exported.Add(fields);
			}
		}

		return cells.Select(c => (c.Row, c.Column, c.Value, c.NumberFormat, c.Text, Field(exported, c.Row - (top - 1), c.Column - (left - 1)))).ToList();
	}

	/// <summary>A workbook's first worksheet's fields, from A1, under one <see cref="ExcelText"/>.</summary>
	/// <param name="path">The workbook's file.</param>
	/// <param name="text">What text the reader gives.</param>
	/// <returns>Each row's fields.</returns>
	private static List<string[]> Fields(string path, ExcelText text)
	{
		var rows = new List<string[]>();
		using var reader = ExcelReader.FromFile(path, new ExcelReaderOptions { Text = text, Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } });
		while (reader.Read())
		{
			var fields = new string[reader.FieldCount];
			for (var i = 0; i < fields.Length; i++)
			{
				fields[i] = reader.GetString(i);
			}

			rows.Add(fields);
		}

		return rows;
	}

	/// <summary>A field of rows, or empty text where the rows don't reach it.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="row">The 0-based row.</param>
	/// <param name="column">The 0-based column.</param>
	/// <returns>The field.</returns>
	private static string Field(List<string[]> rows, int row, int column) => row >= 0 && row < rows.Count && column >= 0 && column < rows[row].Length ? rows[row][column] : "";

	/// <summary>
	/// Excel's text of a cell, as the tests' <c>ExcelShown.Expected</c> chooses it: <c>Range.Text</c> at a column of 255, but
	/// its CSV export's where the column's width changed it (a run of <c>#</c>, a <c>*</c> fill, General, a text cut at
	/// 8,221 characters); <see langword="null"/> where Excel can't show the value.
	/// </summary>
	/// <param name="text">The cell's <c>Range.Text</c>.</param>
	/// <param name="export">Its text in Excel's CSV export.</param>
	/// <param name="numberFormat">Its <c>NumberFormat</c>.</param>
	/// <returns>The text.</returns>
	private static string? Expected(string text, string export, string numberFormat)
	{
		// A lone surrogate, which a code's _ or * leaves of a pair, is U+FFFD in UTF-8, as the reader gives it.
		text = Valid(text);
		export = Valid(export);
		var shown = text;
		if (export != text && (Hashes(text) || numberFormat.Contains('*') || numberFormat == "General" || (text.Length >= 8221 && export.StartsWith(text, StringComparison.Ordinal))))
		{
			shown = export;
		}

		return Hashes(shown) ? null : shown;
	}

	/// <summary>A text with each lone surrogate as U+FFFD, as UTF-8 holds it.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text.</returns>
	private static string Valid(string text) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));

	/// <summary>Whether a text is a run of <c>#</c>, Excel's sign that it can't show a value in its column.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether it is.</returns>
	private static bool Hashes(string text) => text.Length > 0 && text.All(c => c == '#');

	/// <summary>Text for the report: ASCII as it is, and every other code unit as <c>\uXXXX</c>, cut after 80 code units.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text shown.</returns>
	private static string Show(string text)
	{
		var shown = new StringBuilder("\"");
		foreach (var c in text.Length > 80 ? text.Substring(0, 80) : text)
		{
			shown.Append(c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:X4}");
		}

		shown.Append(text.Length > 80 ? $"\"... ({text.Length} code units)" : "\"");

		return shown.ToString();
	}
}
