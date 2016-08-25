using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;
using System.Collections.Generic;
using System.IO;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class IEnumerableConverterTests
	{
		[TestMethod]
		public void ConvertNoIndexEndTest()
		{
			var rowMock = new Mock<ICsvReaderRow>();
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<int>() ) ).Returns<int>( index => currentRecord[index] );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "List" ) )
			{
				Index = 2
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IEnumerableConverter();
			var enumerable = (IEnumerable)converter.ConvertFromString( "1", rowMock.Object, data );
			var list = enumerable.Cast<string>().ToList();

			Assert.AreEqual( 3, list.Count );
			Assert.AreEqual( "1", list[0] );
			Assert.AreEqual( "2", list[1] );
			Assert.AreEqual( "3", list[2] );
		}

		[TestMethod]
		public void ConvertWithIndexEndTest()
		{
			var rowMock = new Mock<ICsvReaderRow>();
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<int>() ) ).Returns<int>( index => currentRecord[index] );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "List" ) )
			{
				Index = 2,
				IndexEnd = 3
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IEnumerableConverter();
			var enumerable = (IEnumerable)converter.ConvertFromString( "1", rowMock.Object, data );
			var list = enumerable.Cast<string>().ToList();

			Assert.AreEqual( 2, list.Count );
			Assert.AreEqual( "1", list[0] );
			Assert.AreEqual( "2", list[1] );
		}

		[TestMethod]
		public void FullReadTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "1,2,3" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.Cast<string>().ToList();

				Assert.AreEqual( 3, list.Count );
				Assert.AreEqual( "1", list[0] );
				Assert.AreEqual( "2", list[1] );
				Assert.AreEqual( "3", list[2] );
			}
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
					new Test { List = new List<int> { 1, 2, 3 } }
				};
				csv.Configuration.HasHeaderRecord = false;
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "1,2,3\r\n", result );
			}
		}

		private class Test
		{
			public IEnumerable List { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.List ).Index( 0 );
			}
		}
	}
}
