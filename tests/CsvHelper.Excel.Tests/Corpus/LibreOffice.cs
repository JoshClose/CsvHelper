using System.Diagnostics;
using CsvHelper.Excel.Corpus;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// LibreOffice as an oracle: every corpus workbook, and every workbook of the written set as this runtime writes it,
/// converted to CSV, a file per sheet, with its headless <c>--convert-to</c> in a throwaway profile. A workbook with a
/// sheet whose name can't be a file name (<c>Q&amp;A &lt;1&gt;</c>) is converted to <c>.xlsx</c> instead, since LibreOffice
/// stops exporting at that sheet. CI installs LibreOffice; elsewhere the tests are skipped without it.
/// </summary>
internal static class LibreOffice
{
	/// <summary>
	/// The CSV filter's options: comma, double quote, UTF-8 (76), from line 1, cells as shown, each sheet to a file of its
	/// own (-1), named <c>{workbook}-{sheet}.csv</c>.
	/// </summary>
	private const string CsvFilter = "csv:Text - txt - csv (StarCalc):44,34,76,1,,0,false,true,true,false,false,-1";

	/// <summary>LibreOffice's Excel 2007 filter, for workbooks it can't export as CSV.</summary>
	private const string XlsxFilter = "xlsx:Calc MS Excel 2007 XML";

	/// <summary>The folder of the corpus's converted sheets, made once for every test.</summary>
	private static readonly Lazy<string> Converted = new(() => Convert(CorpusFiles.Workbooks.Select(f => (Flat(f) + Path.GetExtension(f), File.ReadAllBytes(CorpusFiles.PathOf(f)))), CsvFilter));

	/// <summary>The written set's workbooks with every sheet's name a file name, converted to CSV.</summary>
	private static readonly Lazy<string> WrittenCsv = new(() => Convert(Written.Workbooks.Where(CanExport).Select(w => (w.Name, Written.Write(w))), CsvFilter));

	/// <summary>The written set's other workbooks, converted to <c>.xlsx</c>.</summary>
	private static readonly Lazy<string> WrittenXlsx = new(() => Convert(Written.Workbooks.Where(w => !CanExport(w)).Select(w => (w.Name, Written.Write(w))), XlsxFilter));

	/// <summary>LibreOffice's <c>soffice</c>: <c>CSVHELPER_SOFFICE</c>, or the default install's; <see langword="null"/> when there is none.</summary>
	public static string? Program { get; } = new[] { Environment.GetEnvironmentVariable("CSVHELPER_SOFFICE"), @"C:\Program Files\LibreOffice\program\soffice.exe" }
		.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

	/// <summary>The records of a corpus sheet as LibreOffice exports them: one per row, from row 1, each field the cell's shown text.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The records, or <see langword="null"/> when LibreOffice exported no such sheet.</returns>
	public static List<string[]>? Sheet(string file, string sheet) => Records(Path.Combine(Converted.Value, $"{Flat(file)}-{sheet}.csv"));

	/// <summary>The records of a written workbook's sheet as LibreOffice exports them, as <see cref="Sheet"/> gives them.</summary>
	/// <param name="workbook">The workbook of the written set.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The records, or <see langword="null"/> when LibreOffice exported no such sheet.</returns>
	public static List<string[]>? WrittenSheet(WrittenWorkbook workbook, string sheet) =>
		Records(Path.Combine(WrittenCsv.Value, $"{Path.GetFileNameWithoutExtension(workbook.Name)}-{sheet}.csv"));

	/// <summary>A written workbook that LibreOffice can't export as CSV, as it saves it in <c>.xlsx</c>.</summary>
	/// <param name="workbook">The workbook of the written set.</param>
	/// <returns>The file LibreOffice saved.</returns>
	public static string WrittenAsXlsx(WrittenWorkbook workbook) => Path.Combine(WrittenXlsx.Value, workbook.Name);

	/// <summary>Whether LibreOffice can export every sheet of a written workbook as a CSV file named after it.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>Whether every sheet's name can be a file name.</returns>
	public static bool CanExport(WrittenWorkbook workbook) => workbook.Sheets.SelectMany(s => s.Names).All(n => n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

	/// <summary>A CSV file's records, or <see langword="null"/> when there is no such file.</summary>
	/// <param name="path">The file.</param>
	/// <returns>The records.</returns>
	private static List<string[]>? Records(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}

		using var reader = CsvReader.FromFile(path, new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false });
		var records = new List<string[]>();
		while (reader.Read())
		{
			records.Add([.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)]);
		}

		return records;
	}

	/// <summary>A corpus file's name without folders, so that every workbook converts to a name of its own.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The name, such as <c>excel_basic</c>.</returns>
	private static string Flat(string file) => Path.ChangeExtension(file, null).Replace('/', '_');

	/// <summary>Converts workbooks in one run of LibreOffice.</summary>
	/// <param name="workbooks">Each workbook's file name and bytes.</param>
	/// <param name="filter">The filter to convert with.</param>
	/// <returns>The folder of the converted files.</returns>
	private static string Convert(IEnumerable<(string Name, byte[] Bytes)> workbooks, string filter)
	{
		var folder = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.LibreOffice." + Guid.NewGuid().ToString("N"));
		var inputs = Directory.CreateDirectory(Path.Combine(folder, "in")).FullName;
		var output = Directory.CreateDirectory(Path.Combine(folder, "out")).FullName;
		foreach (var (name, bytes) in workbooks)
		{
			File.WriteAllBytes(Path.Combine(inputs, name), bytes);
		}

		if (!Directory.EnumerateFiles(inputs).Any())
		{
			return output;
		}

		// Arguments as one string, which net48 needs; none of them holds a quote.
		var arguments = new[] { "--headless", "--norestore", "-env:UserInstallation=" + new Uri(Path.Combine(folder, "profile")).AbsoluteUri, "--convert-to", filter, "--outdir", output }
			.Concat(Directory.EnumerateFiles(inputs));
		var info = new ProcessStartInfo(Program!, string.Join(" ", arguments.Select(a => '"' + a + '"')))
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		using var process = Process.Start(info)!;
		var stdout = process.StandardOutput.ReadToEndAsync();
		var stderr = process.StandardError.ReadToEndAsync();
		if (!process.WaitForExit(300_000))
		{
			throw new TimeoutException("LibreOffice didn't convert the workbooks within 5 minutes.");
		}

		Assert.True(process.ExitCode == 0, $"LibreOffice exited with {process.ExitCode}: {stdout.Result}{stderr.Result}");

		return output;
	}
}

/// <summary>A theory that runs only where LibreOffice is installed (<see cref="LibreOffice.Program"/>).</summary>
public sealed class LibreOfficeTheoryAttribute : TheoryAttribute
{
	/// <summary>Creates the attribute, skipping the theory when LibreOffice is missing.</summary>
	public LibreOfficeTheoryAttribute()
	{
		if (LibreOffice.Program is null)
		{
			Skip = "LibreOffice is not installed (set CSVHELPER_SOFFICE to its soffice.exe).";
		}
	}
}
