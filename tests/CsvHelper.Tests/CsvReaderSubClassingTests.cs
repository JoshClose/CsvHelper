// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvReaderSubClassingTests
	{
		[Fact]
		public void GetRecordTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csvReader = new MyCsvReader(parserMock);
			csvReader.GetRecords<Test>().ToList();
		}

		private class MyCsvReader : CsvReader
		{
			public MyCsvReader(IParser parser) : base(parser) { }
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; } = string.Empty;
		}
	}
}
