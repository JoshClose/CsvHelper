namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Every workbook of the corpus, read by CsvHelper.Excel and by each oracle, cell by cell. CsvHelper.Excel must match the
/// Open XML SDK's raw cells read by its documented rules exactly. ClosedXML and ExcelDataReader must agree with it except
/// in the cells listed here, each with its reason; Excel 365 reads each of those as CsvHelper.Excel does (checked through
/// COM on 2026-11-02), apart from the <c>t="d"</c> cells, which Phase 1 leaves as text.
/// </summary>
public class OracleTests
{
	/// <summary>ClosedXML returns a line break of an inline string or a <c>str</c> value as CR LF.</summary>
	private const string LineBreaks = "ClosedXML returns the line break of an inline or str text as CR LF; Excel keeps it LF";

	/// <summary>ClosedXML doesn't decode the escapes of an inline string or a <c>str</c> value.</summary>
	private const string Escapes = "ClosedXML doesn't decode the _xHHHH_ escapes of an inline or str text; Excel decodes them";

	/// <summary>ClosedXML keeps the white space around a text without <c>xml:space="preserve"</c>.</summary>
	private const string Spaces = "ClosedXML keeps the white space around a text that xml:space=\"preserve\" doesn't apply to; Excel trims it";

	/// <summary>ExcelDataReader converts ISO 8601 date cells, which Phase 1 reads as text.</summary>
	private const string DateCells = "ExcelDataReader converts t=\"d\" ISO 8601 cells to dates; Phase 1 reads them as text (Phase 2 converts them)";

	/// <summary>LibreOffice shows ISO 8601 date cells as dates in their formats.</summary>
	private const string ShownDates = "LibreOffice shows t=\"d\" ISO 8601 cells formatted as dates; Phase 1 reads them as text";

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

	/// <summary>The cells where ClosedXML reads the corpus otherwise, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ClosedXmlDifferences =
	[
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

	/// <summary>The cells where ExcelDataReader reads the corpus otherwise, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] ExcelDataReaderDifferences =
	[
		("excel/strict.xlsx", "Data!B31", DateCells),
		("excel/strict.xlsx", "Data!B32", DateCells),
		("excel/strict.xlsx", "Data!B33", DateCells),
		("excel/strict.xlsx", "Data!B34", DateCells),
		("excel/strict.xlsx", "'Q1 Orders'!E2", DateCells),
		("excel/strict.xlsx", "'Q1 Orders'!E3", DateCells),
		("excel/strict.xlsx", "'Q1 Orders'!E4", DateCells),
		("openxmlsdk/basic.xlsx", "Data!B8", Booleans),
		("openxmlsdk/basic.xlsx", "'Q1 Orders'!D2", Booleans),
		("openxmlsdk/basic.xlsx", "'Q1 Orders'!D4", Booleans),
	];

	/// <summary>The text cells LibreOffice exports otherwise (shown text, with <c>--convert-to csv</c>), and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeDifferences =
	[
		("excel/strict.xlsx", "Data!B31", ShownDates),
		("excel/strict.xlsx", "Data!B32", ShownDates),
		("excel/strict.xlsx", "Data!B34", ShownDates),
		("miniexcel/basic.xlsx", "Data!B16", StrEscapes),
		("miniexcel/basic.xlsx", "Data!B19", StrEscapes),
		("miniexcel/basic.xlsx", "Data!B20", StrEscapes),
		("spreadcheetah/basic.xlsx", "Data!B17", KeptSpaces),
	];

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesTheOpenXmlSdksCells(string file)
	{
		var ours = CorpusFiles.Read(file, out _);
		if (RejectsTheZip(file, () => Oracles.OpenXmlSdk(file)))
		{
			return;
		}

		var sdk = Oracles.OpenXmlSdk(file);

		Assert.Equal(sdk.Keys.OrderBy(k => k, StringComparer.Ordinal), ours.Keys.OrderBy(k => k, StringComparer.Ordinal));
		var differences = CorpusFiles.Differences(ours, sdk, ours.Keys);
		Assert.True(differences.Count == 0, $"{file}:\n" + string.Join("\n", differences));
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesClosedXmlsValues(string file)
	{
		var ours = CorpusFiles.Read(file, out var date1904);
		if (file == "excel/strict.xlsx")
		{
			// ClosedXML 0.105.1 can't parse the t="d" dates Excel writes in a Strict workbook (17 digits of fraction).
			Assert.Throws<FormatException>(() => Oracles.ClosedXml(file, date1904));

			return;
		}

		if (RejectsTheZip(file, () => Oracles.ClosedXml(file, date1904)))
		{
			return;
		}

		var closedXml = Oracles.ClosedXml(file, date1904);
		AssertDifferences(file, ClosedXmlDifferences, CorpusFiles.Differences(ours, closedXml, ours.Keys.Intersect(closedXml.Keys)));
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void MatchesExcelDataReadersValues(string file)
	{
		var ours = CorpusFiles.Read(file, out var date1904);
		var excelDataReader = Oracles.ExcelDataReader(file, date1904);

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
	/// On .NET Framework, checks that an oracle built on <c>System.IO.Packaging</c> rejects <see cref="Zip64Descriptors"/>
	/// as corrupt, as WindowsBase does; CsvHelper.Excel has read it by then.
	/// </summary>
	/// <param name="file">The workbook.</param>
	/// <param name="read">Reads the workbook with the oracle.</param>
	/// <returns>Whether the oracle can't read the workbook here, so that there is nothing to compare.</returns>
	private static bool RejectsTheZip(string file, Action read)
	{
#if NETFRAMEWORK
		if (file == Zip64Descriptors)
		{
			var e = Assert.ThrowsAny<FormatException>(read);
			Assert.Equal("System.IO.FileFormatException", e.GetType().FullName);

			return true;
		}
#endif

		return false;
	}

	/// <summary>Checks that an oracle differs from CsvHelper.Excel in exactly the cells listed for a file.</summary>
	/// <param name="file">The workbook.</param>
	/// <param name="expected">The cells where the oracle differs, by file.</param>
	/// <param name="differences">The differences found, each starting with its cell's location.</param>
	private static void AssertDifferences(string file, (string File, string Cell, string Reason)[] expected, List<string> differences)
	{
		var cells = expected.Where(e => e.File == file).Select(e => e.Cell).ToList();
		var found = differences.Select(d => d.Substring(0, d.IndexOf(": ours", StringComparison.Ordinal))).ToList();

		Assert.True(cells.SequenceEqual(found), $"{file}: expected differences in [{string.Join(", ", cells)}], found:\n" + string.Join("\n", differences));
	}
}
