// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	
    public class CultureInfoAttributeTests
    {
		private const decimal AMOUNT = 123_456.789M;
		private const string CULTURE = "fr-FR";
		private readonly string amount = AMOUNT.ToString(new CultureInfo(CULTURE));

		[Fact]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			var map = context.AutoMap<Foo>();

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo(CULTURE), map.ParameterMaps[1].Data.TypeConverterOptions.CultureInfo);
		}

		[Fact]
		public void GetRecords_WithCultureInfoAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "amount" },
				{ "1", amount },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(AMOUNT, records[0].Amount);
			}
		}

		[Fact]
		public void GetRecords_WithCultureInfoAttributes_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", amount },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(AMOUNT, records[0].Amount);
			}
		}

		[Fact]
		public void WriteRecords_WithCultureInfoAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, AMOUNT),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Amount\r\n");
				expected.Append($"1,{AMOUNT}\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public decimal Amount { get; private set; }

			public Foo(int id, [CultureInfo(CULTURE)] decimal amount)
			{
				Id = id;
				Amount = amount;
			}
		}

	}
}
