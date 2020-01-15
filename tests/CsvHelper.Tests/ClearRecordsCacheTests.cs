// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ClearRecordsCacheTests
	{
		[TestMethod]
		public void ClearReaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<TestMap1>();
				csv.Read();
				var record = csv.GetRecord<Test>();

				Assert.IsNotNull(record);
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual(null, record.Name);

				stream.Position = 0;
				csv.Context.ClearCache(Caches.ReadRecord);

				csv.Configuration.RegisterClassMap<TestMap2>();
				csv.Read();
				record = csv.GetRecord<Test>();

				Assert.IsNotNull(record);
				Assert.AreEqual(0, record.Id);
				Assert.AreEqual("two", record.Name);
			}
		}

		[TestMethod]
		public void ClearWriterTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.RegisterClassMap<TestMap1>();
				var record = new Test { Id = 1, Name = "one" };
				csv.WriteRecord(record);
				csv.NextRecord();

				csv.Context.ClearCache(Caches.WriteRecord);
				csv.Configuration.RegisterClassMap<TestMap2>();
				record = new Test { Id = 2, Name = "two" };
				csv.WriteRecord(record);
				csv.NextRecord();

				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("1");
				expected.AppendLine("two");

				Assert.AreEqual(expected.ToString(), data);
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestMap1 : ClassMap<Test>
		{
			public TestMap1()
			{
				Map(m => m.Id);
			}
		}

		private sealed class TestMap2 : ClassMap<Test>
		{
			public TestMap2()
			{
				Map(m => m.Name);
			}
		}
	}
}
