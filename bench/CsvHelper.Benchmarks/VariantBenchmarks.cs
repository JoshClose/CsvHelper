using BenchmarkDotNet.Attributes;
using nietras.SeparatedValues;

namespace CsvHelper.Benchmarks;

/// <summary>CsvHelper-only variants (input sources, options) used to understand where time goes.</summary>
[MemoryDiagnoser]
public class CsvHelperVariants : ReadBenchmarkBase
{
	protected override string Generate() => DataGenerator.PackageAssets();

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Stream_Rows()
	{
		using var reader = CsvReader.Create(NewStream());
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Memory_Rows()
	{
		using var reader = CsvReader.Create(Utf8);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	private byte[]? unquoted;

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int MemoryNoQuotes_Rows()
	{
		unquoted ??= System.Text.Encoding.UTF8.GetBytes(Text.Replace("\"", "").Replace(",Version=", " Version="));
		using var reader = CsvReader.Create(unquoted);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int SepMemory_Rows()
	{
		using var reader = Sep.Reader().From(Utf8);
		var n = 0;
		foreach (var _ in reader) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Memory_Cols()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Memory_RowCols()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.FieldCount; i++) total += row.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Copy")]
	public long CopyOnly()
	{
		var stream = NewStream();
		var buffer = new byte[65536];
		var total = 0L;
		int n;
		while ((n = stream.Read(buffer, 0, buffer.Length)) > 0) total += n;

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Copy")]
	public long CopyAfterGarbage()
	{
		// Allocate and keep ~80 MB of small objects (like Records does), then copy.
		var keep = new List<string>(1_250_000);
		for (var i = 0; i < 1_250_000; i++) keep.Add(new string('x', 24));

		return CopyOnly() + keep.Count;
	}

	[Benchmark]
	[BenchmarkCategory("Copy")]
	public long GarbageOnly()
	{
		var keep = new List<string>(1_250_000);
		for (var i = 0; i < 1_250_000; i++) keep.Add(new string('x', 24));

		return keep.Count;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Memory_Records()
	{
		using var reader = CsvReader.Create(Utf8);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Stream_Records()
	{
		using var reader = CsvReader.Create(NewStream());
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Pooled_Records()
	{
		using var reader = CsvReader.Create(NewStream(), new CsvReaderOptions { PoolStrings = true });
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>CsvHelper-only variants on the quoted data set, to separate input copying, unescaping and string creation.</summary>
[MemoryDiagnoser]
public class CsvHelperQuotedVariants : ReadBenchmarkBase
{
	protected override string Generate() => DataGenerator.Quoted();

	private static readonly CsvReaderOptions Pooled = new() { PoolStrings = true };

	[Benchmark]
	public long Stream_Strings()
	{
		using var reader = CsvReader.Create(NewStream());
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark]
	public long Memory_Strings()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark]
	public long Memory_StringsPooled()
	{
		using var reader = CsvReader.Create(Utf8, Pooled);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark]
	public long Memory_Spans()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	public long Memory_RawSpans()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetRawSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	public long Memory_RowsOnly()
	{
		using var reader = CsvReader.Create(Utf8);
		var total = 0L;
		while (reader.Read()) total += reader.FieldCount;

		return total;
	}
}
