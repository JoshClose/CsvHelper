// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.Tests.AutoMapping
{
	
	public class MappingTypeOfTypeTest
	{
		[Fact]
		public void ClassWithPropertyOfTypeTypeShouldNotCauseStackOverflowExceptionTest()
		{
			var parser = new ParserMock
			{
				{ "Id" },
				{ "1" },
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<EquipmentDataPoint>().ToList();
				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
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
