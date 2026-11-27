using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// The parallel write's base (Phase 9's step 10): a sheet of <typeparamref name="T"/> records written through the sheet's
/// <see cref="CsvWriter"/> (<see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/>) with
/// <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> (the baseline) and with
/// <see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/> at 2, 4, 8 and 16 workers, at the defaults; and
/// with shared strings, which a parallel write writes serially (its decision 37), each way, so that the parallel methods'
/// serial path is timed against <c>WriteRecords</c>'. On CCD0.
/// </summary>
/// <remarks>
/// The workers make the sheet's XML, and the sheet's entry deflates it in 1 MB chunks on threads of its own, as many as the
/// workers (<see cref="ExcelWriterOptions.CompressionThreads"/> is 1), so every degree writes the same bytes. The CPU column
/// shows what the threads cost.
/// </remarks>
/// <typeparam name="T">The records' type.</typeparam>
public abstract class ExcelParallelWriteBase<T> : ExcelWriteBenchmarkBase
{
	/// <summary>The shared strings' options.</summary>
	private ExcelWriterOptions shared = null!;

	/// <summary>The records.</summary>
	protected T[] Records { get; private set; } = [];

	/// <summary>The sheet's name.</summary>
	protected abstract string SheetName { get; }

	/// <summary>The workbook's options at the defaults.</summary>
	protected abstract ExcelWriterOptions Options { get; }

	/// <summary>Builds the records, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		Records = Create();
		shared = Options with { Strings = ExcelStrings.Shared };
		var counting = new CountingStream(keep: false);
		using (var writer = CsvWriter.Create(counting, leaveOpen: true))
		{
			writer.WriteRecords(Records);
		}

		DataBytes = counting.Count;
	}

	/// <summary><c>WriteRecords</c> at the defaults: the baseline.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Inline")]
	public long WriteRecords() => Write(Options, 0);

	/// <summary><c>ParallelWriteRecords</c> at 2 workers.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Inline")]
	public long ParallelWriteRecords_2() => Write(Options, 2);

	/// <summary><c>ParallelWriteRecords</c> at 4 workers.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Inline")]
	public long ParallelWriteRecords_4() => Write(Options, 4);

	/// <summary><c>ParallelWriteRecords</c> at 8 workers.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Inline")]
	public long ParallelWriteRecords_8() => Write(Options, 8);

	/// <summary><c>ParallelWriteRecords</c> at 16 workers.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Inline")]
	public long ParallelWriteRecords_16() => Write(Options, 16);

	/// <summary><c>WriteRecords</c> with shared strings: the baseline of the shared strings' pair.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Shared")]
	public long WriteRecords_Shared() => Write(shared, 0);

	/// <summary><c>ParallelWriteRecords</c> at 8 workers with shared strings, which it writes serially, item by item.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Shared")]
	public long ParallelWriteRecords_Shared_8() => Write(shared, 8);

	/// <summary>Makes the records.</summary>
	/// <returns>The records.</returns>
	protected abstract T[] Create();

	/// <summary>Writes the records' sheet with <c>WriteRecords</c>, or with <c>ParallelWriteRecords</c> at a degree.</summary>
	/// <param name="options">The workbook's options.</param>
	/// <param name="degree">The workers, or 0 for <c>WriteRecords</c>.</param>
	/// <returns>The bytes written.</returns>
	private long Write(ExcelWriterOptions options, int degree)
	{
		var stream = NewOutput();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		using (var writer = workbook.AddSheet<T>(SheetName))
		{
			if (degree == 0)
			{
				writer.WriteRecords(Records);
			}
			else
			{
				writer.ParallelWriteRecords(Records, degree);
			}
		}

		return stream.Count;
	}
}

/// <summary>
/// The million package assets as <see cref="PackageAsset"/> records, 25 short text fields each, written by
/// <see cref="ExcelParallelWriteBase{T}"/>'s methods.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(Ccd0GcWrittenConfig))]
public class ExcelPackageAssetsParallelWrite : ExcelParallelWriteBase<PackageAsset>
{
	/// <inheritdoc/>
	protected override string SheetName => ExcelDataGenerator.SheetName;

	/// <inheritdoc/>
	protected override ExcelWriterOptions Options { get; } = new();

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<PackageAsset>().ToList();
		}
	}

	/// <inheritdoc/>
	protected override PackageAsset[] Create()
	{
		using var reader = CsvReader.FromText(ExcelDataGenerator.PackageAssetsCsv(ExcelDataGenerator.PackageAssetRows));

		return reader.GetRecords<PackageAsset>().ToArray();
	}
}

/// <summary>
/// The million orders (<see cref="ExcelOrders.Generate"/>) written by <see cref="ExcelParallelWriteBase{T}"/>'s methods,
/// with <see cref="ExcelDateTimeOffsets.Utc"/> as <c>ExcelOrdersWrite</c> writes them.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(Ccd0GcWrittenConfig))]
public class ExcelOrdersParallelWrite : ExcelParallelWriteBase<OrderRecord>
{
	/// <inheritdoc/>
	protected override string SheetName => ExcelOrders.SheetName;

	/// <inheritdoc/>
	protected override ExcelWriterOptions Options { get; } = new() { DateTimeOffsets = ExcelDateTimeOffsets.Utc };

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<OrderRecord>().ToList();
		}
	}

	/// <inheritdoc/>
	protected override OrderRecord[] Create() => ExcelOrders.Generate(ExcelDataGenerator.OrderRows);
}
