using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Linq;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Queries of the package assets data set (25 short text columns) with CsvHelper.Linq, against the same queries in LINQ
/// to Objects over GetRecords, which reads every member of every record. Filter = a Where on one column and a Select
/// of two; Count = counting the records; Skip = the record in the middle; Dynamic = the same filter on dynamic rows.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsLinq : ReadBenchmarkBase
{
	private static readonly CsvQueryOptions NoPruning = new() { PruneColumns = false };
	private static readonly CsvQueryOptions Parallel = new() { MaxDegreeOfParallelism = -1 };

	private int middle;

	protected override string Generate() => DataGenerator.PackageAssets();

	protected override void AfterSetup() => middle = Text.Count(c => c == '\n') / 2;

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Filter")]
	public int LinqToObjects_Filter()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<PackageAsset>().Where(a => a.PatternSet == "RuntimeAssembly").Select(a => new { a.Id, a.Version }).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Filter")]
	public int Linq_Filter() =>
		CsvQueryable.FromBytes<PackageAsset>(Utf8).Where(a => a.PatternSet == "RuntimeAssembly").Select(a => new { a.Id, a.Version }).AsEnumerable().Count();

	[Benchmark]
	[BenchmarkCategory("Filter")]
	public int Linq_FilterWithoutPruning() =>
		CsvQueryable.FromBytes<PackageAsset>(Utf8, NoPruning).Where(a => a.PatternSet == "RuntimeAssembly").Select(a => new { a.Id, a.Version }).AsEnumerable().Count();

	[Benchmark]
	[BenchmarkCategory("Filter")]
	public int Linq_FilterInParallel() =>
		CsvQueryable.FromBytes<PackageAsset>(Utf8, Parallel).Where(a => a.PatternSet == "RuntimeAssembly").Select(a => new { a.Id, a.Version }).AsEnumerable().Count();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Count")]
	public int LinqToObjects_Count()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<PackageAsset>().Count();
	}

	[Benchmark]
	[BenchmarkCategory("Count")]
	public int Linq_Count() => CsvQueryable.FromBytes<PackageAsset>(Utf8).Count();

	[Benchmark]
	[BenchmarkCategory("Count")]
	public int Reader_Count()
	{
		using var reader = CsvReader.Create(NewStream());
		var count = 0;
		while (reader.Read())
		{
			count++;
		}

		return count;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Skip")]
	public string LinqToObjects_Skip()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<PackageAsset>().Skip(middle).First().Id;
	}

	[Benchmark]
	[BenchmarkCategory("Skip")]
	public string Linq_Skip() => CsvQueryable.FromBytes<PackageAsset>(Utf8).Skip(middle).Select(a => a.Id).First();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Dynamic")]
	public int LinqToObjects_Dynamic()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<CsvDynamicRecord>().Where(r => r["PatternSet"] == "RuntimeAssembly").Select(r => new { Id = r["Id"], Version = r["Version"] }).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Dynamic")]
	public int Linq_Dynamic() =>
		CsvQueryable.FromBytes(Utf8).Where(r => r["PatternSet"] == "RuntimeAssembly").Select(r => new { Id = r["Id"], Version = r["Version"] }).AsEnumerable().Count();
}

/// <summary>
/// Writing the package assets records with CsvHelper.Linq's WriteCsv: with the generated record writer, as
/// CsvWriter.WriteRecords does, and as an anonymous type of all 25 members, which it writes with reflection.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsLinqWrite : WriteBenchmarkBase
{
	private PackageAsset[] assets = [];
	private string path = "";
	private string copy = "";

	[GlobalSetup]
	public void Setup()
	{
		using (var reader = CsvReader.FromText(DataGenerator.PackageAssets()))
		{
			assets = reader.GetRecords<PackageAsset>().ToArray();
		}

		path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csvhelper-linq-bench.csv");
		copy = path + ".source";
		assets.WriteCsv(copy);
		DataBytes = new FileInfo(copy).Length;
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		File.Delete(path);
		File.Delete(copy);
	}

	[IterationSetup(Targets = [nameof(Linq_Delete), nameof(ReadAndWrite_Delete)])]
	public void CopyFile() => File.Copy(copy, path, true);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long CsvWriter_WriteRecords()
	{
		using var writer = CsvWriter.Create(ResetOutput(), leaveOpen: true);
		writer.WriteRecords(assets);

		return writer.RecordCount;
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Linq_WriteCsv() => assets.WriteCsv(ResetOutput(), leaveOpen: true);

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Linq_WriteCsvAnonymous() => assets.Select(a => new
	{
		a.ScanId, a.ScanTimestamp, a.Id, a.Version, a.Created, a.ResultType, a.PatternSet, a.PropertyAnyValue, a.PropertyCodeLanguage,
		a.PropertyTargetFrameworkMoniker, a.PropertyLocale, a.PropertyManagedAssembly, a.PropertyMSBuild, a.PropertyRuntimeIdentifier,
		a.PropertySatelliteAssembly, a.Path, a.FileName, a.FileExtension, a.TopLevelFolder, a.RoundTripTargetFrameworkMoniker,
		a.FrameworkName, a.FrameworkVersion, a.FrameworkProfile, a.PlatformName, a.PlatformVersion,
	}).WriteCsv(ResetOutput(), leaveOpen: true);

	/// <summary>Removing the records of one locale (about one in nine) from a file, keeping the rest byte for byte.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rewrite")]
	public long ReadAndWrite_Delete()
	{
		var kept = path + ".kept";
		using (var reader = CsvReader.FromFile(path))
		using (var writer = CsvWriter.ToFile(kept))
		{
			writer.WriteRecords(reader.GetRecords<PackageAsset>().Where(a => a.PropertyLocale != "ja"));
		}

		File.Replace(kept, path, null);

		return 0;
	}

	[Benchmark]
	[BenchmarkCategory("Rewrite")]
	public long Linq_Delete() => CsvQueryable.FromFile<PackageAsset>(path).Delete(a => a.PropertyLocale == "ja");
}
