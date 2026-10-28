using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace CsvHelper.Benchmarks;

/// <summary>
/// The reader's index flatteners on the 512-bit scanner: trailing zero counts (Bit) against one AVX-512 VBMI2 compress per
/// block (Compress). Reads from memory, so that the scan dominates. Rows = find records only; Cols = touch every field span.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public abstract class FlattenReadBase : ReadBenchmarkBase
{
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Bit_Rows() => Rows(compress: false);

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Compress_Rows() => Rows(compress: true);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long Bit_Cols() => Cols(compress: false);

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Compress_Cols() => Cols(compress: true);

	private int Rows(bool compress)
	{
		using var reader = CsvReader.Create(Utf8);
		reader.compressIndex = compress;
		var n = 0;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	private long Cols(bool compress)
	{
		using var reader = CsvReader.Create(Utf8);
		reader.compressIndex = compress;
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetSpan(i).Length;
			}
		}

		return total;
	}
}

/// <summary>25 short text columns, about 5 fields per 64 bytes.</summary>
public class FlattenPackageAssets : FlattenReadBase
{
	protected override string Generate() => DataGenerator.PackageAssets();
}

/// <summary>Float columns, about 6 fields per 64 bytes.</summary>
public class FlattenFloats : FlattenReadBase
{
	protected override string Generate() => DataGenerator.Floats();
}

/// <summary>Long quoted text fields, about 2 fields per 64 bytes.</summary>
public class FlattenQuoted : FlattenReadBase
{
	protected override string Generate() => DataGenerator.Quoted();
}

/// <summary>20 columns of numbers below 100, about 20 fields per 64 bytes.</summary>
public class FlattenDense : FlattenReadBase
{
	protected override string Generate()
	{
		var rng = new Random(42);
		var sb = new StringBuilder();
		for (var r = 0; r < 250_000; r++)
		{
			for (var c = 0; c < 20; c++)
			{
				if (c > 0)
				{
					sb.Append(',');
				}

				sb.Append(rng.Next(100));
			}

			sb.Append('\n');
		}

		return sb.ToString();
	}
}
