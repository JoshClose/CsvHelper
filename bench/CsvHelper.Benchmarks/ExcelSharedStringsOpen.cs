using BenchmarkDotNet.Attributes;
using CsvHelper.Excel;
using SylvanExcelReader = Sylvan.Data.Excel.ExcelDataReader;
using SylvanWorkbookType = Sylvan.Data.Excel.ExcelWorkbookType;

namespace CsvHelper.Benchmarks;

/// <summary>The workbooks <see cref="ExcelSharedStringsOpen"/> opens.</summary>
public enum SharedStringsWorkbook
{
	/// <summary>The package assets, a million rows, with shared strings: 1,695 rows repeated, so a small table (3,085 strings).</summary>
	PackageAssetsShared,

	/// <summary>The orders, 100,000 rows: 100,000 distinct <c>CustomerId</c>s in the table, about 4 MB of it loaded.</summary>
	Orders100k,

	/// <summary>The orders, a million rows: a million distinct <c>CustomerId</c>s in the table, about 40 MB of it loaded.</summary>
	Orders1M,
}

/// <summary>
/// Opening a workbook whose cells are shared strings: what a reader does before its first record, and how much of it is
/// the shared strings table. <see cref="OpenPackage"/> reads the zip's directory and the workbook part;
/// <see cref="LoadTable"/> adds the table, so the difference is the table's load alone; the <c>FirstRecord</c> methods
/// open a reader and read the first record's fields as strings, ours (which loads the whole table first) against
/// Sylvan's (which reads the table only as far as the strings it's asked for).
/// </summary>
/// <remarks>
/// The orders at 100,000 and at a million show how the load grows with the table: its time, and its large arrays
/// (the Allocated column), which on the million orders are most of every orders benchmark's allocation.
/// </remarks>
[MemoryDiagnoser]
public class ExcelSharedStringsOpen
{
	/// <summary>The default limits.</summary>
	private static readonly PartLimits Limits = ExcelReaderOptions.Default.Limits;

	/// <summary>The workbook's bytes.</summary>
	private byte[] workbook = [];

	/// <summary>Gets or sets the workbook.</summary>
	[Params(SharedStringsWorkbook.PackageAssetsShared, SharedStringsWorkbook.Orders100k, SharedStringsWorkbook.Orders1M)]
	public SharedStringsWorkbook Workbook { get; set; }

	/// <summary>Reads the workbook from the cache.</summary>
	[GlobalSetup]
	public void Setup()
	{
		BenchWorkbooks.Ensure();
		workbook = Workbook switch
		{
			SharedStringsWorkbook.PackageAssetsShared => ExcelDataGenerator.PackageAssets(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Shared),
			SharedStringsWorkbook.Orders100k => ExcelDataGenerator.Orders(100_000),
			_ => ExcelDataGenerator.Orders(ExcelDataGenerator.OrderRows),
		};
	}

	/// <summary>The package and its workbook part: what <see cref="LoadTable"/> reads besides the table.</summary>
	/// <returns>The number of sheets.</returns>
	[Benchmark(Baseline = true)]
	public int OpenPackage()
	{
		using var archive = OpenArchive();

		return LoadWorkbookPart(archive).Sheets.Count;
	}

	/// <summary>The package, its workbook part, and the shared strings table.</summary>
	/// <returns>The number of strings.</returns>
	[Benchmark]
	public int LoadTable()
	{
		using var archive = OpenArchive();
		var part = LoadWorkbookPart(archive);

		return SharedStrings.LoadAsync(archive, part.SharedStringsPart, Limits, false, default).GetAwaiter().GetResult().Count;
	}

	/// <summary>CsvHelper.Excel: opens a reader and reads the first record's fields as strings.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	public long CsvHelper_FirstRecord()
	{
		using var reader = ExcelReader.Create(NewStream());
		var total = 0L;
		if (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	/// <summary>Sylvan.Data.Excel: opens a reader and reads the first record's fields as strings.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	public long Sylvan_FirstRecord()
	{
		using var reader = SylvanExcelReader.Create(NewStream(), SylvanWorkbookType.ExcelXml);
		var total = 0L;
		if (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	/// <summary>Reads a package's workbook part.</summary>
	/// <param name="archive">The package.</param>
	/// <returns>The workbook part.</returns>
	private static WorkbookPart LoadWorkbookPart(PackageArchive archive) => WorkbookPart.LoadAsync(archive, Limits, false, default).GetAwaiter().GetResult();

	/// <summary>Opens the workbook's package.</summary>
	/// <returns>The package.</returns>
	private PackageArchive OpenArchive() => PackageArchive.OpenAsync(NewStream(), false, Limits, false, default).GetAwaiter().GetResult();

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewStream() => new(workbook, writable: false);
}
