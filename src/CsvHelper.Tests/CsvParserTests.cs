#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System.IO;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvParserTests
	{
		[Fact]
		public void ReadNewRecordTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "four,five,six" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader );

			var count = 0;
			while( parser.Read() != null )
			{
				count++;
			}

			Assert.Equal( 2, count );
		}

		[Fact]
		public void ReadEmptyRowsTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "four,five,six" );
			writer.WriteLine( ",," );
			writer.WriteLine( "" );
			writer.WriteLine( "" );
			writer.WriteLine( "seven,eight,nine" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader );

			var count = 0;
			while( parser.Read() != null )
			{
				count++;
			}

			Assert.Equal( 4, count );
		}

		[Fact]
		public void ReadTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "four,five,six" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader );

			var record = parser.Read();
			Assert.Equal( "one", record[0] );
			Assert.Equal( "two", record[1] );
			Assert.Equal( "three", record[2] );

			record = parser.Read();
			Assert.Equal( "four", record[0] );
			Assert.Equal( "five", record[1] );
			Assert.Equal( "six", record[2] );

			record = parser.Read();
			Assert.Null( record );
		}

		[Fact]
		public void ReadFieldQuotesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,\"two\",three" );
			writer.WriteLine( "four,\"\"\"five\"\"\",six" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { BufferSize = 2000 } };

			var record = parser.Read();
			Assert.Equal( "one", record[0] );
			Assert.Equal( "two", record[1] );
			Assert.Equal( "three", record[2] );

			record = parser.Read();
			Assert.Equal( "four", record[0] );
			Assert.Equal( "\"five\"", record[1] );
			Assert.Equal( "six", record[2] );

			record = parser.Read();
			Assert.Null( record );
		}

		[Fact]
		public void ReadSpacesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( " one , \"two three\" , four " );
			writer.WriteLine( " \" five \"\" six \"\" seven \" " );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { BufferSize = 2 } };

			var record = parser.Read();
			Assert.Equal( " one ", record[0] );
			Assert.Equal( " two three ", record[1] );
			Assert.Equal( " four ", record[2] );

			record = parser.Read();
			Assert.Equal( "  five \" six \" seven  ", record[0] );

			record = parser.Read();
			Assert.Null( record );
		}

		[Fact]
		public void CallingReadMultipleTimesAfterDoneReadingTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "four,five,six" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader );

			parser.Read();
			parser.Read();
			parser.Read();
			parser.Read();
		}

		[Fact]
		public void ParseEmptyTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				var record = parser.Read();
				Assert.Null( record );
			}
		}

		[Fact]
		public void Parse1RecordWithNoCrlfTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.Write( "one,two,three" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void Parse2RecordsLastWithNoCrlfTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.WriteLine( "one,two,three" );
				streamWriter.Write( "four,five,six" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				parser.Read();
				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "four", record[0] );
				Assert.Equal( "five", record[1] );
				Assert.Equal( "six", record[2] );
			}
		}

		[Fact]
		public void ParseFirstFieldIsEmptyQuotedTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.WriteLine( "\"\",\"two\",\"three\"" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseLastFieldIsEmptyQuotedTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.WriteLine( "\"one\",\"two\",\"\"" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "", record[2] );
			}
		}

		[Fact]
		public void ParseQuoteOnlyQuotedFieldTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.WriteLine( "\"\"\"\",\"two\",\"three\"" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 3, record.Length );
				Assert.Equal( "\"", record[0] );
				Assert.Equal( "two", record[1] );
				Assert.Equal( "three", record[2] );
			}
		}

		[Fact]
		public void ParseRecordsWithOnlyOneField()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.WriteLine( "row one" );
				streamWriter.WriteLine( "row two" );
				streamWriter.WriteLine( "row three" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 1, record.Length );
				Assert.Equal( "row one", record[0] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 1, record.Length );
				Assert.Equal( "row two", record[0] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 1, record.Length );
				Assert.Equal( "row three", record[0] );
			}
		}

		[Fact]
		public void ParseRecordWhereOnlyCarriageReturnLineEndingIsUsed()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.Write( "one,two\r" );
				streamWriter.Write( "three,four\r" );
				streamWriter.Write( "five,six\r" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "three", record[0] );
				Assert.Equal( "four", record[1] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "five", record[0] );
				Assert.Equal( "six", record[1] );
			}
		}

		[Fact]
		public void ParseRecordWhereOnlyLineFeedLineEndingIsUsed()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var streamWriter = new StreamWriter( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				streamWriter.Write( "one,two\n" );
				streamWriter.Write( "three,four\n" );
				streamWriter.Write( "five,six\n" );
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "one", record[0] );
				Assert.Equal( "two", record[1] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "three", record[0] );
				Assert.Equal( "four", record[1] );

				record = parser.Read();
				Assert.NotNull( record );
				Assert.Equal( 2, record.Length );
				Assert.Equal( "five", record[0] );
				Assert.Equal( "six", record[1] );
			}
		}

		[Fact]
		public void ParseCommentedOutLineWithCommentsOn()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "#four,five,six" );
			writer.WriteLine( "seven,eight,nine" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { AllowComments = true } };

			parser.Read();
			var record = parser.Read();
			Assert.Equal( "seven", record[0] );
		}

		[Fact]
		public void ParseCommentedOutLineWithCommentsOff()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "#four,five,six" );
			writer.WriteLine( "seven,eight,nine" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { AllowComments = false } };

			parser.Read();
			var record = parser.Read();
			Assert.Equal( "#four", record[0] );
		}

		[Fact]
		public void ParseCommentedOutLineWithDifferentCommentCommentsOn()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three" );
			writer.WriteLine( "*four,five,six" );
			writer.WriteLine( "seven,eight,nine" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { AllowComments = true, Comment = '*' } };

			parser.Read();
			var record = parser.Read();
			Assert.Equal( "seven", record[0] );
		}

		[Fact]
		public void ParseUsingDifferentDelimiter()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one\ttwo\tthree" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { Delimiter = '\t' } };

			var record = parser.Read();
			Assert.Equal( "one", record[0] );
			Assert.Equal( "two", record[1] );
			Assert.Equal( "three", record[2] );
		}

		[Fact]
		public void ParseUsingDifferentQuote()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "'one','two','three'" );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader ) { Configuration = { Quote = '\'' } };

			var record = parser.Read();
			Assert.Equal( "one", record[0] );
			Assert.Equal( "two", record[1] );
			Assert.Equal( "three", record[2] );
		}

		[Fact]
		public void ReadFinalRecordWithNoEndOfLineTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			writer.WriteLine( "one,two,three," );
			writer.Write( "four,five,six," );
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader( stream );

			var parser = new CsvParser( reader );

			var record = parser.Read();

			Assert.NotNull( record );
			Assert.Equal( "", record[3] );

			record = parser.Read();

			Assert.NotNull( record );
			Assert.Equal( "", record[3] );
		}
	}
}
