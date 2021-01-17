// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Culture
{
	[TestClass]
	public class TypeConverterOptionsFactoryTests
	{
		[TestMethod]
		public void AddGetRemoveTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			var customOptions = new TypeConverterOptions
			{
				Formats = new string[] { "custom" },
			};
			context.TypeConverterOptionsCache.AddOptions<string>(customOptions);
			var options = context.TypeConverterOptionsCache.GetOptions<string>();

			Assert.AreEqual(customOptions.Formats, options.Formats);

			context.TypeConverterOptionsCache.RemoveOptions<string>();

			options = context.TypeConverterOptionsCache.GetOptions<string>();

			Assert.AreNotEqual(customOptions.Formats, options.Formats);
		}

		[TestMethod]
		public void GetFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				writer.WriteLine("\"1,234\",\"5,678\"");
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyles = NumberStyles.AllowThousands };
				csvReader.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.Read();
				Assert.AreEqual(1234, csvReader.GetField<int>(0));
				Assert.AreEqual(5678, csvReader.GetField(typeof(int), 1));
			}
		}

		[TestMethod]
		public void GetFieldSwitchCulturesTest()
		{
			GetFieldForCultureTest("\"1234,32\",\"5678,44\"", "fr-FR", 1234.32M, 5678.44M);
			GetFieldForCultureTest("\"9876.54\",\"3210.98\"", "en-GB", 9876.54M, 3210.98M);
			GetFieldForCultureTest("\"4455,6677\",\"9988,77\"", "el-GR", 4455.6677M, 9988.77M);
		}

		private static void GetFieldForCultureTest(string csvText, string culture, decimal expected1, decimal expected2)
		{
			var config = new CsvConfiguration(new CultureInfo(culture))
			{
				HasHeaderRecord = false,
				Delimiter = ",",
			};
			using (var reader = new StringReader(csvText))
			using (var csvReader = new CsvReader(reader, config))
			{
				csvReader.Read();
				Assert.AreEqual(expected1, csvReader.GetField<decimal>(0));
				Assert.AreEqual(expected2, csvReader.GetField(typeof(decimal), 1));
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				writer.WriteLine("\"1,234\",\"5,678\"");
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyles = NumberStyles.AllowThousands };
				csvReader.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void GetRecordsAppliedWhenMappedTest()
		{
			var config = new CsvConfiguration(new CultureInfo("en-US"))
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				writer.WriteLine("\"1,234\",\"$5,678\"");
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyles = NumberStyles.AllowThousands };
				csvReader.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.Context.RegisterClassMap<TestMap>();
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void WriteFieldTest()
		{
			var config = new CsvConfiguration(new CultureInfo("en-US"))
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, config))
			{
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvWriter.WriteField(1234);
				csvWriter.NextRecord();
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual("\"$1,234.00\"\r\n", record);
			}
		}

		[TestMethod]
		public void WriteRecordsTest()
		{
			var config = new CsvConfiguration(new CultureInfo("en-US"))
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvWriter.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual("\"$1,234.00\",\"$5,678.00\"\r\n", record);
			}
		}

		[TestMethod]
		public void WriteRecordsAppliedWhenMappedTest()
		{
			var config = new CsvConfiguration(new CultureInfo("en-US"))
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Context.TypeConverterOptionsCache.AddOptions<int>(options);
				csvWriter.Context.RegisterClassMap<TestMap>();
				csvWriter.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual("\"$1,234.00\",\"5,678.00\"\r\n", record);
			}
		}

		private class Test
		{
			public int Number { get; set; }

			public int NumberOverridenInMap { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.Number);
				Map(m => m.NumberOverridenInMap)
					.TypeConverterOption.NumberStyles(NumberStyles.AllowThousands | NumberStyles.AllowCurrencySymbol)
					.TypeConverterOption.Format("N");
			}
		}
	}
}
