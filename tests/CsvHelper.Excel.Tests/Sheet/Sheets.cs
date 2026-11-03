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
	/// <returns>The scanner.</returns>
	public static SheetScanner Open(string xml, ScanMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20)
	{
		var bytes = Encoding.UTF8.GetBytes(xml);
		Stream stream = mode.Trickle ? new TrickleStream(bytes) : new MemoryStream(bytes);
		var scanner = new XmlScanner(new XmlWindow(stream, PartName, maxSize, mode.WindowSize));

		return new SheetScanner(scanner, sheetName, strings ?? SharedStrings.Empty, StyleTable.Empty, skipEmptyRows, null);
	}

	/// <summary>Reads a sheet's records, described (<see cref="CapturedRecord.ToString"/>).</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <returns>The records.</returns>
	public static async Task<List<string>> ReadAsync(string xml, SheetMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20)
	{
		var records = await ReadRecordsAsync(xml, mode, skipEmptyRows, strings, sheetName, maxSize);

		return [.. records.Select(r => r.ToString())];
	}

	/// <summary>Reads a sheet's records.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> for none.</param>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <returns>The records.</returns>
	public static async Task<List<CapturedRecord>> ReadRecordsAsync(string xml, SheetMode mode, bool skipEmptyRows = true, SharedStrings? strings = null, string sheetName = "Data", long maxSize = 1 << 20)
	{
		using var sheet = Open(xml, mode.Scan, skipEmptyRows, strings, sheetName, maxSize);
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
	/// Reads the first sheet of a package through the package layer, so that gap fields count toward the workbook's
	/// uncompressed size.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="limits">The limits of the read.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <returns>The records, and the workbook's uncompressed total after them.</returns>
	public static async Task<(List<CapturedRecord> Records, long Total)> ReadPackageAsync(TestPackage package, bool async, PartLimits limits, bool skipEmptyRows = true)
	{
		using var archive = await package.OpenAsync(async);
		var stream = await Packages.Run(async, a => archive.OpenPartAsync(PartName, false, limits, a, CancellationToken.None));
		using var sheet = new SheetScanner(new XmlScanner(new XmlWindow(stream, PartName, limits.MaxPartSize)), "Data", SharedStrings.Empty, StyleTable.Empty, skipEmptyRows, stream);
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
