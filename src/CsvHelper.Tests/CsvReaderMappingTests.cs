// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
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
			csvReader.Configuration.ClassMapping<MultipleNamesClassMap>();

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
			csvReader.Configuration.ClassMapping<ConstructorMappingClassMap>();

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
			csvReader.Configuration.ClassMapping<ConvertUsingMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 2, records.Count );
			Assert.AreEqual( 3, records[0].IntColumn );
			Assert.AreEqual( 7, records[1].IntColumn );
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
			csvReader.Configuration.ClassMapping<ConvertUsingBlockMap>();

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
			csvReader.Configuration.ClassMapping<ConvertUsingConstantMap>();

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
			csv.Configuration.ClassMapping<SameNameMultipleTimesClassMap>();

			var records = csv.GetRecords<SameNameMultipleTimesClass>().ToList();

			Assert.IsNotNull( records );
			Assert.AreEqual( 1, records.Count );
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
			public override void CreateMap()
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
			public override void CreateMap()
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
			public override void CreateMap()
			{
				ConstructUsing( () => new ConstructorMappingClass( "one" ) );
				Map( m => m.IntColumn ).Index( 0 );
			}
		}

		private sealed class ConvertUsingMap : CsvClassMap<TestClass>
		{
			public override void CreateMap()
			{
				Map( m => m.IntColumn ).ConvertUsing( row => row.GetField<int>( 0 ) + row.GetField<int>( 1 ) );
			}
		}

		private sealed class ConvertUsingBlockMap : CsvClassMap<TestClass>
		{
			public override void CreateMap()
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
			public override void CreateMap()
			{
				Map( m => m.IntColumn ).ConvertUsing( row => 1 );
			}
		}
	}
}
