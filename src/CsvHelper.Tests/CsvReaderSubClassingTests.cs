// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderSubClassingTests
	{
		[Fact]
		public void GetRecordTest()
		{
			var data = new List<string[]>
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				new[] { "2", "two" },
				null
			};

			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var count = -1;
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				count++;
				return data[count];
			} );

			var csvReader = new MyCsvReader( parserMock.Object );
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
	}
}
