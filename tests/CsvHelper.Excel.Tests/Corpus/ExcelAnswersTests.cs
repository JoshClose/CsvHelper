using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Every corpus workbook read by CsvHelper.Excel against Excel 365's own reading of it (<see cref="ExcelAnswers"/>, from
/// the corpus tool's <c>oracle</c> command): each cell's kind and value, the text of its booleans, errors and the
/// serials around the date systems' limits, and its formula as text. They agree except in the cells listed here, each with
/// its reason.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kinds.</b> Excel's <c>Range.Value</c> gives a date for a format that shows a day, a month or a year; a time alone
/// or an elapsed time is a number to it, which <c>CELL("format")</c> and the text of a day and a half tell apart, as
/// <see cref="NumberFormatsTests"/> does.
/// </para>
/// <para>
/// <b>Formulas.</b> Excel shows a formula otherwise than the file stores it, which the comparison allows for: newer functions
/// without their <c>_xlfn.</c> and <c>_xlws.</c> prefixes, <c>_xlfn.ANCHORARRAY(H14)</c> as <c>H14#</c>, and a table's
/// <c>Orders[[#This Row],[Qty]]</c> as <c>[@Qty]</c>. It shows a legacy array's formula in each of its cells, whose
/// other cells read their values (decision 16), so their formulas aren't compared.
/// </para>
/// </remarks>
public class ExcelAnswersTests
{
	private const string PastTheLastDay = "a serial past the date system's last day (9999-12-31) in a date format: Excel's Value has no date for it, and CsvHelper.Excel reads it as a date cell that no date target takes";

	private const string NegativeDates = "Excel's 1904 date system shows a negative serial as a date before 1904 (-1 as -1904-01-02); CsvHelper.Excel takes dates from serial 0, and reads -1 as a number's text";

	private const string NewerErrors = "Excel stores #CALC! and #SPILL! as #VALUE! with rich value metadata, which shows the newer error";

	private const string DateCompatibility = "a Strict workbook (dateCompatibility=\"0\") counts days as OLE Automation dates do, without 1900-02-29: its t=\"d\" dates before March 1900 read as the same dates in the 1900 system, whose serials are a day less, and 1899-12-30 stays text";

	private const string Overflow = "the producer wrote the largest double with 15 or 16 digits, past it, which Excel reads as #NULL!";

	private const string Subnormal = "the producer wrote the least normal double with 15 or 16 digits, below it, and Excel reads a subnormal number as 0";

	private const string ClosedXmlNa = "ClosedXML 0.105.1 wrote NA()'s result #N/A as a number, without t=\"e\"; Excel repairs the cell to #VALUE!";

	private const string PivotTable = "Google Sheets writes a pivot table's sheet without its cells, which Excel builds from the pivot table when it opens the file";

	private const string ArrayValues = "Excel shows #VALUE! for a legacy array formula's other cell, whatever value the file stores there";

	private const string EmptyArrayCells = "openpyxl writes no value in an array formula's other cells, which Excel shows as 0";

	private const string LowercaseFormulas = "LibreOffice writes error literals and ANCHORARRAY in lowercase (#ref!, _xlfn.anchorarray), which Excel shows in capitals";

