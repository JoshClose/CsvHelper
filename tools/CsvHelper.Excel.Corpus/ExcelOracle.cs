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
/// Writes <c>excel-oracle/</c>: Excel's answers, through COM, to what each corpus workbook's cells hold. Excel opens each
/// workbook read-only, with calculation manual, so that it shows the values the file stores rather than its own, and
/// with macros disabled. For each cell of each worksheet's used range that has a value or a formula, it writes
/// <c>Value2</c> and the type of <c>Value</c>, the text Excel shows (with the columns widened in memory), the number
/// format, the formula, and for numbers <c>CELL("format")</c> and the format's text of a day and a half, which tell
/// times and elapsed times from numbers (see <c>excel-formats.json</c>). A workbook Excel opens only by repairing it is
/// answered as repaired, with the records of Excel's repair log.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelOracle
{
	/// <summary><c>xlCalculationManual</c>.</summary>
	private const int CalculationManual = -4135;

	/// <summary><c>xlRepairFile</c>, for a workbook Excel opens only by repairing it.</summary>
	private const int RepairFile = 1;

	/// <summary><c>msoAutomationSecurityForceDisable</c>: no macro runs.</summary>
	private const int MacrosDisabled = 3;

	/// <summary>The widest a column can be, so that a number's text is never <c>####</c> for want of room.</summary>
	private const double WidestColumn = 255;

	/// <summary>The SCODE of an error value as COM passes it, less Excel's code: <c>0x800A0000</c>.</summary>
	private const int ErrorScode = unchecked((int)0x800A0000);

	/// <summary>
	/// The corpus workbooks left out: the encrypted one Excel can't open without its password, and the benchmarks'
	/// cross-checks, of 42,000 text cells and 12,000 typed cells.
	/// </summary>
	private static readonly string[] LeftOut = ["excel/encrypted.xlsx", "excel/packageassets-1695.xlsx", "excel/orders-1000.xlsx"];

	/// <summary>The texts of Excel's error values, by their codes.</summary>
	private static readonly Dictionary<int, string> Errors = new()
	{
		[2000] = "#NULL!",
		[2007] = "#DIV/0!",
		[2015] = "#VALUE!",
		[2023] = "#REF!",
		[2029] = "#NAME?",
		[2036] = "#NUM!",
		[2042] = "#N/A",
		[2043] = "#GETTING_DATA",
		[2045] = "#SPILL!",
		[2046] = "#CONNECT!",
		[2047] = "#BLOCKED!",
		[2048] = "#UNKNOWN!",
		[2049] = "#FIELD!",
		[2050] = "#CALC!",
	};

	/// <summary>Writes the answers for every workbook of the corpus.</summary>
	/// <param name="files">The corpus folder.</param>
	public static void Write(string files)
	{
		var workbooks = Directory.EnumerateFiles(files, "*", SearchOption.AllDirectories)
			.Where(p => Path.GetExtension(p) is ".xlsx" or ".xlsm" or ".xltx")
			.Select(p => Path.GetRelativePath(files, p).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(p => !LeftOut.Contains(p))
			.OrderBy(p => p, StringComparer.Ordinal)
			.ToList();
		var folder = Path.Combine(files, "excel-oracle");
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, true);
		}

		ExcelProducer.Run(excel =>
		{
			// Calculation is the application's, set by the first workbook open: a blank one, kept open throughout.
			var blank = excel.Workbooks.Add();
			try
			{
				excel.Calculation = CalculationManual;
				excel.AutomationSecurity = MacrosDisabled;
				excel.EnableEvents = false;
				foreach (var file in workbooks)
				{
					var target = Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar) + ".json");
					Directory.CreateDirectory(Path.GetDirectoryName(target)!);
					File.WriteAllBytes(target, Answer(excel, files, file));
					Console.WriteLine($"  {target}");
				}
			}
			finally
			{
				blank.Close(false);
			}
		});
	}

	/// <summary>Excel's answers for one workbook, as indented JSON.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="files">The corpus folder.</param>
	/// <param name="file">The workbook, relative to it.</param>
	/// <returns>The JSON's UTF-8.</returns>
	private static byte[] Answer(dynamic excel, string files, string file)
	{
		using var stream = new MemoryStream();
		using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
		{
			var path = Path.Combine(files, file);
			var started = DateTime.Now.AddSeconds(-1);
			List<string>? repairs = null;
			dynamic workbook;
			try
			{
				workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false);
			}
			catch (COMException)
			{
				// Excel repairs the file to open it: its answers are then the repaired workbook's, and the log says what it repaired.
				workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false, CorruptLoad: RepairFile);
				repairs = RepairRecords(started);
			}

			try
			{
				json.WriteStartObject();
				json.WriteString("file", file);
				json.WriteBoolean("date1904", (bool)workbook.Date1904);
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
				foreach (var sheet in workbook.Worksheets)
				{
					WriteSheet(json, sheet);
				}

				json.WriteEndArray();
				json.WriteEndObject();
			}
			finally
			{
				workbook.Close(false);
			}
		}

		return stream.ToArray();
	}

	/// <summary>
	/// What Excel says it repaired, from the log it writes in the temporary folder: each record, such as "Repaired Records:
	/// Cell information from /xl/worksheets/sheet2.xml part". The log's summary, which names the file's full path, is left out.
	/// </summary>
	/// <param name="since">When the repair started.</param>
	/// <returns>The records.</returns>
	private static List<string> RepairRecords(DateTime since)
	{
		var log = new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("error*.xml")
			.Where(f => f.LastWriteTime >= since)
			.OrderByDescending(f => f.LastWriteTime)
			.FirstOrDefault();
		if (log is null)
		{
			return [];
		}

		var xml = System.Xml.Linq.XDocument.Load(log.FullName);

		return [.. xml.Descendants().Where(e => e.Name.LocalName is "repairedRecord" or "removedRecord").Select(e => e.Value)];
	}

	/// <summary>Writes a worksheet's answers: its name, and each cell of its used range with a value or a formula.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteSheet(Utf8JsonWriter json, dynamic sheet)
	{
		json.WriteStartObject();
		json.WriteString("name", (string)sheet.Name);
		json.WriteStartArray("cells");
		var used = sheet.UsedRange;
		used.Columns.ColumnWidth = WidestColumn;
		int top = used.Row;
		int left = used.Column;
		int rows = used.Rows.Count;
		int columns = used.Columns.Count;

		// Value2 for the whole range at once: an array, or a value for a range of one cell. (Value and Formula can't be
		// read so: a date past 9999 fails Value, and a text longer than a formula can be fails Formula.) A formula without
		// a value has an empty Value2, not null.
		var values2 = Grid(used.Value2, rows, columns);
		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < columns; c++)
			{
				var value2 = values2[r, c];
				if (value2 is not null)
				{
					WriteCell(json, sheet, sheet.Cells[top + r, left + c], value2);
				}
			}
		}

		json.WriteEndArray();
		json.WriteEndObject();
	}

	/// <summary>Writes a cell's answers.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The cell's worksheet, which evaluates formulas about it.</param>
	/// <param name="cell">The cell.</param>
	/// <param name="value2">Its <c>Value2</c>.</param>
	private static void WriteCell(Utf8JsonWriter json, dynamic sheet, dynamic cell, object value2)
	{
		var address = ((string)cell.Address).Replace("$", "", StringComparison.Ordinal);
		json.WriteStartObject();
		json.WriteString("cell", address);
		string type;
		switch (value2)
		{
			case string text:
				json.WriteString("value2", text);
				type = text.Length == 0 ? "empty" : "text";

				break;
			case bool flag:
				json.WriteBoolean("value2", flag);
				type = "boolean";

				break;
			case int code:
				var error = Errors.TryGetValue(code - ErrorScode, out var known) ? known : (string)cell.Text;
				json.WriteString("value2", error);
				type = "error";

				break;
			case double number:
				json.WriteNumber("value2", number);
				type = ValueType(cell);

				break;
			default:
				json.WriteString("value2", Convert.ToString(value2, System.Globalization.CultureInfo.InvariantCulture));
				type = value2.GetType().Name;

				break;
		}

		json.WriteString("type", type);
		string shown = cell.Text;
		if (!(value2 is string same && same == shown))
		{
			json.WriteString("text", shown);
		}

		string numberFormat = cell.NumberFormat;
		json.WriteString("numberFormat", numberFormat);
		if (type is "number" or "date" or "currency")
		{
			json.WriteString("category", (string)sheet.Evaluate($"CELL(\"format\",{address})"));
			json.WriteString("elapsed", Convert.ToString(sheet.Evaluate($"TEXT(1.5,\"{numberFormat.Replace("\"", "\"\"", StringComparison.Ordinal)}\")"), System.Globalization.CultureInfo.InvariantCulture));
		}

		if ((bool)cell.HasFormula)
		{
			string formula = cell.Formula;
			string formula2 = cell.Formula2;
			json.WriteString("formula", formula);
			if (formula2 != formula)
			{
				json.WriteString("formula2", formula2);
			}

			if ((bool)cell.HasArray)
			{
				json.WriteBoolean("hasArray", true);
			}
		}

		if ((bool)cell.HasSpill)
		{
			json.WriteBoolean("hasSpill", true);
		}

		json.WriteEndObject();
	}

	/// <summary>
	/// The type of a number cell's <c>Value</c>: <c>date</c> when Excel gives a date (for a format that shows a day, a
	/// month or a year), <c>currency</c> for a currency format, else <c>number</c>. A serial past 9999-12-31 in a date
	/// format fails <c>Value</c>, which is <c>number</c>, since Excel has no date for it.
	/// </summary>
	/// <param name="cell">The cell.</param>
	/// <returns>The type.</returns>
	private static string ValueType(dynamic cell)
	{
		try
		{
			object? value = cell.Value();

			return value is DateTime ? "date" : value is decimal ? "currency" : "number";
		}
		catch (COMException)
		{
			return "number";
		}
	}

	/// <summary>A range's values as a 0-based grid: COM gives a 1-based array, or a lone value for a range of one cell.</summary>
	/// <param name="values">What COM gave.</param>
	/// <param name="rows">The range's rows.</param>
	/// <param name="columns">The range's columns.</param>
	/// <returns>The grid.</returns>
	private static object?[,] Grid(object? values, int rows, int columns)
	{
		var grid = new object?[rows, columns];
		if (values is Array array)
		{
			for (var r = 0; r < rows; r++)
			{
				for (var c = 0; c < columns; c++)
				{
					grid[r, c] = array.GetValue(r + 1, c + 1);
				}
			}
		}
		else
		{
			grid[0, 0] = values;
		}

		return grid;
	}
}
