﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class TypeConverterOptionsTests
	{
		[Fact]
		public void GlobalNullValueTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine(",");
				writer.Flush();
				stream.Position = 0;

				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty);
				var records = csv.GetRecords<Test>().ToList();

				Assert.Null(records[0].Id);
				Assert.Null(records[0].Name);
			}
		}

		[Fact]
		public void MappingNullValueTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine(",");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.Null(records[0].Id);
				Assert.Null(records[0].Name);
			}
		}

		[Fact]
		public void GlobalAndMappingNullValueTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine(",");
				writer.Flush();
				stream.Position = 0;

				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("null");
				csv.Context.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.Null(records[0].Id);
				Assert.Null(records[0].Name);
			}
		}

		private class Test
		{
			public int? Id { get; set; }
			public string? Name { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).TypeConverterOption.NullValues(string.Empty);
			}
		}

		// auto map options have defaults
		// map options could be default or custom if set
		// global has defaults or custom
		// merge global with map
	}
}
