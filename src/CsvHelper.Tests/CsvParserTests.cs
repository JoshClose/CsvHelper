#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System.IO;
using System.Text;
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

		[Fact]
		public void CharReadTotalTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				parser.Configuration.AllowComments = true;

				// This is a breakdown of the char counts.
				// Read() will read up to the first line end char
				// and any more on the line will get read with the next read.

				// [I][d][,][N][a][m][e][\r][\n]
				//  1  2  3  4  5  6  7   8   9
				// [1][,][o][n][e][\r][\n]
				// 10 11 12 13 14  15  16
				// [,][\r][\n]
				// 17  18  19
				// [\r][\n]
				//  20  21
				// [#][ ][c][o][m][m][e][n][t][s][\r][\n]
				// 22 23 24 25 26 27 28 29 30 31  32  33
				// [2][,][t][w][o][\r][\n]
				// 34 35 36 37 38  39  40
				// [3][,]["][t][h][r][e][e][,][ ][f][o][u][r]["][\r][\n]
				// 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55  56  57
				
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "," );
				writer.WriteLine( "" );
				writer.WriteLine( "# comments" );
				writer.WriteLine( "2,two" );
				writer.WriteLine( "3,\"three, four\"" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal( 8, parser.Position );

				parser.Read();
				Assert.Equal( 15, parser.Position );

				parser.Read();
				Assert.Equal( 18, parser.Position );

				parser.Read();
				Assert.Equal( 39, parser.Position );

				parser.Read();
				Assert.Equal( 56, parser.Position );

				parser.Read();
				Assert.Equal( 57, parser.Position );
			}
		}

		[Fact]
		public void StreamSeekingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				parser.Configuration.AllowComments = true;

				// This is a breakdown of the char counts.
				// Read() will read up to the first line end char
				// and any more on the line will get read with the next read.

				// [I][d][,][N][a][m][e][\r][\n]
				//  1  2  3  4  5  6  7   8   9
				// [1][,][o][n][e][\r][\n]
				// 10 11 12 13 14  15  16
				// [,][\r][\n]
				// 17  18  19
				// [\r][\n]
				//  20  21
				// [#][ ][c][o][m][m][e][n][t][s][\r][\n]
				// 22 23 24 25 26 27 28 29 30 31  32  33
				// [2][,][t][w][o][\r][\n]
				// 34 35 36 37 38  39  40
				// [3][,]["][t][h][r][e][e][,][ ][f][o][u][r]["][\r][\n]
				// 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55  56  57

				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "," );
				writer.WriteLine( "" );
				writer.WriteLine( "# comments" );
				writer.WriteLine( "2,two" );
				writer.WriteLine( "3,\"three, four\"" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();
				Assert.Equal( "Id", record[0] );
				Assert.Equal( "Name", record[1] );

				stream.Position = 0;
				stream.Seek( parser.Position, SeekOrigin.Begin );
				record = parser.Read();
				Assert.Equal( "1", record[0] );
				Assert.Equal( "one", record[1] );

				stream.Position = 0;
				stream.Seek( parser.Position, SeekOrigin.Begin );
				record = parser.Read();
				Assert.Equal( "", record[0] );
				Assert.Equal( "", record[1] );

				stream.Position = 0;
				stream.Seek( parser.Position, SeekOrigin.Begin );
				record = parser.Read();
				Assert.Equal( "2", record[0] );
				Assert.Equal( "two", record[1] );

				stream.Position = 0;
				stream.Seek( parser.Position, SeekOrigin.Begin );
				record = parser.Read();
				Assert.Equal( "3", record[0] );
				Assert.Equal( "three, four", record[1] );
			}
		}
	}
}
