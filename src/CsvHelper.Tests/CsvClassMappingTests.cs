// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvClassMappingTests
	{
		[TestMethod]
		public void MapTest()
		{
			var map = new TestMappingDefaultClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( "GuidColumn", map.PropertyMaps[0].NameValue );
			Assert.AreEqual( -1, map.PropertyMaps[0].IndexValue );
			Assert.AreEqual( typeof( GuidConverter ), map.PropertyMaps[0].TypeConverterValue.GetType() );

			Assert.AreEqual( "IntColumn", map.PropertyMaps[1].NameValue );
			Assert.AreEqual( -1, map.PropertyMaps[1].IndexValue );
			Assert.AreEqual( typeof( Int32Converter ), map.PropertyMaps[1].TypeConverterValue.GetType() );

			Assert.AreEqual( "StringColumn", map.PropertyMaps[2].NameValue );
			Assert.AreEqual( -1, map.PropertyMaps[2].IndexValue );
			Assert.AreEqual( typeof( StringConverter ), map.PropertyMaps[2].TypeConverterValue.GetType() );
		}

		[TestMethod]
		public void MapNameTest()
		{
			var map = new TestMappingNameClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( "Guid Column", map.PropertyMaps[0].NameValue );
			Assert.AreEqual( "Int Column", map.PropertyMaps[1].NameValue );
			Assert.AreEqual( "String Column", map.PropertyMaps[2].NameValue );
		}

		[TestMethod]
		public void MapIndexTest()
		{
			var map = new TestMappingIndexClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( 2, map.PropertyMaps[0].IndexValue );
			Assert.AreEqual( 3, map.PropertyMaps[1].IndexValue );
			Assert.AreEqual( 1, map.PropertyMaps[2].IndexValue );
		}

		[TestMethod]
		public void MapIgnoreTest()
		{
			var map = new TestMappingIngoreClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.IsTrue( map.PropertyMaps[0].IgnoreValue );
			Assert.IsFalse( map.PropertyMaps[1].IgnoreValue );
			Assert.IsTrue( map.PropertyMaps[2].IgnoreValue );
		}

		[TestMethod]
		public void MapTypeConverterTest()
		{
			var map = new TestMappingTypeConverterClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.IsInstanceOfType( map.PropertyMaps[0].TypeConverterValue, typeof( Int16Converter ) );
			Assert.IsInstanceOfType( map.PropertyMaps[1].TypeConverterValue, typeof( StringConverter ) );
			Assert.IsInstanceOfType( map.PropertyMaps[2].TypeConverterValue, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void MapMultipleNamesTest()
		{
			var map = new TestMappingMultipleNamesClass();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( 3, map.PropertyMaps[0].NamesValue.Length );
			Assert.AreEqual( 3, map.PropertyMaps[1].NamesValue.Length );
			Assert.AreEqual( 3, map.PropertyMaps[2].NamesValue.Length );

			Assert.AreEqual("guid1", map.PropertyMaps[0].NamesValue[0]);
			Assert.AreEqual("guid2", map.PropertyMaps[0].NamesValue[1]);
			Assert.AreEqual("guid3", map.PropertyMaps[0].NamesValue[2]);

			Assert.AreEqual("int1", map.PropertyMaps[1].NamesValue[0]);
			Assert.AreEqual("int2", map.PropertyMaps[1].NamesValue[1]);
			Assert.AreEqual("int3", map.PropertyMaps[1].NamesValue[2]);

			Assert.AreEqual("string1", map.PropertyMaps[2].NamesValue[0]);
			Assert.AreEqual("string2", map.PropertyMaps[2].NamesValue[1]);
			Assert.AreEqual("string3", map.PropertyMaps[2].NamesValue[2]);
		}

		[TestMethod]
		public void MapConstructorTest()
		{
			var map = new TestMappingConstructorClass();

			Assert.IsNotNull( map.Constructor );
		}

		[TestMethod]
		public void MapMultipleTypesTest()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<AMap>();
			config.ClassMapping<BMap>();

			Assert.IsNotNull( config.Maps[typeof( A )] );
			Assert.IsNotNull( config.Maps[typeof( B )] );
		}

		private class A
		{
			public int AId { get; set; }
		}

		private sealed class AMap : CsvClassMap<A>
		{
			public AMap()
			{
				Map( m => m.AId );
			}
		}

		private class B
		{
			public int BId { get; set; }
		}

		private sealed class BMap : CsvClassMap<B>
		{
			public BMap()
			{
				Map( m => m.BId );
			}
		}

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
			public Guid GuidColumn { get; set; }
			public string NotUsedColumn { get; set; }

			public TestClass(){}

			public TestClass( string stringColumn )
			{
				StringColumn = stringColumn;
			}
		}
		
		private sealed class TestMappingConstructorClass : CsvClassMap<TestClass>
		{
			public TestMappingConstructorClass()
			{
				ConstructUsing( () => new TestClass( "String Column" ) );
			}
		}

		private sealed class TestMappingDefaultClass : CsvClassMap<TestClass>
		{
			public TestMappingDefaultClass()
			{
				Map( m => m.GuidColumn );
				Map( m => m.IntColumn );
				Map( m => m.StringColumn );
			}
		}

		private sealed class TestMappingNameClass : CsvClassMap<TestClass>
		{
			public TestMappingNameClass()
			{
				Map( m => m.GuidColumn ).Name( "Guid Column" );
				Map( m => m.IntColumn ).Name( "Int Column" );
				Map( m => m.StringColumn ).Name( "String Column" );
			}
		}

		private sealed class TestMappingIndexClass : CsvClassMap<TestClass>
		{
			public TestMappingIndexClass()
			{
				Map( m => m.GuidColumn ).Index( 3 );
				Map( m => m.IntColumn ).Index( 2 );
				Map( m => m.StringColumn ).Index( 1 );
			}
		}

		private sealed class TestMappingIngoreClass : CsvClassMap<TestClass>
		{
			public TestMappingIngoreClass()
			{
				Map( m => m.GuidColumn ).Ignore();
				Map( m => m.IntColumn );
				Map( m => m.StringColumn ).Ignore();
			}
		}

		private sealed class TestMappingTypeConverterClass : CsvClassMap<TestClass>
		{
			public TestMappingTypeConverterClass()
			{
				Map( m => m.GuidColumn ).TypeConverter<Int16Converter>();
				Map( m => m.IntColumn ).TypeConverter<StringConverter>();
				Map( m => m.StringColumn ).TypeConverter( new Int64Converter() );
			}
		}

		private sealed class TestMappingMultipleNamesClass : CsvClassMap<TestClass>
		{
			public TestMappingMultipleNamesClass()
			{
				Map( m => m.GuidColumn ).Name( "guid1", "guid2", "guid3" );
				Map( m => m.IntColumn ).Name( "int1", "int2", "int3" );
				Map( m => m.StringColumn ).Name( "string1", "string2", "string3" );
			}
		}
	}
}
