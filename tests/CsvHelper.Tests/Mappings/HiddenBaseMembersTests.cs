// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings
{
	
    public class HiddenBaseMembersTests
    {
		[Fact]
		public void ReadWithAutoMapTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
			};
			using (var csv = new CsvReader(parserMock))
			{
				var records = csv.GetRecords<Bar>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
			}
		}

		[Fact]
        public void ReadWithClassMapTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
			};
			using (var csv = new CsvReader(parserMock))
			{
				csv.Context.RegisterClassMap<BarMap>();
				var records = csv.GetRecords<Bar>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
			}
		}

		[Fact]
		public void WriteWithAutoMapTest()
		{
			var records = new List<Bar>
			{
				new Bar { Id = 1 },
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		[Fact]
		public void WriteWithClassMapTest()
		{
			var records = new List<Bar>
			{
				new Bar { Id = 1 },
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<BarMap>();
				csv.WriteRecords(records);

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private abstract class Foo
		{
			public string Id { get; set; }
		}

		private class Bar : Foo
		{
			public new int Id { get; set; }
		}

		private class BarMap : ClassMap<Bar>
		{
			public BarMap()
			{
				Map(m => m.Id);
			}
		}
    }
}
