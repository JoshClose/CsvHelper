// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Xunit;
using System.Linq;

namespace CsvHelper.Tests.Mappings.Attribute
{
	
	public class OptionalTests
	{
		[Fact]
		public void OptionalTest()
		{
			var parser = new ParserMock
			{
				{ "Id" },
				{ "1" },
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<OptionalTestClass>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Null(records[0].Name);
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
