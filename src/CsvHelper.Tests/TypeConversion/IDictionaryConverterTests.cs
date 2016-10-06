using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class IDictionaryConverterTests
	{
		[TestMethod]
		public void FullWriteTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<Test>
				{
					new Test { Dictionary = new Dictionary<string, int> { { "Prop1", 1 }, { "Prop2", 2 }, { "Prop3", 3 } } }
				};
				csv.Configuration.HasHeaderRecord = false;
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( ",1,2,3,\r\n", result );
			}
		}

		[TestMethod]
		public void FullReadTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Prop1,Prop2,Prop3,Prop4,Prop5" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var dict = records[0].Dictionary;

				Assert.AreEqual( 3, dict.Count );
				Assert.AreEqual( "2", dict["Prop2"] );
				Assert.AreEqual( "3", dict["Prop3"] );
				Assert.AreEqual( "4", dict["Prop4"] );
			}
		}

		[TestMethod]
		public void FullReadNoHeaderTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// You can't read into a dictionary without a header.
					// You need to header value to use as the key.
				}
			}
		}

		[TestMethod]
		public void FullReadWithHeaderIndexDifferentNamesTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary1,Dictionary2,Dictionary3,After" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].Dictionary;

				Assert.AreEqual( 3, list.Count );
				Assert.AreEqual( "2", list["Dictionary1"] );
				Assert.AreEqual( "3", list["Dictionary2"] );
				Assert.AreEqual( "4", list["Dictionary3"] );
			}
		}

		[TestMethod]
		public void FullReadWithHeaderIndexSameNamesTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary,Dictionary,Dictionary,After" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		[TestMethod]
		public void FullReadWithDefaultHeaderDifferentNamesTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary1,Dictionary2,Dictionary3,After" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestDefaultMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// Indexes must be specified for dictionaries.
				}
			}
		}

		[TestMethod]
		public void FullReadWithDefaultHeaderSameNamesTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary,Dictionary,Dictionary,After" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestDefaultMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// Headers can't have the same name.
				}
			}
		}

		[TestMethod]
		public void FullReadWithNamedHeaderTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary,Dictionary,Dictionary,After" );
				writer.WriteLine( "1,2,3,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// Header's can't have the same name.
				}
			}
		}

		[TestMethod]
		public void FullReadWithHeaderListItemsScattered()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,Dictionary,A,Dictionary,B,Dictionary,After" );
				writer.WriteLine( "1,2,3,4,5,6,7" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch( CsvReaderException )
				{
					// Header's can't have the same name.
				}
			}
		}

		[TestMethod]
		public void ReadNullValuesIndexTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,D1,D2,D3,After" );
				writer.WriteLine( "1,null,NULL,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();
				var list = records[0].Dictionary;

				Assert.AreEqual( 3, list.Count );
				Assert.AreEqual( null, list["D1"] );
				Assert.AreEqual( null, list["D2"] );
				Assert.AreEqual( "4", list["D3"] );
			}
		}

		private class Test
		{
			public string Before { get; set; }
			public IDictionary Dictionary { get; set; }
			public string After { get; set; }
		}

		private sealed class TestIndexMap : CsvClassMap<Test>
		{
			public TestIndexMap()
			{
				Map( m => m.Before ).Index( 0 );
				Map( m => m.Dictionary ).Index( 1, 3 );
				Map( m => m.After ).Index( 4 );
			}
		}

		private sealed class TestNamedMap : CsvClassMap<Test>
		{
			public TestNamedMap()
			{
				Map( m => m.Before ).Name( "Before" );
				Map( m => m.Dictionary ).Name( "Dictionary" );
				Map( m => m.After ).Name( "After" );
			}
		}

		private sealed class TestDefaultMap : CsvClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map( m => m.Before );
				Map( m => m.Dictionary );
				Map( m => m.After );
			}
		}
	}
}
