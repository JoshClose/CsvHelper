// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper35.Tests
{
	[TestClass]
	public class CsvWriterTests
	{
		[TestMethod]
		public void WriteRecordNonGenericTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.WriteRecord( typeof( Test ), new Test { Id = 1, Name = "one" } );
				csv.WriteRecord( typeof( Test ), new Test { Id = 2, Name = "two" } );

				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "1,one" );
				expected.AppendLine( "2,two" );

				Assert.AreEqual( expected.ToString(), text );
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
