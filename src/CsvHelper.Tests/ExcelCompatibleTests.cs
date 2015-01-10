// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System.Collections.Generic;
using System.IO;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class ExcelCompatibleTests
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

		[TestMethod]
		public void ParserSepCrLfTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "sep=;\r\n" );
				writer.Write( "1;2;3\r\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.HasExcelSeparator = true;
				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( "1", record[0] );
				Assert.AreEqual( "2", record[1] );
				Assert.AreEqual( "3", record[2] );
			}
		}

		[TestMethod]
		public void ParserSepCrTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "sep=;\r" );
				writer.Write( "1;2;3\r" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.HasExcelSeparator = true;
				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( "1", record[0] );
				Assert.AreEqual( "2", record[1] );
				Assert.AreEqual( "3", record[2] );
			}
		}

		[TestMethod]
		public void ParserSepLfTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "sep=;\n" );
				writer.Write( "1;2;3\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.HasExcelSeparator = true;
				var record = parser.Read();

				Assert.IsNotNull( record );
				Assert.AreEqual( "1", record[0] );
				Assert.AreEqual( "2", record[1] );
				Assert.AreEqual( "3", record[2] );
			}
		}

		[TestMethod]
		public void WriteRecordsSepTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.Delimiter = ";";
				csv.Configuration.HasExcelSeparator = true;
				var list = new List<Simple>
				{
					new Simple
					{
						Id = 1,
						Name = "one",
					},
				};
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "sep=;\r\nId;Name\r\n1;one\r\n", text );
			}
		}

		[TestMethod]
		public void WriteRecordSepTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var record = new Simple
				{
					Id = 1,
					Name = "one",
				};

				csv.Configuration.Delimiter = ";";
				csv.Configuration.HasExcelSeparator = true;
				csv.WriteExcelSeparator();
				csv.WriteHeader<Simple>();
				csv.WriteRecord( record );

				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "sep=;\r\nId;Name\r\n1;one\r\n", text );
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
