// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterMappingTests
	{
		[TestMethod]
		public void WriteMultipleNamesTest()
		{
			var records = new List<MultipleNamesAttributeClass>
			{
				new MultipleNamesAttributeClass { IntColumn = 1, StringColumn = "one" },
				new MultipleNamesAttributeClass { IntColumn = 2, StringColumn = "two" }
			};

			string csv;
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.ClassMapping<MultipleNamesAttributeClassMap>();
				csvWriter.WriteRecords( records );

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = string.Empty;
			expected += "int1,string1\r\n";
			expected += "1,one\r\n";
			expected += "2,two\r\n";

			Assert.IsNotNull( csv );
			Assert.AreEqual( expected, csv );
		}

		private class MultipleNamesAttributeClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class MultipleNamesAttributeClassMap : CsvClassMap<MultipleNamesAttributeClass>
		{
			public MultipleNamesAttributeClassMap()
			{
				Map( m => m.IntColumn ).Name( "int1", "int2", "int3" );
				Map( m => m.StringColumn ).Name( "string1", "string2", "string3" );
			}
		}
	}
}
