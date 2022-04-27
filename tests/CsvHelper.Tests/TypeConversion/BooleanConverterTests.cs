// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class BooleanConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new BooleanConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.Equal("True", converter.ConvertToString(true, null, memberMapData));

			Assert.Equal("False", converter.ConvertToString(false, null, memberMapData));

			Assert.Equal("", converter.ConvertToString(null, null, memberMapData));
			Assert.Equal("1", converter.ConvertToString(1, null, memberMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			var memberMapData = new MemberMapData(null);
			memberMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			Assert.True((bool)converter.ConvertFromString("true", null, memberMapData));
			Assert.True((bool)converter.ConvertFromString("True", null, memberMapData));
			Assert.True((bool)converter.ConvertFromString("TRUE", null, memberMapData));
			Assert.True((bool)converter.ConvertFromString("1", null, memberMapData));
			Assert.True((bool)converter.ConvertFromString(" true ", null, memberMapData));

			Assert.False((bool)converter.ConvertFromString("false", null, memberMapData));
			Assert.False((bool)converter.ConvertFromString("False", null, memberMapData));
			Assert.False((bool)converter.ConvertFromString("FALSE", null, memberMapData));
			Assert.False((bool)converter.ConvertFromString("0", null, memberMapData));
			Assert.False((bool)converter.ConvertFromString(" false ", null, memberMapData));
			Assert.False((bool)converter.ConvertFromString(" 0 ", null, memberMapData));

			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, memberMapData));
		}

		[Fact]
		public void ConvertToString_SingleBooleanTrueValue_UsesValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanTrueValues = { "Foo" },
				},
			};

			var value = converter.ConvertToString(true, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void ConvertToString_MultipleBooleanTrueValues_UsesFirstValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanTrueValues = { "Foo", "Bar" },
				},
			};

			var value = converter.ConvertToString(true, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void ConvertToString_SingleBooleanFalseValue_UsesValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanFalseValues = { "Foo" },
				},
			};

			var value = converter.ConvertToString(false, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void ConvertToString_MultipleBooleanFalseValues_UsesFirstValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanFalseValues = { "Foo", "Bar" },
				},
			};

			var value = converter.ConvertToString(false, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void WriteField_TrueValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanTrueValues.Add("Foo");

				csv.WriteField(true);
				csv.Flush();
				writer.Flush();

				Assert.Equal("Foo", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_FalseValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanFalseValues.Add("Foo");

				csv.WriteField(false);
				csv.Flush();
				writer.Flush();

				Assert.Equal("Foo", writer.ToString());
			}
		}
	}
}
