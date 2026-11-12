using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Opens in Excel, through COM, the workbooks the writer's fuzzers saved (their <c>CSVHELPER_EXCEL_FUZZ_SAVE</c> folder),
/// as Phase 5's "Testing" asks of them on demand: whether Excel opens each as it is, only by repairing it (with the records
/// of its repair log), or not at all. A workbook Excel repairs or refuses is opened again, alone, in an Excel of its own,
/// and counts only if it fails again: in a long run, an open that Excel turned down once while busy would otherwise count,
/// with the last workbook's repair log. For each workbook it opens, it lists the cells with a number that Excel shows as
/// <c>########</c> at the widths written, with each one's number format, value and column's width, since auto-fit means to
/// leave none but those Excel can't show at any width. Writes the report to standard output and to <c>fuzzed.txt</c> in the
/// folder. The tool fails when a workbook isn't opened as it is. Nothing is committed: the fuzzers' workbooks change with
/// their seeds.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelFuzzed
{
	/// <summary><c>xlCalculationAutomatic</c>.</summary>
	private const int CalculationAutomatic = -4105;

	/// <summary><c>xlRepairFile</c>, for a workbook Excel opens only by repairing it.</summary>
	private const int RepairFile = 1;

	/// <summary><c>msoAutomationSecurityForceDisable</c>: no macro runs.</summary>
	private const int MacrosDisabled = 3;

	/// <summary>The workbooks opened before Excel is started again, so that one session doesn't grow through thousands.</summary>
	private const int PerSession = 500;

	/// <summary>Opens each workbook of a folder, and reports.</summary>
	/// <param name="folder">The folder of the fuzzers' workbooks.</param>
	/// <returns>0 when Excel opened every one as it is, else 1.</returns>
	public static int Open(string folder)
	{
		var files = Directory.GetFiles(folder, "*.xlsx").OrderBy(f => f, StringComparer.Ordinal).ToList();
		var failed = new List<string>();
		var hashes = new List<string>();
		var opened = 0;
		for (var start = 0; start < files.Count; start += PerSession)
		{
			var session = files.Skip(start).Take(PerSession).ToList();
			Run(session, (file, outcome, lines) =>
			{
				if (outcome is null)
				{
					opened++;
					hashes.AddRange(lines);
				}
				else
				{
					failed.Add(file);
				}
			});
			Console.WriteLine($"  {Math.Min(start + PerSession, files.Count)} of {files.Count} opened");
		}

		// Each workbook Excel turned down, again, alone.
		var report = new List<string>();
		var (repaired, refused, again) = (0, 0, 0);
		foreach (var file in failed)
		{
			Run([file], (_, outcome, lines) =>
			{
				if (outcome is null)
				{
					again++;
					opened++;
					hashes.AddRange(lines);
					report.Add($"{Path.GetFileName(file)}: turned down once, and opened as it is alone");

					return;
				}

				if (outcome.StartsWith("refused", StringComparison.Ordinal))
				{
					refused++;
				}
				else
				{
					repaired++;
				}

				report.Add($"{Path.GetFileName(file)}: {outcome}");
			});
		}

		report.Insert(0, $"{files.Count} workbooks in {folder}: {opened} opened as they are ({again} of them on a second try, alone), {repaired} repaired, {refused} refused; {hashes.Count} numbers shown as ########.");
		report.AddRange(hashes);
		File.WriteAllLines(Path.Combine(folder, "fuzzed.txt"), report);
		foreach (var line in report)
		{
			Console.WriteLine(line);
		}

		return repaired + refused == 0 ? 0 : 1;
	}

	/// <summary>Opens workbooks in one Excel, one after another.</summary>
	/// <param name="files">The workbooks' files.</param>
	/// <param name="answer">
	/// Takes each workbook's file, what went wrong (<c>repaired: </c> and the repair log's records, or <c>refused: </c> and
	/// Excel's error) or <see langword="null"/> when Excel opened it as it is, and then its cells shown as <c>########</c>.
	/// </param>
	private static void Run(List<string> files, Action<string, string?, List<string>> answer)
	{
		ExcelProducer.Run(excel =>
		{
			// Calculation is the application's, set by the first workbook open: a blank one, kept open throughout.
			var blank = excel.Workbooks.Add();
			try
			{
				excel.Calculation = CalculationAutomatic;
				excel.AutomationSecurity = MacrosDisabled;
				excel.EnableEvents = false;
				foreach (var file in files)
				{
					var name = Path.GetFileName(file);
					var started = DateTime.Now.AddSeconds(-1);
					dynamic workbook;
					try
					{
						// Opened as it is, once Excel isn't busy.
						workbook = ExcelOracle.WhenReady(() => excel.Workbooks.Open(Filename: file, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false));
					}
					catch (COMException)
					{
						try
						{
							workbook = ExcelOracle.WhenReady(() => excel.Workbooks.Open(Filename: file, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false, CorruptLoad: RepairFile));
						}
						catch (COMException e)
						{
							answer(file, $"refused: {e.Message}", []);

							continue;
						}

						var records = ExcelOracle.RepairRecords(started);
						workbook.Close(false);
						answer(file, "repaired: " + (records.Count == 0 ? "(no repair log)" : string.Join("; ", records)), []);

						continue;
					}

					try
					{
						var lines = new List<string>();
						foreach (var sheet in workbook.Worksheets)
						{
							List<string> found = Hashes(sheet);
							lines.AddRange(found.Select(line => $"{name} {line}"));
						}

						answer(file, null, lines);
					}
					finally
					{
						workbook.Close(false);
					}
				}
			}
			finally
			{
				blank.Close(false);
			}
		});
	}

	/// <summary>The cells of a sheet's used range with a number Excel shows as <c>########</c>: each one's place, value, format and column's width.</summary>
	/// <param name="sheet">The worksheet.</param>
	/// <returns>A line for each.</returns>
	private static List<string> Hashes(dynamic sheet)
	{
		var lines = new List<string>();
		var used = sheet.UsedRange;
		int rows = used.Rows.Count;
		int columns = used.Columns.Count;
		int top = used.Row;
		int left = used.Column;
		var values = ExcelOracle.Grid(used.Value2, rows, columns);
		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < columns; c++)
			{
				if (values[r, c] is not double value)
				{
					continue;
				}

				var cell = sheet.Cells[top + r, left + c];
				var text = (string)cell.Text;
				if (text.Length > 0 && text.All(ch => ch == '#'))
				{
					var reference = ExcelOracle.ColumnName(left + c) + (top + r).ToString(CultureInfo.InvariantCulture);
					lines.Add($"'{(string)sheet.Name}'!{reference}: {value.ToString("R", CultureInfo.InvariantCulture)} in {(string)cell.NumberFormat} shows {text.Length} # at {(double)cell.ColumnWidth}");
				}
			}
		}

		return lines;
	}
}
