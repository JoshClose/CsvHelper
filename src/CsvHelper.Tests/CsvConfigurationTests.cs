// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

using System;
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
		public void AddingMappingsWithGenericMethod1Test()
		{
			var config = new CsvConfiguration();
			config.RegisterClassMap<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].PropertyMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithGenericMethod2Test()
		{
			var config = new CsvConfiguration();
			config.RegisterClassMap<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].PropertyMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithNonGenericMethodTest()
		{
			var config = new CsvConfiguration();
			config.RegisterClassMap( typeof( TestClassMappings ) );

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].PropertyMaps.Count );
		}

        [TestMethod]
        public void AddingMappingsWithInstanceMethodTest()
        {
            var config = new CsvConfiguration();
            config.RegisterClassMap(new TestClassMappings());

            Assert.AreEqual(2, config.Maps[typeof(TestClass)].PropertyMaps.Count);
        }

		[TestMethod]
		public void RegisterClassMapGenericTest()
		{
			var config = new CsvConfiguration();

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap<TestClassMappings>();
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void RegisterClassMapNonGenericTest()
		{
			var config = new CsvConfiguration();

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );
		}

        [TestMethod]
        public void RegisterClassInstanceTest()
        {
            var config = new CsvConfiguration();

            Assert.IsNull(config.Maps[typeof(TestClass)]);
            config.RegisterClassMap(new TestClassMappings());
            Assert.IsNotNull(config.Maps[typeof(TestClass)]);
        }

		[TestMethod]
		public void UnregisterClassMapGenericTest()
		{
			var config = new CsvConfiguration();

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap<TestClassMappings>();
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );

			config.UnregisterClassMap<TestClassMappings>();
			Assert.IsNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void UnregisterClassNonMapGenericTest()
		{
			var config = new CsvConfiguration();

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );

			config.UnregisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNull( config.Maps[typeof( TestClass )] );
		}


#if !WINRT_4_5
		[TestMethod]
		public void AddingMappingsWithNonGenericMethodThrowsWhenNotACsvClassMap()
		{
			try
			{
				new CsvConfiguration().RegisterClassMap( typeof( TestClass ) );
				Assert.Fail();
			}
			catch( ArgumentException ) {}
		}
#endif

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
		}

		private class TestClassMappings : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( c => c.StringColumn );
				Map( c => c.IntColumn );
			}
		}
	}
}
