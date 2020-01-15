// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class NoPropertyMappingTests
	{
		[TestMethod]
		public void NoPropertyWithHeaderAndNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Configuration.RegisterClassMap<TestWithNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("Id,Constant,Name");
				expected.AppendLine("1,const,");
				expected.AppendLine("2,const,");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void NoPropertyWithHeaderAndNoNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Configuration.RegisterClassMap<TestWithNoNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("Id,");
				expected.AppendLine("1,const");
				expected.AppendLine("2,const");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void NoPropertyWithNoHeaderAndNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestWithNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("1,const,");
				expected.AppendLine("2,const,");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void NoPropertyWithNoHeaderAndNoNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestWithNoNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("1,const");
				expected.AppendLine("2,const");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void OutOfOrderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
					new Test { Id = 2, Name = "two" }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestMapOutOfOrderWithEmptyFieldsMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("one,,,1");
				expected.AppendLine("two,,,2");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestWithNameMap : ClassMap<Test>
		{
			public TestWithNameMap()
			{
				Map(m => m.Id);
				Map().Name("Constant").Constant("const");
				Map(m => m.Name);
			}
		}

		private sealed class TestWithNoNameMap : ClassMap<Test>
		{
			public TestWithNoNameMap()
			{
				Map(m => m.Id);
				Map().Constant("const");
			}
		}

		private sealed class TestMapOutOfOrderWithEmptyFieldsMap : ClassMap<Test>
		{
			public TestMapOutOfOrderWithEmptyFieldsMap()
			{
				Map(m => m.Name).Index(0);
				Map().Index(1).Constant(null);
				Map().Index(2).Constant(string.Empty);
				Map(m => m.Id).Index(3);
			}
		}
	}
}
