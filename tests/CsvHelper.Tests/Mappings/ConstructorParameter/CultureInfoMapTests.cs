﻿// Copyright 2009-2024 Josh Close
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
using System.Threading;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	
    public class CultureInfoMapTests
    {
		private const decimal AMOUNT = 123_456.789M;
		private const string CULTURE = "fr-FR";
		private readonly string amount = AMOUNT.ToString(new CultureInfo(CULTURE));

		[Fact]
		public void Parameter_WithName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id");
			map.Parameter("amount").TypeConverterOption.CultureInfo(new CultureInfo(CULTURE));

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo(CULTURE), map.ParameterMaps[1].Data.TypeConverterOptions.CultureInfo);
		}

		[Fact]
		public void Parameter_WithConstructorFunctionAndName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter(() => ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo))), "id");
			map.Parameter(() => ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo))), "amount").TypeConverterOption.CultureInfo(new CultureInfo(CULTURE));

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo(CULTURE), map.ParameterMaps[1].Data.TypeConverterOptions.CultureInfo);
		}

		[Fact]
		public void Parameter_WithConstructorAndProperty_CreatesParameterMaps()
		{
			var constructor = ConfigurationFunctions.GetConstructor(new GetConstructorArgs(typeof(Foo)));
			var parameters = constructor.GetParameters();

			var map = new DefaultClassMap<Foo>();
			map.Parameter(constructor, parameters[0]);
			map.Parameter(constructor, parameters[1]).TypeConverterOption.CultureInfo(new CultureInfo(CULTURE));

			Assert.Equal(2, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo(CULTURE), map.ParameterMaps[1].Data.TypeConverterOptions.CultureInfo);
		}

		[Fact]
		public void GetRecords_WithParameterMap_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "amount" },
				{ "1", amount },
			};
			using (var csv = new CsvReader(parser))
			{
				var map = csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(AMOUNT, records[0].Amount);
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
				{ "1", amount },
			};
			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooMap>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(AMOUNT, records[0].Amount);
			}
		}

		[Fact]
		public void WriteRecords_WithParameterMap_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, AMOUNT),
			};

			var prevCulture = Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
				using (var writer = new StringWriter())
				using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
				{
					csv.Context.RegisterClassMap<FooMap>();

					csv.WriteRecords(records);

					var expected = new StringBuilder();
					expected.Append("Id,Amount\r\n");
					expected.Append($"1,{AMOUNT}\r\n");

					Assert.Equal(expected.ToString(), writer.ToString());
			}
			} finally {
				Thread.CurrentThread.CurrentCulture = prevCulture;
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
				Parameter("amount").TypeConverterOption.CultureInfo(new CultureInfo(CULTURE));
			}
		}
	}
}
