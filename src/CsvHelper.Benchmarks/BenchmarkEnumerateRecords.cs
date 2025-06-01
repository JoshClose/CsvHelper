using System;
using System.Globalization;
using System.IO;

using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

[MemoryDiagnoser]
public class BenchmarkEnumerateRecords
{
	private const int entryCount = 10000;
	private readonly MemoryStream stream = new();

	public class Simple
	{
		public int Id { get; set; }
		public string Name { get; set; }
	}

	[GlobalSetup]
	public void GlobalSetup()
	{
		using var streamWriter = new StreamWriter(this.stream, null, -1, true);
		using var writer = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, true);
		var random = new Random(42); // Pick a known seed to keep things consistent

		var chars = new char[10];
		string getRandomString()
		{
			for (int i = 0; i < 10; ++i)
				chars[i] = (char)random.Next('a', 'z' + 1);
			return new string(chars);
		}

		writer.WriteHeader(typeof(Simple));
		writer.NextRecord();
		for (int i = 0; i < BenchmarkEnumerateRecords.entryCount; ++i)
		{
			writer.WriteRecord(new Simple()
			{
				Id = random.Next(),
				Name = getRandomString()
			});
			writer.NextRecord();
		}
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		this.stream.Dispose();
	}

	[Benchmark]
	public void EnumerateRecords()
	{
		this.stream.Position = 0;
		using var streamReader = new StreamReader(this.stream, null, true, -1, true);
		using var csv = new CsvReader(streamReader, CultureInfo.InvariantCulture, true);
		foreach (var record in csv.GetRecords<Simple>())
		{
			_ = record;
		}
	}
}
