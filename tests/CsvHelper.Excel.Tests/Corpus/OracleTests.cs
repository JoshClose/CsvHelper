namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Every workbook of the corpus, read by CsvHelper.Excel and by each oracle, cell by cell. CsvHelper.Excel must match the
/// Open XML SDK's raw cells read by its documented rules exactly. ClosedXML and ExcelDataReader must agree with it except
/// in the cells listed here, each with its reason: their dates as <see cref="DateTime"/> and durations as
/// <see cref="TimeSpan"/> against CsvHelper.Excel's conversions, and ClosedXML's <c>FormulaA1</c> against the formulas
/// read as text. Excel's own answers (<see cref="ExcelAnswersTests"/>) side with CsvHelper.Excel in each listed cell,
/// unless that test lists it too.
/// </summary>
public class OracleTests
{
	/// <summary>ClosedXML returns a line break of an inline string or a <c>str</c> value as CR LF.</summary>
	private const string LineBreaks = "ClosedXML returns the line break of an inline or str text as CR LF; Excel keeps it LF";

	private const string TimesAsTimeSpans = "ClosedXML reads a time of day (h:mm:ss) as a TimeSpan, as it does elapsed time; it's a time, not a duration";

	/// <summary>ClosedXML doesn't decode the escapes of an inline string or a <c>str</c> value.</summary>
	private const string Escapes = "ClosedXML doesn't decode the _xHHHH_ escapes of an inline or str text; Excel decodes them";

	/// <summary>ClosedXML keeps the white space around a text without <c>xml:space="preserve"</c>.</summary>
	private const string Spaces = "ClosedXML keeps the white space around a text that xml:space=\"preserve\" doesn't apply to; Excel trims it";

	/// <summary>ExcelDataReader reads an ISO 8601 date and time as a <see cref="DateTimeOffset"/>, and puts a time alone on today's date.</summary>
	private const string LocalDateTimes = "ExcelDataReader reads a t=\"d\" date and time as a DateTimeOffset in the local time zone, a time alone on today's date, and a date with 17 digits of fraction, or in a format it doesn't take for a date, as text";

	private const string IdsSeventyNineAndEighty = "ExcelDataReader takes built-in ids 79 for elapsed time and 80 for a time of day; Excel shows 79 as mm:ss and 80 as [h]:mm:ss";

	private const string FormatSections = "ExcelDataReader classifies e (an era's year) as a number, yyyy-mm-dd [h]:mm as elapsed time and 0;yyyy as a date; Excel takes the first section only, a date part over elapsed time, and e for a date";

	private const string UnroundedTimeSpans = "ExcelDataReader's TimeSpan isn't rounded to the millisecond on .NET (00:59:59.9999999 for 0.041666666666666664 days), where TimeSpan.FromDays doesn't round; CsvHelper.Excel's is, as Excel shows it";

	private const string FrameworkOverflow = "a number past the largest double, from a producer's 15 or 16 digits: .NET Framework's double.Parse throws on it, so ExcelDataReader there gives its text, where .NET gives infinity, as CsvHelper.Excel's number does";

	private const string SerialSixty = "serial 60 is 1900-02-29, which Excel shows and no DateTime has: ExcelDataReader gives 1900-02-28, CsvHelper.Excel no date (decision 10)";

	private const string PastTheLastDay = "a serial past the date system's last day, 9999-12-31: ExcelDataReader gives the number, CsvHelper.Excel a date cell that no date target takes";

	private const string NegativeSerials = "a negative serial: ExcelDataReader gives a date before the date system's day 0, CsvHelper.Excel none, as Excel shows none in the 1900 system";

	private const string DateCompatibility = "a Strict workbook (dateCompatibility=\"0\") counts days as OLE Automation dates do; its 1899-12-30, which the 1900 system lacks, stays text";

	private const string NumberCellText = "ClosedXML wrote NA()'s result #N/A as a number cell: ExcelDataReader reads it as text, CsvHelper.Excel as the number cell it is";

