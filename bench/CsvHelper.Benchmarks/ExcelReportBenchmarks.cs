using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// CsvHelper.Excel reading a part of a sheet, and reading a messy sheet: the package assets data laid out as a report
/// (<see cref="ExcelReport"/>), with shared strings, read as a <see cref="PackageAsset"/> per row through the generated
/// mapper with each of Phase 3's options; and the plain package assets workbook read with and without
/// <see cref="ExcelReaderOptions.FillMergedCells"/>.
/// </summary>
/// <remarks>
/// <para>
/// Report: <see cref="Range"/>, the header and the data rows as a range, is the reference, which each other read differs
/// from in one way. The regions and the keys aren't <see cref="PackageAsset"/>'s columns, so the mapper leaves them out,
/// and each read but <see cref="RangeFirstRows"/> and <see cref="SkipHiddenRows"/> gives the same records as the plain
/// workbook.
/// </para>
/// <para>
/// Plain: the workbook of <c>ExcelPackageAssetsRead</c>, which has no merged cells, so that turning on
/// <see cref="ExcelReaderOptions.FillMergedCells"/> costs only the read through the sheet that finds none.
/// </para>
/// <para>The MB/s column is of the same data as CSV, as in the other Excel benchmarks.</para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelReportRead : ReadBenchmarkBase
{
	/// <summary>The data rows <see cref="RangeFirstRows"/> reads.</summary>
	private const int FirstRows = 1_000;

	/// <summary>The report.</summary>
	private byte[] report = [];

	/// <summary>The plain package assets workbook, with shared strings.</summary>
	private byte[] plain = [];

	/// <inheritdoc/>
	protected override string Generate() => ExcelDataGenerator.PackageAssetsCsv(ExcelDataGenerator.PackageAssetRows);

	/// <inheritdoc/>
	protected override void AfterSetup()
	{
		report = ExcelDataGenerator.PackageAssetsReport(ExcelDataGenerator.PackageAssetRows);
		plain = ExcelDataGenerator.PackageAssets(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Shared);

		// The CSV's bytes are kept for the MB/s column; its string isn't needed.
		Text = "";
	}

	// ----- Report -----

	/// <summary>The header and the data rows as a range, <c>B4:AB1000004</c>: the reference.</summary>
	/// <returns>The records.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> Range() => Read(report, new ExcelReaderOptions { Range = ExcelReport.Area(ExcelDataGenerator.PackageAssetRows) });

	/// <summary>The header and the first 1,000 data rows as a range: the reading stops after the range's last row.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> RangeFirstRows() => Read(report, new ExcelReaderOptions { Range = ExcelReport.Area(FirstRows) });

	/// <summary>The table, its header the table's column names: the keys and the fields, without the regions.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> Table() => Read(report, new ExcelReaderOptions { Table = ExcelReport.TableName });

	/// <summary>The whole sheet, its header found in its first 20 rows by <see cref="PackageAsset"/>'s mapper.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> HeaderSearch() => Read(report, new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find<PackageAsset>() });

	/// <summary>The range, without its hidden rows: one data row in 50.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> SkipHiddenRows() =>
		Read(report, new ExcelReaderOptions { Range = ExcelReport.Area(ExcelDataGenerator.PackageAssetRows), SkipHiddenRows = true });

	/// <summary>The range, without its hidden column, the keys'.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> SkipHiddenColumns() =>
		Read(report, new ExcelReaderOptions { Range = ExcelReport.Area(ExcelDataGenerator.PackageAssetRows), SkipHiddenColumns = true });

	/// <summary>The range, with the regions' merged cells filled: the sheet is read once to find them, then again.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Report")]
	public List<PackageAsset> FillMergedCells() =>
		Read(report, new ExcelReaderOptions { Range = ExcelReport.Area(ExcelDataGenerator.PackageAssetRows), FillMergedCells = true });

	// ----- Plain -----

	/// <summary>The plain workbook, as <c>ExcelPackageAssetsRead.CsvHelper_Records</c> reads it.</summary>
	/// <returns>The records.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Plain")]
	public List<PackageAsset> Plain() => Read(plain, null);

	/// <summary>The plain workbook with merged cells filled: the read that finds none, then the same read as <see cref="Plain"/>.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Plain")]
	public List<PackageAsset> PlainFillMergedCells() => Read(plain, new ExcelReaderOptions { FillMergedCells = true });

	/// <summary>Reads a workbook's records through the mapper generated for <see cref="PackageAsset"/>.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
	/// <returns>The records.</returns>
	private static List<PackageAsset> Read(byte[] workbook, ExcelReaderOptions? options)
	{
		using var reader = ExcelReader.Create(new MemoryStream(workbook, writable: false), options);

		return reader.GetRecords<PackageAsset>().ToList();
	}
}
