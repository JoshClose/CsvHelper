using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace CsvHelper.Benchmarks;

/// <summary>The floats data set as a record type: 3 text columns and 25 floats.</summary>
[CsvRecord]
public sealed partial class FloatRecord
{
	public string Set = "", FileName = "", DataSplit = "";

	public float Feature0, Feature1, Feature2, Feature3, Feature4, Feature5, Feature6, Feature7, Feature8, Feature9,
		Feature10, Feature11, Feature12, Feature13, Feature14, Feature15, Feature16, Feature17, Feature18, Feature19,
		Feature20, Feature21, Feature22, Feature23, Feature24;
}

/// <summary>
/// WriteRecords against ParallelWriteRecords with 4, 8 and 16 workers and one per processor (All), to memory and to a
/// file. These run on every core, unlike the other benchmarks.
/// </summary>
public abstract class ParallelWriteBenchmarkBase<T> : WriteBenchmarkBase
{
	private T[] records = [];
	private string path = "";

	protected abstract T[] Create();

	[GlobalSetup]
	public void Setup()
	{
		records = Create();
		path = Path.Combine(Path.GetTempPath(), $"CsvHelper.Benchmarks.{GetType().Name}.csv");
		WriteRecords();
		DataBytes = Output.Length;
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Memory")]
	public long WriteRecords()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteRecords(records);
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public long ParallelWriteRecords_4() => ParallelToMemory(4);

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public long ParallelWriteRecords_8() => ParallelToMemory(8);

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public long ParallelWriteRecords_16() => ParallelToMemory(16);

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public long ParallelWriteRecords_All() => ParallelToMemory(-1);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("File")]
	public long WriteRecords_File()
	{
		using (var writer = CsvWriter.ToFile(path))
		{
			writer.WriteRecords(records);
		}

		return new FileInfo(path).Length;
	}

	[Benchmark]
	[BenchmarkCategory("File")]
	public long ParallelWriteRecords_File()
	{
		using (var writer = CsvWriter.ToFile(path))
		{
			writer.ParallelWriteRecords(records);
		}

		return new FileInfo(path).Length;
	}

	private long ParallelToMemory(int degree)
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.ParallelWriteRecords(records, degree);
		}

		return stream.Length;
	}
}

/// <summary>The package assets records (25 short text fields per record).</summary>
[Config(typeof(AllCoresConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsParallelWrite : ParallelWriteBenchmarkBase<PackageAsset>
{
	protected override PackageAsset[] Create()
	{
		using var reader = CsvReader.FromText(DataGenerator.PackageAssets());

		return reader.GetRecords<PackageAsset>().ToArray();
	}
}

/// <summary>The floats records (3 text fields and 25 floats per record).</summary>
[Config(typeof(AllCoresConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FloatsParallelWrite : ParallelWriteBenchmarkBase<FloatRecord>
{
	protected override FloatRecord[] Create()
	{
		using var reader = CsvReader.FromText(DataGenerator.Floats());

		return reader.GetRecords<FloatRecord>().ToArray();
	}
}

/// <summary>Orders: dates, a decimal, a Guid, numbers and some text per record.</summary>
[Config(typeof(AllCoresConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class OrdersParallelWrite : ParallelWriteBenchmarkBase<OrderRecord>
{
	protected override OrderRecord[] Create() => OrderRecord.Generate(DataGenerator.DefaultRows);
}
