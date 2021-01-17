// Copyright 2009-2021 Josh Close
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
		public void ValidateHeader_ValidHeaders_NoException()
		{
			using (var csv = new CsvReader(new StringReader("Id,Name"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				csv.ValidateHeader<Test>();
			}
		}

		[TestMethod]
		public void ValidateHeader_PropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				try
				{
					csv.ValidateHeader<Test>();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void ValidateHeader_ReferencePropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				try
				{
					csv.ValidateHeader<HasReference>();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void ValidateHeader_ConstructorParametersDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				csv.ReadHeader();
				try
				{
					csv.ValidateHeader<HasConstructor>();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void GetRecord_PropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				try
				{
					csv.GetRecord(typeof(Test));
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void GetRecordGeneric_PropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				csv.Read();
				try
				{
					csv.GetRecord<Test>();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void GetRecords_PropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				try
				{
					csv.GetRecords(typeof(Test)).ToList();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void GetRecordsGeneric_PropertiesDontMatch_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("bad data"), CultureInfo.InvariantCulture))
			{
				try
				{
					csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(2, ex.InvalidHeaders.Length);
				}
			}
		}

		[TestMethod]
		public void GetRecordsGeneric_PrivateSetter_NoException()
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
		public void GetRecordsGeneric_IgnoreProperty_NoException()
		{
			var data = new StringBuilder();
			data.AppendLine("Id");
			data.AppendLine("1");
			using (var csv = new CsvReader(new StringReader(data.ToString()), CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<HasIgnoredPropertyMap>();
				var records = csv.GetRecords<Test>().ToList();
				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.IsNull(record.Name);
			}
		}

		[TestMethod]
		public void ValidateHeader_NoNamesMapped_NoException()
		{
			using (var csv = new CsvReader(new StringReader("Id"), CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<HasIndexNoNameMap>();

				csv.Read();
				csv.ReadHeader();
				csv.ValidateHeader<Test>();
			}
		}

		[TestMethod]
		public void ValidateHeader_HasIndexAndName_ThrowsHeaderValidationException()
		{
			using (var csv = new CsvReader(new StringReader("Id"), CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<HasIndexAndNameMap>();

				csv.Read();
				csv.ReadHeader();
				try
				{
					csv.ValidateHeader<Test>();
					Assert.Fail();
				}
				catch (HeaderValidationException ex)
				{
					Assert.AreEqual(1, ex.InvalidHeaders.Length);
					Assert.AreEqual("Name", ex.InvalidHeaders[0].Names[0]);
				}
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
