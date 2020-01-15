// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
			using( var parser = new CsvParser(reader, CultureInfo.InvariantCulture) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "3,4\r\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\r\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( string.Empty, parser.FieldReader.Context.RawRecord );
			}
		}

		[TestMethod]
		public void RawRecordCrTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser(reader, CultureInfo.InvariantCulture) )
			{
				writer.Write( "1,2\r" );
				writer.Write( "3,4\r" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\r", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( string.Empty, parser.FieldReader.Context.RawRecord );
			}
		}

		[TestMethod]
		public void RawRecordLfTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser(reader, CultureInfo.InvariantCulture) )
			{
				writer.Write( "1,2\n" );
				writer.Write( "3,4\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual( "1,2\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( string.Empty, parser.FieldReader.Context.RawRecord );
			}
		}

		[TestMethod]
		public void RawRecordCr2DelimiterTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser(reader, CultureInfo.InvariantCulture) )
			{
				writer.Write( "1;;2\r" );
				writer.Write( "3;;4\r" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.Delimiter = ";;";

				parser.Read();
				Assert.AreEqual( "1;;2\r", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( "3;;4\r", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( string.Empty, parser.FieldReader.Context.RawRecord );
			}
		}

		[TestMethod]
		public void TinyBufferTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var parser = new CsvParser(reader, CultureInfo.InvariantCulture) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "3,4\r\n" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.BufferSize = 1;

				parser.Read();
				Assert.AreEqual( "1,2\r\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( "3,4\r\n", parser.FieldReader.Context.RawRecord );

				parser.Read();
				Assert.AreEqual( string.Empty, parser.FieldReader.Context.RawRecord );
			}
		}
	}
}
