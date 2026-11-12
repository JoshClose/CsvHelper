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
/// shows as <c>########</c>. They hold its look too: the frozen panes, the filter, the tables, the merged ranges, the
/// links, the hidden columns, the rows Excel sized otherwise than its standard height, each cell's font, cell style, wrap
/// and (for a cell without a value) number format where they aren't Normal's, from row 1 to the row below the used range;
/// and last, each column's width at Excel's own AutoFit. With them go the workbook's defined names, its
/// <see cref="Written.PartsHash"/>, the fingerprint of the culture data it read (<see cref="Written.CultureData"/>), the
/// screen's DPI, which Excel's widths depend on, and the operating system, whose culture data .NET reads on Windows (its ICU).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelWritten
{
	/// <summary><c>xlCalculationAutomatic</c>.</summary>
	private const int CalculationAutomatic = -4105;

	/// <summary><c>xlUnderlineStyleNone</c>, Normal's underline.</summary>
	private const int NoUnderline = -4142;

	/// <summary>Normal's font.</summary>
	private const string NormalFont = "Calibri";

	/// <summary>Normal's font size.</summary>
	private const double NormalSize = 11;

	/// <summary><c>xlNormal</c>, a window neither minimized nor maximized, which takes a size.</summary>
	private const int WindowNormal = -4143;

	/// <summary>The width a workbook's window is given, in points: room for its frozen columns.</summary>
	private const double WindowWidth = 800;

	/// <summary>The height a workbook's window is given, in points: room for its frozen rows.</summary>
	private const double WindowHeight = 500;

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

				ExcelOracle.WriteNames(json, opened);
				json.WriteStartArray("sheets");
				foreach (var sheet in opened.Worksheets)
				{
					WriteSheet(json, opened, sheet);
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
	/// Writes a worksheet's answers: its name, each column's <c>ColumnWidth</c> from A to the used range's last, each cell
	/// of the used range with a value or a formula, at the widths written, its look (<see cref="WriteLook"/>), and last each
	/// column's width at Excel's own AutoFit.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The worksheet's workbook, whose window shows its panes.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteSheet(Utf8JsonWriter json, dynamic workbook, dynamic sheet)
	{
		json.WriteStartObject();
		json.WriteString("name", (string)sheet.Name);
		var used = sheet.UsedRange;
		int top = used.Row;
		int left = used.Column;
		int rows = used.Rows.Count;
		int columns = used.Columns.Count;
		json.WriteString("used", (string)Address(used));
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
		WriteLook(json, workbook, sheet, used, top + rows - 1, left + columns - 1);

		// Excel's own AutoFit of each column, last, since it changes the widths; a hidden column's AutoFit would show it.
		json.WriteStartArray("autoFit");
		for (var column = 1; column < left + columns; column++)
		{
			var each = sheet.Columns[column];
			if ((bool)each.Hidden)
			{
				json.WriteNullValue();
			}
			else
			{
				each.AutoFit();
				json.WriteNumberValue((double)each.ColumnWidth);
			}
		}

		json.WriteEndArray();
		json.WriteEndObject();
	}

	/// <summary>
	/// Writes a worksheet's look: its frozen panes (<c>freeze</c>, the rows and columns frozen, or null), its filter's range
	/// (<c>filter</c>, or null), its tables, its merged ranges, its links (each one's cell, <c>Address</c> and
	/// <c>SubAddress</c>), its hidden columns, the rows whose height isn't the sheet's standard height (<c>heights</c>, with
	/// <c>standardHeight</c>), and the cells whose look isn't Normal's (<c>looks</c>): bold, a size or a font other than
	/// Calibri 11, an underline, a colour, a cell style other than Normal, wrapped, and for a cell without a value a number
	/// format other than General, from row 1 to the row below the used range, from column A to the used range's last.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The worksheet's workbook.</param>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="used">Its used range.</param>
	/// <param name="lastRow">The used range's last row.</param>
	/// <param name="lastColumn">The used range's last column.</param>
	private static void WriteLook(Utf8JsonWriter json, dynamic workbook, dynamic sheet, dynamic used, int lastRow, int lastColumn)
	{
		// The window shows the active sheet's panes. An invisible Excel lays out a workbook opened after the first in a
		// window with no room, and then reports a split clipped to it (SplitRow 1 for 2 rows frozen, or 0), so the window is
		// given a size first, and must then have room.
		sheet.Activate();
		var window = workbook.Windows[1];
		window.WindowState = WindowNormal;
		window.Width = WindowWidth;
		window.Height = WindowHeight;
		if ((double)window.UsableHeight <= 0 || (double)window.UsableWidth <= 0)
		{
			throw new InvalidOperationException($"Excel's window of {sheet.Name} has no room ({window.UsableWidth} by {window.UsableHeight}), so its split can't be read.");
		}

		if ((bool)window.FreezePanes)
		{
			json.WriteStartObject("freeze");
			json.WriteNumber("rows", (int)window.SplitRow);
			json.WriteNumber("columns", (int)window.SplitColumn);
			json.WriteEndObject();
		}
		else
		{
			json.WriteNull("freeze");
		}

		if ((bool)sheet.AutoFilterMode)
		{
			json.WriteString("filter", (string)Address(sheet.AutoFilter.Range));
		}
		else
		{
			json.WriteNull("filter");
		}

		json.WriteStartArray("tables");
		foreach (var list in sheet.ListObjects)
		{
			json.WriteStartObject();
			json.WriteString("name", (string)list.Name);
			json.WriteString("range", (string)Address(list.Range));
			json.WriteString("style", (string?)TableStyle(list));
			json.WriteBoolean("stripes", (bool)list.ShowTableStyleRowStripes);
			json.WriteBoolean("buttons", (bool)list.ShowAutoFilter);
			json.WriteStartArray("columns");
			foreach (var column in list.ListColumns)
			{
				json.WriteStringValue((string)column.Name);
			}

			json.WriteEndArray();
			json.WriteEndObject();
		}

		json.WriteEndArray();
		ExcelOracle.WriteMerged(json, used);
		json.WriteStartArray("links");
		foreach (var link in sheet.Hyperlinks)
		{
			json.WriteStartObject();
			json.WriteString("cell", (string)Address(link.Range));
			json.WriteString("address", (string?)NullIfEmpty((object?)link.Address));
			json.WriteString("subAddress", (string?)NullIfEmpty((object?)link.SubAddress));
			json.WriteEndObject();
		}

		json.WriteEndArray();
		json.WriteStartArray("hiddenColumns");
		for (var column = 1; column <= lastColumn; column++)
		{
			if ((bool)sheet.Columns[column].Hidden)
			{
				json.WriteStringValue(ExcelOracle.ColumnName(column));
			}
		}

		json.WriteEndArray();
		double standard = sheet.StandardHeight;
		json.WriteNumber("standardHeight", standard);
		json.WriteStartArray("heights");
		for (var row = 1; row <= lastRow + 1; row++)
		{
			double height = sheet.Rows[row].RowHeight;
			if (height != standard)
			{
				json.WriteStartObject();
				json.WriteNumber("row", row);
				json.WriteNumber("height", height);
				json.WriteEndObject();
			}
		}

		json.WriteEndArray();
		json.WriteStartArray("looks");
		for (var row = 1; row <= lastRow + 1; row++)
		{
			for (var column = 1; column <= lastColumn; column++)
			{
				WriteCellLook(json, sheet.Cells[row, column]);
			}
		}

		json.WriteEndArray();
	}

	/// <summary>Writes a cell's look when it isn't Normal's: its font, cell style and wrap, and its number format when it has no value.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="cell">The cell.</param>
	private static void WriteCellLook(Utf8JsonWriter json, dynamic cell)
	{
		var font = cell.Font;
		bool bold = font.Bold;
		double size = font.Size;
		string name = font.Name;
		int underline = font.Underline;
		double color = font.Color;
		string style = cell.Style.Name;
		bool wrap = cell.WrapText;
		string? format = cell.Value2 is null && (string)cell.NumberFormat is var code && code != "General" ? code : null;
		if (!bold && size == NormalSize && name == NormalFont && underline == NoUnderline && color == 0 && style == "Normal" && !wrap && format is null)
		{
			return;
		}

		json.WriteStartObject();
		json.WriteString("cell", (string)Address(cell));
		if (bold)
		{
			json.WriteBoolean("bold", true);
		}

		if (size != NormalSize)
		{
			json.WriteNumber("size", size);
		}

		if (name != NormalFont)
		{
			json.WriteString("font", name);
		}

		if (underline != NoUnderline)
		{
			json.WriteNumber("underline", underline);
		}

		if (color != 0)
		{
			json.WriteNumber("color", color);
		}

		if (style != "Normal")
		{
			json.WriteString("style", style);
		}

		if (wrap)
		{
			json.WriteBoolean("wrap", true);
		}

		if (format is not null)
		{
			json.WriteString("numberFormat", format);
		}

		json.WriteEndObject();
	}

	/// <summary>A table's style's name, or <see langword="null"/> for none, which COM gives as a text of a NUL.</summary>
	/// <param name="list">The table.</param>
	/// <returns>The name.</returns>
	private static string? TableStyle(dynamic list)
	{
		object? style = list.TableStyle;

		return style switch
		{
			null => null,
			string text => NullIfEmpty(text.TrimEnd('\0')),
			_ => (string)((dynamic)style).Name,
		};
	}

	/// <summary>A range's address without <c>$</c> signs.</summary>
	/// <param name="range">The range.</param>
	/// <returns>The address, such as <c>A1:C3</c>.</returns>
	private static string Address(dynamic range) => ((string)range.Address).Replace("$", "", StringComparison.Ordinal);

	/// <summary>A COM text, <see langword="null"/> when it is missing or empty.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text, or <see langword="null"/>.</returns>
	private static string? NullIfEmpty(object? text) => text is string value && value.Length > 0 ? value : null;

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
