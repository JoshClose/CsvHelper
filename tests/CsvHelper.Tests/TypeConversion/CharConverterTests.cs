// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{

	public class CharConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new CharConverter();
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.Equal("a", converter.ConvertToString('a', null!, propertyMapData));

			Assert.Equal("True", converter.ConvertToString(true, null!, propertyMapData));

			Assert.Equal("", converter.ConvertToString(null, null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new CharConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			Assert.Equal('a', converter.ConvertFromString("a", null!, propertyMapData));
			Assert.Equal('a', converter.ConvertFromString(" a ", null!, propertyMapData));
			Assert.Equal(' ', converter.ConvertFromString(" ", null!, propertyMapData));

			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
		}
	}
}
