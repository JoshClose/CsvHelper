using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes the corpus's files with .NET libraries, each from the basic workbook, as far as the library can: ClosedXML,
/// the Open XML SDK, EPPlus 4.5.3.3 (the last LGPL version), MiniExcel, SpreadCheetah and LargeXlsx.
/// </summary>
internal static class DotNetProducers
{
	/// <summary>The author the producers that record one get, in place of the user's name.</summary>
	internal const string Author = "CsvHelper.Excel.Corpus";

	/// <summary>The cached results of the basic workbook's formulas, as Excel computes them, for producers that write them as given.</summary>
	private static readonly Dictionary<string, object> FormulaResults = new()
	{
		["NA()"] = "#N/A",
		["1/0"] = "#DIV/0!",
		["B3*2"] = 84.0,
		["A2&\"!\""] = "text!",
		["B2+1"] = "#VALUE!",
	};

	/// <summary>Writes every .NET producer's files.</summary>
	/// <param name="files">The corpus folder.</param>
	public static void Write(string files)
	{
		ClosedXmlProducer.Write(Folder(files, "closedxml"));
		OpenXmlSdkProducer.Write(Folder(files, "openxmlsdk"));
		EpplusProducer.Write(Folder(files, "epplus"));
		MiniExcelProducer.Write(Folder(files, "miniexcel"));
		SpreadCheetahProducer.Write(Folder(files, "spreadcheetah"));
		LargeXlsxProducer.Write(Folder(files, "largexlsx"));
	}

	/// <summary>A producer's folder in the corpus, created if need be.</summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="name">The producer's folder name.</param>
	/// <returns>The folder.</returns>
	private static string Folder(string files, string name) => Directory.CreateDirectory(Path.Combine(files, name)).FullName;

	/// <summary>The worksheets of the basic workbook, in tab order.</summary>
	internal static IEnumerable<SheetSpec> Worksheets => Basic.Sheets.Where(s => s.Kind == "worksheet");

	/// <summary>A formula's cached result: a row number times 10 for <c>ROW()*10</c>, else Excel's.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="row">The formula's row.</param>
	/// <returns>The result: a number or a string (text or an error).</returns>
	internal static object Result(string formula, int row) => formula == "ROW()*10" ? row * 10.0 : FormulaResults[formula];

	/// <summary>Whether a cached result is an error value.</summary>
	/// <param name="result">The result.</param>
	/// <returns>Whether it is <c>#N/A</c>, <c>#DIV/0!</c> or the like.</returns>
	internal static bool IsError(object result) => result is string text && text.StartsWith('#');

	/// <summary>Writes a file, replacing it, and reports it.</summary>
	/// <param name="path">The file's path.</param>
	/// <param name="write">Writes the file at the path.</param>
	internal static void Save(string path, Action<string> write)
	{
		File.Delete(path);
		write(path);
		Console.WriteLine($"  {path}");
	}
}
