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
