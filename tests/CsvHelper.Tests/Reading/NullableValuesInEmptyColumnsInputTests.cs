// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;
using System.Globalization;

namespace CsvHelper.Tests.Reading
{
	
	public class NullableValuesInEmptyColumnsInputTests
	{
		[Fact]
		public void SingleColumnCsvWithHeadersAndSingleNullDataRowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
				MissingFieldFound = null,
			};
			var parser = new ParserMock(config)
			{
				{ "NullableInt32Field" },
				{ new string[0] },
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<int?>().NullValues.Add(string.Empty);

				// Read header row, assert header row columns:
				Assert.True(csv.Read());
				Assert.True(csv.ReadHeader());
				Assert.Single(csv.HeaderRecord);
				Assert.Equal("NullableInt32Field", csv.HeaderRecord?[0]);

				// Read single data row, assert single null value:
				Assert.True(csv.Read());

				var nullableIntValueByIndex = csv.GetField<int?>(index: 0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.False(nullableIntValueByIndex.HasValue);
				Assert.False(nullableIntValueByName.HasValue);

				// Read to end of file:
				Assert.False(csv.Read());
			}
		}

		[Fact]
		public void SingleColumnCsvWithHeadersAndPresentAndNullDataRowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
				MissingFieldFound = null,
			};
			var parser = new ParserMock(config)
			{
				{ "NullableInt32Field" },
				{ "1" },
				{ new string[0] },
				{ "3" },
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<int?>().NullValues.Add(string.Empty);

				// Read header row, assert header row columns:
				Assert.True(csv.Read());
				Assert.True(csv.ReadHeader());
				Assert.Single(csv.HeaderRecord);
				Assert.Equal("NullableInt32Field", csv.HeaderRecord?[0]);

				// Read first data row, assert "1" value:
				Assert.True(csv.Read());

				var nullableIntValueByIndex = csv.GetField<int?>(0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.True(nullableIntValueByIndex.HasValue);
				Assert.True(nullableIntValueByName.HasValue);

				Assert.Equal(1, nullableIntValueByIndex);
				Assert.Equal(1, nullableIntValueByName);

				// Read second data row, assert null value:
				Assert.True(csv.Read());

				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.False(nullableIntValueByIndex.HasValue);
				Assert.False(nullableIntValueByName.HasValue);

				// Read third data row, assert "3" value:
				Assert.True(csv.Read());

				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.True(nullableIntValueByIndex.HasValue);
				Assert.True(nullableIntValueByName.HasValue);

				Assert.Equal(3, nullableIntValueByIndex);
				Assert.Equal(3, nullableIntValueByName);

				// Read to end of file:
				Assert.False(csv.Read());
			}
		}

		[Fact]
		public void TwoColumnCsvWithHeadersAndPresentAndNullDataRowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
				MissingFieldFound = null,
			};
			var parser = new ParserMock(config)
			{
				{ "NullableInt32Field", "NullableStringField" },
				{ "1" },
				{ "", "Foo" },
				{ "", "" },
				{ "4", "Bar" },
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty); // Read empty fields as nulls instead of `""`.

				// Read header row, assert header row columns:
				Assert.True(csv.Read());
				Assert.True(csv.ReadHeader());
				Assert.Equal(2, csv.HeaderRecord?.Length);
				Assert.Equal("NullableInt32Field", csv.HeaderRecord?[0]);
				Assert.Equal("NullableStringField", csv.HeaderRecord?[1]);

				// Read first data row:
				Assert.True(csv.Read());

				// Read `Int32?`, assert "1" value:
				var nullableIntValueByIndex = csv.GetField<int?>(0);
				var nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.True(nullableIntValueByIndex.HasValue);
				Assert.True(nullableIntValueByName.HasValue);

				Assert.Equal(1, nullableIntValueByIndex);
				Assert.Equal(1, nullableIntValueByName);

				// Read nullable String, assert null value:
				var strByIndex = csv.GetField<string>(1);
				var strByName = csv.GetField<string>("NullableStringField");

				Assert.Null(strByIndex);
				Assert.Null(strByName);

				// Read second data row:
				Assert.True(csv.Read());

				// Read `Int32?`, assert NULL value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.False(nullableIntValueByIndex.HasValue);
				Assert.False(nullableIntValueByName.HasValue);

				// Read nullable String, assert "Foo" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.Equal("Foo", strByIndex);
				Assert.Equal("Foo", strByName);

				// Read third data row:
				Assert.True(csv.Read());

				// Read `Int32?`, assert NULL value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.False(nullableIntValueByIndex.HasValue);
				Assert.False(nullableIntValueByName.HasValue);

				// Read nullable String, assert "Foo" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.Null(strByIndex);
				Assert.Null(strByName);

				// Read fourth data row:
				Assert.True(csv.Read());

				// Read `Int32?`, assert "3" value:
				nullableIntValueByIndex = csv.GetField<int?>(0);
				nullableIntValueByName = csv.GetField<int?>("NullableInt32Field");

				Assert.True(nullableIntValueByIndex.HasValue);
				Assert.True(nullableIntValueByName.HasValue);

				Assert.Equal(4, nullableIntValueByIndex);
				Assert.Equal(4, nullableIntValueByName);

				// Read nullable String, assert "Bar" value:
				strByIndex = csv.GetField<string>(1);
				strByName = csv.GetField<string>("NullableStringField");

				Assert.Equal("Bar", strByIndex);
				Assert.Equal("Bar", strByName);

				// Read to end of file:
				Assert.False(csv.Read());
			}
		}
	}
}
