// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

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
