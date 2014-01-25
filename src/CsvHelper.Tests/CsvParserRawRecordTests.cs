// Copyright 2009-2014 Josh Close and Contributors
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
	public class CsvParserRawRecordTests
	{
		[TestMethod]
		public void RawRecordCrLfTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "3,4\r\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\r\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( null, parser.RawRecord );
			}
		}

		[TestMethod]
		public void RawRecordCrTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "1,2\r" );
				writer.Write( "3,4\r" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\r", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( null, parser.RawRecord );
			}
		}

		[TestMethod]
		public void RawRecordLfTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "1,2\n" );
				writer.Write( "3,4\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( null, parser.RawRecord );
			}
		}

		[TestMethod]
		public void TinyBufferTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "3,4\r\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.BufferSize = 1;

				parser.Read();
				Assert.AreEqual( "1,2\r\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r\n", parser.RawRecord );

				parser.Read();
				Assert.AreEqual( null, parser.RawRecord );
			}
		}
	}
}
