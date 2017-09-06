using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
    public class TrimTests
    {
		[TestMethod]
		public void OutsideStartTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  a,b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideStartSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  a b c,d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideEndTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "a  ,b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideEndSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "a b c  ,d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideBothTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  a  ,b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideBothSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  a b c  ,d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesStartTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  \"a\",b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesStartSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  \"a b c\",d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesEndTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"a\"  ,b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesEndSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"a b c\"  ,d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesBothTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  \"a\"  ,b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  \"a b c\"  ,d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  a b c  ,  d e f  " );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
				Assert.AreEqual( "d e f", record[1] );
			}
		}

		[TestMethod]
		public void InsideQuotesStartTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"  a\",b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"  a b c\",b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesEndTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"a  \",b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesEndSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"a b c  \",d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesBothTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"  a  \",b" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"  a b c  \",d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "\"  a b c  \",\"  d e f  \"" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
				Assert.AreEqual( "d e f", record[1] );
			}
		}

		[TestMethod]
		public void OutsideAndInsideQuotesTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "  \"  a b c  \"  ,  \"  d e f  \"  " );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual( "a b c", record[0] );
				Assert.AreEqual( "d e f", record[1] );
			}
		}
		
		[TestMethod]
		public void ReadingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "A,B" );
				writer.WriteLine( "  \"  a b c  \"  ,  \"  d e f  \"  " );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var records = csv.GetRecords<dynamic>().ToList();

				var record = records[0];
				Assert.AreEqual( "a b c", record.A );
				Assert.AreEqual( "d e f", record.B );
			}
		}
	}
}
