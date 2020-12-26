using CsvHelper.Configuration;
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
    public class DateTimeStylesMapTests
	{
		private const string DATE = "12/25/2020";
		private readonly DateTimeOffset date = DateTimeOffset.Parse(DATE);

		[TestMethod]
		public void Parameter_WithName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter("id");
			map.Parameter("date").TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowLeadingWhite);

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.DateTimeStyle);
			Assert.AreEqual(DateTimeStyles.AllowLeadingWhite, map.ParameterMaps[1].Data.TypeConverterOptions.DateTimeStyle);
		}

		[TestMethod]
		public void Parameter_WithConstructorFunctionAndName_CreatesParameterMaps()
		{
			var map = new DefaultClassMap<Foo>();
			map.Parameter(() => ConfigurationFunctions.GetConstructor(typeof(Foo)), "id");
			map.Parameter(() => ConfigurationFunctions.GetConstructor(typeof(Foo)), "date").TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowLeadingWhite);

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.DateTimeStyle);
			Assert.AreEqual(DateTimeStyles.AllowLeadingWhite, map.ParameterMaps[1].Data.TypeConverterOptions.DateTimeStyle);
		}

		[TestMethod]
		public void Parameter_WithConstructorAndProperty_CreatesParameterMaps()
		{
			var constructor = ConfigurationFunctions.GetConstructor(typeof(Foo));
			var parameters = constructor.GetParameters();

			var map = new DefaultClassMap<Foo>();
			map.Parameter(constructor, parameters[0]);
			map.Parameter(constructor, parameters[1]).TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowLeadingWhite);

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.DateTimeStyle);
			Assert.AreEqual(DateTimeStyles.AllowLeadingWhite, map.ParameterMaps[1].Data.TypeConverterOptions.DateTimeStyle);
		}

		[TestMethod]
		public void GetRecords_WithParameterMap_HasHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "id", "date" },
				new [] { "1", DATE },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				var map = csv.Configuration.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void GetRecords_WithParameterMap_NoHeader_CreatesRecords()
		{
			var rows = new Queue<string[]>(new List<string[]>
			{
				new [] { "1", DATE },
				null
			});
			using (var parser = new ParserMock(rows))
			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<FooMap>();

				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void WriteRecords_WithParameterMap_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, date),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.Configuration.RegisterClassMap<FooMap>();

				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Date", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual(date.ToString(null, CultureInfo.InvariantCulture), serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public DateTimeOffset Date { get; private set; }

			public Foo(int id, DateTimeOffset date)
			{
				Id = id;
				Date = date;
			}
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Date);
				Parameter("id");
				Parameter("date").TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowLeadingWhite);
			}
		}
	}
}
