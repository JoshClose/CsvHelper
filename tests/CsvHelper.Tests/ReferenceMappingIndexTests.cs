// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class ReferenceMappingIndexTests
	{
		[Fact]
		public void MapByIndexTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				new[] { "0", "1" },
				new[] { "2", "3" },
			};

			var csv = new CsvReader(parserMock);
			csv.Context.RegisterClassMap<AMap>();

			var records = csv.GetRecords<A>().ToList();
			Assert.Equal(1, records[0].Id);
			Assert.Equal(0, records[0].B.Id);
			Assert.Equal(3, records[1].Id);
			Assert.Equal(2, records[1].B.Id);
		}

		private class A
		{
			public int Id { get; set; }

			public B B { get; set; } = new B();
		}

		private class B
		{
			public int Id { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				Map(m => m.Id).Index(1);
				References<BMap>(m => m.B);
			}
		}

		private sealed class BMap : ClassMap<B>
		{
			public BMap()
			{
				Map(m => m.Id).Index(0);
			}
		}
	}
}
