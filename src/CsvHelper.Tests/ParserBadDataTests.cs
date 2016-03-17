﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ParserBadDataTests
	{
		[TestMethod]
		public void CallbackTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( " a\"bc\",d" );
				writer.WriteLine( "\"a\"\"b\"c \" ,d" );
				writer.WriteLine( "\"a\"\"b\",c" );
				writer.Flush();
				stream.Position = 0;

				string field = null;
				parser.Configuration.BadDataCallback = f => field = f;
				parser.Read();

				Assert.IsNotNull( field );
				Assert.AreEqual( " a\"bc\"", field );

				field = null;
				parser.Read();
				Assert.IsNotNull( field );
				Assert.AreEqual( "a\"bc \" ", field );

				field = null;
				parser.Read();
				Assert.IsNull( field );
			}
		}

		[TestMethod]
		public void ThrowExceptionTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var parser = new CsvParser( reader ) )
			{
				writer.WriteLine( "1,2" );
				writer.WriteLine( " a\"bc\",d" );
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.ThrowOnBadData = true;
				parser.Read();
				try
				{
					parser.Read();
					Assert.Fail( "Failed to throw exception on bad data." );
				}
				catch( CsvBadDataException ) { }
			}
		}
	}
}
