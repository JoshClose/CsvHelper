// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class MappingTypeOfTypeTest
	{
		[TestMethod]
		public void ClassWithPropertyOfTypeTypeShouldNotCauseStackOverflowExceptionTest()
		{
			var data = new List<string[]>
			{
				new[] { "Id" },
				new[] { "1" },
				null
			};
			var queue = new Queue<string[]>(data);
			var parser = new ParserMock(queue);

			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<EquipmentDataPoint>().ToList();
				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
			}
		}

		public class EquipmentDataPoint
		{
			public int Id { get; set; }

			[CsvHelper.Configuration.Attributes.Ignore]
			public Type ValueType { get; set; }
		}
	}
}