	/// <summary>The cells where Excel reads the corpus otherwise, and why. A cell of <c>Sheet!</c> alone stands for every cell of the sheet.</summary>
	private static readonly (string File, string Cell, string Reason)[] Differences =
	[
		("closedxml/typed.xlsx", "Dates!B100", PastTheLastDay),
		("closedxml/typed.xlsx", "Numbers!B21", Overflow),
		("closedxml/typed.xlsx", "Numbers!B22", Subnormal),
		("closedxml/typed.xlsx", "Numbers!B38", ClosedXmlNa),
		("excel/typed.xlsx", "Dates!B100", PastTheLastDay),
		("excel/typed.xlsx", "Numbers!B45", NewerErrors),
		("excel/typed-1904.xlsx", "Dates!B98", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B99", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B100", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B101", NegativeDates),
		("excel/typed-1904.xlsx", "Numbers!B45", NewerErrors),
		.. new[] { "Dates!B91", "Dates!B93", "Dates!B94", "Dates!B95", "Dates!B96", "Dates!B110" }.Select(c => ("excel/typed-strict.xlsx", c, DateCompatibility)),
		("excel/typed-strict.xlsx", "Dates!B100", PastTheLastDay),
		("excel/typed-strict.xlsx", "Numbers!B45", NewerErrors),
		("google/64450.xlsx", "'Hoja 2'!", PivotTable),
		("google/66365.xlsx", "Sheet1!B2", ArrayValues),
		("libreoffice/typed.xlsx", "Dates!B100", PastTheLastDay),
		("libreoffice/typed.xlsx", "Numbers!B22", Subnormal),
		("libreoffice/typed.xlsx", "Numbers!B34", LowercaseFormulas),
		("libreoffice/typed.xlsx", "Formulas!J14", LowercaseFormulas),
		("libreoffice/typed.xlsx", "Formulas!A18", LowercaseFormulas),
		.. new[] { "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx" }.SelectMany(f => new[]
		{
			(f, "Dates!B100", PastTheLastDay), (f, "Numbers!B21", Overflow), (f, "Numbers!B22", Subnormal),
			(f, "Formulas!G15", EmptyArrayCells), (f, "Formulas!G16", EmptyArrayCells),
		}),
		("poi/typed.xlsx", "Dates!B100", PastTheLastDay),
		("xlsxwriter/typed.xlsx", "Dates!B100", PastTheLastDay),
		("xlsxwriter/typed.xlsx", "Numbers!B21", Overflow),
		("xlsxwriter/typed.xlsx", "Numbers!B22", Subnormal),
	];

	/// <summary>The workbooks that have Excel's answers: all but the benchmarks' cross-checks, of 42,000 and 12,000 cells.</summary>
	public static IEnumerable<object[]> AnsweredWorkbooks =>
		CorpusFiles.WorkbookData.Where(w => (string)w[0] is not "excel/packageassets-1695.xlsx" and not "excel/orders-1000.xlsx");

	[Fact]
	public void AnswersForEveryWorkbook()
	{
		Assert.All(AnsweredWorkbooks, w => Assert.NotNull(ExcelAnswers.Load((string)w[0])));

		// Excel opened every workbook as it is, but ClosedXML's typed one, whose NA() result it repaired.
		Assert.Equal(
			["closedxml/typed.xlsx"],
			AnsweredWorkbooks.Select(w => (string)w[0]).Where(f => ExcelAnswers.Load(f)!.Repaired.Count > 0));
	}

	[Theory]
	[MemberData(nameof(AnsweredWorkbooks))]
	public void ReadsEveryCellAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var ours = CorpusFiles.Read(file, out var date1904);
		var formulas = CorpusFiles.Read(file, out _, ExcelFormulas.FormulaText);
		Assert.Equal(answers.Date1904, date1904);
		Assert.Equal(answers.Sheets.Keys.OrderBy(k => k, StringComparer.Ordinal), ours.Keys.OrderBy(k => k, StringComparer.Ordinal));

		var differences = new List<string>();
		foreach (var pair in answers.Sheets)
		{
			var (sheet, cells) = (pair.Key, pair.Value);
			var mine = ours[sheet];
			var mineFormulas = formulas[sheet];
			foreach (var key in cells.Keys.Union(mine.Keys).OrderBy(k => k.Row).ThenBy(k => k.Column))
			{
				var location = new SheetLocation(sheet).Cell(key.Column, key.Row);
				var excel = cells.TryGetValue(key, out var answer) ? answer : null;
				var expected = excel is null ? null : Expected(excel);
				var hasOurs = mine.TryGetValue(key, out var value);
				if (expected is null != !hasOurs || (expected is { } e && !CorpusFiles.Same(value, e)))
				{
					differences.Add($"{location}: ours {(hasOurs ? value.ToString() : "valueless")}, Excel {expected?.ToString() ?? "valueless"}");

					continue;
				}

				// The text of a boolean or an error, and of the serials shown to the millisecond.
				if (hasOurs && excel is not null && ShownText(value, excel, date1904) is { } shown && shown != excel.Text)
				{
					differences.Add($"{location}: ours shows {shown}, Excel {excel.Text}");

					continue;
				}

				var ourFormula = mineFormulas.TryGetValue(key, out var f) && f.Kind == SheetCellKind.Text && f.Text.StartsWith("=", StringComparison.Ordinal) ? f.Text : null;
				if (excel is { HasArray: true } && ourFormula is null)
				{
					continue;
				}

				if (Shown(ourFormula) != excel?.Formula)
				{
					differences.Add($"{location}: ours {ourFormula ?? "no formula"}, Excel {excel?.Formula ?? "no formula"}");
				}
			}
		}

		var listed = Differences.Where(d => d.File == file).ToList();
		var unexplained = differences.Where(d => !listed.Any(l => d.StartsWith(l.Cell.EndsWith("!", StringComparison.Ordinal) ? l.Cell : l.Cell + ":", StringComparison.Ordinal))).ToList();
		var unseen = listed.Where(l => !differences.Any(d => d.StartsWith(l.Cell.EndsWith("!", StringComparison.Ordinal) ? l.Cell : l.Cell + ":", StringComparison.Ordinal))).Select(l => l.Cell).ToList();
		Assert.True(unexplained.Count == 0 && unseen.Count == 0, $"{file}: not listed:\n{string.Join("\n", unexplained)}\nlisted but not found: [{string.Join(", ", unseen)}]");
	}

