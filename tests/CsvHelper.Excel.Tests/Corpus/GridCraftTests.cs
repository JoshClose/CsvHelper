using System.Globalization;
using CsvHelper.Excel.Corpus;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// ArtCraft's GridCraft (<see cref="GridCraft"/>) against Excel 365's answers: every corpus workbook Excel answered about
/// (<see cref="ExcelAnswers"/>), its cells and structures, and every workbook of the written set as this runtime writes it
/// (<see cref="WrittenAnswers"/>), its cells and look. GridCraft reads them as Excel does but in the cells listed here, each
/// with its reason, as LibreOffice's are; a listed cell that no longer differs fails too, so that a GridCraft version that
/// reads it as Excel does takes it off the list.
/// </summary>
/// <remarks>
/// <para>
/// The comparison allows for how each reports a cell, which isn't what it reads: a type by its family (Excel's
/// <c>Value</c> is a <c>date</c> or <c>currency</c> by the format, GridCraft's a <c>number</c>); a number format's code
/// without the <c>\</c> and quotes Excel's <c>NumberFormat</c> leaves out where a code doesn't need them, and without case,
/// which it changes too, and an xf without a code as General; text Excel shows as <c>#</c> signs of any length (Excel's answers about the corpus widen the columns
/// to 255 characters); and Excel's <c>Text</c>, which stops at 8,221 characters.
/// </para>
/// <para>
/// GridCraft recalculates every formula when it opens a workbook and can't show the file's cached values, which Excel's
/// answers about the corpus are (Excel was asked with calculation manual). So a corpus formula cell's formula and number
/// format are compared, not its value, and a cell GridCraft fills from a dynamic array it recalculated isn't either. The
/// written set's answers were made with calculation automatic, so every value is compared.
/// </para>
/// </remarks>
/// <param name="output">The tests' output.</param>
public class GridCraftTests(ITestOutputHelper output)
{
	/// <summary>The most characters Excel's <c>Text</c> gives.</summary>
	private const int TextLimit = 8221;

	private const string GeneralDecimals = "GridCraft's General shows 0.00001 and 0.0000001 in E notation (1E-05, 1E-07), where Excel shows their decimals, which fit its 11 characters";

	private const string GeneralSign = "GridCraft's General counts a minus sign in its 11 characters (-1234567.89); Excel doesn't (-1234567.891)";

	private const string UnmappedIds = "GridCraft shows General for the built-in format ids Excel maps by its locale (East Asian 27-36 and 50-58, Thai 59-81), which en-US Excel shows as m/d/yyyy, h:mm:ss, 0 and others";

	private const string SpecCodes = "GridCraft gives built-in formats 37 and 47 the specification's codes, #,##0 ;(#,##0) and mmss.0; Excel's are #,##0_);(#,##0) and mm:ss.0";

	private const string Locales = "GridCraft shows a format's day and month names in English under [$-407], [$-40C], [$-411] and [$-415] (Monday, January), where Excel shows the locale's (Montag, Januar)";

	private const string TextSections = "Excel shows a boolean in a format's text section (Paid: TRUE); GridCraft shows it without the section (TRUE)";

	private const string HoursAfterADate = "in a format of a date and then [h], Excel shows the hours of the day (2024-01-15 12:00), GridCraft the hours since the date system's start (1087356:00)";

	private const string Negative1904 = "Excel's 1904 date system shows a negative serial (-12:00:00.000, -1904-01-02); GridCraft shows ########";

	private const string StrictDates = "GridCraft ignores a Strict workbook's dateCompatibility=\"0\", under which Excel counts serials as OLE Automation dates do, from 1899-12-30 and without 1900-02-29: its t=\"d\" dates before March 1900 have a serial a day less, 1899-12-30 and 31 read as 3799, serials below 1 show as 1900-01-00, and durations stored as dates have no value";

	private const string PivotTable = "Google Sheets writes a pivot table's sheet without its cells, which Excel builds from the pivot table when it opens the file; GridCraft shows the sheet empty";

	private const string ArrayFormulas = "Excel shows a legacy array formula in each of its cells; GridCraft gives its other cells no formula";

	private const string OwnTable = "Excel shows a structured reference to the table the cell is in without the table's name ([@Qty], [Qty]); GridCraft with it (Orders[@Qty])";

	private const string FormulaLineBreaks = "GridCraft drops the line breaks of a formula outside its strings, which Excel keeps";

	private const string Overflow = "the producer wrote the largest double with 15 or 16 digits, past it, which Excel reads as #NULL! and GridCraft as no value";

