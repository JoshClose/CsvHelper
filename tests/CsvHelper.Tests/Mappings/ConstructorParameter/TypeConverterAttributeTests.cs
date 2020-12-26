using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
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
	public class TypeConverterAttributeTests
    {
		[TestMethod]
		public void AutoMap_WithConstantAttributes_ConfiguresParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsInstanceOfType(map.ParameterMaps[0].Data.TypeConverter, typeof(Int32Converter));
			Assert.IsInstanceOfType(map.ParameterMaps[1].Data.TypeConverter, typeof(CustomConverter));
		}

		[TestMethod]
		public void GetRecords_WithConstantAttributes_HasHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "id", "name" },
				new [] { "1", "one" },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("Bar", records[0].Name);
			}
		}

		[TestMethod]
		public void GetRecords_WithConstantAttributes_NoHeader_CreatesRecords()
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

				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("Bar", records[0].Name);
			}
		}

		[TestMethod]
		public void WriteRecords_WithConstantAttributes_DoesntUseParameterMaps()
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

			public Foo(int id, [TypeConverter(typeof(CustomConverter))] string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class CustomConverter : DefaultTypeConverter
		{
			public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
			{
				return "Bar";
			}
		}
	}
}
