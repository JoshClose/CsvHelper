// Copyright 2009-2013 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.IO;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvParserExcelCompatibleTests
	{
		[TestMethod]
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

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
		public void ParseEscapedFieldsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// "one","two","three"
				writer.WriteLine( "\"one\",\"two\",\"three\"" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
		public void ParseEscapedAndNonFieldsTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// one,"two",three
				writer.WriteLine( "one,\"two\",three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithSpaceAfterTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				// one,"two" ,three
				writer.WriteLine( "one,\"two\" ,three" );
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( "two ", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
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

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "one", record[0] );
				Assert.AreEqual( " \"two\"", record[1] );
				Assert.AreEqual( "three", record[2] );
			}
		}

		[TestMethod]
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

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "1", record[0] );
				Assert.AreEqual( "two \"2", record[1] );
				Assert.AreEqual( "3", record[2] );

				Assert.IsNull( parser.Read() );
			}
		}

		[TestMethod]
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

				Assert.IsNotNull( record );
				Assert.AreEqual( 3, record.Length );
				Assert.AreEqual( "1", record[0] );
				Assert.AreEqual( "two \" 2", record[1] );
				Assert.AreEqual( "3", record[2] );
			}
		}
	}
}
