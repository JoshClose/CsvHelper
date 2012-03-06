#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.ComponentModel;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvClassMappingTests
	{
		[Fact]
		public void MapTest()
		{
			var map = new TestMappingDefaultClass();

			Assert.Equal( 3, map.Properties.Count );

			Assert.Equal( "GuidColumn", map.Properties[0].NameValue );
			Assert.Equal( -1, map.Properties[0].IndexValue );
			Assert.Equal( typeof( GuidConverter ), map.Properties[0].TypeConverterValue.GetType() );

			Assert.Equal( "IntColumn", map.Properties[1].NameValue );
			Assert.Equal( -1, map.Properties[1].IndexValue );
			Assert.Equal( typeof( Int32Converter ), map.Properties[1].TypeConverterValue.GetType() );

			Assert.Equal( "StringColumn", map.Properties[2].NameValue );
			Assert.Equal( -1, map.Properties[2].IndexValue );
			Assert.Equal( typeof( StringConverter ), map.Properties[2].TypeConverterValue.GetType() );
		}

		[Fact]
		public void MapNameTest()
		{
			var map = new TestMappingNameClass();

			Assert.Equal( 3, map.Properties.Count );

			Assert.Equal( "Guid Column", map.Properties[0].NameValue );
			Assert.Equal( "Int Column", map.Properties[1].NameValue );
			Assert.Equal( "String Column", map.Properties[2].NameValue );
		}

		[Fact]
		public void MapIndexTest()
		{
			var map = new TestMappingIndexClass();

			Assert.Equal( 3, map.Properties.Count );

			Assert.Equal( 3, map.Properties[0].IndexValue );
			Assert.Equal( 2, map.Properties[1].IndexValue );
			Assert.Equal( 1, map.Properties[2].IndexValue );
		}

		[Fact]
		public void MapIgnoreTest()
		{
			var map = new TestMappingIngoreClass();

			Assert.Equal( 3, map.Properties.Count );

			Assert.True( map.Properties[0].IgnoreValue );
			Assert.False( map.Properties[1].IgnoreValue );
			Assert.True( map.Properties[2].IgnoreValue );
		}

		[Fact]
		public void MapTypeConverterTest()
		{
			var map = new TestMappingTypeConverterClass();

			Assert.Equal( 3, map.Properties.Count );

			Assert.IsType<Int16Converter>( map.Properties[0].TypeConverterValue );
			Assert.IsType<StringConverter>( map.Properties[1].TypeConverterValue );
			Assert.IsType<Int64Converter>( map.Properties[2].TypeConverterValue );
		}

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
			public Guid GuidColumn { get; set; }
			public string NotUsedColumn { get; set; }
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
	}
}
