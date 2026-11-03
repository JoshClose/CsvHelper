using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes the corpus's Excel files by driving Excel through COM (late bound, with <c>dynamic</c>): the basic workbook in
/// each format, the formats CsvHelper.Excel doesn't read, and NCsvPerf's data saved from Excel.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelProducer
{
	/// <summary><c>xlOpenXMLWorkbook</c>: <c>.xlsx</c>.</summary>
	private const int Xlsx = 51;

	/// <summary><c>xlOpenXMLStrictWorkbook</c>: Strict Open XML <c>.xlsx</c>.</summary>
	private const int StrictXlsx = 61;

	/// <summary><c>xlOpenXMLWorkbookMacroEnabled</c>: <c>.xlsm</c>.</summary>
	private const int Xlsm = 52;

	/// <summary><c>xlOpenXMLTemplate</c>: <c>.xltx</c>.</summary>
	private const int Xltx = 54;

	/// <summary><c>xlExcel12</c>: the binary <c>.xlsb</c>.</summary>
	private const int Xlsb = 50;

	/// <summary><c>xlExcel8</c>: the Excel 97-2003 <c>.xls</c>.</summary>
	private const int Xls = 56;

	/// <summary><c>xlColumnClustered</c>.</summary>
	private const int ColumnChart = 51;

	/// <summary><c>xlSheetHidden</c>.</summary>
	private const int SheetHidden = 0;

	/// <summary><c>xlSheetVeryHidden</c>.</summary>
	private const int SheetVeryHidden = 2;

	/// <summary><c>xlDelimited</c>.</summary>
	private const int Delimited = 1;

	/// <summary><c>xlTextFormat</c>, for a column that <c>OpenText</c> reads as text.</summary>
	private const int TextColumn = 2;

	/// <summary>The code page of UTF-8, for <c>OpenText</c>'s origin.</summary>
	private const int Utf8 = 65001;

	/// <summary>The password of the encrypted workbook.</summary>
	public const string Password = "secret";

	/// <summary>The files saved in the public folder, and where each goes in the corpus.</summary>
	private static readonly List<(string Staged, string Path)> Pending = [];

	/// <summary>Writes <c>excel/</c>: the basic workbook as each format, and the formats that aren't read.</summary>
	/// <param name="files">The corpus folder.</param>
	public static void Write(string files)
	{
		var folder = Directory.CreateDirectory(Path.Combine(files, "excel")).FullName;
		Run(excel =>
		{
			Save(excel, Path.Combine(folder, "basic.xlsx"), Xlsx);
			Save(excel, Path.Combine(folder, "date1904.xlsx"), Xlsx, date1904: true);
			Save(excel, Path.Combine(folder, "strict.xlsx"), StrictXlsx);
			Save(excel, Path.Combine(folder, "macro.xlsm"), Xlsm);
			Save(excel, Path.Combine(folder, "template.xltx"), Xltx);
			Save(excel, Path.Combine(folder, "binary.xlsb"), Xlsb);
			Save(excel, Path.Combine(folder, "legacy.xls"), Xls);
			Save(excel, Path.Combine(folder, "encrypted.xlsx"), Xlsx, password: Password);
		});
	}

	/// <summary>
	/// Writes <c>excel/packageassets-1695.xlsx</c>: NCsvPerf's PackageAssets CSV opened in Excel with every column as text,
	/// as the benchmark's data is, and saved as a workbook.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="csv">The path of <c>PackageAssets.csv</c>.</param>
	public static void WritePackageAssets(string files, string csv)
	{
		var folder = Directory.CreateDirectory(Path.Combine(files, "excel")).FullName;
		var columns = File.ReadLines(csv).Max(line => line.Count(c => c == ',') + 1);

		// OpenText ignores its column formats for a .csv file, so Excel reads a .txt copy.
		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		try
		{
			var text = Path.Combine(temp, "PackageAssets.txt");
			File.Copy(csv, text);
			Run(excel =>
			{
				var fieldInfo = Enumerable.Range(1, columns).Select(i => (object)new object[] { i, TextColumn }).ToArray();
				excel.Workbooks.OpenText(Filename: text, Origin: Utf8, StartRow: 1, DataType: Delimited, Comma: true, FieldInfo: fieldInfo);
				var workbook = excel.ActiveWorkbook;
				try
				{
					SaveAs(workbook, Path.Combine(folder, "packageassets-1695.xlsx"), Xlsx, null);
				}
				finally
				{
					workbook.Close(false);
				}

				CopyPending();
			});
		}
		finally
		{
			Directory.Delete(temp, true);
		}
	}

	/// <summary>
	/// Starts Excel, runs <paramref name="work"/>, and quits Excel whatever happens. Excel stays running while any of its
	/// objects is referenced, so every object the work used is released (collected) before Excel quits.
	/// </summary>
	/// <param name="work">What to do with Excel.</param>
	private static void Run(Action<dynamic> work)
	{
		var type = Type.GetTypeFromProgID("Excel.Application") ?? throw new InvalidOperationException("Excel is not installed.");
		dynamic excel = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Excel didn't start.");
		try
		{
			RunWork(excel, work);
		}
		finally
		{
			Collect();
			excel.Quit();
			Marshal.FinalReleaseComObject(excel);
			Collect();
		}
	}

	/// <summary>Runs the work in a frame of its own, so that its objects are unreferenced once it returns.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="work">What to do with Excel.</param>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void RunWork(dynamic excel, Action<dynamic> work)
	{
		excel.Visible = false;
		excel.DisplayAlerts = false;
		excel.SheetsInNewWorkbook = 1;
		work(excel);
	}

	/// <summary>Collects twice, running the finalizers between, so that every unreferenced COM object is released.</summary>
	private static void Collect()
	{
		for (var i = 0; i < 2; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
	}

	/// <summary>Builds the basic workbook in a new workbook and saves it.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="path">Where to save it.</param>
	/// <param name="format">The file format.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="password">The password to open it, or <see langword="null"/>.</param>
	private static void Save(dynamic excel, string path, int format, bool date1904 = false, string? password = null)
	{
		var workbook = excel.Workbooks.Add();
		try
		{
			if (date1904)
			{
				workbook.Date1904 = true;
			}

			Build(workbook);
			SaveAs(workbook, path, format, password);
		}
		finally
		{
			workbook.Close(false);
		}

		CopyPending();
	}

	/// <summary>
	/// Saves a workbook without personal information. Excel removes the author and the last author from its properties,
	/// and the workbook is saved in a public folder, since Excel records the folder it saves in (<c>x15ac:absPath</c>);
	/// <see cref="CopyPending"/> copies it to the corpus once the workbook is closed, which keeps Excel's bytes.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="path">Where it goes in the corpus.</param>
	/// <param name="format">The file format.</param>
	/// <param name="password">The password to open it, or <see langword="null"/>.</param>
	private static void SaveAs(dynamic workbook, string path, int format, string? password)
	{
		var publicFolder = Environment.GetEnvironmentVariable("PUBLIC") ?? throw new InvalidOperationException("%PUBLIC% is not set.");
		var staging = Directory.CreateDirectory(Path.Combine(publicFolder, "CsvHelper.Excel.Corpus")).FullName;
		var staged = Path.Combine(staging, Path.GetFileName(path));
		File.Delete(staged);
		workbook.RemovePersonalInformation = true;
		if (password is null)
		{
			workbook.SaveAs(Filename: staged, FileFormat: format);
		}
		else
		{
			workbook.SaveAs(Filename: staged, FileFormat: format, Password: password);
		}

		Pending.Add((staged, path));
	}

	/// <summary>Copies the files saved in the public folder to the corpus, once Excel has closed them.</summary>
	private static void CopyPending()
	{
		foreach (var (staged, path) in Pending)
		{
			File.Copy(staged, path, true);
			File.Delete(staged);
			Console.WriteLine($"  {path}");
		}

		Pending.Clear();
	}

	/// <summary>Builds the basic workbook's sheets, in tab order, in a new workbook.</summary>
	/// <param name="workbook">The workbook, with one sheet.</param>
	private static void Build(dynamic workbook)
	{
		var worksheets = Basic.Sheets.Where(s => s.Kind == "worksheet").ToList();
		var previous = workbook.Worksheets[1];
		previous.Name = worksheets[0].Name;
		foreach (var spec in worksheets.Skip(1))
		{
			var added = workbook.Worksheets.Add(After: previous);
			added.Name = spec.Name;
			previous = added;
		}

		foreach (var spec in worksheets)
		{
			Fill(workbook.Worksheets[spec.Name], spec);
		}

		// The chartsheet, in its place in tab order.
		var chartIndex = Basic.Sheets.ToList().FindIndex(s => s.Kind == "chartsheet");
		var chart = workbook.Charts.Add(After: workbook.Sheets[chartIndex]);
		chart.ChartType = ColumnChart;
		chart.SetSourceData(workbook.Worksheets["Q1 Orders"].Range["A1:B4"]);
		chart.Name = Basic.Sheets[chartIndex].Name;

		foreach (var spec in worksheets.Where(s => s.State != "visible"))
		{
			workbook.Worksheets[spec.Name].Visible = spec.State == "hidden" ? SheetHidden : SheetVeryHidden;
		}

		workbook.Worksheets[worksheets[0].Name].Activate();
	}

	/// <summary>Writes a sheet's cells.</summary>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="spec">The sheet's content.</param>
	private static void Fill(dynamic sheet, SheetSpec spec)
	{
		foreach (var row in spec.Rows)
		{
			foreach (var cell in row.Cells)
			{
				var range = sheet.Range[cell.Reference(row.Row)];
				switch (cell.Type)
				{
					case CellType.Text:
						range.Value2 = cell.Text;

						break;
					case CellType.Number:
						range.Value2 = cell.Number!.Value;

						break;
					case CellType.Bool:
						range.Value2 = cell.Bool!.Value;

						break;
					case CellType.Formula:
						range.Formula = "=" + cell.Formula;

						break;
					case CellType.Date:
						// A time alone as its fraction of a day, which is the same in both date systems; a date as a date, so
						// that Excel gives it the serial of the workbook's date system.
						if (cell.Date is { } date)
						{
							range.Value = date;
						}
						else
						{
							range.Value2 = cell.Number!.Value;
						}

						range.NumberFormat = cell.Format;

						break;
					case CellType.Rich:
						range.Value2 = string.Concat(cell.Runs!.Select(r => r.Text));
						var start = 1;
						foreach (var run in cell.Runs!)
						{
							range.Characters[start, run.Text.Length].Font.Bold = run.Bold;
							start += run.Text.Length;
						}

						break;
					case CellType.Phonetic:
						// Excel without the Japanese editing language ignores this, as it does SetPhonetic and Phonetics.Add.
						range.Value2 = cell.Text;
						range.Characters[1, cell.Text!.Length].PhoneticCharacters = cell.Phonetic;

						break;
				}
			}
		}

		// A formula in consecutive rows of a column is written once over them, as filling it down does: a shared formula.
		var formulas = spec.Rows.SelectMany(r => r.Cells.Where(c => c.Type == CellType.Formula).Select(c => (Row: r.Row, Cell: c))).ToList();
		foreach (var run in formulas.GroupBy(f => (f.Cell.Column, f.Cell.Formula)).Where(g => g.Count() > 1))
		{
			var rows = run.Select(f => f.Row).ToList();
			if (rows.Max() - rows.Min() == rows.Count - 1)
			{
				sheet.Range[$"{run.Key.Column}{rows.Min()}:{run.Key.Column}{rows.Max()}"].Formula = "=" + run.Key.Formula;
			}
		}
	}
}
