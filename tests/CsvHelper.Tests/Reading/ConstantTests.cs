// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;
using System.IO;
using System.Globalization;

namespace CsvHelper.Tests.Reading
{
	
	public class ConstantTests
	{
		[Fact]
		public void ConstantAlwaysReturnsSameValueTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csv = new CsvReader(parser);
			csv.Context.RegisterClassMap<TestStringMap>();
			var records = csv.GetRecords<Test>().ToList();

			Assert.Equal(1, records[0].Id);
			Assert.Equal("constant", records[0].Name);
			Assert.Equal(2, records[1].Id);
			Assert.Equal("constant", records[1].Name);
		}

		[Fact]
		public void ConstantIsNullTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue(new[] { "Id", "Name" });
			rows.Enqueue(new[] { "1", "one" });
			rows.Enqueue(new[] { "2", "two" });
			rows.Enqueue(null);
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csv = new CsvReader(parser);
			csv.Context.RegisterClassMap<TestNullMap>();
			var records = csv.GetRecords<Test>().ToList();

			Assert.Equal(1, records[0].Id);
			Assert.Null(records[0].Name);
			Assert.Equal(2, records[1].Id);
			Assert.Null(records[1].Name);
		}

		[Fact]
		public void IntConstantTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var reader = new StringReader("1,one\r\n"))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.RegisterClassMap<TestIntMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.Equal(-1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestStringMap : ClassMap<Test>
		{
			public TestStringMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Constant("constant");
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

		private sealed class TestIntMap : ClassMap<Test>
		{
			public TestIntMap()
			{
				Map(m => m.Id).Constant(-1);
				Map(m => m.Name);
			}
		}
	}
}
