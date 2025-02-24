// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.Property
{
	
    public class ConstantTests
    {
		[Fact]
		public void GetRecords_ConstantSet_FieldExists_ReturnsRecordsWithConstant()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Equal("Bar", records[0].Name);
			}
		}

		[Fact]
		public void GetRecords_ConstantSet_FieldMissing_ReturnsRecordsWithConstant()
		{
			var s = new StringBuilder();
			s.Append("Id\r\n");
			s.Append("1\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Equal("Bar", records[0].Name);
			}
		}

		private class Foo
		{
			public string? Id { get; set; }

			public string? Name { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id).Index(0);
				Map(m => m.Name).Index(1).Constant("Bar");
			}
		}
	}
}
