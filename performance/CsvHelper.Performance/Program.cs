// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CsvHelper.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Performance
{
	class Program
	{
		static void Main(string[] args)
		{
			BenchmarkRunner.Run<Benchmarks>(); return;

			//Test(); return;

			//WriteField(50, 1_000_000, new CsvConfiguration(CultureInfo.InvariantCulture) { ShouldQuote = args => true }); return;
			//WriteRecords(1_000_000);

			//for (var i = 0; i < 10; i++)
			//{
			//	Parse();

			//	ReadGetField();
			//	ReadGetRecords();
			//	ReadGetRecordsAsync().Wait();

			//	Console.WriteLine();
			//}
		}

		public static string GetFilePath()
		{
			var homePath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
			var filePath = Path.Combine(homePath, "Documents", "performance.csv");

			return filePath;
		}

		static void WriteField(int columns = 50, int rows = 1_000_000, CsvConfiguration config = null)
		{
			Console.WriteLine("Writing using WriteField");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			config ??= new CsvConfiguration(CultureInfo.InvariantCulture);

			using (var stream = File.Create(GetFilePath()))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				for (var column = 1; column <= columns; column++)
				{
					csv.WriteField($"Column{column}");
				}
				csv.NextRecord();

				for (var row = 1; row <= rows; row++)
				{
					for (var column = 1; column <= columns; column++)
					{
						//csv.WriteField($"{row:N0}_{column}");
						csv.WriteField($"{row}_{column}");
					}
					csv.NextRecord();
				}
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static void WriteRecords(int rows = 2_000_000)
		{
			Console.WriteLine("Writing using WriteRecords");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var random = new Random();

			using (var stream = File.Create(GetFilePath()))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				//csv.Configuration.ShouldQuote = (field, context) => true;

				var records = new List<Columns50>();
				for (var i = 0; i < rows; i++)
				{
					var record = new Columns50
					{
						Column1 = random.Next(),
						Column2 = random.Next(),
						Column3 = random.Next(),
						Column4 = random.Next(),
						Column5 = random.Next(),
						Column6 = random.Next(),
						Column7 = random.Next(),
						Column8 = random.Next(),
						Column9 = random.Next(),
						Column10 = random.Next(),
						Column11 = random.Next(),
						Column12 = random.Next(),
						Column13 = random.Next(),
						Column14 = random.Next(),
						Column15 = random.Next(),
						Column16 = random.Next(),
						Column17 = random.Next(),
						Column18 = random.Next(),
						Column19 = random.Next(),
						Column20 = random.Next(),
						Column21 = random.Next(),
						Column22 = random.Next(),
						Column23 = random.Next(),
						Column24 = random.Next(),
						Column25 = random.Next(),
						Column26 = random.Next(),
						Column27 = random.Next(),
						Column28 = random.Next(),
						Column29 = random.Next(),
						Column30 = random.Next(),
						Column31 = random.Next(),
						Column32 = random.Next(),
						Column33 = random.Next(),
						Column34 = random.Next(),
						Column35 = random.Next(),
						Column36 = random.Next(),
						Column37 = random.Next(),
						Column38 = random.Next(),
						Column39 = random.Next(),
						Column40 = random.Next(),
						Column41 = random.Next(),
						Column42 = random.Next(),
						Column43 = random.Next(),
						Column44 = random.Next(),
						Column45 = random.Next(),
						Column46 = random.Next(),
						Column47 = random.Next(),
						Column48 = random.Next(),
						Column49 = random.Next(),
						Column50 = random.Next(),
					};
					records.Add(record);
				}

				csv.WriteRecords(records);
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static void Parse()
		{
			Console.WriteLine("CsvHelper parsing");

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var stopwatch = new Stopwatch();
				stopwatch.Start();

				string[] record;
				while (parser.Read())
				{
					record = parser.Record;
				}

				stopwatch.Stop();
				Console.WriteLine(stopwatch.Elapsed);
			}
		}

		static void ReadGetField()
		{
			Console.WriteLine("Reading using GetField");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				// Read header.
				csv.Read();

				while (csv.Read())
				{
					for (var i = 0; i < 50; i++)
					{
						csv.GetField<int>(i);
					}
				}
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static void ReadGetRecords()
		{
			Console.WriteLine($"Reading using GetRecords");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<Columns50>();
				foreach (var record in records)
				{
				}
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static async Task ReadGetRecordsAsync()
		{
			Console.WriteLine("Reading using GetRecordsAsync");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				while (await csv.ReadAsync())
				{
					var record = csv.GetRecord<Columns50>();
				}
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		private class Data
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public int Age { get; set; }

			public DateTimeOffset Birthday { get; set; }
		}

		private class DataMap : ClassMap<Data>
		{
			public DataMap()
			{
				Map(m => m.Id).Index(0);
				Map(m => m.Name).Index(1);
				Map(m => m.Age).Index(2);
				Map(m => m.Birthday).Index(3);
			}
		}

		public class Columns50
		{
			public int Column1 { get; set; }
			public int Column2 { get; set; }
			public int Column3 { get; set; }
			public int Column4 { get; set; }
			public int Column5 { get; set; }
			public int Column6 { get; set; }
			public int Column7 { get; set; }
			public int Column8 { get; set; }
			public int Column9 { get; set; }
			public int Column10 { get; set; }
			public int Column11 { get; set; }
			public int Column12 { get; set; }
			public int Column13 { get; set; }
			public int Column14 { get; set; }
			public int Column15 { get; set; }
			public int Column16 { get; set; }
			public int Column17 { get; set; }
			public int Column18 { get; set; }
			public int Column19 { get; set; }
			public int Column20 { get; set; }
			public int Column21 { get; set; }
			public int Column22 { get; set; }
			public int Column23 { get; set; }
			public int Column24 { get; set; }
			public int Column25 { get; set; }
			public int Column26 { get; set; }
			public int Column27 { get; set; }
			public int Column28 { get; set; }
			public int Column29 { get; set; }
			public int Column30 { get; set; }
			public int Column31 { get; set; }
			public int Column32 { get; set; }
			public int Column33 { get; set; }
			public int Column34 { get; set; }
			public int Column35 { get; set; }
			public int Column36 { get; set; }
			public int Column37 { get; set; }
			public int Column38 { get; set; }
			public int Column39 { get; set; }
			public int Column40 { get; set; }
			public int Column41 { get; set; }
			public int Column42 { get; set; }
			public int Column43 { get; set; }
			public int Column44 { get; set; }
			public int Column45 { get; set; }
			public int Column46 { get; set; }
			public int Column47 { get; set; }
			public int Column48 { get; set; }
			public int Column49 { get; set; }
			public int Column50 { get; set; }
		}
	}

	public class Benchmarks
	{
		[GlobalSetup]
		public void GlobalSetup()
		{
		}

		[GlobalCleanup]
		public void GlobalCleanup()
		{
		}

		[IterationSetup]
		public void IterationSetup()
		{
		}

		[IterationCleanup]
		public void IterationCleanup()
		{
		}

		[Benchmark]
		public void GetRecordsFieldCache()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CacheFields = false,
			};
			using (var stream = File.OpenRead(Program.GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				Program.Columns50 record;
				while (csv.Read())
				{
					record = csv.GetRecord<Program.Columns50>();
				}
			}
		}

		//[Benchmark]
		public void GetRecordsSpan()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CacheFields = false,
			};
			using (var stream = File.OpenRead(Program.GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				Program.Columns50 record;
				while (csv.Read())
				{
					record = csv.GetRecord<Program.Columns50>();
				}
			}
		}

		//[Benchmark]
		public void Parse()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			using (var stream = File.OpenRead(Program.GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				string[] record;
				while (parser.Read())
				{
					record = parser.Record;
				}
			}
		}
	}
}
