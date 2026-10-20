using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using nietras.SeparatedValues;
using SylvanReader = Sylvan.Data.Csv.CsvDataReader;

namespace CsvHelper.Benchmarks;

/// <summary>Column string sources for each library, used to build <see cref="PackageAsset"/> records.</summary>
internal readonly ref struct SepSource(SepReader.Row row) : IColumnSource
{
	private readonly SepReader.Row row = row;
	public string Get(int index) => this.row[index].ToString();
}

internal readonly struct CsvHelperSource(CsvReader reader) : IColumnSource
{
	public string Get(int index) => reader.GetString(index);
}

internal readonly struct SylvanSource(SylvanReader reader) : IColumnSource
{
	public string Get(int index) => reader.GetString(index);
}

public abstract class ReadBenchmarkBase
{
	protected byte[] Utf8 = [];
	protected string Text = "";

	public long DataBytes => Utf8.Length;

	protected abstract string Generate();

	[GlobalSetup]
	public void Setup()
	{
		Text = Generate();
		Utf8 = Encoding.UTF8.GetBytes(Text);
		AfterSetup();
	}

	protected virtual void AfterSetup() { }

	protected MemoryStream NewStream() => new(Utf8, writable: false);

	protected static readonly SepReaderOptions SepUnescape = Sep.Reader(o => o with { Unescape = true });
}

