using System;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderConstructorTests
	{
		[Fact]
		public void InvalidParameterTest()
		{
			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvReader( (TextReader)null );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvReader( null, new CsvConfiguration() );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				using( var stream = new MemoryStream() )
				using( var reader = new StreamReader( stream ) )
				{
					new CsvReader( reader, null );
				}
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvReader( (CsvParser)null );
			} );

			Assert.Throws<CsvConfigurationException>( () =>
			{
				new CsvReader( new TestParser() );
			} );
		}

		[Fact]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				Assert.Same( csv.Configuration, csv.Parser.Configuration );
			}
		}

		[Fact]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderAndConfigurationTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader, new CsvConfiguration() ) )
			{
				Assert.Same( csv.Configuration, csv.Parser.Configuration );
			}
		}

		[Fact]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingParserTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			{
				var parser = new CsvParser( reader );

				using( var csv = new CsvReader( parser ) )
				{
					Assert.Same( csv.Configuration, csv.Parser.Configuration );
					Assert.Same( parser, csv.Parser );
				}
			}
		}

		private class TestParser : ICsvParser
		{
			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public CsvConfiguration Configuration { get; private set; }

			public int FieldCount
			{
				get { throw new NotImplementedException(); }
			}

			public string[] Read()
			{
				throw new NotImplementedException();
			}
		}
	}
}
