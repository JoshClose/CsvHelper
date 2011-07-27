#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	/// <summary>
	/// Summary description for CsvFieldAttributeMappingTests
	/// </summary>
	[TestClass]
	public class CsvFieldAttributeMappingTests
	{
		[TestMethod]
		public void FieldAttributeNameTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.AreEqual( 4, config.Properties.Count );

			Assert.AreEqual( "Guid Column", config.Properties[0].NameValue );
			Assert.AreEqual( "Int Column", config.Properties[1].NameValue );
			Assert.AreEqual( "String Column", config.Properties[2].NameValue );
			Assert.AreEqual( "NotUsedColumn", config.Properties[3].NameValue );
		}

		[TestMethod]
		public void FieldAttributeIndexTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.AreEqual( 4, config.Properties.Count );

			Assert.AreEqual( 1, config.Properties[0].IndexValue );
			Assert.AreEqual( 2, config.Properties[1].IndexValue );
			Assert.AreEqual( 3, config.Properties[2].IndexValue );
			Assert.AreEqual( -1, config.Properties[3].IndexValue );
		}

		[TestMethod]
		public void FieldAttributeTypeConverterTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.AreEqual( 4, config.Properties.Count );

			Assert.IsInstanceOfType( config.Properties[0].TypeConverterValue, typeof( StringConverter ) );
			Assert.IsInstanceOfType( config.Properties[1].TypeConverterValue, typeof( Int32Converter ) );
			Assert.IsInstanceOfType( config.Properties[2].TypeConverterValue, typeof( Int16Converter ) );
			Assert.IsInstanceOfType( config.Properties[3].TypeConverterValue, typeof( StringConverter ) );
		}

		[TestMethod]
		public void FieldAttributeIgnoreTest()
		{
			var config = new CsvConfiguration();
			config.AttributeMapping<TestClass>();

			Assert.AreEqual( 4, config.Properties.Count );

			Assert.IsFalse( config.Properties[0].IgnoreValue );
			Assert.IsTrue( config.Properties[1].IgnoreValue );
			Assert.IsFalse( config.Properties[2].IgnoreValue );
			Assert.IsFalse( config.Properties[3].IgnoreValue );
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
