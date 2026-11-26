using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// A workbook's records through <c>ParallelEnumerate</c> at 1, 2, 4, 8 and 16 workers, against <c>GetRecords</c> on one
/// thread (Records); and the rows alone through a selector that maps nothing, at 2, 4, 8 and 16 workers and over the
/// workbook's stored copy at 8, against <c>Read</c> (Rows), which measures how the read itself (inflating and scanning)
/// scales, without mapping or the garbage collections mapping causes.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
/// <remarks>
/// <para>
/// The classes run under <see cref="Ccd0GcConfig"/>: on CCD0's 16 logical processors, once with workstation GC and once
/// with server GC (garbage collection bounds the package assets' records), with the lock contentions and work items of
/// each operation shown.
/// </para>
/// <para>
/// The degrees are explicit, not -1: BenchmarkDotNet sets the affinity after the process starts, so the runtime's
/// processor count can be the machine's.
/// </para>
/// </remarks>
public abstract class ExcelParallelReadBase<T>
{
	/// <summary>The workbook.</summary>
	private byte[] workbook = [];

	/// <summary>The workbook's bytes with every entry stored, not deflated: set up for <see cref="CsvHelper_RowsParallelStored_8"/> only.</summary>
	private byte[] stored = [];

	/// <summary>Reads the workbook from the cache, for every method but <see cref="CsvHelper_RowsParallelStored_8"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		BenchWorkbooks.Ensure();
		workbook = Create();
	}

	/// <summary>
	/// Reads the stored copy of the workbook from the cache (<see cref="ExcelScanStages.ReadStored"/>), for
	/// <see cref="CsvHelper_RowsParallelStored_8"/> alone.
	/// </summary>
	[GlobalSetup(Target = nameof(CsvHelper_RowsParallelStored_8))]
	public void SetupStored()
	{
		BenchWorkbooks.Ensure();
		stored = ExcelScanStages.ReadStored(Stage);
	}

	/// <summary>The records on one thread.</summary>
	/// <returns>The records.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_Records()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.GetRecords<T>().ToList();
	}

	/// <summary>The records through one worker: what the parallel machinery costs by itself.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_RecordsParallel_1() => RecordsParallel(1);

	/// <summary>The records through two workers.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_RecordsParallel_2() => RecordsParallel(2);

	/// <summary>The records through four workers.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_RecordsParallel_4() => RecordsParallel(4);

	/// <summary>The records through eight workers: one per physical core of CCD0.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_RecordsParallel_8() => RecordsParallel(8);

	/// <summary>The records through sixteen workers: one per logical processor of CCD0.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<T> CsvHelper_RecordsParallel_16() => RecordsParallel(16);

	/// <summary>The records counted on one thread.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());
		var n = 0;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	/// <summary>The records counted through two workers, whose selector maps nothing.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_RowsParallel_2() => RowsParallel(2);

	/// <summary>The records counted through four workers, whose selector maps nothing.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_RowsParallel_4() => RowsParallel(4);

	/// <summary>The records counted through eight workers, whose selector maps nothing.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_RowsParallel_8() => RowsParallel(8);

	/// <summary>
	/// The records of the workbook's stored copy counted through eight workers, whose selector maps nothing: the parallel
	/// read with nothing to inflate, so that it shows how far the scan scales once inflating doesn't bound it.
	/// </summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_RowsParallelStored_8()
	{
		using var reader = ExcelReader.Create(new MemoryStream(stored, writable: false));

		return reader.ParallelEnumerate(static r => r.FieldCount, 8).Count();
	}

	/// <summary>The records counted through sixteen workers, whose selector maps nothing.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_RowsParallel_16() => RowsParallel(16);

	/// <summary>The workbook as <see cref="ExcelScanStages"/> names it, for its stored copy.</summary>
	protected abstract StageWorkbook Stage { get; }

	/// <summary>Returns the workbook's bytes.</summary>
	/// <returns>The bytes.</returns>
	protected abstract byte[] Create();

	/// <summary>Reads the records through <c>ParallelEnumerate</c>.</summary>
	/// <param name="degree">The number of workers.</param>
	/// <returns>The records, in order.</returns>
	private List<T> RecordsParallel(int degree)
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.ParallelEnumerate<T>(degree).ToList();
	}

	/// <summary>Counts the records through <c>ParallelEnumerate</c>, with a selector that reads nothing.</summary>
	/// <param name="degree">The number of workers.</param>
	/// <returns>The number of records.</returns>
	private int RowsParallel(int degree)
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.ParallelEnumerate(static r => r.FieldCount, degree).Count();
	}

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewWorkbookStream() => new(workbook, writable: false);
}

/// <summary>The package assets, with shared strings, as <see cref="ExcelPackageAssetsRead"/> reads them, in parallel.</summary>
[Config(typeof(Ccd0GcConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelPackageAssetsParallelRead : ExcelParallelReadBase<PackageAsset>
{
	/// <inheritdoc/>
	protected override StageWorkbook Stage => StageWorkbook.Shared;

	/// <inheritdoc/>
	protected override byte[] Create() => ExcelDataGenerator.PackageAssets(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Shared);
}

/// <summary>The orders, as <see cref="ExcelOrdersRead"/> reads them, in parallel.</summary>
[Config(typeof(Ccd0GcConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelOrdersParallelRead : ExcelParallelReadBase<OrderRecord>
{
	/// <inheritdoc/>
	protected override StageWorkbook Stage => StageWorkbook.Orders;

	/// <inheritdoc/>
	protected override byte[] Create() => ExcelDataGenerator.Orders(ExcelDataGenerator.OrderRows);
}
