using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Benchmarks
{

	[MemoryDiagnoser]
	public class CsvWriterBenches
	{

		public class Foo
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		List<Foo> records;

		[GlobalSetup]
		public void GlobalSetup()
		{
			records = new List<Foo>();
			for (int i = 0; i < 1000; i++)
			{
				records.Add(new Foo { Id = i, Name = $"I am {i}" });
			}
		}

		[Benchmark]
		public void WriteCsv()
		{
			using (var memoryStream = new MemoryStream())
			using (var textWriter = new StreamWriter(memoryStream))
			using (var csv = new CsvWriter(textWriter, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);
			}

		}


	}
}