	private const string ArrayFormulas = "ClosedXML repeats a legacy array formula in each of its cells; CsvHelper.Excel reads the other cells' values, which the file gives them (decision 16)";

	private const string ComputedResults = "LibreOffice computes a formula's result itself, and shows its own error for FILTER of nothing (Err:533)";

	/// <summary>LibreOffice doesn't decode the escapes of a <c>str</c> value.</summary>
	private const string StrEscapes = "LibreOffice doesn't decode the _xHHHH_ escapes of a str value; Excel decodes them";

	/// <summary>LibreOffice keeps the white space around a text without <c>xml:space="preserve"</c>.</summary>
	private const string KeptSpaces = "LibreOffice keeps the white space around a text that xml:space=\"preserve\" doesn't apply to; Excel trims it";

	/// <summary>ExcelDataReader reads only <c>1</c> as true.</summary>
	private const string Booleans = "the Open XML SDK writes booleans as true and false; ExcelDataReader reads only 1 as true, and Excel reads both";

	/// <summary>
	/// The workbook whose zip .NET Framework's <c>System.IO.Packaging</c> (WindowsBase), which the Open XML SDK and
	/// ClosedXML use there, rejects: POI's SXSSF writes a Zip64 data descriptor after each entry, while the local headers
	/// declare version 45 without a Zip64 field and the central directory version 20. Excel, .NET's packaging and
	/// <c>ZipArchive</c> read it.
	/// </summary>
	private const string Zip64Descriptors = "poi/sxssf.xlsx";

	/// <summary>
	/// The workbook whose relationships <c>System.IO.Packaging</c> rejects on every target: Google Sheets wrote a
	/// relationship with no <c>Id</c> or <c>Target</c> in its pivot cache's relationships (POI's bug 64450). Excel opens it;
	/// CsvHelper.Excel ignores such a relationship, and doesn't read pivot caches.
	/// </summary>
	private const string RelationshipWithoutId = "google/64450.xlsx";

	/// <summary>The files whose Data!B33 is a time of day (<c>h:mm:ss</c>), which ClosedXML reads as a <see cref="TimeSpan"/>.</summary>
	private static readonly string[] TimeOfDayFiles =
	[
		"closedxml/basic.xlsx", "epplus/basic.xlsx", "excel/basic.xlsx", "excel/date1904.xlsx", "excel/macro.xlsm", "excel/template.xltx",
		"largexlsx/basic.xlsx", "libreoffice/basic-ooxml.xlsx", "libreoffice/basic.xlsx", "openpyxl/basic.xlsx", "openxmlsdk/basic.xlsx",
		"poi/sxssf.xlsx", "poi/xssf.xlsx", "spreadcheetah/basic.xlsx", "xlsxwriter/basic.xlsx", "xlsxwriter/constant_memory.xlsx",
	];

	/// <summary>The cells where ClosedXML reads the corpus otherwise, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ClosedXmlDifferences =
	[
		.. TimeOfDayFiles.Select(f => (f, "Data!B33", TimesAsTimeSpans)),
		("largexlsx/basic.xlsx", "Data!B18", LineBreaks),
		("largexlsx/basic.xlsx", "Data!B20", Escapes),
		("miniexcel/basic.xlsx", "Data!B16", Escapes),
		("miniexcel/basic.xlsx", "Data!B19", Escapes),
		("miniexcel/basic.xlsx", "Data!B20", Escapes),
		("openpyxl/basic.xlsx", "Data!B18", LineBreaks),
		("openpyxl/basic.xlsx", "Data!B20", Escapes),
		("pandas/openpyxl.xlsx", "Data!B17", LineBreaks),
		("pandas/openpyxl.xlsx", "Data!B19", Escapes),
		("poi/sxssf.xlsx", "Data!B18", LineBreaks),
		("poi/sxssf.xlsx", "Data!B20", Escapes),
		("spreadcheetah/basic.xlsx", "Data!B17", Spaces),
		("spreadcheetah/basic.xlsx", "Data!B18", LineBreaks),
		("spreadcheetah/basic.xlsx", "Data!B20", Escapes),
		("xlsxwriter/constant_memory.xlsx", "Data!B18", LineBreaks),
		("xlsxwriter/constant_memory.xlsx", "Data!B19", Escapes),
		("xlsxwriter/constant_memory.xlsx", "Data!B20", Escapes),
	];

