#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvParserTests
	{
		[TestMethod]
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

			Assert.AreEqual( 2, count );
		}

		[TestMethod]
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

			Assert.AreEqual( 4, count );
		}

		[TestMethod]
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
			Assert.AreEqual( "one", record[0] );
			Assert.AreEqual( "two", record[1] );
			Assert.AreEqual( "three", record[2] );

			record = parser.Read();
			Assert.AreEqual( "four", record[0] );
			Assert.AreEqual( "five", record[1] );
			Assert.AreEqual( "six", record[2] );

			record = parser.Read();
			Assert.IsNull( record );
		}

		[TestMethod]
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
			Assert.AreEqual( "one", record[0] );
			Assert.AreEqual( "two", record[1] );
			Assert.AreEqual( "three", record[2] );

			record = parser.Read();
			Assert.AreEqual( "four", record[0] );
			Assert.AreEqual( "\"five\"", record[1] );
			Assert.AreEqual( "six", record[2] );

			record = parser.Read();
			Assert.IsNull( record );
		}

		[TestMethod]
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
			Assert.AreEqual( " one ", record[0] );
			Assert.AreEqual( " two three ", record[1] );
			Assert.AreEqual( " four ", record[2] );

			record = parser.Read();
			Assert.AreEqual( "  five \" six \" seven  ", record[0] );

			record = parser.Read();
			Assert.IsNull( record );
		}

		[TestMethod]
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

		[TestMethod]
		public void ParseEmptyTest()
		{
			using( var memoryStream = new MemoryStream() )
			using( var streamReader = new StreamReader( memoryStream ) )
			using( var parser = new CsvParser( streamReader ) )
			{
				var record = parser.Read();
				Assert.IsNull( record );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "four", record[0] );
				Assert.AreEqual( "five", record[1] );
				Assert.AreEqual( "six", record[2] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "", record[2] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "\"", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 1, record.Length );
				Assert.AreEqual( "row one", record[0] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 1, record.Length );
				Assert.AreEqual( "row two", record[0] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 1, record.Length );
				Assert.AreEqual( "row three", record[0] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "three", record[0] );
				Assert.AreEqual( "four", record[1] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "five", record[0] );
				Assert.AreEqual( "six", record[1] );
			}
		}

		[TestMethod]
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
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "three", record[0] );
				Assert.AreEqual( "four", record[1] );

				record = parser.Read();
				Assert.IsNotNull( record );
				Assert.AreEqual( 2, record.Length );
				Assert.AreEqual( "five", record[0] );
				Assert.AreEqual( "six", record[1] );
			}
		}

		[TestMethod]
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
			Assert.AreEqual( "seven", record[0] );
		}

		[TestMethod]
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
			Assert.AreEqual( "#four", record[0] );
		}

		[TestMethod]
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
			Assert.AreEqual( "seven", record[0] );
		}

		[TestMethod]
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
			Assert.AreEqual( "one", record[0] );
			Assert.AreEqual( "two", record[1] );
			Assert.AreEqual( "three", record[2] );
		}

		[TestMethod]
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
			Assert.AreEqual( "one", record[0] );
			Assert.AreEqual( "two", record[1] );
			Assert.AreEqual( "three", record[2] );
		}

		[TestMethod]
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

			var count = 0;
			while( parser.Read() != null )
			{
				count++;
			}

			Assert.AreEqual( 2, count );
		}
	}
}
