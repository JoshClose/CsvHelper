// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Reflection;
using CsvHelper.Tests.Mocks;
using CsvHelper.Configuration.Attributes;
using System.Linq;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class CollectionGenericConverterTests
	{
		[Fact]
		public void FullWriteTest()
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
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal("1,2,3\r\n", result);
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

		private class Test
		{
			public List<int?> List { get; set; }
		}

		private sealed class TestIndexMap : ClassMap<Test>
		{
			public TestIndexMap()
			{
				Map(m => m.List).Index(1, 3);
			}
		}

		private sealed class TestNamedMap : ClassMap<Test>
		{
			public TestNamedMap()
			{
				Map(m => m.List).Name("List");
			}
		}

		private sealed class TestDefaultMap : ClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map(m => m.List);
			}
		}

		private class NullValuesAttributeIndexTest
		{
			[Index(0, 2)]
			[NullValues("NULL")]
			public List<int?> List { get; set; }
		}
	}
}
