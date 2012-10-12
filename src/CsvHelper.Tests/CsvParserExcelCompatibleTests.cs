using System.IO;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvParserExcelCompatibleTests
	{
		[Fact]
		public void ParseTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "one,two,three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseEscapedFieldsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"one\",\"two\",\"three\"" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseEscapedAndNonFieldsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "one,\"two\",three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseEscapedFieldWithSpaceAfterTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "one,\"two\" ,three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two ", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseEscapedFieldWithSpaceBeforeTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// one, "two",three
				writer.WriteLine( "one, \"two\",three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( " \"two\"", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseEscapedFieldWithQuoteAfterTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// 1,"two" "2,3
				writer.WriteLine( "1,\"two\" \"2,3" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "1", record[0] );
				Assert.Equal( "two \"2", record[1] );
				Assert.Equal( "3", record[2] );

				Assert.Null( parser.Read() );
			}
		}

		[Fact]
		public void ParseEscapedFieldWithEscapedQuoteTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// 1,"two "" 2",3
				writer.WriteLine( "1,\"two \"\" 2\",3" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "1", record[0] );
				Assert.Equal( "two \" 2", record[1] );
				Assert.Equal( "3", record[2] );
			}
		}
	}
}
