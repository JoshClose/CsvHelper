// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests
{
	[TestClass]
	public class HeaderValidationTests
	{
		[TestMethod]
		public void CorrectHeadersTest()
		{
			using (var csv = new CsvReader(new StringReader("Id,Name"), CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				csv.Read();
				csv.ReadHeader();
				csv.ValidateHeader<Test>();
			}
		}

		[TestMethod]
		public void PropertiesTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				Assert.ThrowsException<HeaderValidationException>(() => csv.ValidateHeader<Test>());
			}
		}

		[TestMethod]
		public void ReferencesTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				Assert.ThrowsException<HeaderValidationException>(() => csv.ValidateHeader<HasReference>());
			}
		}

		[TestMethod]
		public void ConstructorParametersTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				Assert.ThrowsException<HeaderValidationException>(() => csv.ValidateHeader<HasConstructor>());
			}
		}

		[TestMethod]
		public void PropertiesGetRecordTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				Assert.ThrowsException<HeaderValidationException>(() => csv.GetRecord(typeof(Test)));
			}
		}

		[TestMethod]
		public void PropertiesGetRecordGenericTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				Assert.ThrowsException<HeaderValidationException>(() => csv.GetRecord<Test>());
			}
		}

		[TestMethod]
		public void PropertiesGetRecordsTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				Assert.ThrowsException<HeaderValidationException>(() => csv.GetRecords(typeof(Test)).ToList());
			}
		}

		[TestMethod]
		public void PropertiesGetRecordsGenericTest()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				Assert.ThrowsException<HeaderValidationException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		[TestMethod]
		public void PrivateSetterTest()
		{
			var data = new StringBuilder();
			data.AppendLine("Number");
			data.AppendLine("1");
			using (var csv = new CsvReader(new StringReader(data.ToString()), CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<HasPrivateSetter>().ToList();
				var record = records[0];
				Assert.AreEqual(1, record.Number);
				Assert.AreEqual(2, record.Double);
			}
		}

		[TestMethod]
		public void IgnorePropertyTest()
		{
			var data = new StringBuilder();
			data.AppendLine("Id");
			data.AppendLine("1");
			using (var csv = new CsvReader(new StringReader(data.ToString()), CultureInfo.InvariantCulture))
			{
				csv.Configuration.RegisterClassMap<HasIgnoredPropertyMap>();
				var records = csv.GetRecords<Test>().ToList();
				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.IsNull(record.Name);
			}
		}

		[TestMethod]
		public void HasIndexNoNameTest()
		{
			using (var csv = new CsvReader(new StringReader("Id"), CultureInfo.InvariantCulture))
			{
				csv.Configuration.RegisterClassMap<HasIndexNoNameMap>();

				csv.Read();
				csv.ReadHeader();
				csv.ValidateHeader<Test>();
			}
		}

		[TestMethod]
		public void HasIndexAndNameTest()
		{
			using (var csv = new CsvReader(new StringReader("Id"), CultureInfo.InvariantCulture))
			{
				csv.Configuration.RegisterClassMap<HasIndexAndNameMap>();

				csv.Read();
				csv.ReadHeader();
				Assert.ThrowsException<HeaderValidationException>(() => csv.ValidateHeader<Test>());
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private class HasReference
		{
			public Test Reference { get; set; }
		}

		private class HasConstructor
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public HasConstructor(int Id, string Name)
			{
				this.Id = Id;
				this.Name = Name;
			}
		}

		private class HasPrivateSetter
		{
			public int Number { get; set; }

			public int Double => Number * 2;
		}

		private sealed class HasIgnoredPropertyMap : ClassMap<Test>
		{
			public HasIgnoredPropertyMap()
			{
				AutoMap(CultureInfo.InvariantCulture);
				Map(m => m.Name).Ignore();
			}
		}

		private sealed class HasIndexNoNameMap : ClassMap<Test>
		{
			public HasIndexNoNameMap()
			{
				Map(m => m.Id).Index(0);
				Map(m => m.Name).Index(1);
			}
		}

		private sealed class HasIndexAndNameMap : ClassMap<Test>
		{
			public HasIndexAndNameMap()
			{
				Map(m => m.Id).Index(0).Name("Id");
				Map(m => m.Name).Index(1).Name("Name");
			}
		}
	}
}
