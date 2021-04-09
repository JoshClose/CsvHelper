// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class TypeConverterTests
	{
		[Fact]
		public void ReaderInheritedConverter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1" },
			};
			var csv = new CsvReader(parserMock);
			csv.Context.RegisterClassMap<TestMap>();
			var list = csv.GetRecords<Test>().ToList();
		}

		private class Test
		{
			public int IntColumn { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.IntColumn).Index(0).TypeConverter<Converter>();
			}
		}

		private class Converter : Int32Converter
		{
		}
	}
}
