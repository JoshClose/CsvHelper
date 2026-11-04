using System.Text;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>A way of reading a sheet: a <see cref="ScanMode"/>, and the number of records after which a fill's target is full.</summary>
/// <param name="scan">How the part is read.</param>
/// <param name="recordsPerFill">The records a fill takes.</param>
public sealed class SheetMode(ScanMode scan, int recordsPerFill)
{
	/// <summary>Every scan mode, with fills that take every record and fills that take one.</summary>
	public static IReadOnlyList<SheetMode> All { get; } = [.. ScanMode.All.SelectMany(s => new[] { new SheetMode(s, int.MaxValue), new SheetMode(s, 1) })];

	/// <summary>How the part is read.</summary>
	public ScanMode Scan => scan;

	/// <summary>The records a fill takes.</summary>
	public int RecordsPerFill => recordsPerFill;

	/// <inheritdoc/>
	public override string ToString() => recordsPerFill == int.MaxValue ? scan.Name : $"{scan.Name}, {recordsPerFill} record per fill";
}

/// <summary>
/// How a sheet's typed cells are read: the workbook's styles and date system, the error cells' policy, and what cells with
/// a formula read as.
/// </summary>
/// <param name="styles">The workbook's styles, or <see langword="null"/> for none.</param>
/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
/// <param name="errorCells">How error cells read.</param>
/// <param name="formulas">What cells with a formula read as.</param>
internal sealed class CellReading(
	StyleTable? styles = null, bool date1904 = false, ExcelErrorCells errorCells = ExcelErrorCells.Text, ExcelFormulas formulas = ExcelFormulas.CachedValue)
{
	/// <summary>No styles, the 1900 date system, error cells as text, and formulas as their values.</summary>
	public static CellReading Default { get; } = new();

	/// <summary>No styles, the 1900 date system, error cells as text, and formulas as text.</summary>
	public static CellReading FormulaText { get; } = new(formulas: ExcelFormulas.FormulaText);

	/// <summary>The workbook's styles.</summary>
	public StyleTable Styles => styles ?? StyleTable.Empty;

	/// <summary>Whether the workbook uses the 1904 date system.</summary>
	public bool Date1904 => date1904;

	/// <summary>How error cells read.</summary>
	public ExcelErrorCells ErrorCells => errorCells;

	/// <summary>What cells with a formula read as.</summary>
	public ExcelFormulas Formulas => formulas;
}

/// <summary>Reads sheets with <see cref="SheetScanner"/> into <see cref="Captured"/> records.</summary>
internal static class Sheets
{
	/// <summary>The part name of the tests' sheets: <see cref="TestPackage.Workbook(TestSheet[])"/>'s first sheet.</summary>
	public const string PartName = "xl/worksheets/sheet1.xml";

	/// <summary>A worksheet part as Excel writes one, around these rows.</summary>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <returns>The part's XML.</returns>
	public static string Worksheet(string rows) =>
		TestPackage.Declaration +
		$"<worksheet xmlns=\"{TestPackage.MainNamespace}\" xmlns:r=\"{TestPackage.RelationshipsNamespace}\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">" +
		"<dimension ref=\"A1:C2\"/><sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"><selection activeCell=\"A1\" sqref=\"A1\"/></sheetView></sheetViews>" +
		"<sheetFormatPr defaultRowHeight=\"14.4\" x14ac:dyDescent=\"0.3\"/><cols><col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/></cols>" +
		$"<sheetData>{rows}</sheetData>" +
		"<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>";

	/// <summary>Shared strings with these texts.</summary>
	/// <param name="texts">The texts, plain.</param>
	/// <returns>The shared strings.</returns>
	public static SharedStrings Strings(params string[] texts)
	{
		var items = texts.Select(t => t.Length == 0 ? "<si><t/></si>" : $"<si><t xml:space=\"preserve\">{Escape(t)}</t></si>");
		var bytes = Encoding.UTF8.GetBytes($"<sst xmlns=\"{TestPackage.MainNamespace}\">{string.Concat(items)}</sst>");
		using var scanner = XmlScan.Open(bytes, ScanMode.All[0]);

		return SyncOrAsync.Result(SharedStrings.ReadAsync(scanner, bytes.Length, false, CancellationToken.None));
	}

	/// <summary>Escapes text for XML: <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c>.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The XML.</returns>
	public static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

