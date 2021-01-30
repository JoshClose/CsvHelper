// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	[TestClass]
    public class EnumIgnoreCaseTests
    {
		[TestMethod]
		public void AutoMap_WithEnumIgnoreCaseAttributes_ConfiguresParameterMaps()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
			var map = context.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual("id", map.ParameterMaps[0].Data.Names.First());
			Assert.AreEqual("bar", map.ParameterMaps[1].Data.Names.First());
			Assert.IsTrue(map.ParameterMaps[1].Data.TypeConverterOptions.EnumIgnoreCase.GetValueOrDefault());
		}

		[TestMethod]
		public void GetRecords_WithEnumIgnoreCaseAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "bar" },
				{ "1", "one" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(Bar.One, records[0].Bar);
			}
		}

		[TestMethod]
		public void GetRecords_WithEnumIgnoreCaseAttributes_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(Bar.One, records[0].Bar);
			}
		}

		[TestMethod]
		public void WriteRecords_WithIgnoreAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, Bar.None),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Bar\r\n");
				expected.Append("1,None\r\n");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public Bar Bar { get; private set; }

			public Foo(int id, [EnumIgnoreCase] Bar bar)
			{
				Id = id;
				Bar = bar;
			}
		}

		private enum Bar
		{
			None = 0,
			One = 1
		}
	}
}
