// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderSubClassingTests
	{
		[TestMethod]
		public void GetRecordTest()
		{
			var data = new List<string[]>
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				new[] { "2", "two" },
				null
			};

			var parserMock = new ParserMock( new Queue<string[]>( data ) );

			var csvReader = new MyCsvReader( parserMock );
			csvReader.Configuration.ClassMapping<TestMap>();
			csvReader.GetRecords<Test>().ToList();
		}

		private class MyCsvReader : CsvReader
		{
			public MyCsvReader( ICsvParser parser ) : base( parser ){}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Id );
				Map( m => m.Name );
			}
		}
	}
}
