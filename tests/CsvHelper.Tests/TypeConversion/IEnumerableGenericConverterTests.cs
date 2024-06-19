// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using System.Reflection;
using CsvHelper.Tests.Mocks;
using CsvHelper.Configuration.Attributes;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class IEnumerableGenericConverterTests
	{
		[Fact]
		public void FullReadNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List!.ToList();

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list[0]);
				Assert.Equal(3, list[1]);
				Assert.Equal(4, list[2]);
			}
		}

		[Fact]
		public void FullReadWithHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List!.ToList();

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list[0]);
				Assert.Equal(3, list[1]);
				Assert.Equal(4, list[2]);
			}
		}

		[Fact]
		public void FullReadWithDefaultHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestDefaultMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List!.ToList();

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list[0]);
				Assert.Equal(3, list[1]);
				Assert.Equal(4, list[2]);
			}
		}

		[Fact]
		public void FullReadWithNamedHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List!.ToList();

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list[0]);
				Assert.Equal(3, list[1]);
				Assert.Equal(4, list[2]);
			}
		}

		[Fact]
		public void FullReadWithHeaderListItemsScattered()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,A,List,B,List,After");
				writer.WriteLine("1,2,3,4,5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List!.ToList();

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list[0]);
				Assert.Equal(4, list[1]);
				Assert.Equal(6, list[2]);
			}
		}

		[Fact]
		public void FullWriteNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { List = new List<int?> { 1, 2, 3 } }
				};
				csv.Context.RegisterClassMap<TestIndexMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal(",1,2,3,\r\n", result);
			}
		}

		[Fact]
		public void FullWriteWithHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<Test>
				{
					new Test { List = new List<int?> { 1, 2, 3 } }
				};
				csv.Context.RegisterClassMap<TestIndexMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();
				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Before,List1,List2,List3,After");
				expected.AppendLine(",1,2,3,");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void GetRecords_NullValuesAttributeWithIndex_UsesCustomNullValue()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "NULL", "", "2" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<NullValuesAttributeIndexTest>().ToList();
			}
		}

		private class NullValuesAttributeIndexTest
		{
			[Index(0, 2)]
			[NullValues("NULL")]
			public List<int?> List { get; set; } = new List<int?>();
		}

		private class Test
		{
			public string? Before { get; set; }
			public IEnumerable<int?>? List { get; set; }
			public string? After { get; set; }
		}

		private sealed class TestIndexMap : ClassMap<Test>
		{
			public TestIndexMap()
			{
				Map(m => m.Before).Index(0);
				Map(m => m.List).Index(1, 3);
				Map(m => m.After).Index(4);
			}
		}

		private sealed class TestNamedMap : ClassMap<Test>
		{
			public TestNamedMap()
			{
				Map(m => m.Before).Name("Before");
				Map(m => m.List).Name("List");
				Map(m => m.After).Name("After");
			}
		}

		private sealed class TestDefaultMap : ClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map(m => m.Before);
				Map(m => m.List);
				Map(m => m.After);
			}
		}
	}
}