	private const string Subnormal = "Excel reads a subnormal double as 0; GridCraft keeps it";

	private const string Spaces = "GridCraft keeps the white space around a text that xml:space=\"preserve\" doesn't apply to; Excel trims it";

	private const string Escapes = "GridCraft doesn't decode _xHHHH_ in a formula's strings and in its str value, which Excel decodes";

	private const string Widths = "Excel's answers about the written set show a cell at its column's width (######## or General narrowed, 1.23E+14), and GridCraft's text doesn't depend on widths";

	private const string SheetNameEscapes = "GridCraft doesn't decode _x005F_ in a sheet's name (a_x005F_x0041_b), which Excel shows as a_x0041_b";

	private const string BuiltInNames = "GridCraft lists no built-in name: a filter's _FilterDatabase, Print_Area and Print_Titles";

	private const string FutureNames = "GridCraft lists no _xlfn. name, which Excel keeps, hidden, for each newer function the formulas use";

	private const string DeletedSheet = "Excel shows a name of a deleted sheet's range as =#REF!#REF!, GridCraft as =#REF!";

	private const string HiddenRows = "GridCraft hides no row that the sheet's default height of 0 hides past the file's last row, nor a row of height 0 without hidden=\"1\"";

	private const string FileLinks = "GridCraft gives a link to a file as its file: URI, Excel as its path";

	/// <summary>The basic workbook as each producer writes it, and Excel's in its other formats.</summary>
	private static readonly string[] Basic =
	[
		"closedxml/basic.xlsx", "epplus/basic.xlsx", "excel/basic.xlsx", "excel/date1904.xlsx", "excel/macro.xlsm", "excel/strict.xlsx", "excel/template.xltx",
		"largexlsx/basic.xlsx", "libreoffice/basic-ooxml.xlsx", "libreoffice/basic.xlsx", "miniexcel/basic.xlsx", "openpyxl/basic.xlsx", "openxmlsdk/basic.xlsx",
		"pandas/openpyxl.xlsx", "pandas/xlsxwriter.xlsx", "poi/sxssf.xlsx", "poi/xssf.xlsx", "spreadcheetah/basic.xlsx", "xlsxwriter/basic.xlsx", "xlsxwriter/constant_memory.xlsx",
	];

	/// <summary>The typed workbook as each producer writes it, and Excel's in the 1904 date system and as Strict Open XML.</summary>
	private static readonly string[] Typed =
	[
		"closedxml/typed.xlsx", "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "excel/typed.xlsx", "libreoffice/typed.xlsx", "openpyxl/typed-isodates.xlsx",
		"openpyxl/typed.xlsx", "poi/typed.xlsx", "xlsxwriter/typed.xlsx",
	];

	/// <summary>The typed workbooks whose producers set a number format by its id, which Excel can't.</summary>
	private static readonly string[] SetById = ["closedxml/typed.xlsx", "poi/typed.xlsx", "xlsxwriter/typed.xlsx"];

	/// <summary>The messy workbook as each producer that writes a chartsheet writes it.</summary>
	private static readonly string[] Messy = ["closedxml/messy.xlsx", "excel/messy.xlsx", "libreoffice/messy.xlsx", "poi/messy.xlsx", "xlsxwriter/messy.xlsx"];

