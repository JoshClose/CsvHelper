using System;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class CharConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new CharConverter();

			Assert.Equal( "a", converter.ConvertToString( 'a' ) );
			
			Assert.Equal( "True", converter.ConvertToString( true ) );
	
			Assert.Equal( "", converter.ConvertToString( null ) );
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new CharConverter();

			Assert.Equal( 'a', converter.ConvertFromString( "a" ) );
			Assert.Equal( 'a', converter.ConvertFromString( " a " ) );

			Assert.Throws<NotSupportedException>( () => converter.ConvertFromString( null ) );
		}
	}
}
