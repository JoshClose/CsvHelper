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
    public class DateTimeStylesAttributeTests
	{
		private const string DATE = "12/25/2020";
		private readonly DateTimeOffset date = DateTimeOffset.Parse(DATE);

		[TestMethod]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresParameterMaps()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
			var map = context.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.DateTimeStyle);
			Assert.AreEqual(DateTimeStyles.AllowLeadingWhite, map.ParameterMaps[1].Data.TypeConverterOptions.DateTimeStyle);
		}

		[TestMethod]
		public void GetRecords_WithCultureInfoAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "date" },
				{ "1", $" {DATE}" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void GetRecords_WithCultureInfoAttributes_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", $" {DATE}" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void WriteRecords_WithCultureInfoAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, date),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Date\r\n");
				expected.Append($"1,{date.ToString(null, CultureInfo.InvariantCulture)}\r\n");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public DateTimeOffset Date { get; private set; }

			public Foo(int id, [DateTimeStyles(DateTimeStyles.AllowLeadingWhite)] DateTimeOffset date)
			{
				Id = id;
				Date = date;
			}
		}
	}
}
