// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderDynamicMappingsTests
	{
		[Fact]
		public void CanMapDynamics()
		{
			var parserMock = new ParserMock
			{
				{ "IntCol", "StringCol" },
				{ "1", "one" },
				{ "2", "two" },
			};
			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<DynamicMappingsTypeClassMap>();

			var records = csvReader.GetRecords<DynamicMappingsType>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(1, records[0].IntCol);
			Assert.Equal("one", records[0].StringCol);
			Assert.Equal(2, records[1].IntCol);
			Assert.Equal("two", records[1].StringCol);
		}

		private class DynamicMappingsType
		{
			public int IntCol { get; set; }
			public string StringCol { get; set; }

			public static IEnumerable<Expression<Func<DynamicMappingsType, dynamic>>> Mappings =>
				new List<Expression<Func<DynamicMappingsType, dynamic>>>
			{
				i => i.IntCol,
				i => i.StringCol
			};
		}

		private class DynamicMappingsTypeClassMap : ClassMap<DynamicMappingsType>
		{
			public DynamicMappingsTypeClassMap()
			{
				foreach (var mapping in DynamicMappingsType.Mappings)
				{
					Map(mapping);
				}
			}
		}
	}
}
