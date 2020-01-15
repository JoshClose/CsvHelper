// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
    public class HiddenBaseMembersTests
    {
		[TestMethod]
		public void ReadWithAutoMapTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
				null
			};
			using (var csv = new CsvReader(parserMock))
			{
				var records = csv.GetRecords<Bar>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
			}
		}

		[TestMethod]
        public void ReadWithClassMapTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
				null
			};
			using (var csv = new CsvReader(parserMock))
			{
				csv.Configuration.RegisterClassMap<BarMap>();
				var records = csv.GetRecords<Bar>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
			}
		}

		[TestMethod]
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

				var expected = new StringBuilder();
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		[TestMethod]
		public void WriteWithClassMapTest()
		{
			var records = new List<Bar>
			{
				new Bar { Id = 1 },
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.RegisterClassMap<BarMap>();
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.AreEqual(expected.ToString(), writer.ToString());
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
