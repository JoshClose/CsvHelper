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
			var records = new List<MultipleNamesClass>
			{
				new MultipleNamesClass { IntColumn = 1, StringColumn = "one" },
				new MultipleNamesClass { IntColumn = 2, StringColumn = "two" }
			};

			string csv;
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.RegisterClassMap<MultipleNamesClassMap>();
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

		[TestMethod]
		public void SameNameMultipleTimesTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<SameNameMultipleTimesClass>
				{
					new SameNameMultipleTimesClass
					{
						Name1 = "1",
						Name2 = "2",
						Name3 = "3"
					}
				};
				csv.Configuration.RegisterClassMap<SameNameMultipleTimesClassMap>();
				csv.WriteRecords( records );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				var expected = "ColumnName,ColumnName,ColumnName\r\n1,2,3\r\n";
				Assert.AreEqual( expected, text );
			}
		}

		private class SameNameMultipleTimesClass
		{
			public string Name1 { get; set; }

			public string Name2 { get; set; }

			public string Name3 { get; set; }
		}

		private sealed class SameNameMultipleTimesClassMap : CsvClassMap<SameNameMultipleTimesClass>
		{
			public override void CreateMap()
			{
				Map( m => m.Name1 ).Name( "ColumnName" ).NameIndex( 1 );
				Map( m => m.Name2 ).Name( "ColumnName" ).NameIndex( 2 );
				Map( m => m.Name3 ).Name( "ColumnName" ).NameIndex( 0 );
			}
		}

		private class MultipleNamesClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class MultipleNamesClassMap : CsvClassMap<MultipleNamesClass>
		{
			public override void CreateMap()
			{
				Map( m => m.IntColumn ).Name( "int1", "int2", "int3" );
				Map( m => m.StringColumn ).Name( "string1", "string2", "string3" );
			}
		}
	}
}
