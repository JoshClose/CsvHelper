using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

public class BenchmarkWriteCsv
{
	private const int entryCount = 10000;
	private readonly List<Simple> records = new(entryCount);
	
	public class Simple
	{
		public int Id1 { get; set; }
		public int Id2 { get; set; }
		public string Name1 { get; set; }
		public string Name2 { get; set; }
	}

	[GlobalSetup]
	public void GlobalSetup()
	{
		var random = new Random(42);
		var chars = new char[10];
		string getRandomString()
		{
			for (int i = 0; i < 10; ++i)
				chars[i] = (char)random.Next('a', 'z' + 1);
			return new string(chars);
		}

		for (int i = 0; i < entryCount; ++i)
		{
			records.Add(new Simple
			{
				Id1 = random.Next(),
				Id2 = random.Next(),
				Name1 = getRandomString(),
				Name2 = getRandomString(),
			});
		}
	}

	[Benchmark]
	public void WriteRecords()
	{
		using var stream = new MemoryStream();        
		using var streamWriter = new StreamWriter(stream);
		using var writer = new CsvHelper.CsvWriter(streamWriter, CultureInfo.InvariantCulture);
		writer.WriteRecords(records);
		streamWriter.Flush();
	}
}
