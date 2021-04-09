// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	
	public class TryGetTests
	{
		[Fact]
		public void TryGetFieldInvalidIndexTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "One", "Two" },
				new[] { "one", "two" },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			var got = reader.TryGetField(0, out int field);
			Assert.False(got);
			Assert.Equal(default(int), field);
		}

		[Fact]
		public void TryGetFieldInvalidNameTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "One", "Two" },
				new[] { "one", "two" },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();

			var got = reader.TryGetField("One", out int field);
			Assert.False(got);
			Assert.Equal(default(int), field);
		}

		[Fact]
		public void TryGetFieldTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "One", "Two" },
				new[] { "1", "2" },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(0, out int field);
			Assert.True(got);
			Assert.Equal(1, field);
		}

		[Fact]
		public void TryGetFieldStrictTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "One", "Two" },
				new[] { "1", "2" },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField("One", out int field);
			Assert.True(got);
			Assert.Equal(1, field);
		}

		[Fact]
		public void TryGetFieldEmptyDate()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};

			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an empty string for a date.
			var parserMock = new ParserMock(config)
			{
				new[] { " " },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			var got = reader.TryGetField(0, out DateTime field);

			Assert.False(got);
			Assert.Equal(DateTime.MinValue, field);
		}

		[Fact]
		public void TryGetNullableFieldEmptyDate()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};

			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an empty string for a date.
			var parserMock = new ParserMock(config)
			{
				new[] { " " },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			var got = reader.TryGetField(0, out DateTime? field);

			Assert.False(got);
			Assert.Null(field);
		}

		[Fact]
		public void TryGetDoesNotThrowWhenWillThrowOnMissingFieldIsEnabled()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};

			var parserMock = new ParserMock(config)
			{
				new[] { "1" },
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			Assert.False(reader.TryGetField("test", out string field));
		}

		[Fact]
		public void TryGetFieldIndexTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two", "Two" },
				{ "1", "2", "3" }
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField("Two", 0, out int field);
			Assert.True(got);
			Assert.Equal(2, field);

			got = reader.TryGetField("Two", 1, out field);
			Assert.True(got);
			Assert.Equal(3, field);
		}

		[Fact]
		public void TryGetMissingDateTimeFieldTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1" },
				null
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(typeof(DateTime), "Name", out object field);

			Assert.False(got);
			Assert.Equal(DateTime.MinValue, field);
		}

		[Fact]
		public void TryGetMissingDateTimeOffsetFieldTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "DateTime" },
				{ "1" },
				null
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(typeof(DateTimeOffset), "DateTime", out object field);

			Assert.False(got);
			Assert.Equal(DateTimeOffset.MinValue, field);
		}
	}
}
