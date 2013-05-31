// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Linq;
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
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( "GuidColumn", map.PropertyMaps[0].Data.Names.FirstOrDefault() );
			Assert.AreEqual( 0, map.PropertyMaps[0].Data.Index );
			Assert.AreEqual( typeof( GuidConverter ), map.PropertyMaps[0].Data.TypeConverter.GetType() );

			Assert.AreEqual( "IntColumn", map.PropertyMaps[1].Data.Names.FirstOrDefault() );
			Assert.AreEqual( 1, map.PropertyMaps[1].Data.Index );
			Assert.AreEqual( typeof( Int32Converter ), map.PropertyMaps[1].Data.TypeConverter.GetType() );

			Assert.AreEqual( "StringColumn", map.PropertyMaps[2].Data.Names.FirstOrDefault() );
			Assert.AreEqual( 2, map.PropertyMaps[2].Data.Index );
			Assert.AreEqual( typeof( StringConverter ), map.PropertyMaps[2].Data.TypeConverter.GetType() );
		}

		[TestMethod]
		public void MapNameTest()
		{
			var map = new TestMappingNameClass();
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( "Guid Column", map.PropertyMaps[0].Data.Names.FirstOrDefault() );
			Assert.AreEqual( "Int Column", map.PropertyMaps[1].Data.Names.FirstOrDefault() );
			Assert.AreEqual( "String Column", map.PropertyMaps[2].Data.Names.FirstOrDefault() );
		}

		[TestMethod]
		public void MapIndexTest()
		{
			var map = new TestMappingIndexClass();
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( 2, map.PropertyMaps[0].Data.Index );
			Assert.AreEqual( 3, map.PropertyMaps[1].Data.Index );
			Assert.AreEqual( 1, map.PropertyMaps[2].Data.Index );
		}

		[TestMethod]
		public void MapIgnoreTest()
		{
			var map = new TestMappingIngoreClass();
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.IsTrue( map.PropertyMaps[0].Data.Ignore );
			Assert.IsFalse( map.PropertyMaps[1].Data.Ignore );
			Assert.IsTrue( map.PropertyMaps[2].Data.Ignore );
		}

		[TestMethod]
		public void MapTypeConverterTest()
		{
			var map = new TestMappingTypeConverterClass();
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.IsInstanceOfType( map.PropertyMaps[0].Data.TypeConverter, typeof( Int16Converter ) );
			Assert.IsInstanceOfType( map.PropertyMaps[1].Data.TypeConverter, typeof( StringConverter ) );
			Assert.IsInstanceOfType( map.PropertyMaps[2].Data.TypeConverter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void MapMultipleNamesTest()
		{
			var map = new TestMappingMultipleNamesClass();
			map.CreateMap();

			Assert.AreEqual( 3, map.PropertyMaps.Count );

			Assert.AreEqual( 3, map.PropertyMaps[0].Data.Names.Count );
			Assert.AreEqual( 3, map.PropertyMaps[1].Data.Names.Count );
			Assert.AreEqual( 3, map.PropertyMaps[2].Data.Names.Count );

			Assert.AreEqual("guid1", map.PropertyMaps[0].Data.Names[0]);
			Assert.AreEqual("guid2", map.PropertyMaps[0].Data.Names[1]);
			Assert.AreEqual("guid3", map.PropertyMaps[0].Data.Names[2]);

			Assert.AreEqual("int1", map.PropertyMaps[1].Data.Names[0]);
			Assert.AreEqual("int2", map.PropertyMaps[1].Data.Names[1]);
			Assert.AreEqual("int3", map.PropertyMaps[1].Data.Names[2]);

			Assert.AreEqual("string1", map.PropertyMaps[2].Data.Names[0]);
			Assert.AreEqual("string2", map.PropertyMaps[2].Data.Names[1]);
			Assert.AreEqual("string3", map.PropertyMaps[2].Data.Names[2]);
		}

		[TestMethod]
		public void MapConstructorTest()
		{
			var map = new TestMappingConstructorClass();
			map.CreateMap();

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
			public override void CreateMap()
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
			public override void CreateMap()
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
			public override void CreateMap()
			{
				ConstructUsing( () => new TestClass( "String Column" ) );
			}
		}

		private sealed class TestMappingDefaultClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn );
				Map( m => m.IntColumn );
				Map( m => m.StringColumn );
			}
		}

		private sealed class TestMappingNameClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn ).Name( "Guid Column" );
				Map( m => m.IntColumn ).Name( "Int Column" );
				Map( m => m.StringColumn ).Name( "String Column" );
			}
		}

		private sealed class TestMappingIndexClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn ).Index( 3 );
				Map( m => m.IntColumn ).Index( 2 );
				Map( m => m.StringColumn ).Index( 1 );
			}
		}

		private sealed class TestMappingIngoreClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn ).Ignore();
				Map( m => m.IntColumn );
				Map( m => m.StringColumn ).Ignore();
			}
		}

		private sealed class TestMappingTypeConverterClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn ).TypeConverter<Int16Converter>();
				Map( m => m.IntColumn ).TypeConverter<StringConverter>();
				Map( m => m.StringColumn ).TypeConverter( new Int64Converter() );
			}
		}

		private sealed class TestMappingMultipleNamesClass : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.GuidColumn ).Name( "guid1", "guid2", "guid3" );
				Map( m => m.IntColumn ).Name( "int1", "int2", "int3" );
				Map( m => m.StringColumn ).Name( "string1", "string2", "string3" );
			}
		}
	}
}
