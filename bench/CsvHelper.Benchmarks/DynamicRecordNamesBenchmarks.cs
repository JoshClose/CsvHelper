using System.Dynamic;
using System.Linq.Expressions;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

[MemoryDiagnoser]
public class DynamicRecordNamesBenchmarks
{
	private CsvDynamicRecord record = null!;
	private DynamicMetaObject metaObject = null!;
	private byte[] data = null!;

	[Params(8, 64)]
	public int Columns { get; set; } = 8;

	[Params(16, 4096)]
	public int ValueLength { get; set; } = 16;

	[GlobalSetup]
	public void Setup()
	{
		var header = string.Join(",", Enumerable.Range(0, Columns).Select(i => "Column" + i));
		var row = string.Join(",", Enumerable.Repeat(new string('x', ValueLength), Columns)) + "\n";
		data = Encoding.UTF8.GetBytes(header + "\n" + string.Concat(Enumerable.Repeat(row, 32)));
		using var reader = CsvReader.Create(data);
		record = reader.GetRecords<CsvDynamicRecord>().First();
		metaObject = ((IDynamicMetaObjectProvider)record).GetMetaObject(Expression.Constant(record));
	}

	[Benchmark]
	public int Keys()
	{
		var length = 0;
		foreach (var name in ((IReadOnlyDictionary<string, string?>)record).Keys)
		{
			length += name.Length;
		}

		return length;
	}

	[Benchmark]
	public int DynamicMemberNames()
	{
		var length = 0;
		foreach (var name in metaObject.GetDynamicMemberNames())
		{
			length += name.Length;
		}

		return length;
	}

	[Benchmark]
	public int ReadAndEnumerateKeys()
	{
		using var reader = CsvReader.Create(data);
		var length = 0;
		foreach (var row in reader.GetRecords<CsvDynamicRecord>())
		{
			foreach (var name in ((IReadOnlyDictionary<string, string?>)row).Keys)
			{
				length += name.Length;
			}
		}

		return length;
	}

	[Benchmark]
	public int Values()
	{
		var length = 0;
		foreach (var value in ((IReadOnlyDictionary<string, string?>)record).Values)
		{
			length += value?.Length ?? 0;
		}

		return length;
	}
}
