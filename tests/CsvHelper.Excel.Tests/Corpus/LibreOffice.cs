using System.Diagnostics;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// LibreOffice as an oracle: every corpus workbook converted to CSV, a file per sheet, with its headless
/// <c>--convert-to</c> in a throwaway profile. Local only: CI doesn't install LibreOffice (Phase 4 adds it there).
/// </summary>
internal static class LibreOffice
{
	/// <summary>
	/// The CSV filter's options: comma, double quote, UTF-8 (76), from line 1, cells as shown, each sheet to a file of its
	/// own (-1), named <c>{workbook}-{sheet}.csv</c>.
	/// </summary>
	private const string CsvFilter = "csv:Text - txt - csv (StarCalc):44,34,76,1,,0,false,true,true,false,false,-1";

	/// <summary>The folder of the converted sheets, made once for every test.</summary>
	private static readonly Lazy<string> Converted = new(ConvertAll);

	/// <summary>LibreOffice's <c>soffice</c>: <c>CSVHELPER_SOFFICE</c>, or the default install's; <see langword="null"/> when there is none.</summary>
	public static string? Program { get; } = new[] { Environment.GetEnvironmentVariable("CSVHELPER_SOFFICE"), @"C:\Program Files\LibreOffice\program\soffice.exe" }
		.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

	/// <summary>The records of a sheet as LibreOffice exports them: one per row, from row 1, each field the cell's shown text.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The records, or <see langword="null"/> when LibreOffice exported no such sheet.</returns>
	public static List<string[]>? Sheet(string file, string sheet)
	{
		var path = Path.Combine(Converted.Value, $"{Flat(file)}-{sheet}.csv");
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

	/// <summary>Converts every corpus workbook in one run of LibreOffice.</summary>
	/// <returns>The folder of the CSV files.</returns>
	private static string ConvertAll()
	{
		var folder = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Tests.LibreOffice." + Guid.NewGuid().ToString("N"));
		var inputs = Directory.CreateDirectory(Path.Combine(folder, "in")).FullName;
		var output = Directory.CreateDirectory(Path.Combine(folder, "out")).FullName;
		foreach (var file in CorpusFiles.Workbooks)
		{
			File.Copy(CorpusFiles.PathOf(file), Path.Combine(inputs, Flat(file) + Path.GetExtension(file)));
		}

		// Arguments as one string, which net48 needs; none of them holds a quote.
		var arguments = new[] { "--headless", "--norestore", "-env:UserInstallation=" + new Uri(Path.Combine(folder, "profile")).AbsoluteUri, "--convert-to", CsvFilter, "--outdir", output }
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
			throw new TimeoutException("LibreOffice didn't convert the corpus within 5 minutes.");
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
