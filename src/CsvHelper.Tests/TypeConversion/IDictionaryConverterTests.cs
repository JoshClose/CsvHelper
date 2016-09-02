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
		public void ConvertNoIndexEndTest()
		{
			var rowMock = new Mock<ICsvReaderRow>();
			var headers = new[] { "Id", "Name", "Prop1", "Prop2", "Prop3" };
			var currentRecord = new[] { "1", "One", "1", "2", "3" };
			rowMock.Setup( m => m.FieldHeaders ).Returns( headers );
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<int>() ) ).Returns<int>( index => currentRecord[index] );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "Dictionary" ) )
			{
				Index = 2
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IDictionaryConverter();
			var dictionary = (IDictionary)converter.ConvertFromString( "1", rowMock.Object, data );

			Assert.AreEqual( 3, dictionary.Count );
			Assert.AreEqual( "1", dictionary["Prop1"] );
			Assert.AreEqual( "2", dictionary["Prop2"] );
			Assert.AreEqual( "3", dictionary["Prop3"] );
		}

		[TestMethod]
		public void ConvertWithIndexEndTest()
		{
			var rowMock = new Mock<ICsvReaderRow>();
			var headers = new[] { "Id", "Name", "Prop1", "Prop2", "Prop3" };
			var currentRecord = new[] { "1", "One", "1", "2", "3" };
			rowMock.Setup( m => m.FieldHeaders ).Returns( headers );
			rowMock.Setup( m => m.CurrentRecord ).Returns( currentRecord );
			rowMock.Setup( m => m.GetField( It.IsAny<int>() ) ).Returns<int>( index => currentRecord[index] );
			var data = new CsvPropertyMapData( typeof( Test ).GetProperty( "Dictionary" ) )
			{
				Index = 2,
				IndexEnd = 3
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IDictionaryConverter();
			var dictionary = (IDictionary)converter.ConvertFromString( "1", rowMock.Object, data );

			Assert.AreEqual( 2, dictionary.Count );
			Assert.AreEqual( "1", dictionary["Prop1"] );
			Assert.AreEqual( "2", dictionary["Prop2"] );
		}

		[TestMethod]
		public void FullReadTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Prop1,Prop2,Prop3" );
				writer.WriteLine( "1,2,3" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				var dict = records[0].Dictionary;

				Assert.AreEqual( 3, dict.Count );
				Assert.AreEqual( "1", dict["Prop1"] );
				Assert.AreEqual( "2", dict["Prop2"] );
				Assert.AreEqual( "3", dict["Prop3"] );
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
					new Test { Dictionary = new Dictionary<string, int> { { "Prop1", 1 }, { "Prop2", 2 }, { "Prop3", 3 } } }
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
			public IDictionary Dictionary { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Dictionary ).Index( 0 );
			}
		}
	}
}
