// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvClassMappingTests
	{
		[TestMethod]
		public void MapTest()
		{
			var map = new TestMappingDefaultClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.AreEqual( 0, map.MemberMaps[0].Data.Names.Count );
			Assert.AreEqual( 0, map.MemberMaps[0].Data.Index );
			Assert.IsNull( map.MemberMaps[0].Data.TypeConverter );

			Assert.AreEqual( 0, map.MemberMaps[1].Data.Names.Count );
			Assert.AreEqual( 1, map.MemberMaps[1].Data.Index );
			Assert.IsNull( map.MemberMaps[1].Data.TypeConverter );

			Assert.AreEqual( 0, map.MemberMaps[2].Data.Names.Count );
			Assert.AreEqual( 2, map.MemberMaps[2].Data.Index );
			Assert.IsNull( map.MemberMaps[2].Data.TypeConverter );
		}

		[TestMethod]
		public void MapNameTest()
		{
			var map = new TestMappingNameClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.AreEqual( "Guid Column", map.MemberMaps[0].Data.Names.FirstOrDefault() );
			Assert.AreEqual( "Int Column", map.MemberMaps[1].Data.Names.FirstOrDefault() );
			Assert.AreEqual( "String Column", map.MemberMaps[2].Data.Names.FirstOrDefault() );
		}

		[TestMethod]
		public void MapIndexTest()
		{
			var map = new TestMappingIndexClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.AreEqual( 2, map.MemberMaps[0].Data.Index );
			Assert.AreEqual( 3, map.MemberMaps[1].Data.Index );
			Assert.AreEqual( 1, map.MemberMaps[2].Data.Index );
		}

		[TestMethod]
		public void MapIgnoreTest()
		{
			var map = new TestMappingIgnoreClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.IsTrue( map.MemberMaps[0].Data.Ignore );
			Assert.IsFalse( map.MemberMaps[1].Data.Ignore );
			Assert.IsTrue( map.MemberMaps[2].Data.Ignore );
		}

		[TestMethod]
		public void MapTypeConverterTest()
		{
			var map = new TestMappingTypeConverterClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.IsInstanceOfType( map.MemberMaps[0].Data.TypeConverter, typeof( Int16Converter ) );
			Assert.IsInstanceOfType( map.MemberMaps[1].Data.TypeConverter, typeof( StringConverter ) );
			Assert.IsInstanceOfType( map.MemberMaps[2].Data.TypeConverter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void MapMultipleNamesTest()
		{
			var map = new TestMappingMultipleNamesClass();
			//map.CreateMap();

			Assert.AreEqual( 3, map.MemberMaps.Count );

			Assert.AreEqual( 3, map.MemberMaps[0].Data.Names.Count );
			Assert.AreEqual( 3, map.MemberMaps[1].Data.Names.Count );
			Assert.AreEqual( 3, map.MemberMaps[2].Data.Names.Count );

			Assert.AreEqual("guid1", map.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("guid2", map.MemberMaps[0].Data.Names[1]);
			Assert.AreEqual("guid3", map.MemberMaps[0].Data.Names[2]);

			Assert.AreEqual("int1", map.MemberMaps[1].Data.Names[0]);
			Assert.AreEqual("int2", map.MemberMaps[1].Data.Names[1]);
			Assert.AreEqual("int3", map.MemberMaps[1].Data.Names[2]);

			Assert.AreEqual("string1", map.MemberMaps[2].Data.Names[0]);
			Assert.AreEqual("string2", map.MemberMaps[2].Data.Names[1]);
			Assert.AreEqual("string3", map.MemberMaps[2].Data.Names[2]);
		}

		[TestMethod]
		public void MapMultipleTypesTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap<AMap>();
			config.RegisterClassMap<BMap>();

			Assert.IsNotNull( config.Maps[typeof( A )] );
			Assert.IsNotNull( config.Maps[typeof( B )] );
		}

		[TestMethod]
		public void PropertyMapAccessTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap<AMap>();
			config.Maps.Find<A>().Map( m => m.AId ).Ignore();

			Assert.AreEqual( true, config.Maps[typeof( A )].MemberMaps[0].Data.Ignore );
		}

		private class A
		{
			public int AId { get; set; }
		}

		private sealed class AMap : ClassMap<A>
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

		private sealed class BMap : ClassMap<B>
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

		private sealed class TestMappingDefaultClass : ClassMap<TestClass>
		{
			public TestMappingDefaultClass()
			{
				Map( m => m.GuidColumn );
				Map( m => m.IntColumn );
				Map( m => m.StringColumn );
			}
		}

		private sealed class TestMappingNameClass : ClassMap<TestClass>
		{
			public TestMappingNameClass()
			{
				Map( m => m.GuidColumn ).Name( "Guid Column" );
				Map( m => m.IntColumn ).Name( "Int Column" );
				Map( m => m.StringColumn ).Name( "String Column" );
			}
		}

		private sealed class TestMappingIndexClass : ClassMap<TestClass>
		{
			public TestMappingIndexClass()
			{
				Map( m => m.GuidColumn ).Index( 3 );
				Map( m => m.IntColumn ).Index( 2 );
				Map( m => m.StringColumn ).Index( 1 );
			}
		}

		private sealed class TestMappingIgnoreClass : ClassMap<TestClass>
		{
			public TestMappingIgnoreClass()
			{
				Map( m => m.GuidColumn ).Ignore();
				Map( m => m.IntColumn );
				Map( m => m.StringColumn ).Ignore();
			}
		}

		private sealed class TestMappingTypeConverterClass : ClassMap<TestClass>
		{
			public TestMappingTypeConverterClass()
			{
				Map( m => m.GuidColumn ).TypeConverter<Int16Converter>();
				Map( m => m.IntColumn ).TypeConverter<StringConverter>();
				Map( m => m.StringColumn ).TypeConverter( new Int64Converter() );
			}
		}

		private sealed class TestMappingMultipleNamesClass : ClassMap<TestClass>
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
