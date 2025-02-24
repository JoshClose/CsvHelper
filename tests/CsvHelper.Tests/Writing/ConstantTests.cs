﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Writing
{
	
	public class ConstantTests
	{
		[Fact]
		public void StringConstantTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
					new Test { Id = 2, Name = "two" }
				};

				csv.Context.RegisterClassMap<TestStringMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,constant");
				expected.AppendLine("2,constant");

				var result = reader.ReadToEnd();

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void NullConstantTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
				};

				csv.Context.RegisterClassMap<TestNullMap>();
				csv.WriteRecords(records);
				writer.Flush();

				Assert.Equal("1,\r\n", writer.ToString());
			}
		}

		[Fact]
		public void IntConstantTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
				};

				csv.Context.RegisterClassMap<TestIntMap>();
				csv.WriteRecords(records);
				writer.Flush();

				Assert.Equal("-1,one\r\n", writer.ToString());
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string? Name { get; set; }
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