	/// <summary>What Excel's answer says a cell holds, in <see cref="CellValue"/> form, or <see langword="null"/> for valueless.</summary>
	/// <param name="cell">Excel's answer.</param>
	/// <returns>The value.</returns>
	private static CellValue? Expected(ExcelCell cell) => cell.Type switch
	{
		"empty" => null,
		"text" => new CellValue(cell.Value2, SheetCellKind.Text),
		"boolean" => new CellValue(cell.Value2 == "true" ? "1" : "0", SheetCellKind.Bool),
		"error" => new CellValue(cell.Value2, SheetCellKind.Error),
		_ => new CellValue(
			cell.Value2,
			NumberFormatsTests.TimesAndCalendarParts.Contains(cell.NumberFormat) ? SheetCellKind.DateNumber : NumberFormatsTests.Expected(cell.Type == "date", cell.Category, cell.Elapsed)),
	};

	/// <summary>
	/// The text CsvHelper.Excel gives a cell, in Excel's form, where the two can be compared: a boolean's and an error's,
	/// and a date's or a duration's in a format to the millisecond (<c>yyyy-mm-dd hh:mm:ss.000</c>, <c>[h]:mm:ss.000</c>),
	/// unless Excel fills the cell with <c>#</c> for want of a date.
	/// </summary>
	/// <param name="value">Our value.</param>
	/// <param name="excel">Excel's answer.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The text in Excel's form, or <see langword="null"/> when there is nothing to compare.</returns>
	private static string? ShownText(CellValue value, ExcelCell excel, bool date1904)
	{
		var text = CellTexts.Of(value, date1904);
		switch (value.Kind)
		{
			case SheetCellKind.Bool:
			case SheetCellKind.Error:
				return text;
			case SheetCellKind.DateNumber when excel.NumberFormat == "yyyy-mm-dd hh:mm:ss.000" && excel.Text.Trim('#').Length > 0:
				// Ours is yyyy-MM-dd, HH:mm:ss or both, with the milliseconds there are; Excel's always has both. Excel shows
				// day 0 as 1900-01-00, a date ours doesn't have (decision 9), which the comparison leaves to Excel's.
				var match = Regex.Match(text, @"^(?:(\d{4}-\d{2}-\d{2})T?)?(\d{2}:\d{2}:\d{2})?(?:\.(\d+))?$");
				if (!match.Success)
				{
					return text;
				}

				var day = match.Groups[1].Success ? match.Groups[1].Value : excel.Text.Substring(0, 10);
				var time = match.Groups[2].Success ? match.Groups[2].Value : "00:00:00";

				return $"{day} {time}.{(match.Groups[3].Value + "000").Substring(0, 3)}";
			case SheetCellKind.Duration when excel.NumberFormat == "[h]:mm:ss.000" && excel.Text.Trim('#').Length > 0:
				// Excel's hours have no leading zero.
				var duration = Regex.Match(text, @"^(-?)0*(\d+)(:\d{2}:\d{2})(?:\.(\d+))?$");

				return duration.Success ? $"{duration.Groups[1].Value}{duration.Groups[2].Value}{duration.Groups[3].Value}.{(duration.Groups[4].Value + "000").Substring(0, 3)}" : text;
			default:
				return null;
		}
	}

	/// <summary>
	/// A formula as the file stores it, shown as Excel shows it: newer functions without their prefixes, the spill
	/// operator for <c>ANCHORARRAY</c>, and <c>[@Col]</c> for a table's own row.
	/// </summary>
	/// <param name="formula">The formula, or <see langword="null"/>.</param>
	/// <returns>The formula as Excel shows it.</returns>
	private static string? Shown(string? formula)
	{
		if (formula is null)
		{
			return null;
		}

		var shown = Regex.Replace(formula, @"_xlfn\.|_xlws\.", "");
		shown = Regex.Replace(shown, @"ANCHORARRAY\(([^()]+)\)", "$1#");

		return Regex.Replace(shown, @"\w+\[\[#This Row\],\[([^\]]+)\]\]", "[@$1]");
	}
}
