// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
#if NET6_0_OR_GREATER
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using System;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class TimeOnlyConverterTests
    {
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new TimeOnlyConverter();
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var date = DateOnly.FromDateTime(DateTime.Now);

			// Valid conversions.
			Assert.Equal(date.ToString(), converter.ConvertToString(date, null, propertyMapData));

			// Invalid conversions.
			Assert.Equal("1", converter.ConvertToString(1, null, propertyMapData));
			Assert.Equal("", converter.ConvertToString(null, null, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new TimeOnlyConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			var time = TimeOnly.FromDateTime(DateTime.Now);

			// Valid conversions.
			Assert.Equal(time.ToString(), converter.ConvertFromString(time.ToString(), null, propertyMapData).ToString());
			Assert.Equal(time.ToString(), converter.ConvertFromString(time.ToString("o"), null, propertyMapData).ToString());
			Assert.Equal(time.ToString(), converter.ConvertFromString(" " + time + " ", null, propertyMapData).ToString());

			// Invalid conversions.
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
		}
	}
}
#endif
