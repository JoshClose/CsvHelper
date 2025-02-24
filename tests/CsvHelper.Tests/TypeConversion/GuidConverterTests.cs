// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{

	public class GuidConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new GuidConverter();
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = Guid.NewGuid();

			// Valid conversions.
			Assert.Equal(value.ToString(), converter.ConvertToString(value, null!, propertyMapData));

			// Invalid conversions.
			Assert.Equal("1", converter.ConvertToString(1, null!, propertyMapData));
			Assert.Equal("", converter.ConvertToString(null, null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new GuidConverter();
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var row = new CsvReader(new ParserMock());

			var value = Guid.NewGuid();

			// Valid conversions.
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString(), null!, propertyMapData)?.ToString());
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString("N"), null!, propertyMapData)?.ToString());
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString("D"), null!, propertyMapData)?.ToString());
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString("B"), null!, propertyMapData)?.ToString());
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString("P"), null!, propertyMapData)?.ToString());
			Assert.Equal(value.ToString(), converter.ConvertFromString(value.ToString("X"), null!, propertyMapData)?.ToString());

			// Invalid conversions.
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString("", row, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(" ", row, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString("Not A Guid", row, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString("GGGGAAAA-0000-0000-0000-000000000000", row, propertyMapData));
		}
	}
}
