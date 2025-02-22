﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Mappings.Attribute
{
	
	public class NullValuesTests
	{
		[Fact]
		public void NullValuesTest()
		{
			using (var reader = new StringReader("Id,Name\r\nNULL,null\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<NullValuesTestClass>().ToList();
				Assert.Null(records[0].Id);
				Assert.Null(records[0].Name);
			}
		}

		private class NullValuesTestClass
		{
			[NullValues("NULL")]
			public int? Id { get; set; }

			[NullValues("null")]
			public string? Name { get; set; }
		}
	}
}
