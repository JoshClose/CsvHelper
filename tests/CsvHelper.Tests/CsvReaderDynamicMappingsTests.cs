// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderDynamicMappingsTests
	{
		[TestMethod]
		public void CanMapDynamics()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] {"IntCol", "StringCol"});
			queue.Enqueue(new[] {"1", "one"});
			queue.Enqueue(new[] {"2", "two"});
			queue.Enqueue(null);

			var parserMock = new ParserMock(queue);
			var csvReader = new CsvReader(parserMock);
			csvReader.Configuration.RegisterClassMap<DynamicMappingsTypeClassMap>();

			var records = csvReader.GetRecords<DynamicMappingsType>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(1, records[0].IntCol);
			Assert.AreEqual("one", records[0].StringCol);
			Assert.AreEqual(2, records[1].IntCol);
			Assert.AreEqual("two", records[1].StringCol);
		}

		public class DynamicMappingsTypeClassMap : ClassMap<DynamicMappingsType>
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

	public class DynamicMappingsType
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
}
