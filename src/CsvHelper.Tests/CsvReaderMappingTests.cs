// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderMappingTests
	{
		[Fact]
		public void ReadMultipleNamesTest()
		{
			var data = new List<string[]>
			{
				new[] { "int2", "string3" },
				new[] { "1", "one" },
				new[] { "2", "two" },
				null
			};

			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var index = -1;
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				index++;
				return data[index];
			} );

			var csvReader = new CsvReader( parserMock.Object );
			csvReader.Configuration.AttributeMapping<MultipleNamesAttributeClass>();

			var records = csvReader.GetRecords<MultipleNamesAttributeClass>().ToList();

			Assert.NotNull( records );
			Assert.Equal( 2, records.Count );
			Assert.Equal( 1, records[0].IntColumn );
			Assert.Equal( "one", records[0].StringColumn );
			Assert.Equal( 2, records[1].IntColumn );
			Assert.Equal( "two", records[1].StringColumn );
		}

		[Fact]
		public void ConstructUsingTest()
		{
			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( new[] { "1" } );

			var csvReader = new CsvReader( parserMock.Object );
			csvReader.Configuration.ClassMapping<ConstructorMappingClassMap>();

			csvReader.Read();
			var record = csvReader.GetRecord<ConstructorMappingClass>();

			Assert.Equal( "one", record.StringColumn );
		}

		[Fact]
		public void ConvertUsingTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "2" } );
			queue.Enqueue( new[] { "3", "4" } );
			queue.Enqueue( null );

			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( queue.Dequeue );

			var csvReader = new CsvReader( parserMock.Object );
			csvReader.Configuration.HasHeaderRecord = false;
			csvReader.Configuration.ClassMapping<ConvertUsingMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull( records );
			Assert.Equal( 2, records.Count );
			Assert.Equal( 3, records[0].IntColumn );
			Assert.Equal( 7, records[1].IntColumn );
		}

		private class TestClass
		{
			public int IntColumn { get; set; }
		}

		private class MultipleNamesAttributeClass
		{
			[CsvField( Names = new[] { "int1", "int2", "int3" } )]
			public int IntColumn { get; set; }

			[CsvField( Names = new[] { "string1", "string2", "string3" } )]
			public string StringColumn { get; set; }
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
	}
}
