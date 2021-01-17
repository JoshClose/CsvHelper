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
			//BenchmarkRunner.Run<Benchmarks>(); return;

			Test(); return;

			//WriteField(50, 1_000_000, true); return;
			//WriteRecords(1_000_000);

			for (var i = 0; i < 10; i++)
			{
				//LumenworksParse();
				//StefanBertelsParse();
				//StackParse();
				//StackParse2();
				//SoftCircuitsParse();
				//CsvHelperParse();

				//ReadGetField();
				//ReadGetRecords();
				//ReadGetRecordsAsync().Wait();

				Console.WriteLine();
			}
		}

		static void Test()
		{
			var s = new StringBuilder();
			s.Append("\r");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				while (parser.Read())
				{
					var record = parser.Record;
				}
			}
		}

		static string GetFilePath()
		{
			var homePath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
			var filePath = Path.Combine(homePath, "Documents", "performance.csv");

			return filePath;
		}

		static void WriteField(int columns = 50, int rows = 2_000_000, bool quoteAllFields = false)
		{
			Console.WriteLine("Writing using WriteField");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				//Delimiter = ";",
				ShouldQuote = (field, context) => quoteAllFields,
			};
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
						Column1 = 1,
						Column2 = 2,
						Column3 = 3,
						Column4 = 4,
						Column5 = 5,
						Column6 = 6,
						Column7 = 7,
						Column8 = 8,
						Column9 = 9,
						Column10 = 10,
						Column11 = 11,
						Column12 = 12,
						Column13 = 13,
						Column14 = 14,
						Column15 = 15,
						Column16 = 16,
						Column17 = 17,
						Column18 = 18,
						Column19 = 19,
						Column20 = 20,
						Column21 = 21,
						Column22 = 22,
						Column23 = 23,
						Column24 = 24,
						Column25 = 25,
						Column26 = 26,
						Column27 = 27,
						Column28 = 28,
						Column29 = 29,
						Column30 = 30,
						Column31 = 31,
						Column32 = 32,
						Column33 = 33,
						Column34 = 34,
						Column35 = 35,
						Column36 = 36,
						Column37 = 37,
						Column38 = 38,
						Column39 = 39,
						Column40 = 40,
						Column41 = 41,
						Column42 = 42,
						Column43 = 43,
						Column44 = 44,
						Column45 = 45,
						Column46 = 46,
						Column47 = 47,
						Column48 = 48,
						Column49 = 49,
						Column50 = 50,
					};
					records.Add(record);
				}

				csv.WriteRecords(records);
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static void CsvHelperParse()
		{
			Console.WriteLine("CsvHelper parsing 2");

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				//BufferSize = 1024 * 4,
				BufferSize = 16
			};
			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var stopwatch = new Stopwatch();
				stopwatch.Start();

				//string[] record;
				while (parser.Read())
				{
					//record = parser.Record;
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

		static void LumenworksParse()
		{
			Console.WriteLine("Lumenworks parsing");
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			using (var stream = File.OpenRead(GetFilePath()))
			using (var reader = new StreamReader(stream))
			using (var csv = new LumenWorks.Framework.IO.Csv.CsvReader(reader))
			{
				var fieldCount = csv.FieldCount;
				var headers = csv.GetFieldHeaders();
				while (csv.ReadNextRecord())
				{
					var row = new string[fieldCount];
					for (var i = 0; i < fieldCount; i++)
					{
						row[i] = csv[i];
					}
				}
			}

			stopwatch.Stop();
			Console.WriteLine(stopwatch.Elapsed);
		}

		static void SoftCircuitsParse()
		{
			Console.WriteLine("SoftCircuits parsing");

			using (var stream = File.OpenRead(GetFilePath()))
			using (var csv = new SoftCircuits.CsvParser.CsvReader(stream))
			{
				var stopwatch = new Stopwatch();
				stopwatch.Start();

				string[] row = null;
				while (csv.ReadRow(ref row))
				{
				}

				stopwatch.Stop();
				Console.WriteLine(stopwatch.Elapsed);
			}
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

		private class Columns50
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
		private const int LOOPS = 100_000;

		[Benchmark]
		public void A()
		{
			var isQuoted = false;
			var config = new Config();
			for (var i = 0; i < LOOPS; i++)
			{
				isQuoted = !isQuoted;
				var value = new ReadOnlyRefStruct(isQuoted, config);
				Method(value);
			}
		}

		[Benchmark]
		public void B()
		{
			var isQuoted = false;
			var config = new Config();
			for (var i = 0; i < LOOPS; i++)
			{
				isQuoted = !isQuoted;
				var value = new Record
				{
					IsQuoted = isQuoted,
					Config = config,
				};
				Method(value);
			}
		}

		[Benchmark]
		public void C()
		{
			var isQuoted = false;
			var config = new Config();
			var value = new Class
			{
				Config = config,
			};
			for (var i = 0; i < LOOPS; i++)
			{
				isQuoted = !isQuoted;
				value.IsQuoted = isQuoted;
				Method(value);
			}
		}

		private void Method(ReadOnlyRefStruct value)
		{
			if (value.Config.Trim)
			{
				Console.WriteLine("NOOP");
			}
		}

		private void Method(Record value)
		{
			if (value.Config.Trim)
			{
				Console.WriteLine("NOOP");
			}
		}

		private void Method(Class value)
		{
			if (value.Config.Trim)
			{
				Console.WriteLine("NOOP");
			}
		}

		private class Config
		{
			public bool Trim { get; set; }
		}

		private readonly ref struct ReadOnlyRefStruct
		{
			public bool IsQuoted { get; }

			public Config Config { get; }

			public ReadOnlyRefStruct(bool isQuoted, Config config)
			{
				IsQuoted = isQuoted;
				Config = config;
			}
		}

		private record Record
		{
			public bool IsQuoted { get; init; }

			public Config Config { get; init; }
		}

		private class Class
		{
			public bool IsQuoted { get; set; }

			public Config Config { get; set; }
		}
	}
}