	/// <summary>
	/// The workbooks ClosedXML 0.105.1 can't open, with the exception it throws: its Strict t="d" parser, which takes
	/// neither Excel's 17 digits of fraction nor openpyxl's dates without a fraction; serial 60 in a date format, which it
	/// rejects in the 1904 date system too; and a number past the largest double, from the producers that write 15 or 16
	/// digits. CsvHelper.Excel reads each.
	/// </summary>
	private static readonly Dictionary<string, string> ClosedXmlRejects = new()
	{
		["excel/strict.xlsx"] = "System.FormatException",
		["excel/typed-strict.xlsx"] = "System.FormatException",
		["openpyxl/typed-isodates.xlsx"] = "System.FormatException",
		["excel/typed.xlsx"] = "System.ArgumentException",
		["excel/typed-1904.xlsx"] = "System.ArgumentException",
		["libreoffice/typed.xlsx"] = "System.ArgumentException",
		["poi/typed.xlsx"] = "System.ArgumentException",
		["closedxml/typed.xlsx"] = "System.ArgumentException",
		["openpyxl/typed.xlsx"] = "System.ArgumentException",
		["xlsxwriter/typed.xlsx"] = "System.ArgumentException",
	};

	/// <summary>The formula cells where ClosedXML's <c>FormulaA1</c> differs from the formula read as text, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ClosedXmlFormulaDifferences =
	[
		("google/66365.xlsx", "Sheet1!B2", ArrayFormulas),
	];

	/// <summary>The typed workbooks' "Dates" cells where ExcelDataReader's formats, serial 60, the limits and negatives differ, for every producer.</summary>
	private static readonly (string Cell, string Reason)[] TypedDates =
	[
		("Dates!B67", FormatSections), ("Dates!B68", FormatSections), ("Dates!B75", FormatSections),
		("Dates!B95", SerialSixty), ("Dates!B96", SerialSixty), ("Dates!B100", PastTheLastDay), ("Dates!B101", NegativeSerials),
	];

	/// <summary>The cells where ExcelDataReader reads the corpus otherwise, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ExcelDataReaderDifferences =
	[
		("excel/strict.xlsx", "Data!B32", LocalDateTimes),
		("excel/strict.xlsx", "Data!B33", LocalDateTimes),
		("openxmlsdk/basic.xlsx", "Data!B8", Booleans),
		("openxmlsdk/basic.xlsx", "'Q1 Orders'!D2", Booleans),
		("openxmlsdk/basic.xlsx", "'Q1 Orders'!D4", Booleans),
		.. new[] { "excel/typed.xlsx", "closedxml/typed.xlsx", "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx", "poi/typed.xlsx", "xlsxwriter/typed.xlsx" }
			.SelectMany(f => TypedDates.Select(d => (f, d.Cell, d.Reason))),
		.. TypedDates.Where(d => d.Cell != "Dates!B68").Select(d => ("libreoffice/typed.xlsx", d.Cell, d.Reason)),
		("excel/typed-1904.xlsx", "Dates!B67", FormatSections),
		("excel/typed-1904.xlsx", "Dates!B68", FormatSections),
		("excel/typed-1904.xlsx", "Dates!B75", FormatSections),
		("excel/typed-1904.xlsx", "Dates!B98", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B99", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B100", PastTheLastDay),
		("excel/typed-1904.xlsx", "Dates!B101", NegativeSerials),
		.. new[] { "closedxml/typed.xlsx", "poi/typed.xlsx", "xlsxwriter/typed.xlsx" }
			.SelectMany(f => new[] { (f, "Dates!B41", IdsSeventyNineAndEighty), (f, "Dates!B42", IdsSeventyNineAndEighty) }),
#if NETFRAMEWORK
		.. new[] { "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx", "xlsxwriter/typed.xlsx", "closedxml/typed.xlsx" }
			.Select(f => (f, "Numbers!B21", FrameworkOverflow)),
#else
		.. new[] { "openpyxl/typed.xlsx", "openpyxl/typed-isodates.xlsx", "xlsxwriter/typed.xlsx" }
			.SelectMany(f => new[] { (f, "Dates!B87", UnroundedTimeSpans), (f, "Dates!B90", UnroundedTimeSpans) }),
#endif
		.. Enumerable.Range(107, 6).Select(r => ("openpyxl/typed-isodates.xlsx", $"Dates!B{r}", LocalDateTimes)),
		("closedxml/typed.xlsx", "Numbers!B38", NumberCellText),
		.. new[] { 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 75, 92, 96, 99, 102, 103, 104, 105, 106, 107, 108 }
			.Select(r => ("excel/typed-strict.xlsx", $"Dates!B{r}", LocalDateTimes)),
		("excel/typed-strict.xlsx", "Dates!B68", FormatSections),
		("excel/typed-strict.xlsx", "Dates!B91", DateCompatibility),
		("excel/typed-strict.xlsx", "Dates!B100", PastTheLastDay),
		("excel/typed-strict.xlsx", "Dates!B101", NegativeSerials),
	];