/// <summary>
/// The package assets data set: 25 short text columns. Rows = find records only; Cols = touch every field
/// span; Records = materialize every field as a string into an object.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsRead : ReadBenchmarkBase
{
	protected override string Generate() => DataGenerator.PackageAssets();

	// ----- Rows -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Sep_Rows()
	{
		using var reader = Sep.Reader().From(NewStream());
		var n = 0;
		foreach (var _ in reader) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(NewStream());
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Sylvan_Rows()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	// ----- Cols -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long Sep_Cols()
	{
		using var reader = Sep.Reader().From(NewStream());
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].Span.Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.Create(NewStream());
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Sylvan_Cols()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}

	// ----- Records -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sep_Records()
	{
		using var reader = SepUnescape.From(NewStream());
		var list = new List<PackageAsset>();
		foreach (var row in reader)
		{
			var source = new SepSource(row);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = CsvReader.Create(NewStream());
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	/// <summary>CsvHelper with the mapper generated for [CsvRecord] instead of hand-written code.</summary>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_RecordsGenerated()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<PackageAsset>().ToList();
	}

	// ----- Dynamic records: every record read as dynamic, three of its 25 fields read by name -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Dynamic")]
	public long CsvHelper_Dynamic()
	{
		using var reader = CsvReader.Create(NewStream());

		return SumDynamic(reader.GetRecords<dynamic>());
	}

	/// <summary>CsvHelper reading a byte array, which dynamic records reference in place instead of copying.</summary>
	[Benchmark]
	[BenchmarkCategory("Dynamic")]
	public long CsvHelper_DynamicFromBytes()
	{
		using var reader = CsvReader.Create(Utf8);

		return SumDynamic(reader.GetRecords<dynamic>());
	}

	/// <summary>The usual way to read dynamic records: an ExpandoObject per record, filled by CsvHelper's reader.</summary>
	[Benchmark]
	[BenchmarkCategory("Dynamic")]
	public long CsvHelper_ExpandoObject()
	{
		using var reader = CsvReader.Create(NewStream());

		return SumDynamic(ExpandoRecords(reader));
	}

	// ----- Dynamic records kept in memory: every record read as dynamic into a list -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("DynamicToList")]
	public List<dynamic> CsvHelper_DynamicToList()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<dynamic>().ToList();
	}

	[Benchmark]
	[BenchmarkCategory("DynamicToList")]
	public List<dynamic> CsvHelper_ExpandoObjectToList()
	{
		using var reader = CsvReader.Create(NewStream());

		return ExpandoRecords(reader).ToList();
	}

	/// <summary>Reads three fields of every record by name, through dynamic member access.</summary>
	private static long SumDynamic(IEnumerable<dynamic> records)
	{
		long total = 0;
		foreach (var record in records)
		{
			string id = record.Id;
			string version = record.Version;
			string fileName = record.FileName;
			total += id.Length + version.Length + fileName.Length;
		}

		return total;
	}

	/// <summary>The records of a reader as ExpandoObjects: a string per field, added by header name.</summary>
	private static IEnumerable<dynamic> ExpandoRecords(CsvReader reader)
	{
		var names = reader.Headers;
		while (reader.Read())
		{
			IDictionary<string, object?> record = new System.Dynamic.ExpandoObject();
			for (var i = 0; i < names.Count; i++)
			{
				record[names[i]] = reader.GetString(i);
			}

			yield return record;
		}
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sylvan_Records()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var list = new List<PackageAsset>();
		var source = new SylvanSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	// ----- Records with string pooling (each library's own pool) -----

	private static readonly SepReaderOptions SepPooled = Sep.Reader(o => o with { Unescape = true, CreateToString = SepToString.PoolPerCol() });
	private static readonly CsvReaderOptions CsvHelperPooled = new() { PoolStrings = true };

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("RecordsPooled")]
	public List<PackageAsset> Sep_RecordsPooled()
	{
		using var reader = SepPooled.From(NewStream());
		var list = new List<PackageAsset>();
		foreach (var row in reader)
		{
			var source = new SepSource(row);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("RecordsPooled")]
	public List<PackageAsset> CsvHelper_RecordsPooled()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperPooled);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	// ----- Async rows -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("RowsAsync")]
	public async Task<int> Sep_RowsAsync()
	{
		using var reader = await Sep.Reader().FromAsync(NewStream());
		var n = 0;
		await foreach (var _ in reader) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("RowsAsync")]
	public async Task<int> CsvHelper_RowsAsync()
	{
		await using var reader = CsvReader.Create(NewStream());
		var n = 0;
		while (await reader.ReadAsync()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("RowsAsync")]
	public async Task<int> Sylvan_RowsAsync()
	{
		await using var reader = await SylvanReader.CreateAsync(new StreamReader(NewStream()));
		var n = 0;
		while (await reader.ReadAsync()) n++;

		return n;
	}

	// ----- Async records -----

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("RecordsAsync")]
	public async Task<List<PackageAsset>> Sep_RecordsAsync()
	{
		using var reader = await SepUnescape.FromAsync(NewStream());
		var list = new List<PackageAsset>();
		await foreach (var row in reader)
		{
			var source = new SepSource(row);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("RecordsAsync")]
	public async Task<List<PackageAsset>> CsvHelper_RecordsAsync()
	{
		await using var reader = CsvReader.Create(NewStream());
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (await reader.ReadAsync()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("RecordsAsync")]
	public async Task<List<PackageAsset>> CsvHelper_RecordsGeneratedAsync()
	{
		await using var reader = CsvReader.Create(NewStream());
		var list = new List<PackageAsset>();
		await foreach (var asset in reader.GetRecordsAsync<PackageAsset>()) list.Add(asset);

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("RecordsAsync")]
	public async Task<List<PackageAsset>> Sylvan_RecordsAsync()
	{
		await using var reader = await SylvanReader.CreateAsync(new StreamReader(NewStream()));
		var list = new List<PackageAsset>();
		var source = new SylvanSource(reader);
		while (await reader.ReadAsync()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>The same package assets data, read from a .NET string (UTF-16) instead of a UTF-8 stream.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsReadFromString : ReadBenchmarkBase
{
	protected override string Generate() => DataGenerator.PackageAssets();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long Sep_Cols()
	{
		using var reader = Sep.Reader().FromText(Text);
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].Span.Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.FromText(Text);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Sylvan_Cols()
	{
		using var reader = SylvanReader.Create(new StringReader(Text));
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sep_Records()
	{
		using var reader = SepUnescape.FromText(Text);
		var list = new List<PackageAsset>();
		foreach (var row in reader)
		{
			var source = new SepSource(row);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = CsvReader.FromText(Text);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sylvan_Records()
	{
		using var reader = SylvanReader.Create(new StringReader(Text));
		var list = new List<PackageAsset>();
		var source = new SylvanSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>The package assets data read from a file on disk (sync and async file I/O).</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsReadFile : ReadBenchmarkBase
{
	private readonly string path = Path.Combine(Path.GetTempPath(), "csvhelper-bench-packageassets.csv");

	protected override string Generate() => DataGenerator.PackageAssets();

	protected override void AfterSetup() => File.WriteAllBytes(path, Utf8);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("FileCols")]
	public long Sep_Cols()
	{
		using var reader = Sep.Reader().FromFile(path);
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].Span.Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("FileCols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.FromFile(path);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("FileCols")]
	public long Sylvan_Cols()
	{
		using var reader = SylvanReader.Create(path);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("FileColsAsync")]
	public async Task<long> Sep_ColsAsync()
	{
		using var reader = await Sep.Reader().FromFileAsync(path);
		var total = 0L;
		await foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].Span.Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("FileColsAsync")]
	public async Task<long> CsvHelper_ColsAsync()
	{
		await using var reader = CsvReader.FromFile(path, fileOptions: FileOptions.Asynchronous | FileOptions.SequentialScan);
		var total = 0L;
		while (await reader.ReadAsync())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("FileColsAsync")]
	public async Task<long> Sylvan_ColsAsync()
	{
		await using var reader = await SylvanReader.CreateAsync(path);
		var total = 0L;
		while (await reader.ReadAsync())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}
}

/// <summary>Parse 25 float columns per record and sum them.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FloatsRead : ReadBenchmarkBase
{
	private const int First = 3;
	private const int Last = First + DataGenerator.FloatColumns;

	protected override string Generate() => DataGenerator.Floats();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Floats")]
	public double Sep_Floats()
	{
		using var reader = Sep.Reader().From(NewStream());
		var sum = 0.0;
		foreach (var row in reader)
		{
			for (var i = First; i < Last; i++) sum += row[i].Parse<float>();
		}

		return sum;
	}

	[Benchmark]
	[BenchmarkCategory("Floats")]
	public double CsvHelper_Floats()
	{
		using var reader = CsvReader.Create(NewStream());
		var sum = 0.0;
		while (reader.Read())
		{
			for (var i = First; i < Last; i++) sum += reader.GetSingle(i);
		}

		return sum;
	}

	[Benchmark]
	[BenchmarkCategory("Floats")]
	public double Sylvan_Floats()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var sum = 0.0;
		while (reader.Read())
		{
			for (var i = First; i < Last; i++) sum += reader.GetFloat(i);
		}

		return sum;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("FloatsAsync")]
	public async Task<double> Sep_FloatsAsync()
	{
		using var reader = await Sep.Reader().FromAsync(NewStream());
		var sum = 0.0;
		await foreach (var row in reader)
		{
			for (var i = First; i < Last; i++) sum += row[i].Parse<float>();
		}

		return sum;
	}

	[Benchmark]
	[BenchmarkCategory("FloatsAsync")]
	public async Task<double> CsvHelper_FloatsAsync()
	{
		await using var reader = CsvReader.Create(NewStream());
		var sum = 0.0;
		while (await reader.ReadAsync())
		{
			for (var i = First; i < Last; i++) sum += reader.GetSingle(i);
		}

		return sum;
	}

	[Benchmark]
	[BenchmarkCategory("FloatsAsync")]
	public async Task<double> Sylvan_FloatsAsync()
	{
		await using var reader = await SylvanReader.CreateAsync(new StreamReader(NewStream()));
		var sum = 0.0;
		while (await reader.ReadAsync())
		{
			for (var i = First; i < Last; i++) sum += reader.GetFloat(i);
		}

		return sum;
	}
}

/// <summary>
/// Text heavy data where every text field is quoted and many fields contain separators, escaped quotes
/// and line breaks. Sep is configured to unescape so every library returns the same values.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class QuotedRead : ReadBenchmarkBase
{
	protected override string Generate() => DataGenerator.Quoted();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Sep_Rows()
	{
		using var reader = SepUnescape.From(NewStream());
		var n = 0;
		foreach (var _ in reader) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(NewStream());
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Sylvan_Rows()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Strings")]
	public long Sep_Strings()
	{
		using var reader = SepUnescape.From(NewStream());
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].ToString().Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long CsvHelper_Strings()
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
	[BenchmarkCategory("Strings")]
	public long Sylvan_Strings()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()));
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("StringsAsync")]
	public async Task<long> Sep_StringsAsync()
	{
		using var reader = await SepUnescape.FromAsync(NewStream());
		var total = 0L;
		await foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].ToString().Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("StringsAsync")]
	public async Task<long> CsvHelper_StringsAsync()
	{
		await using var reader = CsvReader.Create(NewStream());
		var total = 0L;
		while (await reader.ReadAsync())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("StringsAsync")]
	public async Task<long> Sylvan_StringsAsync()
	{
		await using var reader = await SylvanReader.CreateAsync(new StreamReader(NewStream()));
		var total = 0L;
		while (await reader.ReadAsync())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetString(i).Length;
		}

		return total;
	}
}
