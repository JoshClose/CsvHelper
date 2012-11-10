using System;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class DateTimeConverterTests
	{
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new DateTimeConverter();

			var dateTime = DateTime.Now;

			// Valid conversions.
			Assert.Equal( dateTime.ToString(), converter.ConvertToString( dateTime ) );

			// Invalid conversions.
			Assert.Equal( "1", converter.ConvertToString( 1 ) );
			Assert.Equal( "", converter.ConvertToString( null ) );
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new DateTimeConverter();

			var dateTime = DateTime.Now;

			// Valid conversions.
			Assert.Equal( dateTime.ToString(), converter.ConvertFromString( dateTime.ToString() ).ToString() );
			Assert.Equal( dateTime.ToString(), converter.ConvertFromString( dateTime.ToString( "o" ) ).ToString() );
			Assert.Equal( dateTime.ToString(), converter.ConvertFromString( " " + dateTime + " " ).ToString() );

			// Invalid conversions.
			Assert.Throws<NotSupportedException>( () => converter.ConvertFromString( null ) );
		}
	}
}
