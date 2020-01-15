// Copyright 2009-2020 Josh Close and Contributors
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

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterOptionsFactoryTests
	{
		[TestMethod]
		public void AddGetRemoveTest()
		{
			var customOptions = new TypeConverterOptions
			{
				Formats = new string[] { "custom" },
			};
			var typeConverterOptionsFactory = new TypeConverterOptionsCache();

			typeConverterOptionsFactory.AddOptions<string>(customOptions);
			var options = typeConverterOptionsFactory.GetOptions<string>();

			Assert.AreEqual(customOptions.Formats, options.Formats);

			typeConverterOptionsFactory.RemoveOptions<string>();

			options = typeConverterOptionsFactory.GetOptions<string>();

			Assert.AreNotEqual(customOptions.Formats, options.Formats);
		}

		[TestMethod]
		public void GetFieldTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csvReader.Configuration.Delimiter = ",";
				writer.WriteLine("\"1,234\",\"5,678\"");
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Read();
				Assert.AreEqual(1234, csvReader.GetField<int>(0));
				Assert.AreEqual(5678, csvReader.GetField(typeof(int), 1));
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csvReader.Configuration.Delimiter = ",";
				writer.WriteLine("\"1,234\",\"5,678\"");
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void GetRecordsAppliedWhenMappedTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvReader = new CsvReader(reader, new CultureInfo("en-US")))
			{
				csvReader.Configuration.Delimiter = ",";
				writer.WriteLine("\"1,234\",\"$5,678\"");
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Configuration.RegisterClassMap<TestMap>();
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void WriteFieldTest()
		{
			var options = new TypeConverterOptions { Formats = new string[] { "c" } };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, new CultureInfo("en-US")))
			{
				csvWriter.Configuration.Delimiter = ",";
				csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
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
			var options = new TypeConverterOptions { Formats = new string[] { "c" } };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, new CultureInfo("en-US")))
			{
				csvWriter.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
				csvWriter.Configuration.HasHeaderRecord = false;
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
			var options = new TypeConverterOptions { Formats = new string[] { "c" } };

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, new CultureInfo("en-US")))
			{
				csvWriter.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<int>(options);
				csvWriter.Configuration.HasHeaderRecord = false;
				csvWriter.Configuration.RegisterClassMap<TestMap>();
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
