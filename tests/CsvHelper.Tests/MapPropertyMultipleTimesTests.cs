// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class MapPropertyMultipleTimesTests
	{
		[Fact]
		public void MapPropertiesToMultipleFieldsWhenWritingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<Test>
				{
					new Test { Id = 1, Name = "one" }
				};

				csv.Context.RegisterClassMap<TestMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id1,Name1,Id2,Name2");
				expected.AppendLine("1,one,1,one");

				var result = reader.ReadToEnd();

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void MapPropertiesToMultipleFieldsWhenReadingTest()
		{
			// This is not something that anyone should do, but this
			// is the expected behavior if they do.

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id1,Name1,Id2,Name2");
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.Equal(2, records[0].Id);
				Assert.Equal("two", records[0].Name);
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string? Name { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.Id).Name("Id1");
				Map(m => m.Name).Name("Name1");
				Map(m => m.Id, false).Name("Id2");
				Map(m => m.Name, false).Name("Name2");
			}
		}
	}
}
