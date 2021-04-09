// Copyright 2009-2021 Josh Close
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
	
	public class DateTimeOffsetConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new DateTimeOffsetConverter();
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var dateTime = DateTimeOffset.Now;

			// Valid conversions.
			Assert.Equal(dateTime.ToString(), converter.ConvertToString(dateTime, null, propertyMapData));

			// Invalid conversions.
			Assert.Equal("1", converter.ConvertToString(1, null, propertyMapData));
			Assert.Equal("", converter.ConvertToString(null, null, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new DateTimeOffsetConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			var dateTime = DateTimeOffset.Now;

			// Valid conversions.
			Assert.Equal(dateTime.ToString(), converter.ConvertFromString(dateTime.ToString(), null, propertyMapData).ToString());
			Assert.Equal(dateTime.ToString(), converter.ConvertFromString(dateTime.ToString("o"), null, propertyMapData).ToString());
			Assert.Equal(dateTime.ToString(), converter.ConvertFromString(" " + dateTime + " ", null, propertyMapData).ToString());

			// Invalid conversions.
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
		}

		[Fact]
		public void ComponentModelCompatibilityTest()
		{
			var converter = new DateTimeOffsetConverter();
			var cmConverter = new System.ComponentModel.DateTimeOffsetConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			Assert.Throws<NotSupportedException>(() => cmConverter.ConvertFromString(null));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
			Assert.Throws<FormatException>(() => cmConverter.ConvertFromString("blah"));
			Assert.Throws<FormatException>(() => converter.ConvertFromString("blah", row, propertyMapData));
		}
	}
}
