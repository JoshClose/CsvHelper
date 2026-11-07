using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes <c>excel-written/</c>: Excel's answers, through COM, about the workbooks CsvHelper.Excel writes (the
/// <see cref="Written"/> set). Each workbook is written into a temporary folder and opened read-only, with calculation
/// automatic, since the writer's formulas have no cached values, and with macros disabled. For each worksheet the answers
/// hold its name, the width of each column of its used range, and each cell with a value or a formula as the oracle's
/// answers have it (<c>ExcelOracle.WriteCell</c>); the columns keep their widths, so that a number that doesn't fit
/// shows as <c>########</c>. With them go the workbook's <see cref="Written.PartsHash"/>, the fingerprint of the culture
/// data it read (<see cref="Written.CultureData"/>), the screen's DPI, which Excel's widths depend on, and the operating
/// system, whose culture data .NET reads on Windows (its ICU).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelWritten
{
	/// <summary><c>xlCalculationAutomatic</c>.</summary>
	private const int CalculationAutomatic = -4105;

	/// <summary><c>xlRepairFile</c>, for a workbook Excel opens only by repairing it.</summary>
	private const int RepairFile = 1;

	/// <summary><c>msoAutomationSecurityForceDisable</c>: no macro runs.</summary>
	private const int MacrosDisabled = 3;

	/// <summary><c>DPI_AWARENESS_CONTEXT_SYSTEM_AWARE</c>, for the system's DPI rather than the 96 an unaware thread is told.</summary>
	private static readonly IntPtr SystemAware = new(-2);

	/// <summary>Writes the answers for every workbook of the set.</summary>
	/// <param name="files">The corpus folder.</param>
	public static void Write(string files)
	{
		var folder = Path.Combine(files, "excel-written");
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, true);
		}

		Directory.CreateDirectory(folder);
		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		try
		{
			var dpi = SystemDpi();
			var written = Written.Workbooks.Select(w => (Workbook: w, Bytes: Written.Write(w))).ToList();
			foreach (var (workbook, bytes) in written)
			{
				File.WriteAllBytes(Path.Combine(temp, workbook.Name), bytes);
			}

			ExcelProducer.Run(excel =>
			{
				// Calculation is the application's, set by the first workbook open: a blank one, kept open throughout.
				var blank = excel.Workbooks.Add();
				try
				{
					excel.Calculation = CalculationAutomatic;
					excel.AutomationSecurity = MacrosDisabled;
					excel.EnableEvents = false;
					foreach (var (workbook, bytes) in written)
					{
						var target = Path.Combine(folder, workbook.Name + ".json");
						File.WriteAllBytes(target, Answer(excel, Path.Combine(temp, workbook.Name), workbook, bytes, dpi));
						Console.WriteLine($"  {target}");
					}
				}
				finally
				{
					blank.Close(false);
				}
			});
		}
		finally
		{
			Directory.Delete(temp, true);
		}
	}

	/// <summary>Excel's answers for one workbook, as indented JSON.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="path">The workbook's file.</param>
	/// <param name="workbook">The workbook of the set.</param>
	/// <param name="bytes">The bytes written.</param>
	/// <param name="dpi">The screen's DPI.</param>
	/// <returns>The JSON's UTF-8.</returns>
	private static byte[] Answer(dynamic excel, string path, WrittenWorkbook workbook, byte[] bytes, int dpi)
	{
		using var stream = new MemoryStream();
		using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
		{
			json.WriteStartObject();
			json.WriteString("file", workbook.Name);
			json.WriteString("description", workbook.Description);
			json.WriteString("sha256", Written.PartsHash(bytes));
			if (workbook.Cultures.Count > 0)
			{
				json.WriteStartArray("cultures");
				foreach (var culture in workbook.Cultures)
				{
					json.WriteStringValue(culture);
				}

				json.WriteEndArray();
				json.WriteString("cultureData", Written.CultureData(workbook.Cultures));
			}

			json.WriteNumber("dpi", dpi);
			json.WriteString("os", RuntimeInformation.OSDescription);
			var started = DateTime.Now.AddSeconds(-1);
			List<string>? repairs = null;
			dynamic opened;
			try
			{
				opened = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false);
			}
			catch (COMException)
			{
				try
				{
					opened = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false, CorruptLoad: RepairFile);
				}
				catch (COMException)
				{
					json.WriteBoolean("refused", true);
					json.WriteEndObject();
					json.Flush();

					return stream.ToArray();
				}

				repairs = ExcelOracle.RepairRecords(started);
			}

			try
			{
				if (repairs is not null)
				{
					json.WriteStartArray("repaired");
					foreach (var repair in repairs)
					{
						json.WriteStringValue(repair);
					}

					json.WriteEndArray();
				}

				json.WriteStartArray("sheets");
				foreach (var sheet in opened.Worksheets)
				{
					WriteSheet(json, sheet);
				}

				json.WriteEndArray();
			}
			finally
			{
				opened.Close(false);
			}

			json.WriteEndObject();
		}

		return stream.ToArray();
	}

	/// <summary>
	/// Writes a worksheet's answers: its name, each column's <c>ColumnWidth</c> from A to the used range's last, and each
	/// cell of the used range with a value or a formula, at the widths written.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteSheet(Utf8JsonWriter json, dynamic sheet)
	{
		json.WriteStartObject();
		json.WriteString("name", (string)sheet.Name);
		var used = sheet.UsedRange;
		int top = used.Row;
		int left = used.Column;
		int rows = used.Rows.Count;
		int columns = used.Columns.Count;
		json.WriteStartArray("widths");
		for (var column = 1; column < left + columns; column++)
		{
			json.WriteNumberValue((double)sheet.Columns[column].ColumnWidth);
		}

		json.WriteEndArray();
		json.WriteStartArray("cells");
		// A formula without a value has an empty Value2, not null.
		var values2 = ExcelOracle.Grid(used.Value2, rows, columns);
		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < columns; c++)
			{
				var value2 = values2[r, c];
				if (value2 is not null)
				{
					ExcelOracle.WriteCell(json, sheet, sheet.Cells[top + r, left + c], value2);
				}
			}
		}

		json.WriteEndArray();
		json.WriteEndObject();
	}

	/// <summary>The system's DPI, as a system-aware thread sees it.</summary>
	/// <returns>The DPI: 96 at 100%, 144 at 150%.</returns>
	private static int SystemDpi()
	{
		var previous = SetThreadDpiAwarenessContext(SystemAware);
		try
		{
			return (int)GetDpiForSystem();
		}
		finally
		{
			SetThreadDpiAwarenessContext(previous);
		}
	}

	/// <summary>Sets the calling thread's DPI awareness.</summary>
	/// <param name="context">The awareness context.</param>
	/// <returns>The previous one.</returns>
	[DllImport("user32.dll")]
	private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

	/// <summary>The system's DPI, for the calling thread's awareness.</summary>
	/// <returns>The DPI.</returns>
	[DllImport("user32.dll")]
	private static extern uint GetDpiForSystem();
}
