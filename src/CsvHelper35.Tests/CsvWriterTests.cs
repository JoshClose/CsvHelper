// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
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
				csv.Configuration.RegisterClassMap<TestMap>();

				csv.WriteRecord( new Test { Id = 1, Name = "one" } );
				csv.WriteRecord( new Test { Id = 2, Name = "two" } );

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

		private sealed class TestMap : CsvClassMap<Test>
		{
			public override void CreateMap()
			{
				Map( m => m.Id );
				Map( m => m.Name );
			}
		}
	}
}
