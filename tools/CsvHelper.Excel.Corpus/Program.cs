using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes the corpus of <c>tests/CsvHelper.Excel.Tests/Files</c>. Each command writes one producer's files; <c>all</c>
/// writes every one, Excel first, since LibreOffice converts Excel's basic workbook.
/// </summary>
internal static class Program
{
	/// <summary>The usage text.</summary>
	private const string Usage =
		"""
		Usage: CsvHelper.Excel.Corpus <command> [--files <folder>] [--csv <PackageAssets.csv>] [--java <java.exe or JDK>]

		Commands:
		  excel          Excel through COM: excel/ (the basic workbook in each format, and .xlsb, encrypted and .xls; the
		                 typed workbook as .xlsx, in the 1904 date system, and as Strict Open XML)
		  packageassets  Excel: excel/packageassets-1695.xlsx from NCsvPerf's PackageAssets.csv, every column as text
		  libreoffice    LibreOffice: libreoffice/, converted from excel/basic.xlsx and excel/typed.xlsx
		  python         openpyxl, XlsxWriter and pandas, in a venv outside the repository: openpyxl/, xlsxwriter/, pandas/
		  poi            Apache POI's XSSF and SXSSF, run by a JDK outside the repository: poi/
		  dotnet         ClosedXML, the Open XML SDK, EPPlus, MiniExcel, SpreadCheetah and LargeXlsx
		  downloads      Other projects' test files that downloads.txt pins: google/, excel-mac/
		  formats        Excel through COM: excel-formats.json, which number formats Excel takes for dates
		  oracle         Excel through COM: excel-oracle/, what every corpus workbook's cells hold as Excel reads them
		  spec           The basic workbook's content as JSON, to standard output
		  check          Only the check, which every command ends with, that no file holds personal information
		  all            Every command, in order

		--files defaults to the repository's tests/CsvHelper.Excel.Tests/Files, and --csv to its copy of NCsvPerf's file in
		bench/CsvHelper.Benchmarks/Data.
		--java defaults to the newest jdk-* folder in %LOCALAPPDATA%\CsvHelper.Excel.Corpus, where the venv and POI's jars
		are kept too.
		""";

	/// <summary>Runs a command.</summary>
	/// <param name="args">The command and its options.</param>
	/// <returns>0 when it succeeds.</returns>
	private static int Main(string[] args)
	{
		if (!OperatingSystem.IsWindows())
		{
			Console.Error.WriteLine("The corpus is written on Windows: Excel runs through COM.");

			return 1;
		}

		var command = args.FirstOrDefault() ?? "";
		var root = FindRepository();
		var files = Option(args, "--files") ?? Path.Combine(root, "tests", "CsvHelper.Excel.Tests", "Files");
		var csv = Option(args, "--csv") ?? Path.Combine(root, "bench", "CsvHelper.Benchmarks", "Data", "PackageAssets.csv");
		var python = Path.Combine(root, "tools", "CsvHelper.Excel.Corpus", "python");
		var java = Path.Combine(root, "tools", "CsvHelper.Excel.Corpus", "java");
		var jdk = Option(args, "--java");
		var downloads = Path.Combine(root, "tools", "CsvHelper.Excel.Corpus", "downloads.txt");
		switch (command)
		{
			case "excel":
				ExcelProducer.Write(files);

				break;
			case "packageassets":
				ExcelProducer.WritePackageAssets(files, csv);

				break;
			case "libreoffice":
				LibreOffice(files);

				break;
			case "python":
				Python(files, python);

				break;
			case "poi":
				PoiProducer.Write(files, java, jdk);

				break;
			case "dotnet":
				DotNetProducers.Write(files);

				break;
			case "downloads":
				Downloads.Write(files, downloads);

				break;
			case "formats":
				ExcelFormats.Write(files);

				break;
			case "oracle":
				ExcelOracle.Write(files);

				break;
			case "spec":
				Console.WriteLine(Basic.ToJson());

				break;
			case "check":
				break;
			case "all":
				ExcelProducer.Write(files);
				ExcelProducer.WritePackageAssets(files, csv);
				LibreOffice(files);
				Python(files, python);
				PoiProducer.Write(files, java, jdk);
				DotNetProducers.Write(files);
				Downloads.Write(files, downloads);
				ExcelFormats.Write(files);
				ExcelOracle.Write(files);

				break;
			default:
				Console.Error.WriteLine(Usage);

				return 1;
		}

		// Every file the corpus holds, after every command: they are committed to a public repository.
		if (command != "spec")
		{
			Privacy.Check(files);
			Console.WriteLine("No personal information in the corpus.");
		}

		return 0;
	}

