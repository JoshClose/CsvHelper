using System;
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

			Assert.Equal( "123", converter.ConvertToString( (byte)123 ) );

			Assert.Equal( "", converter.ConvertToString( null ) );
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new ByteConverter();

			Assert.Equal( (byte)123, converter.ConvertFromString( "123" ) );
			Assert.Equal( (byte)123, converter.ConvertFromString( " 123 " ) );

			Assert.Throws<NotSupportedException>( () => converter.ConvertFromString( null ) );
		}
	}
}
