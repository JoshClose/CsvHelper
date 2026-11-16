using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// <para>
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
/// </para>
/// <para>
/// For Phase 7's templates (a workbook with a sheet the set fills), each sheet's answers also hold its visibility (a very
/// hidden lists sheet), its protection, what it allows and whether a wrong password and the right one unprotect it, each
/// cell's validation and the cells that aren't locked, from row 1 to the row below the used range and in row 1,048,576;
/// and the tool fills a copy of the workbook: types each text of the sheet's <see cref="WrittenSheet.Fill"/> as typing
/// enters it (<c>Range.Formula</c>, which no validation stops, as pasting isn't stopped), writes what each probe became
/// and whether its cell's validation takes it, and saves the copy as Excel saves it, into <c>excel-written/filled/</c>.
/// </para>
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

	/// <summary><c>xlSheetVisible</c>.</summary>
	private const int SheetVisible = -1;

	/// <summary><c>xlOpenXMLWorkbook</c>, <c>.xlsx</c>, as a filled copy is saved.</summary>
	private const int OpenXmlWorkbook = 51;

	/// <summary>The last row, whose cells' validations and locks the answers hold, to show their extent.</summary>
	private const int LastRow = 1_048_576;

	/// <summary>The password tried first, which no sheet of the set has.</summary>
	private const string WrongPassword = "wrong";

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
		var filled = Directory.CreateDirectory(Path.Combine(folder, "filled")).FullName;
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
						File.WriteAllBytes(target, Answer(excel, Path.Combine(temp, workbook.Name), workbook, bytes, dpi, filled));
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
	/// <param name="filled">The folder a filled copy is saved into.</param>
	/// <returns>The JSON's UTF-8.</returns>
	private static byte[] Answer(dynamic excel, string path, WrittenWorkbook workbook, byte[] bytes, int dpi, string filled)
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
					WriteSheet(json, opened, sheet, workbook);
				}

				json.WriteEndArray();
			}
			finally
			{
				opened.Close(false);
			}

			if (workbook.Sheets.Any(s => s.Fill.Count > 0))
			{
				Fill(json, excel, path, workbook, Path.Combine(filled, workbook.Name));
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
	/// <param name="written">The workbook of the set, whose templates' sheets get answers of their own.</param>
	private static void WriteSheet(Utf8JsonWriter json, dynamic workbook, dynamic sheet, WrittenWorkbook written)
	{
		json.WriteStartObject();
		string name = sheet.Name;
		json.WriteString("name", name);
		int visible = sheet.Visible;
		if (visible != SheetVisible)
		{
			json.WriteNumber("visible", visible);
		}

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
		WriteLook(json, workbook, sheet, used, top + rows - 1, left + columns - 1, visible == SheetVisible);
		var protects = (bool)sheet.ProtectContents;
		if (protects)
		{
			WriteProtection(json, sheet);
		}

		if (written.Sheets.Any(s => s.Fill.Count > 0))
		{
			WriteValidations(json, sheet, top + rows - 1, left + columns - 1);
		}


		// Excel's own AutoFit of each column, last, since it changes the widths; a hidden column's AutoFit would show it.
		json.WriteStartArray("autoFit");
		for (var column = 1; column < left + columns; column++)
		{
			var each = sheet.Columns[column];
			if ((bool)each.Hidden)
			{
				json.WriteNullValue();
			}
			else if (!AutoFits(each))
			{
				// A protected sheet that doesn't allow resizing columns, as the lists sheet is, refuses AutoFit.
				json.WriteNullValue();
			}
			else
			{
				json.WriteNumberValue((double)each.ColumnWidth);
			}
		}

		json.WriteEndArray();
		if (protects)
		{
			WriteUnprotect(json, sheet, written.Sheets.FirstOrDefault(s => s.Names.Contains(name))?.Look.Options?.Password);
		}

		json.WriteEndObject();
	}

	/// <summary>
	/// Writes a protected sheet's protection (<c>protection</c>): what it allows users, and the cells they can select.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteProtection(Utf8JsonWriter json, dynamic sheet)
	{
		var protection = sheet.Protection;
		json.WriteStartObject("protection");
		json.WriteBoolean("objects", (bool)sheet.ProtectDrawingObjects);
		json.WriteBoolean("scenarios", (bool)sheet.ProtectScenarios);
		json.WriteNumber("selection", (int)sheet.EnableSelection);
		json.WriteBoolean("formatCells", (bool)protection.AllowFormattingCells);
		json.WriteBoolean("formatColumns", (bool)protection.AllowFormattingColumns);
		json.WriteBoolean("formatRows", (bool)protection.AllowFormattingRows);
		json.WriteBoolean("insertColumns", (bool)protection.AllowInsertingColumns);
		json.WriteBoolean("insertRows", (bool)protection.AllowInsertingRows);
		json.WriteBoolean("insertLinks", (bool)protection.AllowInsertingHyperlinks);
		json.WriteBoolean("deleteColumns", (bool)protection.AllowDeletingColumns);
		json.WriteBoolean("deleteRows", (bool)protection.AllowDeletingRows);
		json.WriteBoolean("sort", (bool)protection.AllowSorting);
		json.WriteBoolean("filter", (bool)protection.AllowFiltering);
		json.WriteBoolean("pivotTables", (bool)protection.AllowUsingPivotTables);
		json.WriteEndObject();
	}

	/// <summary>
	/// Writes whether a protected sheet unprotects (<c>unprotect</c>), last, since it does: with a wrong password, and then,
	/// when that is refused, with the set's password. A sheet with a password is never asked to unprotect without one, which
	/// opens Excel's dialog, and an invisible Excel never closes it.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="password">The set's password of the sheet, or <see langword="null"/> for none.</param>
	private static void WriteUnprotect(Utf8JsonWriter json, dynamic sheet, string? password)
	{
		json.WriteStartObject("unprotect");
		json.WriteBoolean("wrong", Unprotects(sheet, WrongPassword));
		if ((bool)sheet.ProtectContents && password is not null)
		{
			json.WriteBoolean("password", Unprotects(sheet, password));
		}

		json.WriteEndObject();
	}

	/// <summary>Whether a password unprotects a sheet.</summary>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="password">The password.</param>
	/// <returns>Whether the sheet isn't protected after.</returns>
	private static bool Unprotects(dynamic sheet, string password)
	{
		try
		{
			sheet.Unprotect(password);
		}
		catch (COMException)
		{
			// Excel refuses a wrong password.
		}

		return !(bool)sheet.ProtectContents;
	}

	/// <summary>
	/// Writes each cell's validation (<c>validations</c>) and the cells that aren't locked (<c>unlocked</c>), from row 1 to
	/// the row below the used range and in row 1,048,576, from column A to the one after the used range's last.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="lastRow">The used range's last row.</param>
	/// <param name="lastColumn">The used range's last column.</param>
	private static void WriteValidations(Utf8JsonWriter json, dynamic sheet, int lastRow, int lastColumn)
	{
		var rows = Enumerable.Range(1, lastRow + 1).Append(LastRow).ToList();
		json.WriteStartArray("validations");
		foreach (var row in rows)
		{
			for (var column = 1; column <= lastColumn + 1; column++)
			{
				WriteValidation(json, sheet.Cells[row, column]);
			}
		}

		json.WriteEndArray();
		json.WriteStartArray("unlocked");
		foreach (var row in rows)
		{
			for (var column = 1; column <= lastColumn + 1; column++)
			{
				var cell = sheet.Cells[row, column];
				if (!(bool)cell.Locked)
				{
					json.WriteStringValue((string)Address(cell));
				}
			}
		}

		json.WriteEndArray();
	}

	/// <summary>
	/// Writes a cell's validation, if it has one: its type (<c>xlDVType</c>), operator, formulas, alert style, whether it
	/// ignores blanks and shows a dropdown, its prompt and its title, and its error message and title when it has them.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="cell">The cell.</param>
	private static void WriteValidation(Utf8JsonWriter json, dynamic cell)
	{
		var validation = cell.Validation;
		int type;
		try
		{
			type = validation.Type;
		}
		catch (COMException)
		{
			// No validation.
			return;
		}

		json.WriteStartObject();
		json.WriteString("cell", (string)Address(cell));
		json.WriteNumber("type", type);
		if (Attempt(() => (object?)validation.Operator) is int op)
		{
			json.WriteNumber("operator", op);
		}

		json.WriteString("formula1", Attempt(() => (object?)validation.Formula1) as string);
		json.WriteString("formula2", Attempt(() => (object?)validation.Formula2) as string);
		json.WriteNumber("alertStyle", (int)validation.AlertStyle);
		json.WriteBoolean("ignoreBlank", (bool)validation.IgnoreBlank);
		json.WriteBoolean("inCellDropdown", (bool)validation.InCellDropdown);
		json.WriteBoolean("showInput", (bool)validation.ShowInput);
		json.WriteBoolean("showError", (bool)validation.ShowError);
		json.WriteString("inputTitle", NullIfEmpty((object?)validation.InputTitle));
		json.WriteString("inputMessage", NullIfEmpty((object?)validation.InputMessage));
		json.WriteString("errorTitle", NullIfEmpty((object?)validation.ErrorTitle));
		json.WriteString("errorMessage", NullIfEmpty((object?)validation.ErrorMessage));
		json.WriteEndObject();
	}

	/// <summary>
	/// Fills a copy of a workbook: types each text of its sheets' <see cref="WrittenSheet.Fill"/> as typing enters it, and
	/// writes each probe's answers (<c>filled</c>): what Excel made of it, and whether its cell's validation takes it; and
	/// the cells whose good text the validation refused (<c>goodRefused</c>), which there should be none of. Saves the copy
	/// as Excel saves it.
	/// </summary>
	/// <param name="json">The JSON.</param>
	/// <param name="excel">Excel.</param>
	/// <param name="path">The workbook's file, which is opened again to be edited.</param>
	/// <param name="workbook">The workbook of the set.</param>
	/// <param name="target">The file the copy is saved as.</param>
	private static void Fill(Utf8JsonWriter json, dynamic excel, string path, WrittenWorkbook workbook, string target)
	{
		var opened = excel.Workbooks.Open(Filename: path, UpdateLinks: 0, ReadOnly: false, AddToMru: false);
		try
		{
			json.WriteStartArray("filled");
			var refused = new List<string>();
			foreach (var sheet in workbook.Sheets.Where(s => s.Fill.Count > 0))
			{
				var worksheet = opened.Worksheets[sheet.Name];
				foreach (var entry in sheet.Fill)
				{
					var cell = worksheet.Range[entry.Cell];
					cell.Formula = entry.Text;
					var valid = (bool)cell.Validation.Value;
					if (!entry.Probe)
					{
						if (!valid)
						{
							refused.Add($"{sheet.Name}!{entry.Cell}");
						}

						continue;
					}

					json.WriteStartObject();
					json.WriteString("sheet", sheet.Name);
					json.WriteString("cell", entry.Cell);
					json.WriteString("entered", entry.Text);
					object? value2 = cell.Value2;
					switch (value2)
					{
						case double number:
							json.WriteNumber("value2", number);
							json.WriteString("type", "number");

							break;
						case bool flag:
							json.WriteBoolean("value2", flag);
							json.WriteString("type", "boolean");

							break;
						default:
							json.WriteString("value2", Convert.ToString(value2, CultureInfo.InvariantCulture));
							json.WriteString("type", value2 is string ? "text" : value2?.GetType().Name ?? "empty");

							break;
					}

					json.WriteString("text", (string)cell.Text);
					json.WriteString("numberFormat", (string)cell.NumberFormat);
					json.WriteBoolean("valid", valid);
					json.WriteEndObject();
				}
			}

			json.WriteEndArray();
			json.WriteStartArray("goodRefused");
			foreach (var cell in refused)
			{
				json.WriteStringValue(cell);
			}

			json.WriteEndArray();

			// As the corpus's own Excel files are saved: without the Office account's name, and in a public folder, since Excel
			// records the folder it saves in; copied into the corpus once closed.
			ExcelProducer.SaveAs(opened, target, OpenXmlWorkbook, null);
		}
		finally
		{
			opened.Close(false);
		}

		ExcelProducer.CopyPending();
	}

	/// <summary>Fits a column with Excel's own AutoFit.</summary>
	/// <param name="column">The column.</param>
	/// <returns>Whether Excel fitted it: a protected sheet that doesn't allow resizing columns refuses.</returns>
	private static bool AutoFits(dynamic column)
	{
		try
		{
			column.AutoFit();

			return true;
		}
		catch (COMException)
		{
			return false;
		}
	}

	/// <summary>A COM property's value, or <see langword="null"/> when Excel refuses to give it, as for a type without one.</summary>
	/// <param name="read">Reads the property.</param>
	/// <returns>The value.</returns>
	private static object? Attempt(Func<object?> read)
	{
		try
		{
			return read();
		}
		catch (COMException)
		{
			return null;
		}
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
	/// <param name="visible">Whether the sheet is visible: a hidden one can't be activated, and has no panes to read.</param>
	private static void WriteLook(Utf8JsonWriter json, dynamic workbook, dynamic sheet, dynamic used, int lastRow, int lastColumn, bool visible)
	{
		// The window shows the active sheet's panes. An invisible Excel lays out a workbook opened after the first in a
		// window with no room, and then reports a split clipped to it (SplitRow 1 for 2 rows frozen, or 0), so the window is
		// given a size first, and must then have room.
		if (!visible)
		{
			json.WriteNull("freeze");
		}
		else
		{
			WriteFreeze(json, workbook, sheet);
		}

		WriteRest(json, sheet, used, lastRow, lastColumn);
	}

	/// <summary>Writes a visible worksheet's frozen panes, through its workbook's window.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="workbook">The worksheet's workbook.</param>
	/// <param name="sheet">The worksheet.</param>
	private static void WriteFreeze(Utf8JsonWriter json, dynamic workbook, dynamic sheet)
	{
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
	}

	/// <summary>Writes the rest of a worksheet's look: its filter, tables, merged ranges, links, hidden columns, heights and cells' looks.</summary>
	/// <param name="json">The JSON.</param>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="used">Its used range.</param>
	/// <param name="lastRow">The used range's last row.</param>
	/// <param name="lastColumn">The used range's last column.</param>
	private static void WriteRest(Utf8JsonWriter json, dynamic sheet, dynamic used, int lastRow, int lastColumn)
	{
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
