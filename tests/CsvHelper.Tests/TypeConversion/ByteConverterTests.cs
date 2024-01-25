// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class ByteConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new ByteConverter();
			var propertyMapData = new MemberMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.Equal( "123", converter.ConvertToString( (byte)123, null, propertyMapData ) );

			Assert.Equal( "", converter.ConvertToString( null, null, propertyMapData ) );
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new ByteConverter();

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			Assert.Equal( (byte)123, converter.ConvertFromString( "123", null, propertyMapData ) );
			Assert.Equal( (byte)123, converter.ConvertFromString( " 123 ", null, propertyMapData ) );

			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString(null, row, propertyMapData));
		}
	}
}
