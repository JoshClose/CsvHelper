﻿using System;
using System.Collections;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class CollectionGenericConverterTests
	{
		[TestMethod]
		public void ConvertNoIndexEndTest()
		{
			var config = new CsvConfiguration { HasHeaderRecord = false };
			var rowMock = new Mock<ICsvReaderRow>();
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			rowMock.Setup( m => m.Configuration ).Returns( config );
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<Type>(), It.IsAny<int>() ) ).Returns<Type, int>( ( type, index ) => Convert.ToInt32( currentRecord[index] ) );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "List" ) )
			{
				Index = 2
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new CollectionGenericConverter();
			var list = (List<int?>)converter.ConvertFromString( "1", rowMock.Object, data );

			Assert.AreEqual( 3, list.Count );
			Assert.AreEqual( 1, list[0] );
			Assert.AreEqual( 2, list[1] );
			Assert.AreEqual( 3, list[2] );
		}

		[TestMethod]
		public void ConvertWithIndexEndTest()
		{
			var config = new CsvConfiguration { HasHeaderRecord = false };
			var rowMock = new Mock<ICsvReaderRow>();
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			rowMock.Setup( m => m.Configuration ).Returns( config );
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<Type>(), It.IsAny<int>() ) ).Returns<Type, int>( ( type, index ) => Convert.ToInt32( currentRecord[index] ) );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "List" ) )
			{
				Index = 2,
				IndexEnd = 3
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new CollectionGenericConverter();
			var list = (List<int?>)converter.ConvertFromString( "1", rowMock.Object, data );

			Assert.AreEqual( 2, list.Count );
			Assert.AreEqual( 1, list[0] );
			Assert.AreEqual( 2, list[1] );
		}

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
					new Test { List = new List<int?> { 1, 2, 3 } }
				};
				csv.Configuration.HasHeaderRecord = false;
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "1,2,3\r\n", result );
			}
		}

		[TestMethod]
		public void ReadNullValuesNameTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Before,List,List,List,After" );
				writer.WriteLine( "1,null,NULL,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual( 3, list.Count );
				Assert.AreEqual( null, list[0] );
				Assert.AreEqual( null, list[1] );
				Assert.AreEqual( 4, list[2] );
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
				writer.WriteLine( "1,null,NULL,4,5" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual( 3, list.Count );
				Assert.AreEqual( null, list[0] );
				Assert.AreEqual( null, list[1] );
				Assert.AreEqual( 4, list[2] );
			}
		}

		private class Test
		{
			public List<int?> List { get; set; }
		}

		private sealed class TestIndexMap : CsvClassMap<Test>
		{
			public TestIndexMap()
			{
				Map( m => m.List ).Index( 1, 3 );
			}
		}

		private sealed class TestNamedMap : CsvClassMap<Test>
		{
			public TestNamedMap()
			{
				Map( m => m.List ).Name( "List" );
			}
		}

		private sealed class TestDefaultMap : CsvClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map( m => m.List );
			}
		}
	}
}
