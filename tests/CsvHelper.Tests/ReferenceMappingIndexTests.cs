// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ReferenceMappingIndexTests
	{
		[TestMethod]
		public void MapByIndexTest()
		{
			var data = new List<string[]>
			{
				new[] { "0", "1" },
				new[] { "2", "3" },
				null,
			};
			var queue = new Queue<string[]>( data );
			var parserMock = new ParserMock( queue );

			var csv = new CsvReader( parserMock );
			csv.Configuration.HasHeaderRecord = false;
			csv.Configuration.RegisterClassMap<AMap>();

			var records = csv.GetRecords<A>().ToList();
			Assert.AreEqual( 1, records[0].Id );
			Assert.AreEqual( 0, records[0].B.Id );
			Assert.AreEqual( 3, records[1].Id );
			Assert.AreEqual( 2, records[1].B.Id );
		}

		private class A
		{
			public int Id { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public int Id { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				Map( m => m.Id ).Index( 1 );
				References<BMap>( m => m.B );
			}
		}

		private sealed class BMap : ClassMap<B>
		{
			public BMap()
			{
				Map( m => m.Id ).Index( 0 );
			}
		}
	}
}
