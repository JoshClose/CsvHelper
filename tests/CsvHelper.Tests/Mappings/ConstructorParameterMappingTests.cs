using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
    public class ConstructorParameterMappingTests
    {
		[TestMethod]
		public void Parameter_WithName_AddsParameter()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id");

			Assert.AreEqual(1, map.ParameterMaps.Count);
			Assert.AreEqual("id", map.ParameterMaps[0].Data.Parameter.Name);
		}

		[TestMethod]
		public void Parameter_WithGetConstructorAndName_AddsParameter()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter(() => ConfigurationFunctions.GetConstructor(typeof(Foo)), "id");

			Assert.AreEqual(1, map.ParameterMaps.Count);
			Assert.AreEqual("id", map.ParameterMaps[0].Data.Parameter.Name);
		}

		[TestMethod]
		public void Parameter_WithConstructorAndParameter_AddsParameter()
		{
			var map = new DefaultClassMap<Foo>();
			var constructor = ConfigurationFunctions.GetConstructor(typeof(Foo));
			var parameter = constructor.GetParameters().Single(p => p.Name == "id");

			map.Parameter(constructor, parameter);

			Assert.AreEqual(1, map.ParameterMaps.Count);
			Assert.AreEqual("id", map.ParameterMaps[0].Data.Parameter.Name);
		}

		[TestMethod]
		public void ClassMap_AddParametersByName_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.RegisterClassMap<FooMapByName>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void ClassMap_AddParametersByIndex_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.RegisterClassMap<FooMapByIndex>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void Attributes_ByName_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<FooNameAttributes>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void Attributes_ByIndex_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<FooIndexAttributes>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public Foo(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class FooNameAttributes
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public FooNameAttributes([Name("Id")]int id, [Name("Name")]string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class FooIndexAttributes
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public FooIndexAttributes([Index(0)] int id, [Index(1)] string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class FooMapByName : ClassMap<Foo>
		{
			public FooMapByName()
			{
				Parameter("id").Name("Id");
				Parameter("name").Name("Name");
			}
		}

		private class FooMapByIndex : ClassMap<Foo>
		{
			public FooMapByIndex()
			{
				Parameter("id").Index(0);
				Parameter("name").Index(1);
			}
		}
	}
}
