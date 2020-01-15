// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
				var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

				Assert.AreSame( csvReader.Configuration, csvReader.Parser.Configuration );

				var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
				var parser = new CsvParser( reader, config );
				csvReader = new CsvReader( parser );

				Assert.AreSame( csvReader.Configuration, csvReader.Parser.Configuration );
			}
		}

		[TestMethod]
		public void AddingMappingsWithGenericMethod1Test()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].MemberMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithGenericMethod2Test()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].MemberMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithNonGenericMethodTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap( typeof( TestClassMappings ) );

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].MemberMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithInstanceMethodTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			config.RegisterClassMap( new TestClassMappings() );

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].MemberMaps.Count );
		}

		[TestMethod]
		public void RegisterClassMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap<TestClassMappings>();
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void RegisterClassMapNonGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void RegisterClassInstanceTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap( new TestClassMappings() );
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void UnregisterClassMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap<TestClassMappings>();
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );

			config.UnregisterClassMap<TestClassMappings>();
			Assert.IsNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void UnregisterClassNonMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsNull( config.Maps[typeof( TestClass )] );
			config.RegisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNotNull( config.Maps[typeof( TestClass )] );

			config.UnregisterClassMap( typeof( TestClassMappings ) );
			Assert.IsNull( config.Maps[typeof( TestClass )] );
		}

		[TestMethod]
		public void AddingMappingsWithNonGenericMethodThrowsWhenNotACsvClassMap()
		{
			try
			{
				new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture).RegisterClassMap( typeof( TestClass ) );
				Assert.Fail();
			}
			catch( ArgumentException ) {}
		}

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
		}

		private class TestClassMappings : ClassMap<TestClass>
		{
			public TestClassMappings()
			{
				Map( c => c.StringColumn );
				Map( c => c.IntColumn );
			}
		}
	}
}
