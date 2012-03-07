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
	public class CsvFieldAttributeMappingTests
	{
		[Fact]
		public void FieldAttributeNameTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.Equal( 4, config.Properties.Count );

			Assert.Equal( "Guid Column", config.Properties[0].NameValue );
			Assert.Equal( "Int Column", config.Properties[1].NameValue );
			Assert.Equal( "String Column", config.Properties[2].NameValue );
			Assert.Equal( "NotUsedColumn", config.Properties[3].NameValue );
		}

		[Fact]
		public void FieldAttributeIndexTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.Equal( 4, config.Properties.Count );

			Assert.Equal( 1, config.Properties[0].IndexValue );
			Assert.Equal( 2, config.Properties[1].IndexValue );
			Assert.Equal( 3, config.Properties[2].IndexValue );
			Assert.Equal( -1, config.Properties[3].IndexValue );
		}

		[Fact]
		public void FieldAttributeTypeConverterTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.Equal( 4, config.Properties.Count );

			Assert.IsType<StringConverter>( config.Properties[0].TypeConverterValue );
			Assert.IsType<Int32Converter>( config.Properties[1].TypeConverterValue );
			Assert.IsType<Int16Converter>( config.Properties[2].TypeConverterValue );
			Assert.IsType<StringConverter>( config.Properties[3].TypeConverterValue );
		}

		[Fact]
		public void FieldAttributeIgnoreTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.Equal( 4, config.Properties.Count );

			Assert.False( config.Properties[0].IgnoreValue );
			Assert.True( config.Properties[1].IgnoreValue );
			Assert.False( config.Properties[2].IgnoreValue );
			Assert.False( config.Properties[3].IgnoreValue );
		}

		private class TestClass
		{
			[TypeConverter( typeof( Int16Converter ) )]
			[CsvField( Index = 3, Name = "String Column" )]
			public string StringColumn { get; set; }

			[CsvField( Index = 2, Name = "Int Column", Ignore = true )]
			public int IntColumn { get; set; }

			[TypeConverter( typeof( StringConverter ) )]
			[CsvField( Index = 1, Name = "Guid Column" )]
			public Guid GuidColumn { get; set; }

			public string NotUsedColumn { get; set; }
		}
	}
}
