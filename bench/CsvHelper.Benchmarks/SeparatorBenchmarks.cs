using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Reading with a separator of two characters (<c>||</c>) through <see cref="CsvSeparatorStream"/>, against CsvHelper on
/// the same data with commas. The Stream category reads the stream alone, with each vector width and compaction.
/// </summary>
public abstract class SeparatorReadBase : ReadBenchmarkBase
{
	private byte[] commas = [];
	private readonly byte[] chunk = new byte[64 * 1024];

	protected abstract string GenerateCommas();

	protected override string Generate() => GenerateCommas().Replace(",", "||");

	protected override void AfterSetup() => commas = Encoding.UTF8.GetBytes(GenerateCommas());

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int CsvHelperCommas_Rows()
	{
		using var reader = CsvReader.Create(new MemoryStream(commas, writable: false));
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(new CsvSeparatorStream(NewStream(), "||"));
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Strings")]
	public long CsvHelperCommas_Strings()
	{
		using var reader = CsvReader.Create(new MemoryStream(commas, writable: false));
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long CsvHelper_Strings()
	{
		using var reader = CsvReader.Create(new CsvSeparatorStream(NewStream(), "||"));
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Stream")]
	public long MemoryStream_Copy() => ReadAll(NewStream());

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_Default() => ReadAll(new CsvSeparatorStream(NewStream(), "||"));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_512_Compress() => ReadAll(Configured(512, CsvSeparatorStream.CompactCompress));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_512_Shuffle() => ReadAll(Configured(512, CsvSeparatorStream.CompactShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_256_Shuffle() => ReadAll(Configured(256, CsvSeparatorStream.CompactShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_128_Shuffle() => ReadAll(Configured(128, CsvSeparatorStream.CompactShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_512_PairShuffle() => ReadAll(Configured(512, CsvSeparatorStream.CompactPairShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_256_PairShuffle() => ReadAll(Configured(256, CsvSeparatorStream.CompactPairShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_128_PairShuffle() => ReadAll(Configured(128, CsvSeparatorStream.CompactPairShuffle));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_64_Scalar() => ReadAll(Configured(64, CsvSeparatorStream.CompactScalar));

	[Benchmark]
	[BenchmarkCategory("Stream")]
	public long Stream_Exact() => ReadAll(Configured(0, CsvSeparatorStream.CompactScalar));

	private CsvSeparatorStream Configured(int width, int compaction) =>
		new(NewStream(), "||") { vectorWidth = width, compaction = compaction };

	private long ReadAll(Stream stream)
	{
		var total = 0L;
		int n;
		while ((n = stream.Read(chunk, 0, chunk.Length)) > 0) total += n;

		return total;
	}
}

/// <summary>The package assets data set with <c>||</c> separators (its one quoted column has <c>||</c> inside).</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SeparatorPackageAssetsRead : SeparatorReadBase
{
	protected override string GenerateCommas() => DataGenerator.PackageAssets();
}

/// <summary>The quoted data set with <c>||</c> separators: quoted fields with separators, quotes and line breaks inside.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SeparatorQuotedRead : SeparatorReadBase
{
	protected override string GenerateCommas() => DataGenerator.Quoted();
}
