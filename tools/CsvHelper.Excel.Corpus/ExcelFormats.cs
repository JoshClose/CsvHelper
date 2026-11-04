using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Writes <c>excel-formats.json</c>: Excel's answers, through COM, to which number formats make a number a date. For a
/// number formatted with each code of <see cref="Codes"/>, and for a cell of each built-in number format id of a workbook
/// this writes, Excel says whether <c>Range.Value</c> is a date (it returns a date for a date or time format, a number
/// otherwise), and what it shows. CsvHelper.Excel's tests classify the same formats and compare.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExcelFormats
{
	/// <summary>The serial of 2024-01-15 12:00 in the 1900 date system, the date each format shows.</summary>
	private const double DateSerial = 45306.5;

	/// <summary>A day and a half, which an elapsed-time format shows as 36 hours.</summary>
	private const double DurationSerial = 1.5;

	/// <summary>The first id of a custom number format, by Excel's convention.</summary>
	private const int FirstCustomId = 164;

	/// <summary>A custom number format's id for the cases of <see cref="Cases"/>.</summary>
	private const int CustomId = 200;

	/// <summary>
	/// The format codes Excel classifies: dates, times, date-times, elapsed times and numbers, with the parts that hide
	/// date letters (quotes, escapes, the characters after <c>_</c> and <c>*</c>, brackets, <c>General</c>, exponents).
	/// </summary>
	public static readonly string[] Codes =
	[
		// Dates, times and both.
		"m/d/yyyy", "d-mmm-yy", "d-mmm", "mmm-yy", "h:mm AM/PM", "h:mm:ss AM/PM", "h:mm", "h:mm:ss", "m/d/yyyy h:mm", "mm:ss",
		"mm:ss.0", "yyyy-mm-dd", "yyyy-mm-dd hh:mm:ss", "yyyy-mm-ddThh:mm:ss.000", "dd/mm/yyyy", "dd.mm.yyyy", "d. mmmm yyyy",
		"dddd, mmmm d, yyyy", "ddd", "dddd", "mmm", "mmmm", "mmmmm", "yy", "yyyy", "d", "dd", "m", "h", "hh", "s", "ss",
		"ss.000", "YYYY-MM-DD", "D/M/YYYY", "HH:MM", "H:MM:SS", "h AM/PM", "h:mm A/P", "h:mm a/p", "h:mm am/pm", "am/pm", "A/P",
		"yyyy-mm-dd;@", "[$-409]mmmm d, yyyy;@", "[$-409]h:mm:ss AM/PM;@", "[$-F800]dddd, mmmm dd, yyyy", "[$-F400]h:mm:ss AM/PM",
		"[$-x-sysdate]dddd, mmmm dd, yyyy", "[$-x-systime]h:mm:ss AM/PM", "[$-en-US]d-mmm-yy;@", "[$-1010409]d/m/yyyy;@",
		"[Red]yyyy-mm-dd", "yyyy-mm-dd;[Red]yyyy-mm-dd", "[<1]h:mm;yyyy-mm-dd", "yyyy\\-mm\\-dd", "yyyy\"/\"mm\"/\"dd",
		"\"Date: \"yyyy-mm-dd", "yyyy-mm-dd \"at\" h:mm", "mmm d \"days\"", "d \"days\" h:mm", "e", "ee", "g", "gg", "ggg", "b",
		"bb", "bbbb", "e-m-d", "ggge\"年\"m\"月\"d\"日\"", "[$-411]ggge\"年\"m\"月\"d\"日\"", "yyyy\"年\"m\"月\"d\"日\"", "aaa",
		"aaaa", "0 h", "0.00 d", "General d", "@ yyyy", "0;yyyy",

		// Elapsed times.
		"[h]:mm:ss", "[h]:mm", "[hh]:mm:ss", "[h]", "[H]:MM", "[m]:ss", "[mm]:ss", "[mm]:ss.000", "[s]", "[ss]", "[ss].00",
		"[h]:mm:ss;@", "[h]:mm;-[h]:mm", "[hhh]:mm", "[h]\" hours\"", "[Red][h]:mm", "[h]:mm;[Red]-[h]:mm", "[h]:mm:ss.000",
		"[$-409][h]:mm:ss;@", "yyyy-mm-dd [h]:mm", "0;[h]:mm",

		// Numbers.
		"General", "general", "GENERAL", "General;-General", "[Blue]General", "0", "0.00", "#,##0", "#,##0.00", "0%", "0.00%",
		"0.00E+00", "##0.0E+0", "0.00e+00", "0.0E-00", "# ?/?", "# ??/??", "@", "$#,##0.00", "$#,##0_);($#,##0)",
		"$#,##0.00_);[Red]($#,##0.00)", "#,##0_);[Red](#,##0)", "_(* #,##0_);_(* (#,##0);_(* \"-\"_);_(@_)",
		"_(\"$\"* #,##0.00_);_(\"$\"* (#,##0.00);_(\"$\"* \"-\"??_);_(@_)", "[$€-2] #,##0.00", "#,##0.00 [$€-1]", "[$$-409]#,##0.00",
		"[$-409]#,##0.00", "#,##0 [$USD]", "0.0\"m\"", "0.0\\m", "0 \"days\"", "0\"h\"", "\"Date:\" 0", "0.00 \"yrs\"", "0_m", "0_s",
		"0*-", "* #,##0", "*d0", "\"s\"0", "\\s0", "[>=100]0;0.00", "[Red][<0]0;0", "[Color10]0.00", "[DBNum1]0",
		"[DBNum1][$-411]General", "0;-0;;@", ";;;", "@\"x\"", "\"yyyy\"", "\"[h]\"0", "0.00;[Red]0.00", "0000", "00000-0000",
		"[<=9999999]###-####;(###) ###-####", "000-00-0000", "#,##0.00\" s\"", "0.0 \"d\"", "0.0E+0\" h\"", "0.00\\ \\d", "\"h\"0",
		"General\" d\"", "General \\d", "0!d", "0.00;@", "#\" \"?/2", "?/??", "0.0%;-0.0%;\"-\"", "0;0;0;\"text \"@", "[s]0",
		"0[h]", "[hh0]0", "[Red]0;[Blue]-0", "[=0]\"zero\";General", "0.00_);(0.00)", "_-* #,##0_-;-* #,##0_-;_-* \"-\"_-;_-@_-",
		"# ?/8", "0 \"h\"\"s\"",
	];

	/// <summary>Writes <c>excel-formats.json</c> to the corpus folder.</summary>
	/// <param name="files">The corpus folder.</param>
	public static void Write(string files)
	{
		var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N"))).FullName;
		try
		{
			var builtIn = Path.Combine(temp, "built-in.xlsx");
			var redefined = Path.Combine(temp, "redefined.xlsx");
			WriteWorkbook(builtIn, BuiltInCases());
			WriteWorkbook(redefined, RedefinedCases());
			var codes = new List<CodeAnswer>();
			var cells = new List<CellAnswer>();
			var language = 0;
			ExcelProducer.Run(excel =>
			{
				// msoLanguageIDUI: the built-in formats Excel shows depend on its language.
				language = (int)excel.LanguageSettings.LanguageID[2];
				codes.AddRange(ClassifyCodes(excel));
				cells.AddRange(ClassifyCells(excel, builtIn, BuiltInCases()));
				cells.AddRange(ClassifyCells(excel, redefined, RedefinedCases()));
			});

			var answers = new Answers(language, DateSerial, DurationSerial, codes, cells);
			var path = Path.Combine(files, "excel-formats.json");
			var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
			File.WriteAllText(path, JsonSerializer.Serialize(answers, options) + "\n", new UTF8Encoding(false));
			Console.WriteLine($"  {path}");
		}
		finally
		{
			Directory.Delete(temp, true);
		}
	}

	/// <summary>Asks Excel about each of <see cref="Codes"/>: a date and a duration formatted with it, in a new workbook.</summary>
	/// <param name="excel">Excel.</param>
	/// <returns>Excel's answer for each code, in order.</returns>
	private static List<CodeAnswer> ClassifyCodes(dynamic excel)
	{
		var answers = new List<CodeAnswer>();
		var workbook = excel.Workbooks.Add();
		try
		{
			var sheet = workbook.Worksheets[1];
			sheet.Columns["A:C"].ColumnWidth = 80;
			for (var i = 0; i < Codes.Length; i++)
			{
				var code = Codes[i];
				var date = sheet.Cells[i + 1, 1];
				var duration = sheet.Cells[i + 1, 2];
				date.Value2 = DateSerial;
				duration.Value2 = DurationSerial;
				try
				{
					date.NumberFormat = code;
					duration.NumberFormat = code;
				}
				catch (COMException)
				{
					// Excel rejects the code.
					answers.Add(new CodeAnswer(code, false, null, null, null, null, null, null));

					continue;
				}

				var category = sheet.Cells[i + 1, 3];
				category.Formula = $"=CELL(\"format\",A{i + 1})";
				answers.Add(new CodeAnswer(code, true, (string)date.NumberFormat, (string)category.Value2, date.Value is DateTime, duration.Value is DateTime,
					(string)date.Text, (string)duration.Text));
			}
		}
		finally
		{
			workbook.Close(false);
		}

		return answers;
	}

	/// <summary>Asks Excel about the cells of a workbook that <see cref="WriteWorkbook"/> wrote: one per case, in column A.</summary>
	/// <param name="excel">Excel.</param>
	/// <param name="path">The workbook.</param>
	/// <param name="cases">Its cases, in row order.</param>
	/// <returns>Excel's answer for each case, in order.</returns>
	private static List<CellAnswer> ClassifyCells(dynamic excel, string path, IReadOnlyList<Case> cases)
	{
		var answers = new List<CellAnswer>();
		var workbook = excel.Workbooks.Open(Filename: path, ReadOnly: true);
		try
		{
			var sheet = workbook.Worksheets[1];
			sheet.Columns["A:B"].ColumnWidth = 80;
			for (var i = 0; i < cases.Count; i++)
			{
				var cell = sheet.Cells[i + 1, 1];
				var category = sheet.Cells[i + 1, 2];
				category.Formula = $"=CELL(\"format\",A{i + 1})";
				var c = cases[i];
				answers.Add(new CellAnswer(c.Name, c.Id, c.Code, c.Applied, (string)cell.NumberFormat, (string)category.Value2, cell.Value is DateTime, (string)cell.Text));
			}
		}
		finally
		{
			workbook.Close(false);
		}

		return answers;
	}

	/// <summary>
	/// The cells of the built-in workbook: each id from 0 to 163 without a definition, and the custom format
	/// <see cref="CustomId"/>, with and without <c>applyNumberFormat="0"</c>, as is a built-in date.
	/// </summary>
	/// <returns>The cases, in row order.</returns>
	private static List<Case> BuiltInCases()
	{
		var cases = Enumerable.Range(0, FirstCustomId).Select(id => new Case($"id {id}", id, null, true)).ToList();
		cases.Add(new Case("custom date", CustomId, "yyyy-mm-dd", true));
		cases.Add(new Case("custom date, applyNumberFormat=0", CustomId, "yyyy-mm-dd", false));
		cases.Add(new Case("id 14, applyNumberFormat=0", 14, null, false));
		cases.Add(new Case("id 2, applyNumberFormat=0", 2, null, false));

		return cases;
	}

	/// <summary>The cells of the workbook whose <c>numFmts</c> redefines built-in ids: a date id as a number, and the reverse.</summary>
	/// <returns>The cases, in row order.</returns>
	private static List<Case> RedefinedCases() =>
	[
		new("id 14 defined as 0.00", 14, "0.00", true),
		new("id 2 defined as yyyy-mm-dd", 2, "yyyy-mm-dd", true),
		new("id 46 defined as 0.00", 46, "0.00", true),
		new("id 22, not defined", 22, null, true),
	];

	/// <summary>
	/// Writes a workbook of one sheet with a number in column A of each row, styled with its case's number format: the
	/// format id, a <c>numFmts</c> entry for a case with a code, and <c>applyNumberFormat</c>.
	/// </summary>
	/// <param name="path">Where to write it.</param>
	/// <param name="cases">The cases, one per row.</param>
	private static void WriteWorkbook(string path, IReadOnlyList<Case> cases)
	{
		var formats = cases.Where(c => c.Code is not null).GroupBy(c => c.Id).Select(g => (Id: g.Key, Code: g.First().Code!)).ToList();
		var styles = new StringBuilder();
		styles.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
		styles.Append("""<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
		if (formats.Count > 0)
		{
			styles.Append($"""<numFmts count="{formats.Count}">""");
			foreach (var (id, code) in formats)
			{
				styles.Append($"""<numFmt numFmtId="{id}" formatCode="{Escape(code)}"/>""");
			}

			styles.Append("</numFmts>");
		}

		styles.Append("""<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>""");
		styles.Append("""<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>""");
		styles.Append("""<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>""");
		styles.Append("""<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>""");
		styles.Append($"""<cellXfs count="{cases.Count + 1}"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>""");
		foreach (var c in cases)
		{
			styles.Append($"""<xf numFmtId="{c.Id}" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="{(c.Applied ? 1 : 0)}"/>""");
		}

		styles.Append("""</cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");

		var sheet = new StringBuilder();
		sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
		sheet.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
		for (var i = 0; i < cases.Count; i++)
		{
			sheet.Append($"""<row r="{i + 1}"><c r="A{i + 1}" s="{i + 1}"><v>{DateSerial.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c></row>""");
		}

		sheet.Append("</sheetData></worksheet>");

		using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
		Add(zip, "[Content_Types].xml",
			"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
		Add(zip, "_rels/.rels",
			"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
		Add(zip, "xl/workbook.xml",
			"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Formats" sheetId="1" r:id="rId1"/></sheets></workbook>""");
		Add(zip, "xl/_rels/workbook.xml.rels",
			"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
		Add(zip, "xl/styles.xml", styles.ToString());
		Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
	}

	/// <summary>Adds a part to a zip.</summary>
	/// <param name="zip">The zip.</param>
	/// <param name="name">The part's name.</param>
	/// <param name="content">Its XML.</param>
	private static void Add(ZipArchive zip, string name, string content)
	{
		using var stream = zip.CreateEntry(name).Open();
		var bytes = new UTF8Encoding(false).GetBytes(content);
		stream.Write(bytes);
	}

	/// <summary>Escapes a format code for an attribute value.</summary>
	/// <param name="text">The code.</param>
	/// <returns>The escaped code.</returns>
	private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

	/// <summary>A cell of a written workbook: its number format, and whether its cell format applies it.</summary>
	/// <param name="Name">What the case checks.</param>
	/// <param name="Id">The cell format's <c>numFmtId</c>.</param>
	/// <param name="Code">The code <c>numFmts</c> defines for the id, or <see langword="null"/> for none.</param>
	/// <param name="Applied">The cell format's <c>applyNumberFormat</c>.</param>
	private sealed record Case(string Name, int Id, string? Code, bool Applied);

	/// <summary>Excel's answers.</summary>
	/// <param name="UiLanguage">Excel's user interface language (an LCID), which the built-in formats depend on.</param>
	/// <param name="DateSerial">The number each code formats as a date.</param>
	/// <param name="DurationSerial">The number each code formats as a duration.</param>
	/// <param name="Codes">The answers for each format code.</param>
	/// <param name="Cells">The answers for each cell of the written workbooks.</param>
	private sealed record Answers(int UiLanguage, double DateSerial, double DurationSerial, List<CodeAnswer> Codes, List<CellAnswer> Cells);

	/// <summary>Excel's answer for a format code.</summary>
	/// <param name="Code">The code.</param>
	/// <param name="Valid">Whether Excel takes it.</param>
	/// <param name="ExcelCode">The code as Excel keeps it.</param>
	/// <param name="Category">
	/// What <c>CELL("format")</c> says of it: <c>D1</c> to <c>D9</c> for Excel's date and time formats, <c>G</c> for
	/// General, and others for numbers.
	/// </param>
	/// <param name="Date">
	/// Whether <c>Range.Value</c> of <see cref="DateSerial"/> in it is a date, which it is when the format shows a day, a
	/// month or a year (not a time alone).
	/// </param>
	/// <param name="DurationDate">Whether <c>Range.Value</c> of <see cref="DurationSerial"/> in it is a date.</param>
	/// <param name="Text">What Excel shows for <see cref="DateSerial"/>.</param>
	/// <param name="DurationText">What Excel shows for <see cref="DurationSerial"/>.</param>
	private sealed record CodeAnswer(string Code, bool Valid, string? ExcelCode, string? Category, bool? Date, bool? DurationDate, string? Text, string? DurationText);

	/// <summary>Excel's answer for a cell of a written workbook.</summary>
	/// <param name="Name">What the case checks.</param>
	/// <param name="Id">The cell format's <c>numFmtId</c>.</param>
	/// <param name="Code">The code <c>numFmts</c> defines for the id, or <see langword="null"/>.</param>
	/// <param name="Applied">The cell format's <c>applyNumberFormat</c>.</param>
	/// <param name="ExcelCode">The code Excel shows for the cell.</param>
	/// <param name="Category">What <c>CELL("format")</c> says of the cell's format.</param>
	/// <param name="Date">Whether <c>Range.Value</c> is a date.</param>
	/// <param name="Text">What Excel shows.</param>
	private sealed record CellAnswer(string Name, int Id, string? Code, bool Applied, string ExcelCode, string Category, bool Date, string Text);
}
