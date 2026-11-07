using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes <c>excel-oracle/</c>: Excel's answers, through COM, to what each corpus workbook's cells hold. Excel opens each
/// workbook read-only, with calculation manual, so that it shows the values the file stores rather than its own, and
/// with macros disabled. For each cell of each worksheet's used range that has a value or a formula, it writes
/// <c>Value2</c> and the type of <c>Value</c>, the text Excel shows (with the columns widened in memory), the number
/// format, the formula, and for numbers <c>CELL("format")</c> and the format's text of a day and a half, which tell
/// times and elapsed times from numbers (see <c>excel-formats.json</c>). A workbook Excel opens only by repairing it is
/// answered as repaired, with the records of Excel's repair log, and one it can't open even so as refused. For each
/// worksheet it also writes the rows and columns Excel hides and the merged ranges, and for the workbook its defined names
/// and tables.
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

	/// <summary>
	/// The HRESULT of Excel's own failure of a call (<c>0x800A03EC</c>), such as <c>SpecialCells</c> finding no cell or
	/// <c>RefersToRange</c> of a name that is no range; COM's failures, such as a busy Excel's, have others.
	/// </summary>
	private const int ExcelError = unchecked((int)0x800A03EC);

	/// <summary><c>RPC_E_SERVERCALL_RETRYLATER</c>: Excel is busy, and asks for the call again.</summary>
	private const int RetryLater = unchecked((int)0x8001010A);

	/// <summary><c>RPC_E_CALL_REJECTED</c>: Excel turned the call away.</summary>
	private const int CallRejected = unchecked((int)0x80010001);

	/// <summary><c>xlCellTypeVisible</c>, for <c>SpecialCells</c>.</summary>
	private const int VisibleCells = 12;

	/// <summary><c>xlSheetVisible</c>.</summary>
	private const int SheetVisible = -1;

	/// <summary>The rows of the grid.</summary>
	private const int MaxRows = 1048576;

	/// <summary>The columns of the grid.</summary>
	private const int MaxColumns = 16384;

	/// <summary>The SCODE of an error value as COM passes it, less Excel's code: <c>0x800A0000</c>.</summary>
	private const int ErrorScode = unchecked((int)0x800A0000);

	/// <summary>
	/// The corpus workbooks left out: the encrypted one Excel can't open without its password, and the benchmarks'
	/// cross-checks, of 42,000 text cells, 12,000 typed cells and the report's 6,800 cells.
	/// </summary>
	private static readonly string[] LeftOut = ["excel/encrypted.xlsx", "excel/packageassets-1695.xlsx", "excel/orders-1000.xlsx", "excel/packageassets-report-250.xlsx"];

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
				// Excel repairs the file to open it: its answers are then the repaired workbook's, and the log says what it
				// repaired. A file it can't open even so is answered as refused.
				try
				{
					workbook = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: true, Editable: true, AddToMru: false, CorruptLoad: RepairFile);
				}
				catch (COMException)
				{
					json.WriteStartObject();
					json.WriteString("file", file);
					json.WriteBoolean("refused", true);
					json.WriteEndObject();
					json.Flush();

					return stream.ToArray();
				}

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
				WriteNames(json, workbook);
				WriteTables(json, workbook);
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
	internal static List<string> RepairRecords(DateTime since)
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
		var used = sheet.UsedRange;

		// What is hidden and merged first: widening the columns shows the hidden ones.
		WriteHidden(json, sheet);
		WriteMerged(json, used);
		json.WriteStartArray("cells");
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

	/// <summary>
	/// Writes a worksheet's hidden rows and columns, over the whole grid, as runs such as <c>8:8</c> and <c>D:XFD</c>: the
	/// rows and columns that no visible cell is in, from <c>SpecialCells(xlCellTypeVisible)</c> over every cell, which
	/// hides what Excel hides however the file says so (a filter, a collapsed group, a height or width of 0, a sheet's
	/// defaults).
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteHidden(Utf8JsonWriter json, dynamic sheet)
	{
		var rows = new bool[MaxRows];
		var columns = new bool[MaxColumns];
		try
		{
			// Excel can still be busy with a workbook it has just opened (building a pivot table's cells), and says so.
			foreach (var area in WhenReady(() => (object)sheet.Cells.SpecialCells(VisibleCells).Areas))
			{
				int row = area.Row;
				int rowCount = area.Rows.Count;
				int column = area.Column;
				int columnCount = area.Columns.Count;
				Array.Fill(rows, true, row - 1, rowCount);
				Array.Fill(columns, true, column - 1, columnCount);
			}
		}
		catch (COMException e) when (e.HResult == ExcelError)
		{
			// No cell of the sheet is visible, so its first row or its first column is hidden. Any other failure (Excel
			// busy, a transient error) must not read as a sheet that hides everything.
			if (!(bool)sheet.Rows[1].Hidden && !(bool)sheet.Columns[1].Hidden)
			{
				throw new InvalidOperationException($"SpecialCells found no visible cell of {sheet.Name}, whose first row and first column show.", e);
			}
		}

		json.WriteStartArray("hiddenRows");
		foreach (var (first, last) in Runs(rows))
		{
			json.WriteStringValue($"{first}:{last}");
		}

		json.WriteEndArray();
		json.WriteStartArray("hiddenColumns");
		foreach (var (first, last) in Runs(columns))
		{
			json.WriteStringValue($"{ColumnName(first)}:{ColumnName(last)}");
		}

		json.WriteEndArray();
	}

	/// <summary>
	/// Makes a COM call, again while Excel answers that it is busy (<c>RPC_E_SERVERCALL_RETRYLATER</c>, or
	/// <c>RPC_E_CALL_REJECTED</c>), every tenth of a second for up to a minute.
	/// </summary>
	/// <param name="call">The call.</param>
	/// <returns>Its result.</returns>
	private static dynamic WhenReady(Func<object> call)
	{
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				return call();
			}
			catch (COMException e) when (e.HResult is RetryLater or CallRejected && attempt < 600)
			{
				Thread.Sleep(100);
			}
		}
	}

	/// <summary>The runs of what isn't shown, 1-based.</summary>
	/// <param name="shown">Whether each row or column is shown.</param>
	/// <returns>The first and last of each run of hidden ones.</returns>
	private static List<(int First, int Last)> Runs(bool[] shown)
	{
		var runs = new List<(int, int)>();
		for (var i = 0; i < shown.Length; i++)
		{
			if (shown[i])
			{
				continue;
			}

			var first = i;
			while (i + 1 < shown.Length && !shown[i + 1])
			{
				i++;
			}

			runs.Add((first + 1, i + 1));
		}

		return runs;
	}

	/// <summary>A column's letters.</summary>
	/// <param name="column">The 1-based column.</param>
	/// <returns>The letters, such as <c>XFD</c>.</returns>
	private static string ColumnName(int column)
	{
		var name = "";
		for (; column > 0; column = (column - 1) / 26)
		{
			name = (char)('A' + (column - 1) % 26) + name;
		}

		return name;
	}

	/// <summary>
	/// Writes a worksheet's merged ranges, each once, as each cell's <c>MergeArea</c> gives them, in the order of their
	/// top-left cells: a merged range is in the used range, which its formatting is part of.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="used">The worksheet's used range.</param>
	private static void WriteMerged(Utf8JsonWriter json, dynamic used)
	{
		json.WriteStartArray("merged");

		// MergeCells is false when no cell of the range is merged, true when all are, and null when some are.
		object? any = used.MergeCells;
		if (any is not false)
		{
			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var cell in used.Cells)
			{
				if ((bool)cell.MergeCells)
				{
					var address = ((string)cell.MergeArea.Address).Replace("$", "", StringComparison.Ordinal);
					if (seen.Add(address))
					{
						json.WriteStringValue(address);
					}
				}
			}
		}

		json.WriteEndArray();
	}

	/// <summary>
	/// Writes the workbook's defined names, as COM's <c>Names</c> gives them: <c>Name</c> (<c>Report!Local</c> for a
	/// sheet's own), <c>RefersTo</c>, the sheet and address of <c>RefersToRange</c> when Excel has a range for it, and
	/// <c>Visible</c>. A relative name is relative to the active cell, so A1 of the first visible worksheet is made active
	/// first, which Excel stores relative names to.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The workbook.</param>
	private static void WriteNames(Utf8JsonWriter json, dynamic workbook)
	{
		foreach (var sheet in workbook.Worksheets)
		{
			if ((int)sheet.Visible == SheetVisible)
			{
				sheet.Activate();
				sheet.Range["A1"].Select();

				break;
			}
		}

		json.WriteStartArray("names");
		foreach (var name in workbook.Names)
		{
			json.WriteStartObject();
			json.WriteString("name", (string)name.Name);
			json.WriteString("refersTo", (string)name.RefersTo);
			dynamic? range = null;
			try
			{
				range = name.RefersToRange;
			}
			catch (COMException e) when (e.HResult == ExcelError)
			{
				// Not a range to Excel: a constant, a formula, a 3-D reference, #REF!.
			}

			if (range is not null)
			{
				json.WriteString("sheet", (string)range.Worksheet.Name);
				json.WriteString("range", ((string)range.Address).Replace("$", "", StringComparison.Ordinal));
			}

			json.WriteBoolean("visible", (bool)name.Visible);
			json.WriteEndObject();
		}

		json.WriteEndArray();
	}

	/// <summary>
	/// Writes each worksheet's tables, as COM's <c>ListObjects</c> gives them: <c>Name</c> (the part's <c>name</c>),
	/// <c>DisplayName</c> (what formulas use), the address of <c>Range</c>, <c>ShowHeaders</c>, <c>ShowTotals</c>, and the
	/// column names.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The workbook.</param>
	private static void WriteTables(Utf8JsonWriter json, dynamic workbook)
	{
		json.WriteStartArray("tables");
		foreach (var sheet in workbook.Worksheets)
		{
			foreach (var list in sheet.ListObjects)
			{
				json.WriteStartObject();
				json.WriteString("sheet", (string)sheet.Name);
				json.WriteString("name", (string)list.Name);
				json.WriteString("displayName", (string)list.DisplayName);
				json.WriteString("range", ((string)list.Range.Address).Replace("$", "", StringComparison.Ordinal));
				json.WriteBoolean("showHeaders", (bool)list.ShowHeaders);
				json.WriteBoolean("showTotals", (bool)list.ShowTotals);
				json.WriteStartArray("columns");
				foreach (var column in list.ListColumns)
				{
					json.WriteStringValue((string)column.Name);
				}

				json.WriteEndArray();
				json.WriteEndObject();
			}
		}

		json.WriteEndArray();
	}

	/// <summary>Writes a cell's answers.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The cell's worksheet, which evaluates formulas about it.</param>
	/// <param name="cell">The cell.</param>
	/// <param name="value2">Its <c>Value2</c>.</param>
	internal static void WriteCell(Utf8JsonWriter json, dynamic sheet, dynamic cell, object value2)
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
	internal static object?[,] Grid(object? values, int rows, int columns)
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