	/// <summary>
	/// Converts Excel's basic workbook with LibreOffice's two Excel filters, and its typed workbook with the first, in a
	/// throwaway profile.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	[SupportedOSPlatform("windows")]
	private static void LibreOffice(string files)
	{
		var soffice = AppPath("soffice.exe") ?? @"C:\Program Files\LibreOffice\program\soffice.exe";
		var folder = Directory.CreateDirectory(Path.Combine(files, "libreoffice")).FullName;
		var conversions = new[]
		{
			("basic.xlsx", "Calc MS Excel 2007 XML", "basic.xlsx"),
			("basic.xlsx", "Calc Office Open XML", "basic-ooxml.xlsx"),
			("typed.xlsx", "Calc MS Excel 2007 XML", "typed.xlsx"),
		};
		foreach (var (from, filter, name) in conversions)
		{
			var source = Path.Combine(files, "excel", from);
			var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
			try
			{
				var profile = new Uri(Path.Combine(temp, "profile")).AbsoluteUri;
				var output = Path.Combine(temp, "out");
				Run(soffice, "--headless", "--norestore", $"-env:UserInstallation={profile}", "--convert-to", $"xlsx:{filter}", "--outdir", output, source);
				var target = Path.Combine(folder, name);
				File.Copy(Path.Combine(output, from), target, true);
				Console.WriteLine($"  {target}");
			}
			finally
			{
				Directory.Delete(temp, true);
			}
		}
	}

	/// <summary>
	/// Writes the Python producers' files: a venv outside the repository with the exact versions of
	/// <c>requirements.txt</c>, and <c>corpus.py</c> run in isolated mode over the basic workbook's JSON.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="scripts">The folder of <c>requirements.txt</c> and <c>corpus.py</c>.</param>
	[SupportedOSPlatform("windows")]
	private static void Python(string files, string scripts)
	{
		var venv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CsvHelper.Excel.Corpus", "venv");
		var python = Path.Combine(venv, "Scripts", "python.exe");
		if (!File.Exists(python))
		{
			Run(FindPython(), "-m", "venv", venv);
		}

		Run(python, "-I", "-m", "pip", "install", "--disable-pip-version-check", "--quiet", "-r", Path.Combine(scripts, "requirements.txt"));
		WithSpecs((basic, typed) => Run(python, "-I", Path.Combine(scripts, "corpus.py"), basic, typed, files));
	}

	/// <summary>Runs work with the basic and typed workbooks' JSON in temporary files, for the producers outside .NET.</summary>
	/// <param name="work">Takes the basic workbook's file and the typed workbook's.</param>
	internal static void WithSpecs(Action<string, string> work)
	{
		var basic = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N") + ".json");
		var typed = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(basic, Basic.ToJson());
			File.WriteAllText(typed, Typed.ToJson());
			work(basic, typed);
		}
		finally
		{
			File.Delete(basic);
			File.Delete(typed);
		}
	}

	/// <summary>Finds Python 3.14 from its PEP 514 registry entry, since shells can resolve <c>python</c> to the Store's alias.</summary>
	/// <returns>The interpreter's path.</returns>
	[SupportedOSPlatform("windows")]
	private static string FindPython()
	{
		foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
		{
			using var key = hive.OpenSubKey(@"Software\Python\PythonCore\3.14\InstallPath");
			if (key?.GetValue("ExecutablePath") is string path && File.Exists(path))
			{
				return path;
			}
		}

		throw new InvalidOperationException("Python 3.14 is not registered (HKCU or HKLM\\Software\\Python\\PythonCore\\3.14).");
	}

	/// <summary>The path an <c>App Paths</c> registry entry gives an executable.</summary>
	/// <param name="name">The executable's name.</param>
	/// <returns>The path, or <see langword="null"/>.</returns>
	[SupportedOSPlatform("windows")]
	private static string? AppPath(string name)
	{
		foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
		{
			using var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + name);
			if (key?.GetValue(null) is string path && File.Exists(path))
			{
				return path;
			}
		}

		return null;
	}

	/// <summary>Runs a program and waits for it, failing with its output when it fails.</summary>
	/// <param name="file">The program.</param>
	/// <param name="args">Its arguments.</param>
	internal static void Run(string file, params string[] args)
	{
		var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
		foreach (var arg in args)
		{
			info.ArgumentList.Add(arg);
		}

		using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} didn't start.");
		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();
		process.WaitForExit();
		Console.Write(output.Result);
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"{Path.GetFileName(file)} exited with {process.ExitCode}: {error.Result}");
		}
	}

	/// <summary>The value of an option, such as <c>--files folder</c>.</summary>
	/// <param name="args">The arguments.</param>
	/// <param name="name">The option.</param>
	/// <returns>The value, or <see langword="null"/>.</returns>
	private static string? Option(string[] args, string name)
	{
		var index = Array.IndexOf(args, name);

		return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
	}

	/// <summary>The repository's root: the folder of <c>CsvHelper.sln</c>, above the tool.</summary>
	/// <returns>The root.</returns>
	private static string FindRepository()
	{
		for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
		{
			if (File.Exists(Path.Combine(folder.FullName, "CsvHelper.sln")))
			{
				return folder.FullName;
			}
		}

		throw new InvalidOperationException("The tool must run from inside the repository.");
	}
}
