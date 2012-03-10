using System;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvParserConstructorTests
	{
		[Fact]
		public void InvalidParametersTest()
		{
			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvParser( null );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvParser( null, new CsvConfiguration() );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				using( var stream = new MemoryStream() )
				using( var reader = new StreamReader( stream ) )
				{
					new CsvParser( reader, null );
				}
			} );
		}

		[Fact]
		public void EnsureInternalsAreSetupWhenPassingReaderAndConfigTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			{
				var config = new CsvConfiguration();
				using( var parser = new CsvParser( reader, config ) )
				{
					Assert.Same( config, parser.Configuration );
				}
			}
		}
	}
}
