﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderMappingTests
	{
		[TestMethod]
		public void ReadMultipleNamesTest()
		{
			var data = new List<string[]>
			{
				new[] { "int2", "string3" },
				new[] { "1", "one" },
				new[] { "2", "two" },
				null
			};

			var queue = new Queue<string[]>( data );
			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.RegisterClassMap<MultipleNamesClassMap>();

			var records = csvReader.GetRecords<MultipleNamesClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 2, records.Count );
			Assert.AreEqual( 1, records[0].IntColumn );
			Assert.AreEqual( "one", records[0].StringColumn );
			Assert.AreEqual( 2, records[1].IntColumn );
			Assert.AreEqual( "two", records[1].StringColumn );
		}

		[TestMethod]
		public void ConstructUsingTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1" } );
			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.RegisterClassMap<ConstructorMappingClassMap>();

			csvReader.Read();
			var record = csvReader.GetRecord<ConstructorMappingClass>();

			Assert.AreEqual( "one", record.StringColumn );
		}

		[TestMethod]
		public void ConvertUsingTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "2" } );
			queue.Enqueue( new[] { "3", "4" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.RegisterClassMap<ConvertUsingMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 2, records.Count );
			Assert.AreEqual( 3, records[0].IntColumn );
			Assert.AreEqual( 7, records[1].IntColumn );
		}

		[TestMethod]
		public void ConvertUsingCovarianceTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new string[] { "1", "2" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.RegisterClassMap<CovarianceClassMap>();

			var records = csvReader.GetRecords<CovarianceClass>().ToList();
		}

		[TestMethod]
		public void ConvertUsingContravarianceTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new string[] { "1", "2" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;

			try
			{
				csvReader.Configuration.RegisterClassMap<ContravarianceClassMap>();
				Assert.Fail();
			}
			catch( CsvConfigurationException )
			{
			}
		}

		[TestMethod]
		public void ConvertUsingBlockTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "2" } );
			queue.Enqueue( new[] { "3", "4" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.RegisterClassMap<ConvertUsingBlockMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 2, records.Count );
			Assert.AreEqual( 3, records[0].IntColumn );
			Assert.AreEqual( 7, records[1].IntColumn );
		}

		[TestMethod]
		public void ConvertUsingConstantTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "2" } );
			queue.Enqueue( new[] { "3", "4" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.RegisterClassMap<ConvertUsingConstantMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 2, records.Count );
			Assert.AreEqual( 1, records[0].IntColumn );
			Assert.AreEqual( 1, records[1].IntColumn );
		}

		[TestMethod]
		public void ReadSameNameMultipleTimesTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "ColumnName", "ColumnName", "ColumnName" } );
			queue.Enqueue( new[] { "2", "3", "1" } );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var csv = new CsvReader( parserMock );
			csv.Configuration.RegisterClassMap<SameNameMultipleTimesClassMap>();

			var records = csv.GetRecords<SameNameMultipleTimesClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 1, records.Count );
		}

		private class CovarianceClass
		{
			public int? Id { get; set; }
		}

		private sealed class CovarianceClassMap : CsvClassMap<CovarianceClass>
		{
			public CovarianceClassMap()
			{
				Map( m => m.Id ).ConvertUsing( row => row.GetField<int>( 0 ) );
			}
		}

		private class ContravarianceClass
		{
			public int Id { get; set; }
		}

		private sealed class ContravarianceClassMap : CsvClassMap<ContravarianceClass>
		{
			public ContravarianceClassMap()
			{
				Map( m => m.Id ).ConvertUsing( row => row.GetField<int?>( 0 ) );
			}
		}

		private class TestClass
		{
			public int IntColumn { get; set; }
		}

		private class SameNameMultipleTimesClass
		{
			public string Name1 { get; set; }

			public string Name2 { get; set; }

			public string Name3 { get; set; }
		}

		private sealed class SameNameMultipleTimesClassMap : CsvClassMap<SameNameMultipleTimesClass>
		{
			public SameNameMultipleTimesClassMap()
			{
				Map( m => m.Name1 ).Name( "ColumnName" ).NameIndex( 1 );
				Map( m => m.Name2 ).Name( "ColumnName" ).NameIndex( 2 );
				Map( m => m.Name3 ).Name( "ColumnName" ).NameIndex( 0 );
			}
		}

		private class MultipleNamesClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class MultipleNamesClassMap : CsvClassMap<MultipleNamesClass>
		{
			public MultipleNamesClassMap()
			{
				Map( m => m.IntColumn ).Name( "int1", "int2", "int3" );
				Map( m => m.StringColumn ).Name( "string1", "string2", "string3" );
			}
		}

		private class ConstructorMappingClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public ConstructorMappingClass( string stringColumn )
			{
				StringColumn = stringColumn;
			}
		}

		private sealed class ConstructorMappingClassMap : CsvClassMap<ConstructorMappingClass>
		{
			public ConstructorMappingClassMap()
			{
				ConstructUsing( () => new ConstructorMappingClass( "one" ) );
				Map( m => m.IntColumn ).Index( 0 );
			}
		}

		private sealed class ConvertUsingMap : CsvClassMap<TestClass>
		{
			public ConvertUsingMap()
			{
				Map( m => m.IntColumn ).ConvertUsing( row => row.GetField<int>( 0 ) + row.GetField<int>( 1 ) );
			}
		}

		private sealed class ConvertUsingBlockMap : CsvClassMap<TestClass>
		{
			public ConvertUsingBlockMap()
			{
				Map( m => m.IntColumn ).ConvertUsing( row =>
				{
					var x = row.GetField<int>( 0 );
					var y = row.GetField<int>( 1 );
					return x + y;
				} );
			}
		}

		private sealed class ConvertUsingConstantMap : CsvClassMap<TestClass>
		{
			public ConvertUsingConstantMap()
			{
				Map( m => m.IntColumn ).ConvertUsing( row => 1 );
			}
		}
	}
}
