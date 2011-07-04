using System;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvClassMappingTests
	{
		[TestMethod]
		public void Test()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestMappingDefaultClass, TestClass>();

			var map = new TestMappingDefaultClass();
		}

		[TestMethod]
		public void MapTest()
		{
			var map = new TestMappingDefaultClass();

			Assert.AreEqual( 3, map.Properties.Count );

			Assert.AreEqual( "GuidColumn", map.Properties[0].NameValue );
			Assert.AreEqual( -1, map.Properties[0].IndexValue );
			Assert.AreEqual( typeof( GuidConverter ), map.Properties[0].TypeConverterValue.GetType() );

			Assert.AreEqual( "IntColumn", map.Properties[1].NameValue );
			Assert.AreEqual( -1, map.Properties[1].IndexValue );
			Assert.AreEqual( typeof( Int32Converter ), map.Properties[1].TypeConverterValue.GetType() );

			Assert.AreEqual( "StringColumn", map.Properties[2].NameValue );
			Assert.AreEqual( -1, map.Properties[2].IndexValue );
			Assert.AreEqual( typeof( StringConverter ), map.Properties[2].TypeConverterValue.GetType() );
		}

		[TestMethod]
		public void MapNameTest()
		{
			var map = new TestMappingNameClass();

			Assert.AreEqual( 3, map.Properties.Count );

			Assert.AreEqual( "Guid Column", map.Properties[0].NameValue );
			Assert.AreEqual( "Int Column", map.Properties[1].NameValue );
			Assert.AreEqual( "String Column", map.Properties[2].NameValue );
		}

		[TestMethod]
		public void MapIndexTest()
		{
			var map = new TestMappingIndexClass();

			Assert.AreEqual( 3, map.Properties.Count );

			Assert.AreEqual( 3, map.Properties[0].IndexValue );
			Assert.AreEqual( 2, map.Properties[1].IndexValue );
			Assert.AreEqual( 1, map.Properties[2].IndexValue );
		}

		public void MapIgnoreTest()
		{
			var map = new TestMappingIngoreClass();

			Assert.AreEqual( 3, map.Properties.Count );

			Assert.IsTrue( map.Properties[0].IgnoreValue );
			Assert.IsFalse( map.Properties[1].IgnoreValue );
			Assert.IsTrue( map.Properties[2].IgnoreValue );
		}

		[TestMethod]
		public void MapTypeConverterTest()
		{
			var map = new TestMappingTypeConverterClass();

			Assert.AreEqual( 3, map.Properties.Count );

			Assert.IsInstanceOfType( map.Properties[0].TypeConverterValue, typeof( Int16Converter ) );
			Assert.IsInstanceOfType( map.Properties[1].TypeConverterValue, typeof( StringConverter ) );
			Assert.IsInstanceOfType( map.Properties[2].TypeConverterValue, typeof( Int64Converter ) );
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
