// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class OptionalTests
	{
		[TestMethod]
		public void OptionalWithExistingColumnTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalIntMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(1, records[0].Id);
			Assert.AreEqual("one", records[0].Name);
			Assert.AreEqual(2, records[1].Id);
			Assert.AreEqual("two", records[1].Name);
		}

		[TestMethod]
		public void OptionalIntTest()
		{
			var parserMock = new ParserMock
			{
				{ "Name" },
				{ "one" },
				{ "two" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalIntMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);

			Assert.AreEqual(0, records[0].Id);
			Assert.AreEqual("one", records[0].Name);

			Assert.AreEqual(0, records[1].Id);
			Assert.AreEqual("two", records[1].Name);
		}

		[TestMethod]
		public void OptionalIntDefaultTest()
		{
			var parserMock = new ParserMock
			{
				{ "Name" },
				{ "one" },
				{ "two" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalIntDefaultMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);

			Assert.AreEqual(int.MinValue, records[0].Id);
			Assert.AreEqual("one", records[0].Name);

			Assert.AreEqual(int.MinValue, records[1].Id);
			Assert.AreEqual("two", records[1].Name);
		}

		[TestMethod]
		public void OptionalStringIntDefaultTest()
		{
			var parserMock = new ParserMock
			{
				{ "Name" },
				{ "one" },
				{ "two" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalStringIntDefaultMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);

			Assert.AreEqual(int.MinValue, records[0].Id);
			Assert.AreEqual("one", records[0].Name);

			Assert.AreEqual(int.MinValue, records[1].Id);
			Assert.AreEqual("two", records[1].Name);
		}

		[TestMethod]
		public void OptionalStringTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
				{ "2" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalStringMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);

			Assert.AreEqual(1, records[0].Id);
			Assert.AreEqual(null, records[0].Name);

			Assert.AreEqual(2, records[1].Id);
			Assert.AreEqual(null, records[1].Name);
		}

		[TestMethod]
		public void OptionalStringDefaultTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
				{ "2" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<FooOptionalStringDefaultMap>();

			var records = csvReader.GetRecords<Foo>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);

			Assert.AreEqual(1, records[0].Id);
			Assert.AreEqual("bar", records[0].Name);

			Assert.AreEqual(2, records[1].Id);
			Assert.AreEqual("bar", records[1].Name);
		}

		private class Foo
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class FooOptionalIntMap : ClassMap<Foo>
		{
			public FooOptionalIntMap()
			{
				Map(m => m.Id).Optional();
				Map(m => m.Name);
			}
		}

		private sealed class FooOptionalIntDefaultMap : ClassMap<Foo>
		{
			public FooOptionalIntDefaultMap()
			{
				Map(m => m.Id).Optional().Default(int.MinValue);
				Map(m => m.Name);
			}
		}

		private sealed class FooOptionalStringIntDefaultMap : ClassMap<Foo>
		{
			public FooOptionalStringIntDefaultMap()
			{
				Map(m => m.Id).Optional().Default(int.MinValue.ToString());
				Map(m => m.Name);
			}
		}

		private sealed class FooOptionalStringMap : ClassMap<Foo>
		{
			public FooOptionalStringMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Optional();
			}
		}

		private sealed class FooOptionalStringDefaultMap : ClassMap<Foo>
		{
			public FooOptionalStringDefaultMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Optional().Default("bar");
			}
		}
	}
}
