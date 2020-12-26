using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	[TestClass]
    public class NameMapTests
    {
		[TestMethod]
		public void Parameter_WithName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id").Name("Id");
			map.Parameter("name").Name("Name");

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual("Id", map.ParameterMaps[0].Data.Names[0]);
			Assert.AreEqual("Name", map.ParameterMaps[1].Data.Names[0]);
		}

		[TestMethod]
		public void Parameter_WithConstructorFunctionAndName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter(() => ConfigurationFunctions.GetConstructor(typeof(Foo)), "id").Name("Id");
			map.Parameter(() => ConfigurationFunctions.GetConstructor(typeof(Foo)), "name").Name("Name");

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual("Id", map.ParameterMaps[0].Data.Names[0]);
			Assert.AreEqual("Name", map.ParameterMaps[1].Data.Names[0]);
		}

		[TestMethod]
		public void Parameter_WithConstructorAndProperty_CreatesParameterMaps()
		{
			var constructor = ConfigurationFunctions.GetConstructor(typeof(Foo));
			var parameters = constructor.GetParameters();

			var map = new DefaultClassMap<Foo>();
			map.Parameter(constructor, parameters[0]).Name("Id");
			map.Parameter(constructor, parameters[1]).Name("Name");

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual("Id", map.ParameterMaps[0].Data.Names[0]);
			Assert.AreEqual("Name", map.ParameterMaps[1].Data.Names[0]);
		}

		[TestMethod]
		public void GetRecords_WithParameterMap_HasHeader_CreatesRecords()
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
				csv.Configuration.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void GetRecords_WithParameterMap_NoHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<FooMap>();

				// Can't read using names when no header is present.
				Assert.ThrowsException<ReaderException>(() => csv.GetRecords<Foo>().ToList());
			}
		}

		[TestMethod]
		public void WriteRecords()
		{
			var records = new List<Foo>
			{
				new Foo(1, "one"),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.Configuration.RegisterClassMap<FooMap>();

				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Name", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual("one", serializer.Records[1][1]);
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

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
				Parameter("id").Name("Id");
				Parameter("name").Name("Name");
			}
		}
	}
}
