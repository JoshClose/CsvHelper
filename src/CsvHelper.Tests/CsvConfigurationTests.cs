﻿// Copyright 2009-2013 Josh Close
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
			config.ClassMapping<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].PropertyMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithGenericMethod2()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings>();

			Assert.AreEqual( 2, config.Maps[typeof( TestClass )].PropertyMaps.Count );
		}

		[TestMethod]
		public void AddingMappingsWithNonGenericMethod()
		{
			var config = new CsvConfiguration();
			config.ClassMapping( typeof( TestClassMappings ) );

			Assert.AreEqual(2, config.Maps[typeof(TestClass)].PropertyMaps.Count);
		}

#if !WINRT_4_5
		[TestMethod]
		[ExpectedException(typeof(CsvConfigurationException))]
		public void AddingMappingsWithNonGenericMethodThrowsWhenNotACsvClassMap()
		{
			new CsvConfiguration().ClassMapping(typeof(TestClass));
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
