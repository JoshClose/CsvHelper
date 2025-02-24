﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class IndexTests
	{
		[Fact]
		public void IndexTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var reader = new StringReader("a,1,b,one,c\r\n"))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<IndexTestClass>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		private class IndexTestClass
		{
			[Index(1)]
			public int Id { get; set; }

			[Index(3)]
			public string? Name { get; set; }
		}
	}
}
