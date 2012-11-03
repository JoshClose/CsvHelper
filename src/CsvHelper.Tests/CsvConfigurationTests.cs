// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvConfigurationTests
	{
		[Fact]
		public void EnsureReaderAndParserConfigIsSameTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			{
				var csvReader = new CsvReader( reader );

				Assert.Same( csvReader.Configuration, csvReader.Parser.Configuration );

				var config = new CsvConfiguration();
				var parser = new CsvParser( reader, config );
				csvReader = new CsvReader( parser );

				Assert.Same( csvReader.Configuration, csvReader.Parser.Configuration );
			}
		}

		[Fact]
		public void AddingMappingsWithGenericMethod1()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings, TestClass>();

			Assert.Equal( 2, config.Properties.Count );
		}

		[Fact]
		public void AddingMappingsWithGenericMethod2()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings>();

			Assert.Equal( 2, config.Properties.Count );
		}

		[Fact]
		public void AddingMappingsFromClassMapInstance()
		{
			var config = new CsvConfiguration();
			config.ClassMapping( new TestClassMappings() );

			Assert.Equal( 2, config.Properties.Count );
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
