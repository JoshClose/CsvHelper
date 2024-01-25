// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvConfigurationTests
	{
		[Fact]
		public void EnsureReaderAndParserConfigIsAreSameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			{
				var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

				Assert.Same(csvReader.Configuration, csvReader.Parser.Configuration);

				var config = new CsvConfiguration(CultureInfo.InvariantCulture);
				var parser = new CsvParser(reader, config);
				csvReader = new CsvReader(parser);

				Assert.Same(csvReader.Configuration, csvReader.Parser.Configuration);
			}
		}

		[Fact]
		public void AddingMappingsWithGenericMethod1Test()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			context.RegisterClassMap<TestClassMappings>();

			Assert.Equal(2, context.Maps[typeof(TestClass)].MemberMaps.Count);
		}

		[Fact]
		public void AddingMappingsWithGenericMethod2Test()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			context.RegisterClassMap<TestClassMappings>();

			Assert.Equal(2, context.Maps[typeof(TestClass)].MemberMaps.Count);
		}

		[Fact]
		public void AddingMappingsWithNonGenericMethodTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			context.RegisterClassMap(typeof(TestClassMappings));

			Assert.Equal(2, context.Maps[typeof(TestClass)].MemberMaps.Count);
		}

		[Fact]
		public void AddingMappingsWithInstanceMethodTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			context.RegisterClassMap(new TestClassMappings());

			Assert.Equal(2, context.Maps[typeof(TestClass)].MemberMaps.Count);
		}

		[Fact]
		public void RegisterClassMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);

			Assert.Null(context.Maps[typeof(TestClass)]);
			context.RegisterClassMap<TestClassMappings>();
			Assert.NotNull(context.Maps[typeof(TestClass)]);
		}

		[Fact]
		public void RegisterClassMapNonGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);

			Assert.Null(context.Maps[typeof(TestClass)]);
			context.RegisterClassMap(typeof(TestClassMappings));
			Assert.NotNull(context.Maps[typeof(TestClass)]);
		}

		[Fact]
		public void RegisterClassInstanceTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);

			Assert.Null(context.Maps[typeof(TestClass)]);
			context.RegisterClassMap(new TestClassMappings());
			Assert.NotNull(context.Maps[typeof(TestClass)]);
		}

		[Fact]
		public void UnregisterClassMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);

			Assert.Null(context.Maps[typeof(TestClass)]);
			context.RegisterClassMap<TestClassMappings>();
			Assert.NotNull(context.Maps[typeof(TestClass)]);

			context.UnregisterClassMap<TestClassMappings>();
			Assert.Null(context.Maps[typeof(TestClass)]);
		}

		[Fact]
		public void UnregisterClassNonMapGenericTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);

			Assert.Null(context.Maps[typeof(TestClass)]);
			context.RegisterClassMap(typeof(TestClassMappings));
			Assert.NotNull(context.Maps[typeof(TestClass)]);

			context.UnregisterClassMap(typeof(TestClassMappings));
			Assert.Null(context.Maps[typeof(TestClass)]);
		}

		[Fact]
		public void AddingMappingsWithNonGenericMethodThrowsWhenNotACsvClassMap()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));

			Assert.Throws<ArgumentException>(() => context.RegisterClassMap(typeof(TestClass)));
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
				Map(c => c.StringColumn);
				Map(c => c.IntColumn);
			}
		}
	}
}
