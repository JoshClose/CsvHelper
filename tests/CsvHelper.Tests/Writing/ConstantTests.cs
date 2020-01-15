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
	public class ConstantTests
	{
		[TestMethod]
		public void StringConstantTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
					new Test { Id = 2, Name = "two" }
				};

				csv.Configuration.RegisterClassMap<TestStringMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,constant");
				expected.AppendLine("2,constant");

				var result = reader.ReadToEnd();

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void NullConstantTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
				};

				csv.Configuration.RegisterClassMap<TestNullMap>();
				csv.Configuration.HasHeaderRecord = false;
				csv.WriteRecords(records);
				writer.Flush();

				Assert.AreEqual("1,\r\n", writer.ToString());
			}
		}

		[TestMethod]
		public void IntConstantTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
				};

				csv.Configuration.RegisterClassMap<TestIntMap>();
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.SanitizeForInjection = false;
				csv.WriteRecords(records);
				writer.Flush();

				Assert.AreEqual("-1,one\r\n", writer.ToString());
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestIntMap : ClassMap<Test>
		{
			public TestIntMap()
			{
				Map(m => m.Id).Constant(-1);
				Map(m => m.Name);
			}
		}

		private sealed class TestNullMap : ClassMap<Test>
		{
			public TestNullMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Constant(null);
			}
		}

		private sealed class TestStringMap : ClassMap<Test>
		{
			public TestStringMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Constant("constant");
			}
		}
	}
}
