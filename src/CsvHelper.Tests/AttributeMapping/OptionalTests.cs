using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
    public class OptionalTests
    {
		[TestMethod]
        public void OptionalTest()
		{
			var parser = new ParserMock
			{
				{ "Id" },
				{ "1" },
				{ null }
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<OptionalTestClass>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.IsNull(records[0].Name);
			}
		}

		private class OptionalTestClass
		{
			public int Id { get; set; }

			[Optional]
			public string Name { get; set; }
		}
    }
}
