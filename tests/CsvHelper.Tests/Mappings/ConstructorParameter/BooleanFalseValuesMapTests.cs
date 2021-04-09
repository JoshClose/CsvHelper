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
	
	public class BooleanFalseValuesMapTests
	{
		[Fact]
		public void AutoMap_WithBooleanFalseValuesAttribute_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id");
			map.Parameter("boolean").TypeConverterOption.BooleanValues(false, true, "Bar");

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Empty(map.ParameterMaps[0].Data.TypeConverterOptions.BooleanTrueValues);
			Assert.Empty(map.ParameterMaps[0].Data.TypeConverterOptions.BooleanFalseValues);
			Assert.Empty(map.ParameterMaps[1].Data.TypeConverterOptions.BooleanTrueValues);
			Assert.Single(map.ParameterMaps[1].Data.TypeConverterOptions.BooleanFalseValues);
			Assert.Equal("Bar", map.ParameterMaps[1].Data.TypeConverterOptions.BooleanFalseValues[0]);
		}

		[Fact]
		public void GetRecords_WithBooleanFalseValuesAttribute_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "boolean" },
				{ "1", "Bar" },
			};
			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.False(records[0].Boolean);
			}
		}

		[Fact]
		public void GetRecords_WithBooleanFalseValuesAttribute_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "Bar" },
			};
			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooMap>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.False(records[0].Boolean);
			}
		}

		[Fact]
		public void WriteRecords_WithBooleanFalseValuesAttribute_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, false),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Boolean\r\n");
				expected.Append("1,False\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public bool Boolean { get; private set; }

			public Foo(int id, bool boolean)
			{
				Id = id;
				Boolean = boolean;
			}
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Boolean);
				Parameter("id");
				Parameter("boolean").TypeConverterOption.BooleanValues(false, true, "Bar");
			}
		}
	}
}