	/// <summary>The text cells LibreOffice exports otherwise (shown text, with <c>--convert-to csv</c>), and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeDifferences =
	[
		("miniexcel/basic.xlsx", "Data!B16", StrEscapes),
		("miniexcel/basic.xlsx", "Data!B19", StrEscapes),
		("miniexcel/basic.xlsx", "Data!B20", StrEscapes),
		("spreadcheetah/basic.xlsx", "Data!B17", KeptSpaces),
		("excel/typed-strict.xlsx", "Dates!B91", DateCompatibility),
		("xlsxwriter/typed.xlsx", "Numbers!B45", ComputedResults),
	];

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesTheOpenXmlSdksCells(string file)
	{
		var ours = CorpusFiles.WithoutDates(CorpusFiles.Read(file, out var date1904));
		if (RejectsThePackage(file, () => Oracles.OpenXmlSdk(file, date1904)))
		{
			return;
		}

		var sdk = CorpusFiles.WithoutDates(Oracles.OpenXmlSdk(file, date1904));

		Assert.Equal(sdk.Keys.OrderBy(k => k, StringComparer.Ordinal), ours.Keys.OrderBy(k => k, StringComparer.Ordinal));
		var differences = CorpusFiles.Differences(ours, sdk, ours.Keys);
		Assert.True(differences.Count == 0, $"{file}:\n" + string.Join("\n", differences));
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesClosedXmlsValues(string file)
	{
		var ours = CorpusFiles.AsDotNet(file, CorpusFiles.Read(file, out _));
		if (ClosedXmlRejects.TryGetValue(file, out var rejection))
		{
			Assert.Equal(rejection, Assert.ThrowsAny<Exception>(() => Oracles.ClosedXml(file)).GetType().FullName);

			return;
		}

		if (RejectsThePackage(file, () => Oracles.ClosedXml(file)))
		{
			return;
		}

		var closedXml = Oracles.ClosedXml(file);
		AssertDifferences(file, ClosedXmlDifferences, CorpusFiles.Differences(ours, closedXml, ours.Keys.Intersect(closedXml.Keys)));

		// The formulas: FormulaA1, with its =, against the formulas read as text, a shared formula's moved to each cell.
		var formulas = new SheetCells();
		foreach (var sheet in CorpusFiles.Read(file, out _, ExcelFormulas.FormulaText))
		{
			formulas[sheet.Key] = sheet.Value.Where(c => c.Value.Kind == SheetCellKind.Text && c.Value.Text.StartsWith("=", StringComparison.Ordinal)).ToDictionary(c => c.Key, c => c.Value);
		}

		var closedXmlFormulas = Oracles.ClosedXmlFormulas(file);
		AssertDifferences(file, ClosedXmlFormulaDifferences, CorpusFiles.Differences(formulas, closedXmlFormulas, formulas.Keys.Intersect(closedXmlFormulas.Keys)));
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesExcelDataReadersValues(string file)
	{
		var ours = CorpusFiles.AsDotNet(file, CorpusFiles.Read(file, out _));
		var excelDataReader = Oracles.ExcelDataReader(file);

		AssertDifferences(file, ExcelDataReaderDifferences, CorpusFiles.Differences(ours, excelDataReader, ours.Keys.Intersect(excelDataReader.Keys)));
	}

	[LibreOfficeTheory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesLibreOfficesTextCells(string file)
	{
		var ours = CorpusFiles.Read(file, out _);
		var differences = new List<string>();
		foreach (var pair in ours)
		{
			var sheet = pair.Key;
			var records = LibreOffice.Sheet(file, sheet);
			if (records is null)
			{
				differences.Add($"{new SheetLocation(sheet).QuotedName}: ours exported, theirs not");

				continue;
			}

			foreach (var cell in pair.Value.Where(c => c.Value.Kind == SheetCellKind.Text).OrderBy(c => c.Key.Row).ThenBy(c => c.Key.Column))
			{
				var (row, column) = cell.Key;
				var theirs = row - 1 < records.Count && column < records[(int)row - 1].Length ? records[(int)row - 1][column] : "(none)";
				if (theirs != cell.Value.Text)
				{
					differences.Add($"{new SheetLocation(sheet).Cell(column, row)}: ours {CellValue.Show(cell.Value.Text)}, theirs {CellValue.Show(theirs)}");
				}
			}
		}

		AssertDifferences(file, LibreOfficeDifferences, differences);
	}

	/// <summary>
	/// Checks that an oracle built on <c>System.IO.Packaging</c> (the Open XML SDK, ClosedXML) rejects the workbooks it
	/// can't open here, with the exception it throws for each: <see cref="RelationshipWithoutId"/> everywhere, and
	/// <see cref="Zip64Descriptors"/> on .NET Framework. CsvHelper.Excel has read the workbook by then.
	/// </summary>
	/// <param name="file">The workbook.</param>
	/// <param name="read">Reads the workbook with the oracle.</param>
	/// <returns>Whether the oracle can't read the workbook here, so that there is nothing to compare.</returns>
	private static bool RejectsThePackage(string file, Action read)
	{
		var expected = file switch
		{
			RelationshipWithoutId => "System.Xml.XmlException",
#if NETFRAMEWORK
			Zip64Descriptors => "System.IO.FileFormatException",
#endif
			_ => null,
		};
		if (expected is null)
		{
			return false;
		}

		var e = Assert.ThrowsAny<Exception>(read);
		Assert.Equal(expected, e.GetType().FullName);

		return true;
	}

	/// <summary>Checks that an oracle differs from CsvHelper.Excel in exactly the cells listed for a file.</summary>
	/// <param name="file">The workbook.</param>
	/// <param name="expected">The cells where the oracle differs, by file.</param>
	/// <param name="differences">The differences found, each starting with its cell's location.</param>
	private static void AssertDifferences(string file, (string File, string Cell, string Reason)[] expected, List<string> differences)
	{
		var cells = expected.Where(e => e.File == file).Select(e => e.Cell).OrderBy(c => c, StringComparer.Ordinal).ToList();
		var found = differences.Select(d => d.Substring(0, d.IndexOf(": ours", StringComparison.Ordinal))).OrderBy(c => c, StringComparer.Ordinal).ToList();

		Assert.True(cells.SequenceEqual(found), $"{file}: expected differences in [{string.Join(", ", cells)}], found:\n" + string.Join("\n", differences));
	}
}
