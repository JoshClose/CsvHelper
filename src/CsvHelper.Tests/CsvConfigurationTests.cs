// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.IO;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvConfigurationTests
	{
		[TestMethod]
		public void EnsureReaderAndParserConfigIsAreSameTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			{
				var csvReader = new CsvReader( reader );

				Assert.AreSame( csvReader.Configuration, csvReader.Parser.Configuration );

				var config = new CsvConfiguration();
				var parser = new CsvParser( reader, config );
				csvReader = new CsvReader( parser );

				Assert.AreSame( csvReader.Configuration, csvReader.Parser.Configuration );
			}
		}

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

		public class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
		}

		public class TestClassMappings : CsvClassMap<TestClass>
		{
			public TestClassMappings()
			{
				Map( c => c.StringColumn );
				Map( c => c.IntColumn );
			}
		}
	}
}
