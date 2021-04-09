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

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	
	public class NumberStylesMapTests
    {
		private const decimal amount = 123;

		[Fact]
		public void Parameter_WithName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id");
			map.Parameter("amount").TypeConverterOption.NumberStyles(NumberStyles.AllowParentheses);

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.NumberStyles);
			Assert.Equal(NumberStyles.AllowParentheses, map.ParameterMaps[1].Data.TypeConverterOptions.NumberStyles);
		}

		[Fact]
		public void Parameter_WithConstructorFunctionAndName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter(() => ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo))), "id");
			map.Parameter(() => ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo))), "amount").TypeConverterOption.NumberStyles(NumberStyles.AllowParentheses);

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.NumberStyles);
			Assert.Equal(NumberStyles.AllowParentheses, map.ParameterMaps[1].Data.TypeConverterOptions.NumberStyles);
		}

		[Fact]
		public void Parameter_WithConstructorAndProperty_CreatesParameterMaps()
		{
			var constructor = ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo)));
			var parameters = constructor.GetParameters();

			var map = new DefaultClassMap<Foo>();
			map.Parameter(constructor, parameters[0]);
			map.Parameter(constructor, parameters[1]).TypeConverterOption.NumberStyles(NumberStyles.AllowParentheses);

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.NumberStyles);
			Assert.Equal(NumberStyles.AllowParentheses, map.ParameterMaps[1].Data.TypeConverterOptions.NumberStyles);
		}

		[Fact]
		public void GetRecords_WithParameterMap_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "amount" },
				{ "1", $"({amount})" },
			};
			using (var csv = new CsvReader(parser))
			{
				var map = csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(-amount, records[0].Amount);
			}
		}

		[Fact]
		public void GetRecords_WithParameterMap_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", $"({amount})" },
			};
			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooMap>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(-amount, records[0].Amount);
			}
		}

		[Fact]
		public void WriteRecords_WithParameterMap_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, amount),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();

				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Amount\r\n");
				expected.Append($"1,{amount}\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public decimal Amount { get; private set; }

			public Foo(int id, decimal amount)
			{
				Id = id;
				Amount = amount;
			}
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Amount);
				Parameter("id");
				Parameter("amount").TypeConverterOption.NumberStyles(NumberStyles.AllowParentheses);
			}
		}
	}
}
