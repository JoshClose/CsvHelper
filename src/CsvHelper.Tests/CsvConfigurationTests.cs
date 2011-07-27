#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvConfigurationTests
	{
		[TestMethod]
		public void AddingMappingsWithGenericMethod1()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings, TestClass>();

			Assert.AreEqual( 2, config.Properties.Count );
		}

		[TestMethod]
		public void AddingMappingsWithGenericMethod2()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings>();

			Assert.AreEqual( 2, config.Properties.Count );
		}

		[TestMethod]
		public void AddingMappingsFromClassMapInstance()
		{
			var config = new CsvConfiguration();
			config.ClassMapping( new TestClassMappings() );

			Assert.AreEqual( 2, config.Properties.Count );
		}

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
		}

		private class TestClassMappings : CsvClassMap<TestClass>
		{
			public TestClassMappings()
			{
				Map( c => c.StringColumn );
				Map( c => c.IntColumn );
			}
		}
	}
}