	/// <summary>The cells where GridCraft reads the corpus otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] CorpusDifferences =
	[
		.. Basic.Select(f => (f, "Data!B6", GeneralDecimals)),
		.. Typed.SelectMany(f => new[] { "Numbers!B24", "Numbers!B27" }.Select(c => (f, c, GeneralDecimals))),
		.. SetById.SelectMany(f => new[]
		{
			"Dates!B11", "Dates!B12", "Dates!B13", "Dates!B14", "Dates!B15", "Dates!B16", "Dates!B17", "Dates!B18", "Dates!B19", "Dates!B20", "Dates!B24", "Dates!B25",
			"Dates!B26", "Dates!B27", "Dates!B28", "Dates!B29", "Dates!B30", "Dates!B31", "Dates!B32", "Dates!B33", "Dates!B34", "Dates!B35", "Dates!B36", "Dates!B37",
			"Dates!B38", "Dates!B39", "Dates!B40", "Dates!B41", "Dates!B42", "Dates!B43", "Dates!B51", "Dates!B52",
		}.Select(c => (f, c, UnmappedIds))),
		.. SetById.SelectMany(f => new[] { "Dates!B23", "Dates!B49" }.Select(c => (f, c, SpecCodes))),
		.. Typed.Where(f => f != "libreoffice/typed.xlsx").Select(f => (f, "Dates!B68", HoursAfterADate)),
		.. new[] { "Dates!B89", "Dates!B101" }.Select(c => ("excel/typed-1904.xlsx", c, Negative1904)),
		.. new[]
		{
			"Dates!B82", "Dates!B83", "Dates!B84", "Dates!B85", "Dates!B86", "Dates!B87", "Dates!B88", "Dates!B90", "Dates!B91", "Dates!B92", "Dates!B93", "Dates!B94",
			"Dates!B95", "Dates!B96", "Dates!B102", "Dates!B106", "Dates!B110",
		}.Select(c => ("excel/typed-strict.xlsx", c, StrictDates)),
		.. new[] { "closedxml/typed.xlsx", "openpyxl/typed-isodates.xlsx", "openpyxl/typed.xlsx", "xlsxwriter/typed.xlsx" }.Select(f => (f, "Numbers!B21", Overflow)),
		.. new[] { "closedxml/typed.xlsx", "libreoffice/typed.xlsx", "openpyxl/typed-isodates.xlsx", "openpyxl/typed.xlsx", "xlsxwriter/typed.xlsx" }.Select(f => (f, "Numbers!B22", Subnormal)),
		.. Typed.SelectMany(f => new[] { "Formulas!G15", "Formulas!G16" }.Select(c => (f, c, ArrayFormulas))),
		("google/66365.xlsx", "Sheet1!B2", ArrayFormulas),
		.. Typed.Select(f => (f, "Formulas!C18", FormulaLineBreaks)),
		.. new[] { "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "excel/typed.xlsx", "libreoffice/typed.xlsx", "xlsxwriter/typed.xlsx" }
			.SelectMany(f => new[] { "Formulas!D21", "Formulas!D22", "Formulas!D23" }.Select(c => (f, c, OwnTable))),
		.. new[] { "excel/messy.xlsx", "libreoffice/messy.xlsx", "poi/messy.xlsx" }.Select(f => (f, "Tables!B5", OwnTable)),
		("google/64450.xlsx", "'Hoja 2'!", PivotTable),
		("spreadcheetah/basic.xlsx", "Data!B17", Spaces),
	];

	/// <summary>The structures where GridCraft reads the corpus otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] CorpusStructureDifferences =
	[
		.. Messy.Where(f => f != "closedxml/messy.xlsx").Select(f => (f, "Filtered!rows", HiddenRows)),
		.. Messy.Select(f => (f, "name Deleted", DeletedSheet)),
		.. Messy.SelectMany(f => new[] { "name Filtered!_FilterDatabase", "name Report!Print_Area", "name Report!Print_Titles" }.Select(c => (f, c, BuiltInNames))),
		.. Typed.Select(f => (f, "name _xlfn.SINGLE", FutureNames)),
		.. new[] { "closedxml/typed.xlsx", "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "excel/typed.xlsx", "libreoffice/typed.xlsx", "xlsxwriter/typed.xlsx" }.Select(f => (f, "name _xlfn.XLOOKUP", FutureNames)),
		.. new[] { "excel/typed-1904.xlsx", "excel/typed-strict.xlsx", "excel/typed.xlsx", "libreoffice/typed.xlsx", "xlsxwriter/typed.xlsx" }
			.SelectMany(f => new[] { "name _xlfn.ANCHORARRAY", "name _xlfn.SEQUENCE", "name _xlfn._xlws.FILTER" }.Select(c => (f, c, FutureNames))),
	];

	/// <summary>The cells where GridCraft reads the written set otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] WrittenDifferences =
	[
		("autofit.xlsx", "General!B7", GeneralDecimals),
		("converted.xlsx", "Columns!B4", GeneralDecimals),
		.. new[] { "Numbers!C4", "Numbers!D4", "Numbers!E4", "Numbers!F4", "Numbers!G4", "Numbers!H4" }.Select(c => ("cultures.xlsx", c, GeneralDecimals)),
		.. new[] { "Numbers!E12", "Numbers!F12", "Numbers!G12" }.Select(c => ("types.xlsx", c, GeneralDecimals)),
		("autofit.xlsx", "General!B4", GeneralSign),
		.. new[]
		{
			"Custom!A3", "Custom!B2", "Custom!B3", "Custom!B4", "Custom!C2", "Custom!C3", "Custom!C4", "Custom!D2", "Custom!D3", "Custom!F2", "Custom!F3", "Custom!F4",
			"Custom!I2", "Custom!I3", "Custom!I4", "Custom!L2", "Custom!L3", "Kinds!D2", "Kinds!D3", "Kinds!D4", "Kinds!F2", "Kinds!F3", "Kinds!F4", "Kinds!G2",
			"Kinds!G3", "Kinds!G4", "Kinds!I2", "Kinds!I3", "Kinds!I4", "Kinds!S2", "Kinds!S3", "Kinds!S4", "Standard!K2", "Standard!K3", "Standard!K4", "Standard!L2",
			"Standard!L3", "Standard!L4", "Standard!M2", "Standard!M3", "Standard!M4", "Standard!R2", "Standard!R3", "Standard!T2", "Standard!T3", "Standard!T4",
			"Standard!U2", "Standard!U3", "Standard!U4", "Standard!V2", "Standard!V3", "Standard!V4", "Standard!AA2", "Standard!AA3", "Standard!AA4", "Standard!AC2",
			"Standard!AC3", "Standard!AC4", "Standard!AD2", "Standard!AD3", "Standard!AD4", "Standard!AE2", "Standard!AE3", "Standard!AE4",
		}.Select(c => ("cultures.xlsx", c, Locales)),
		.. new[] { "Attributes!I2", "Attributes!I3", "Attributes!I4", "Options!I2", "Options!I3", "Options!I4" }.Select(c => ("columns.xlsx", c, TextSections)),
		("types.xlsx", "Numbers!F15", Subnormal),
		.. new[] { "Calc!D5", "Calc!D7" }.Select(c => ("formulas.xlsx", c, Escapes)),
		("formulas.xlsx", "Calc!D6", Escapes + "; and " + FormulaLineBreaks),
		.. new[] { "Numbers!B3", "Numbers!I3" }.Select(c => ("cultures.xlsx", c, Widths)),
		("formats.xlsx", "Early!A3", Widths),
		.. new[] { "'Orders (2)'!C3", "'Orders (3)'!A3" }.Select(c => ("newsheet-look.xlsx", c, Widths)),
		.. new[] { "Numbers!B2", "Numbers!B3", "Numbers!B4", "Numbers!B5", "Numbers!C2", "Numbers!C3", "Numbers!C4", "Numbers!E2", "Numbers!E3", "Numbers!E4", "Numbers!E5" }
			.Select(c => ("options.xlsx", c, Widths)),
		("types-net.xlsx", "Newer!E5", Widths),
		.. new[]
		{
			"Integers!F3", "Numbers!B5", "Numbers!C5", "Numbers!D8", "Numbers!D9", "Numbers!E5", "Numbers!F5", "Numbers!F6", "Numbers!F8", "Numbers!F9", "Numbers!F14",
			"Numbers!G8", "Numbers!G9", "Widths!B3",
		}.Select(c => ("types.xlsx", c, Widths)),
	];

	/// <summary>Where GridCraft reads the written set's look otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LookDifferences =
	[
		.. new[] { "filters.xlsx", "names.xlsx" }.Select(f => (f, "a_x0041_b!sheet", SheetNameEscapes)),
		.. new[] { "name 'Bob''s orders'!_FilterDatabase", "name Header!_FilterDatabase", "name Orders!_FilterDatabase", "name a_x0041_b!_FilterDatabase" }.Select(c => ("filters.xlsx", c, BuiltInNames)),
		.. new[] { "name 'Filtered (2)'!_FilterDatabase", "name 'Filtered (3)'!_FilterDatabase", "name Filtered!_FilterDatabase" }.Select(c => ("newsheet-look.xlsx", c, BuiltInNames)),
		("presentation.xlsx", "name Summary!_FilterDatabase", BuiltInNames),
		.. new[] { "name _xlfn.SINGLE", "name _xlfn.XLOOKUP" }.Select(c => ("formulas.xlsx", c, FutureNames)),
		.. new[] { "Sites!link B2", "Sites!link B3" }.Select(c => ("links-all.xlsx", c, FileLinks)),
	];

	[GridCraftFact]
	public void NamesItsVersion()
	{
		output.WriteLine(GridCraft.Version);

		Assert.StartsWith("gridcraft-cli ", GridCraft.Version, StringComparison.Ordinal);
	}

	[GridCraftTheory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheCorpusAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var workbook = GridCraft.Open(CorpusFiles.PathOf(file));
		var differences = new List<string>();
		var sheets = Sheets(answers.Sheets.Keys, workbook, null);
		var cells = GridCraft.Cells(workbook, sheets.ToDictionary(s => s.Value, s => answers.Sheets[s.Key].Keys.AsEnumerable(), StringComparer.Ordinal));
		foreach (var pair in sheets)
		{
			CompareCells(pair.Key, answers.Sheets[pair.Key], cells[pair.Value], true, differences);
		}

		AssertListed(file, differences, CorpusDifferences);
	}

	[GridCraftTheory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheCorpusStructuresAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var workbook = GridCraft.Open(CorpusFiles.PathOf(file));
		var hidden = GridCraft.Hidden(workbook);
		var differences = new List<string>();
		var sheets = Sheets(answers.Sheets.Keys, workbook, differences);
		foreach (var pair in sheets)
		{
			var (excel, grid) = (answers.Structures[pair.Key], workbook.Sheets.Single(s => s.Name == pair.Value));
			var prefix = SheetLocation.Quote(pair.Key) + "!";
			Compare(prefix + "merged", List(excel.Merged.OrderBy(m => m, StringComparer.Ordinal)), List(grid.Merged.OrderBy(m => m, StringComparer.Ordinal)), differences);
			Compare(prefix + "rows", List(excel.HiddenRows.Select(r => Run(r.First, r.Last))), List(Runs(hidden[pair.Value].Rows)), differences);
			Compare(
				prefix + "columns",
				List(excel.HiddenColumns.Select(c => Run(c.First, c.Last))),
				List(Runs(hidden[pair.Value].Columns.Select(c => (long)c + 1))),
				differences);
		}

		// Tables by name, with their sheets; Excel's ShowHeaders isn't one of GridCraft's answers.
		var excelTables = answers.Tables.ToDictionary(t => "table " + t.Name, t => $"{SheetLocation.Quote(t.Sheet)}!{t.Range}{(t.ShowTotals ? " totals" : "")} [{string.Join("|", t.Columns.Select(CellValue.Show))}]", StringComparer.Ordinal);
		var gridTables = sheets
			.SelectMany(p => workbook.Sheets.Single(s => s.Name == p.Value).Tables.Select(t => (Sheet: p.Key, Table: t)))
			.ToDictionary(t => "table " + t.Table.Name, t => $"{SheetLocation.Quote(t.Sheet)}!{t.Table.Range}{(t.Table.Totals ? " totals" : "")} [{string.Join("|", t.Table.Columns.Select(CellValue.Show))}]", StringComparer.Ordinal);
		CompareEntries(excelTables, gridTables, differences);

		// Names by name; whether Excel shows one isn't one of GridCraft's answers.
		CompareEntries(answers.Names.ToDictionary(n => "name " + n.Name, n => n.RefersTo, StringComparer.Ordinal), workbook.Names.ToDictionary(n => "name " + n.Name, n => n.RefersTo, StringComparer.Ordinal), differences);
		AssertListed(file, differences, CorpusStructureDifferences);
	}

	[GridCraftTheory]
	[MemberData(nameof(WrittenAnswersTests.Workbooks), MemberType = typeof(WrittenAnswersTests))]
	public void ReadsTheWrittenSetAsExcelDoes(string name)
	{
		var (written, answers) = WrittenAnswersTests.Comparable(name, output);
		if (answers is null)
		{
			return;
		}

		var workbook = GridCraft.Open(GridCraft.WrittenPath(written));
		var differences = new List<string>();
		var excel = answers.Sheets.ToDictionary(s => s.Name, s => s.Cells, StringComparer.Ordinal);
		var sheets = Sheets(excel.Keys, workbook, null);
		var cells = GridCraft.Cells(workbook, sheets.ToDictionary(s => s.Value, s => excel[s.Key].Keys.AsEnumerable(), StringComparer.Ordinal));
		foreach (var pair in sheets)
		{
			CompareCells(pair.Key, excel[pair.Key], cells[pair.Value], false, differences);
		}

		AssertListed(name, differences, WrittenDifferences);
	}

	[GridCraftTheory]
	[MemberData(nameof(WrittenAnswersTests.Workbooks), MemberType = typeof(WrittenAnswersTests))]
	public void ShowsTheWrittenLookAsExcelDoes(string name)
	{
		var (written, answers) = WrittenAnswersTests.Comparable(name, output);
		if (answers is null)
		{
			return;
		}

		var workbook = GridCraft.Open(GridCraft.WrittenPath(written));
		var hidden = GridCraft.Hidden(workbook);
		var differences = new List<string>();
		var sheets = Sheets(answers.Sheets.Select(s => s.Name), workbook, differences);

		// Each cell Excel answered about, and each linked one, for their links.
		var asked = sheets.ToDictionary(
			s => s.Value,
			s =>
			{
				var sheet = answers.Sheets.Single(a => a.Name == s.Key);

				return sheet.Cells.Keys.Concat(sheet.Look.Links.Select(l => Position(l.Cell)));
			},
			StringComparer.Ordinal);
		var cells = GridCraft.Cells(workbook, asked);
		foreach (var pair in sheets)
		{
			var (excel, grid) = (answers.Sheets.Single(s => s.Name == pair.Key), workbook.Sheets.Single(s => s.Name == pair.Value));
			var prefix = SheetLocation.Quote(pair.Key) + "!";
			Compare(prefix + "freeze", excel.Look.Freeze?.ToString() ?? "none", grid.Freeze?.ToString() ?? "none", differences);
			Compare(prefix + "filter", excel.Look.Filter ?? "none", grid.Filter ?? "none", differences);
			Compare(prefix + "merged", List(excel.Look.Merged.OrderBy(m => m, StringComparer.Ordinal)), List(grid.Merged.OrderBy(m => m, StringComparer.Ordinal)), differences);
			Compare(prefix + "hidden", List(excel.Look.HiddenColumns), List(hidden[pair.Value].Columns.Select(CellReference.ColumnName)), differences);

			// A table without a style has GridCraft's style "", and its stripes and buttons aren't GridCraft's answers.
			CompareEntries(
				excel.Look.Tables.ToDictionary(t => prefix + "table " + t.Name, t => $"{t.Range} style {t.Style ?? "none"} [{string.Join("|", t.Columns.Select(CellValue.Show))}]", StringComparer.Ordinal),
				grid.Tables.ToDictionary(t => prefix + "table " + t.Name, t => $"{t.Range} style {(t.Style.Length == 0 ? "none" : t.Style)} [{string.Join("|", t.Columns.Select(CellValue.Show))}]", StringComparer.Ordinal),
				differences);

			// A link as GridCraft gives it: its address and its fragment after a #.
			var links = excel.Look.Links.ToDictionary(l => Position(l.Cell), l => (l.Address ?? "") + (l.SubAddress is null ? "" : "#" + l.SubAddress));
			CompareEntries(
				links.ToDictionary(l => prefix + "link " + Reference(l.Key), l => l.Value, StringComparer.Ordinal),
				cells[pair.Value].Where(c => c.Value.Link is not null).ToDictionary(c => prefix + "link " + Reference(c.Key), c => c.Value.Link!, StringComparer.Ordinal),
				differences);
		}

		// Names by name; whether Excel shows one isn't one of GridCraft's answers.
		CompareEntries(answers.Names.ToDictionary(n => "name " + n.Name, n => n.RefersTo, StringComparer.Ordinal), workbook.Names.ToDictionary(n => "name " + n.Name, n => n.RefersTo, StringComparer.Ordinal), differences);
		AssertListed(name, differences, LookDifferences);
	}

	/// <summary>
	/// GridCraft's worksheet for each of Excel's: the one of its name, or the one whose name with its <c>_xHHHH_</c> escapes
	/// decoded is its name, since GridCraft keeps a name's escapes.
	/// </summary>
	/// <param name="names">Excel's worksheets' names.</param>
	/// <param name="workbook">The workbook as GridCraft reads it.</param>
	/// <param name="differences">Where a name that differs, a worksheet GridCraft lacks, and one Excel lacks are added; or <see langword="null"/>.</param>
	/// <returns>GridCraft's name of each of Excel's worksheets it has, by Excel's.</returns>
	private static Dictionary<string, string> Sheets(IEnumerable<string> names, GridCraftWorkbook workbook, List<string>? differences)
	{
		var sheets = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var name in names)
		{
			var grid = workbook.Sheets.FirstOrDefault(s => s.Name == name) ?? workbook.Sheets.FirstOrDefault(s => CorpusFiles.DecodeEscapes(s.Name) == name);
			if (grid is null)
			{
				differences?.Add($"{SheetLocation.Quote(name)}!sheet: GridCraft has no such worksheet");

				continue;
			}

			if (grid.Name != name)
			{
				differences?.Add($"{SheetLocation.Quote(name)}!sheet: GridCraft names it {CellValue.Show(grid.Name)}");
			}

			sheets[name] = grid.Name;
		}

		foreach (var extra in workbook.Sheets.Where(s => !sheets.ContainsValue(s.Name)))
		{
			differences?.Add($"{SheetLocation.Quote(extra.Name)}!sheet: Excel has no such worksheet");
		}

		return sheets;
	}

	/// <summary>Compares a worksheet's cells, each difference starting with its cell.</summary>
	/// <param name="sheet">Excel's name of the worksheet.</param>
	/// <param name="excel">Excel's cells.</param>
	/// <param name="grid">GridCraft's: those Excel answered about, and every other with a value.</param>
	/// <param name="cached">Whether Excel's values are the file's cached values (the corpus), which GridCraft can't show.</param>
	/// <param name="differences">The differences found.</param>
	private static void CompareCells(string sheet, Dictionary<(long Row, int Column), ExcelCell> excel, Dictionary<(long Row, int Column), GridCraftCell> grid, bool cached, List<string> differences)
	{
		foreach (var key in excel.Keys.Union(grid.Keys).OrderBy(k => k.Row).ThenBy(k => k.Column))
		{
			var location = new SheetLocation(sheet).Cell(key.Column, key.Row);
			var theirs = grid[key];
			if (!excel.TryGetValue(key, out var answer))
			{
				if (!(cached && theirs.Spilled))
				{
					differences.Add($"{location}: Excel no cell, GridCraft {Describe(theirs)}");
				}

				continue;
			}

			if (!Same(answer, theirs, cached))
			{
				differences.Add($"{location}: Excel {Describe(answer)}, GridCraft {Describe(theirs)}");
			}
		}
	}

	/// <summary>Whether GridCraft reads a cell as Excel does, by the rules in the class's remarks.</summary>
	/// <param name="excel">Excel's answer.</param>
	/// <param name="grid">GridCraft's.</param>
	/// <param name="cached">Whether Excel's value is the file's cached value.</param>
	/// <returns>Whether they are the same.</returns>
	private static bool Same(ExcelCell excel, GridCraftCell grid, bool cached)
	{
		if (excel.Formula != grid.Formula || Code(excel.NumberFormat) != Code(grid.NumberFormat))
		{
			return false;
		}

		if (cached && excel.Formula is not null)
		{
			return true;
		}

		var type = excel.Type is "date" or "currency" ? "number" : excel.Type;
		var value = type switch
		{
			"number" => Number(excel.Value2) == Number(grid.Value),
			"empty" => true,
			_ => excel.Value2 == grid.Value,
		};

		// Excel's Text stops at 8,221 characters.
		var shown = excel.Value2.Length > TextLimit && excel.Text.Length == TextLimit && grid.Text.Length > TextLimit ? grid.Text.Substring(0, TextLimit) : grid.Text;

		return type == grid.Type && value && (shown == excel.Text || (Hashes(shown) && Hashes(excel.Text)));
	}

	/// <summary>
	/// A number format's code as both can be compared: without <c>\</c> and quotes, in capitals (Excel's
	/// <c>NumberFormat</c> gives <c>yyyy</c> and <c>[Red]</c> for a file's <c>YYYY</c> and <c>[RED]</c>), and General for none.
	/// The text each shows is compared besides.
	/// </summary>
	/// <param name="code">The code.</param>
	/// <returns>The code compared.</returns>
	private static string Code(string code) => code.Length == 0 ? "GENERAL" : code.Replace("\\", "").Replace("\"", "").ToUpperInvariant();

	/// <summary>A number's text as a double, or <see cref="double.NaN"/> when it isn't one.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The number.</returns>
	private static double Number(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

	/// <summary>Whether a text is <c>#</c> signs alone, as Excel shows a number its column is too narrow for.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether it is.</returns>
	private static bool Hashes(string text) => text.Length > 0 && text.All(c => c == '#');

	/// <summary>Excel's answer about a cell, for a message.</summary>
	/// <param name="cell">The answer.</param>
	/// <returns>Its description.</returns>
	private static string Describe(ExcelCell cell) =>
		$"{cell.Type} {CellValue.Show(cell.Value2)} shown {CellValue.Show(cell.Text)} in {CellValue.Show(cell.NumberFormat)}{(cell.Formula is null ? "" : " formula " + CellValue.Show(cell.Formula))}";

	/// <summary>GridCraft's cell, for a message.</summary>
	/// <param name="cell">The cell.</param>
	/// <returns>Its description.</returns>
	private static string Describe(GridCraftCell cell) =>
		$"{cell.Type} {CellValue.Show(cell.Value)} shown {CellValue.Show(cell.Text)} in {CellValue.Show(cell.NumberFormat)}{(cell.Formula is null ? "" : " formula " + CellValue.Show(cell.Formula))}";

	/// <summary>Adds a difference, starting with what it is about, when two texts differ.</summary>
	/// <param name="key">What they are about.</param>
	/// <param name="excel">Excel's.</param>
	/// <param name="grid">GridCraft's.</param>
	/// <param name="differences">The differences found.</param>
	private static void Compare(string key, string excel, string grid, List<string> differences)
	{
		if (excel != grid)
		{
			differences.Add($"{key}: Excel {excel}, GridCraft {grid}");
		}
	}

	/// <summary>Compares entries by what each is about, one missing on either side being a difference.</summary>
	/// <param name="excel">Excel's entries.</param>
	/// <param name="grid">GridCraft's.</param>
	/// <param name="differences">The differences found.</param>
	private static void CompareEntries(Dictionary<string, string> excel, Dictionary<string, string> grid, List<string> differences)
	{
		foreach (var key in excel.Keys.Union(grid.Keys).OrderBy(k => k, StringComparer.Ordinal))
		{
			Compare(key, excel.TryGetValue(key, out var e) ? CellValue.Show(e) : "nothing", grid.TryGetValue(key, out var g) ? CellValue.Show(g) : "nothing", differences);
		}
	}

	/// <summary>Items as a list for a message.</summary>
	/// <param name="items">The items.</param>
	/// <returns>The list, such as <c>[A1:B2, C3:D4]</c>.</returns>
	private static string List(IEnumerable<string> items) => "[" + string.Join(", ", items) + "]";

	/// <summary>A run of rows or columns as Excel's answers give it.</summary>
	/// <param name="first">The first, from 1.</param>
	/// <param name="last">The last.</param>
	/// <returns>The run, such as <c>3-5</c>, or <c>3</c> for one.</returns>
	private static string Run(long first, long last) => first == last ? first.ToString(CultureInfo.InvariantCulture) : $"{first.ToString(CultureInfo.InvariantCulture)}-{last.ToString(CultureInfo.InvariantCulture)}";

	/// <summary>Numbers as runs.</summary>
	/// <param name="numbers">The numbers, in order.</param>
	/// <returns>The runs, as <see cref="Run"/> gives them.</returns>
	private static IEnumerable<string> Runs(IEnumerable<long> numbers)
	{
		var runs = new List<(long First, long Last)>();
		foreach (var number in numbers)
		{
			if (runs.Count > 0 && runs[runs.Count - 1].Last == number - 1)
			{
				runs[runs.Count - 1] = (runs[runs.Count - 1].First, number);
			}
			else
			{
				runs.Add((number, number));
			}
		}

		return runs.Select(r => Run(r.First, r.Last));
	}

	/// <summary>A cell's reference.</summary>
	/// <param name="key">Its row and 0-based column.</param>
	/// <returns>The reference, such as <c>B12</c>.</returns>
	private static string Reference((long Row, int Column) key) => CellReference.ColumnName(key.Column) + key.Row.ToString(CultureInfo.InvariantCulture);

	/// <summary>A reference's row and 0-based column.</summary>
	/// <param name="reference">The reference, such as <c>B12</c>.</param>
	/// <returns>The row and column.</returns>
	private static (long Row, int Column) Position(string reference) => GridCraft.Position(reference);

	/// <summary>
	/// Fails unless the differences are those listed for the workbook, each found. A listed cell of <c>Sheet!</c> alone
	/// stands for everything of the sheet.
	/// </summary>
	/// <param name="name">The workbook.</param>
	/// <param name="differences">The differences, each starting with what it is about.</param>
	/// <param name="list">The differences listed.</param>
	private static void AssertListed(string name, List<string> differences, (string File, string Cell, string Reason)[] list)
	{
		static bool Matches(string difference, string cell) => difference.StartsWith(cell.EndsWith("!", StringComparison.Ordinal) ? cell : cell + ":", StringComparison.Ordinal);

		var listed = list.Where(d => d.File == name).ToList();
		var unexplained = differences.Where(d => !listed.Any(l => Matches(d, l.Cell))).ToList();
		var unseen = listed.Where(l => !differences.Any(d => Matches(d, l.Cell))).Select(l => l.Cell).ToList();
		Assert.True(unexplained.Count == 0 && unseen.Count == 0, $"{name}, {GridCraft.Version}: not listed:\n{string.Join("\n", unexplained)}\nlisted but not found: [{string.Join(", ", unseen)}]");
	}
}
