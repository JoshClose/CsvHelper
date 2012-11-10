using System;
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

			Assert.Equal( "True", converter.ConvertToString( true ) );

			Assert.Equal( "False", converter.ConvertToString( false ) );

			Assert.Equal( "", converter.ConvertToString( null ) );
			Assert.Equal( "1", converter.ConvertToString( 1 ) );
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			Assert.True( (bool)converter.ConvertFromString( "true" ) );
			Assert.True( (bool)converter.ConvertFromString( "True" ) );
			Assert.True( (bool)converter.ConvertFromString( "TRUE" ) );
			Assert.True( (bool)converter.ConvertFromString( "1" ) );
			Assert.True( (bool)converter.ConvertFromString( "yes" ) );
			Assert.True( (bool)converter.ConvertFromString( "YES" ) );
			Assert.True( (bool)converter.ConvertFromString( "y" ) );
			Assert.True( (bool)converter.ConvertFromString( "Y" ) );
			Assert.True( (bool)converter.ConvertFromString( " true " ) );
			Assert.True( (bool)converter.ConvertFromString( " yes " ) );
			Assert.True( (bool)converter.ConvertFromString( " y " ) );

			Assert.False( (bool)converter.ConvertFromString( "false" ) );
			Assert.False( (bool)converter.ConvertFromString( "False" ) );
			Assert.False( (bool)converter.ConvertFromString( "FALSE" ) );
			Assert.False( (bool)converter.ConvertFromString( "0" ) );
			Assert.True( (bool)converter.ConvertFromString( "no" ) );
			Assert.True( (bool)converter.ConvertFromString( "NO" ) );
			Assert.True( (bool)converter.ConvertFromString( "n" ) );
			Assert.True( (bool)converter.ConvertFromString( "N" ) );
			Assert.False( (bool)converter.ConvertFromString( " false " ) );
			Assert.False( (bool)converter.ConvertFromString( " 0 " ) );
			Assert.True( (bool)converter.ConvertFromString( " no " ) );
			Assert.True( (bool)converter.ConvertFromString( " n " ) );

			Assert.Throws<NotSupportedException>( () => converter.ConvertFromString( null ) );
		}
	}
}
