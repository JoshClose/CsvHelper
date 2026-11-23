using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes <c>excel-csv/</c> (Phase 8's step 7): Excel's own "CSV UTF-8" export, through COM, of every worksheet of
/// every corpus workbook that has Excel's answers (<see cref="ExcelOracle"/>'s) and of every workbook of the written
/// set (<see cref="Written"/>), each workbook's in <c>excel-csv/&lt;file&gt;.json</c>. Excel opens each workbook
/// read-only, the corpus's with calculation manual and without calculating before a save, so that formulas show the
/// values the file stores, and the written set's with calculation automatic, since the writer's formulas have no cached
/// values (as <see cref="ExcelWritten"/> opens them). It saves each worksheet as CSV UTF-8 in turn, showing a hidden
/// one first, since Excel exports the active sheet. For each it keeps the sheet's name, its used range, from whose
/// top-left cell the export starts, whether the file began with a byte-order mark, and the CSV's text. A written
/// workbook's answers hold its <see cref="Written.PartsHash"/>, and <c>cultures.xlsx</c>'s the fingerprint of the
/// culture data it read, as <c>excel-written/</c>'s do. <see cref="ExportSet"/> limits a run to the corpus or to the
/// written set, whose answers are <c>excel-csv/excel-written/</c>: the other's answers are kept as they are.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelExports
{
	/// <summary><c>xlCSVUTF8</c>: Excel's "CSV UTF-8".</summary>
	private const int CsvUtf8 = 62;

	/// <summary><c>xlCalculationManual</c>.</summary>
	private const int CalculationManual = -4135;

	/// <summary><c>xlCalculationAutomatic</c>.</summary>
	private const int CalculationAutomatic = -4105;

	/// <summary><c>xlRepairFile</c>, for a workbook Excel opens only by repairing it.</summary>
	private const int RepairFile = 1;

	/// <summary><c>msoAutomationSecurityForceDisable</c>: no macro runs.</summary>
	private const int MacrosDisabled = 3;

	/// <summary><c>xlSheetVisible</c>.</summary>
	private const int SheetVisible = -1;

	/// <summary>The byte-order mark of UTF-8.</summary>
	private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

	/// <summary>
	/// The corpus workbooks left out, as <see cref="ExcelOracle"/> leaves them out: the encrypted one, and the benchmarks'
	/// cross-checks.
	/// </summary>
	private static readonly string[] LeftOut = ["excel/encrypted.xlsx", "excel/packageassets-1695.xlsx", "excel/orders-1000.xlsx", "excel/packageassets-report-250.xlsx"];

	/// <summary>
	/// Writes the exports of the corpus and of the written set, or of one of them: its answers are deleted and made again,
	/// the other's kept.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="set">The workbooks to export.</param>
	public static void Write(string files, ExportSet set = ExportSet.All)
	{
		var folder = Path.Combine(files, "excel-csv");
		var writtenFolder = Path.Combine(folder, "excel-written");
		if (Directory.Exists(folder))
		{
			foreach (var entry in Directory.EnumerateFileSystemEntries(folder).ToList())
			{
				var isWritten = string.Equals(Path.GetFullPath(entry), Path.GetFullPath(writtenFolder), StringComparison.OrdinalIgnoreCase);
				if (set == ExportSet.All || (set == ExportSet.Written) == isWritten)
				{
					if (Directory.Exists(entry))
					{
						Directory.Delete(entry, true);
					}
					else
					{
						File.Delete(entry);
					}
				}
			}
		}

		var corpus = Directory.EnumerateFiles(files, "*", SearchOption.AllDirectories)
			.Where(p => Path.GetExtension(p) is ".xlsx" or ".xlsm" or ".xltx")
			.Select(p => Path.GetRelativePath(files, p).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(p => !LeftOut.Contains(p) && !p.StartsWith("excel-written/", StringComparison.Ordinal) && !p.StartsWith("display/", StringComparison.Ordinal))
			.OrderBy(p => p, StringComparer.Ordinal)
			.ToList();
		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		try
		{
			if (set != ExportSet.Written)
			{
				ExcelProducer.Run(excel => Each(excel, CalculationManual, corpus.Select(f => (f, Path.Combine(files, f), (WrittenWorkbook?)null, (byte[]?)null)), folder, temp));
			}

			if (set != ExportSet.Corpus)
			{
				var written = Written.Workbooks.Select(w => (Workbook: w, Bytes: Written.Write(w))).ToList();
				foreach (var (workbook, bytes) in written)
				{
					File.WriteAllBytes(Path.Combine(temp, workbook.Name), bytes);
				}

				ExcelProducer.Run(excel => Each(excel, CalculationAutomatic, written.Select(w => ("excel-written/" + w.Workbook.Name, Path.Combine(temp, w.Workbook.Name), (WrittenWorkbook?)w.Workbook, (byte[]?)w.Bytes)), folder, temp));
			}
		}
		finally
		{
			Directory.Delete(temp, true);
		}
	}

	/// <summary>Exports workbooks in one run of Excel, with calculation as given.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="calculation">The calculation mode.</param>
	/// <param name="workbooks">Each workbook's name in the answers, its file, and for the written set its workbook and bytes.</param>
	/// <param name="folder">The answers' folder.</param>
	/// <param name="temp">A folder for the CSV files.</param>
	private static void Each(dynamic excel, int calculation, IEnumerable<(string Name, string Path, WrittenWorkbook? Written, byte[]? Bytes)> workbooks, string folder, string temp)
	{
		// Calculation is the application's, set by the first workbook open: a blank one, kept open throughout.
		var blank = excel.Workbooks.Add();
		try
		{
			excel.Calculation = calculation;

			// Excel calculates before it saves, even with calculation manual: the corpus's export would then show its own
			// values rather than those the file stores.
			excel.CalculateBeforeSave = false;
			excel.AutomationSecurity = MacrosDisabled;
			excel.EnableEvents = false;
			foreach (var (name, path, written, bytes) in workbooks)
			{
				var target = Path.Combine(folder, name.Replace('/', Path.DirectorySeparatorChar) + ".json");
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				File.WriteAllBytes(target, Answer(excel, name, path, written, bytes, temp));
				Console.WriteLine($"  {target}");
			}
		}
		finally
		{
			blank.Close(false);
		}
	}

	/// <summary>One workbook's exports, as indented JSON.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="name">The workbook's name in the answers.</param>
	/// <param name="path">Its file.</param>
	/// <param name="written">The workbook of the written set, or <see langword="null"/> for a corpus workbook.</param>
	/// <param name="bytes">The written workbook's bytes, or <see langword="null"/>.</param>
	/// <param name="temp">A folder for the CSV files.</param>
	/// <returns>The JSON's UTF-8.</returns>
	private static byte[] Answer(dynamic excel, string name, string path, WrittenWorkbook? written, byte[]? bytes, string temp)
	{
		using var stream = new MemoryStream();
		using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
		{
			json.WriteStartObject();
			json.WriteString("file", name);
			if (written is not null)
			{
				json.WriteString("sha256", Written.PartsHash(bytes!));
				if (written.Cultures.Count > 0)
				{
					json.WriteString("cultureData", Written.CultureData(written.Cultures));
				}
			}

			dynamic workbook;
			try
			{
				workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false);
			}
			catch (COMException)
			{
				try
				{
					workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false, CorruptLoad: RepairFile);
				}
				catch (COMException)
				{
					json.WriteBoolean("refused", true);
					json.WriteEndObject();
					json.Flush();

					return stream.ToArray();
				}

				json.WriteBoolean("repaired", true);
			}

			try
			{
				json.WriteStartArray("sheets");
				var index = 0;
				foreach (var sheet in workbook.Worksheets)
				{
					index++;
					WriteSheet(json, workbook, sheet, Path.Combine(temp, $"export{index}.csv"));
				}

				json.WriteEndArray();
			}
			finally
			{
				workbook.Close(false);
			}

			json.WriteEndObject();
		}

		return stream.ToArray();
	}

	/// <summary>
	/// Writes a worksheet's export: its name, its used range, whether the file began with a byte-order mark, and the CSV's
	/// text after it. A hidden sheet is shown first, in the workbook opened, since Excel exports the active sheet; one it
	/// won't show is answered with <c>exported</c> false.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="csv">The file to export it to.</param>
	private static void WriteSheet(Utf8JsonWriter json, dynamic workbook, dynamic sheet, string csv)
	{
		json.WriteStartObject();
		json.WriteString("name", (string)sheet.Name);
		json.WriteString("used", ((string)sheet.UsedRange.Address).Replace("$", "", StringComparison.Ordinal));
		int visible = sheet.Visible;
		if (visible != SheetVisible)
		{
			json.WriteNumber("visible", visible);
			try
			{
				sheet.Visible = SheetVisible;
			}
			catch (COMException)
			{
				json.WriteBoolean("exported", false);
				json.WriteEndObject();

				return;
			}
		}

		sheet.Activate();
		workbook.SaveAs(Filename: csv, FileFormat: CsvUtf8);

		// The workbook is now the CSV file, which Excel keeps open until the next save or the workbook's close.
		byte[] bytes;
		using (var file = new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			bytes = new byte[file.Length];
			file.ReadExactly(bytes);
		}

		var bom = bytes.AsSpan().StartsWith(ByteOrderMark);
		json.WriteBoolean("bom", bom);
		json.WriteString("csv", Encoding.UTF8.GetString(bytes, bom ? ByteOrderMark.Length : 0, bytes.Length - (bom ? ByteOrderMark.Length : 0)));
		json.WriteEndObject();
	}
}

/// <summary>The workbooks <see cref="ExcelExports.Write"/> exports: the tool's <c>--set</c>.</summary>
internal enum ExportSet
{
	/// <summary>The corpus's and the written set's: <c>excel-csv/</c> whole.</summary>
	All,

	/// <summary>The corpus's, with Excel's answers: <c>excel-csv/</c> but <c>excel-written/</c>.</summary>
	Corpus,

	/// <summary>The written set's (<see cref="Corpus.Written"/>): <c>excel-csv/excel-written/</c>.</summary>
	Written,
}