	/// <summary>Opens a sheet scanner over a part's XML, as <paramref name="mode"/> says.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <param name="reading">The styles, date system, error policy and formulas, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>The scanner.</returns>
	public static SheetScanner Open(
		string xml, ScanMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20, CellReading? reading = null)
	{
		var bytes = Encoding.UTF8.GetBytes(xml);
		Stream stream = mode.Trickle ? new TrickleStream(bytes) : new MemoryStream(bytes);
		var scanner = new XmlScanner(new XmlWindow(stream, PartName, maxSize, mode.WindowSize));
		reading ??= CellReading.Default;

		return new SheetScanner(scanner, sheetName, strings ?? SharedStrings.Empty, reading.Styles, skipEmptyRows, null, reading.Date1904, reading.ErrorCells, reading.Formulas);
	}

	/// <summary>Reads a sheet's records, described (<see cref="CapturedRecord.ToString"/>).</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <param name="reading">The styles, date system, error policy and formulas, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>The records.</returns>
	public static async Task<List<string>> ReadAsync(
		string xml, SheetMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20, CellReading? reading = null)
	{
		var records = await ReadRecordsAsync(xml, mode, skipEmptyRows, strings, sheetName, maxSize, reading);

		return [.. records.Select(r => r.ToString())];
	}

	/// <summary>Reads a sheet's records.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <param name="reading">The styles, date system, error policy and formulas, or <see langword="null"/> for <see cref="CellReading.Default"/>.</param>
	/// <returns>The records.</returns>
	public static async Task<List<CapturedRecord>> ReadRecordsAsync(
		string xml, SheetMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20, CellReading? reading = null)
	{
		using var sheet = Open(xml, mode.Scan, skipEmptyRows, strings, sheetName, maxSize, reading);
		var captured = new Captured(mode.RecordsPerFill);
		await FillAllAsync(sheet, captured, mode.Scan.Async);

		return captured.Records;
	}

	/// <summary>Fills until the sheet has no more records, checking each fill.</summary>
	/// <param name="sheet">The scanner.</param>
	/// <param name="captured">Takes the records.</param>
	/// <param name="async">Whether to fill asynchronously.</param>
	/// <returns>A task that completes after the last fill.</returns>
	public static async Task FillAllAsync(SheetScanner sheet, Captured captured, bool async)
	{
		var target = new CapturingTarget(captured);
		while (true)
		{
			captured.StartFill();
			var more = async ? await sheet.FillAsync(target, CancellationToken.None) : sheet.Fill(target);
			captured.EndFill(more);
			if (!more)
			{
				return;
			}
		}
	}

	/// <summary>
	/// Reads the first sheet of a package through the package layer, so that gap fields, and shared strings and moved
	/// shared formulas longer than their cells, count toward the workbook's uncompressed size.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="limits">The limits of the read.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="sharedStrings">Whether to load the package's shared strings first, which count toward the total too.</param>
	/// <param name="formulas">What cells with a formula read as.</param>
	/// <returns>The records, and the workbook's uncompressed total after them.</returns>
	public static async Task<(List<CapturedRecord> Records, long Total)> ReadPackageAsync(
		TestPackage package, bool async, PartLimits limits, bool skipEmptyRows = true, bool sharedStrings = false, ExcelFormulas formulas = ExcelFormulas.CachedValue)
	{
		using var archive = await package.OpenAsync(async);
		var strings = sharedStrings ? await Packages.Run(async, a => SharedStrings.LoadAsync(archive, "xl/sharedStrings.xml", limits, a, CancellationToken.None)) : SharedStrings.Empty;
		var stream = await Packages.Run(async, a => archive.OpenPartAsync(PartName, false, limits, a, CancellationToken.None));
		using var sheet = new SheetScanner(new XmlScanner(new XmlWindow(stream, PartName, limits.MaxPartSize)), "Data", strings, StyleTable.Empty, skipEmptyRows, stream, formulas: formulas);
		var captured = new Captured(int.MaxValue);
		await FillAllAsync(sheet, captured, async);

		return (captured.Records, archive.Guard.Total);
	}

	/// <summary>Runs <paramref name="test"/> in every <see cref="SheetMode"/>, and says which mode failed.</summary>
	/// <param name="test">The test, for one mode.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	public static async Task InEveryModeAsync(Func<SheetMode, Task> test)
	{
		foreach (var mode in SheetMode.All)
		{
			try
			{
				await test(mode);
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"In mode '{mode}': {e.Message}", e);
			}
		}
	}
}
