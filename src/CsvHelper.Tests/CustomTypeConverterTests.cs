// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CustomTypeConverterTests
	{
		[Fact]
		public void BooleanTypeConverterTest()
		{
			var converter = new BooleanTypeConverter();

			Assert.True( converter.CanConvertFrom( typeof( string ) ) );
			Assert.True( converter.CanConvertTo( typeof( string ) ) );

			Assert.Equal( true, converter.ConvertFrom( "true" ) );
			Assert.Equal( true, converter.ConvertFrom( "True" ) );
			Assert.Equal( true, converter.ConvertFrom( "1" ) );

			Assert.Equal( false, converter.ConvertFrom( "false" ) );
			Assert.Equal( false, converter.ConvertFrom( "False" ) );
			Assert.Equal( false, converter.ConvertFrom( "0" ) );
		}
	}
}
