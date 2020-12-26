using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	[TestClass]
    public class NullValuesAttributeTests
    {
		[TestMethod]
		public void AutoMap_WithBooleanFalseValuesAttribute_CreatesParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual(0, map.ParameterMaps[0].Data.TypeConverterOptions.NullValues.Count);
			Assert.AreEqual(1, map.ParameterMaps[1].Data.TypeConverterOptions.NullValues.Count);
			Assert.AreEqual("NULL", map.ParameterMaps[1].Data.TypeConverterOptions.NullValues[0]);
		}

		[TestMethod]
		public void GetRecords_WithBooleanFalseValuesAttribute_HasHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "id", "name" },
				new [] { "1", "NULL" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.IsNull(records[0].Name);
			}
		}

		[TestMethod]
		public void GetRecords_WithBooleanFalseValuesAttribute_NoHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "1", "NULL" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.HasHeaderRecord = false;

				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.IsNull(records[0].Name);
			}
		}

		[TestMethod]
		public void WriteRecords_WithBooleanFalseValuesAttribute_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, null),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Name", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual("", serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public Foo(int id, [NullValues("NULL")] string name)
			{
				Id = id;
				Name = name;
			}
		}
	}
}
