// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class NullableValuesInEmptyColumnsInputTests
	{
		[TestMethod]
		public void SingleColumnCsvWithHeadersAndSingleNullDataRowTest()
		{
			var parser = new ParserMock
			{
				{ "NullableInt32Field" },
				{ new string[0] },
				{ null }
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.IgnoreBlankLines = false;
				csv.Configuration.TypeConverterOptionsCache.GetOptions<int?>().NullValues.Add(string.Empty);

				// Read header row, assert header row columns:
				Assert.IsTrue(csv.Read());
				Assert.IsTrue(csv.ReadHeader());
				Assert.AreEqual(1, csv.Context.HeaderRecord.Length);
				Assert.AreEqual("NullableInt32Field", csv.Context.HeaderRecord[0]);

				// Read single data row, assert single null value:
				Assert.IsTrue(csv.Read());

				var nullableIntValueByIndex = csv.GetField<int?>(index: 0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsFalse(nullableIntValueByIndex.HasValue);
				Assert.IsFalse(nullableIntValueByName.HasValue);

				// Read to end of file:
				Assert.IsFalse(csv.Read());
			}
		}

		[TestMethod]
		public void SingleColumnCsvWithHeadersAndPresentAndNullDataRowTest()
		{
			var parser = new ParserMock
			{
				{ "NullableInt32Field" },
				{ "1" },
				{ new string[0] },
				{ "3" },
				{ null }
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.IgnoreBlankLines = false;
				csv.Configuration.TypeConverterOptionsCache.GetOptions<int?>().NullValues.Add(string.Empty);

				// Read header row, assert header row columns:
				Assert.IsTrue(csv.Read());
				Assert.IsTrue(csv.ReadHeader());
				Assert.AreEqual(1, csv.Context.HeaderRecord.Length);
				Assert.AreEqual("NullableInt32Field", csv.Context.HeaderRecord[0]);

				// Read first data row, assert "1" value:
				Assert.IsTrue(csv.Read());

				var nullableIntValueByIndex = csv.GetField<int?>(0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsTrue(nullableIntValueByIndex.HasValue);
				Assert.IsTrue(nullableIntValueByName.HasValue);

				Assert.AreEqual(1, nullableIntValueByIndex);
				Assert.AreEqual(1, nullableIntValueByName);

				// Read second data row, assert null value:
				Assert.IsTrue(csv.Read());

				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsFalse(nullableIntValueByIndex.HasValue);
				Assert.IsFalse(nullableIntValueByName.HasValue);

				// Read third data row, assert "3" value:
				Assert.IsTrue(csv.Read());

				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsTrue(nullableIntValueByIndex.HasValue);
				Assert.IsTrue(nullableIntValueByName.HasValue);

				Assert.AreEqual(3, nullableIntValueByIndex);
				Assert.AreEqual(3, nullableIntValueByName);

				// Read to end of file:
				Assert.IsFalse(csv.Read());
			}
		}

		[TestMethod]
		public void TwoColumnCsvWithHeadersAndPresentAndNullDataRowTest()
		{
			var parser = new ParserMock
			{
				{ "NullableInt32Field", "NullableStringField" },
				{ "1" },
				{ "", "Foo" },
				{ "", "" },
				{ "4", "Bar" },
				{ null }
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Configuration.IgnoreBlankLines = false;
				csv.Configuration.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty); // Read empty fields as nulls instead of `""`.

				// Read header row, assert header row columns:
				Assert.IsTrue(csv.Read());
				Assert.IsTrue(csv.ReadHeader());
				Assert.AreEqual(2, csv.Context.HeaderRecord.Length);
				Assert.AreEqual("NullableInt32Field", csv.Context.HeaderRecord[0]);
				Assert.AreEqual("NullableStringField", csv.Context.HeaderRecord[1]);

				// Read first data row:
				Assert.IsTrue(csv.Read());

				// Read `Int32?`, assert "1" value:
				var nullableIntValueByIndex = csv.GetField<int?>(0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsTrue(nullableIntValueByIndex.HasValue);
				Assert.IsTrue(nullableIntValueByName.HasValue);

				Assert.AreEqual(1, nullableIntValueByIndex);
				Assert.AreEqual(1, nullableIntValueByName);

				// Read nullable String, assert null value:
				var strByIndex = csv.GetField<string>(1);
				var strByName = csv.GetField<string>("NullableStringField");

				Assert.IsNull(strByIndex);
				Assert.IsNull(strByName);

				// Read second data row:
				Assert.IsTrue(csv.Read());

				// Read `Int32?`, assert NULL value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsFalse(nullableIntValueByIndex.HasValue);
				Assert.IsFalse(nullableIntValueByName.HasValue);

				// Read nullable String, assert "Foo" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.AreEqual("Foo", strByIndex);
				Assert.AreEqual("Foo", strByName);

				// Read third data row:
				Assert.IsTrue(csv.Read());

				// Read `Int32?`, assert NULL value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsFalse(nullableIntValueByIndex.HasValue);
				Assert.IsFalse(nullableIntValueByName.HasValue);

				// Read nullable String, assert "Foo" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.IsNull(strByIndex);
				Assert.IsNull(strByName);

				// Read fourth data row:
				Assert.IsTrue(csv.Read());

				// Read `Int32?`, assert "3" value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.IsTrue(nullableIntValueByIndex.HasValue);
				Assert.IsTrue(nullableIntValueByName.HasValue);

				Assert.AreEqual(4, nullableIntValueByIndex);
				Assert.AreEqual(4, nullableIntValueByName);

				// Read nullable String, assert "Bar" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.AreEqual("Bar", strByIndex);
				Assert.AreEqual("Bar", strByName);

				// Read to end of file:
				Assert.IsFalse(csv.Read());
			}
		}
	}
}