#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderTests
	{
		[Fact]
		public void HasHeaderRecordNotReadExceptionTest()
		{
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var reader = new CsvReader( parserMock.Object );

			Assert.Throws<CsvReaderException>( () => reader.GetField<int>(0) );
		}

		[Fact]
		public void HasHeaderRecordTest()
		{
			var isHeaderRecord = true;
			var data1 = new [] { "One", "Two" };
			var data2 = new [] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			// Check to see if the header record and first record are set properly.
			Assert.Equal( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.Equal( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
			Assert.Equal( Convert.ToInt32( data2[0] ), reader.GetField<int>( 0 ) );
			Assert.Equal( Convert.ToInt32( data2[1] ), reader.GetField<int>( 1 ) );
		}

		[Fact]
		public void GetTypeTest()
		{
			var data = new []
            {
                "1",
                "blah",
                DateTime.Now.ToString(),
                "true",
                "c",
                "",
                Guid.NewGuid().ToString(),
            };

			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			Assert.Equal( Convert.ToInt16( data[0] ), reader.GetField<short>( 0 ) );
			Assert.Equal( Convert.ToInt16( data[0] ), reader.GetField<short?>( 0 ) );
			Assert.Equal( null, reader.GetField<short?>( 5 ) );
			Assert.Equal( Convert.ToInt32( data[0] ), reader.GetField<int>( 0 ) );
			Assert.Equal( Convert.ToInt32( data[0] ), reader.GetField<int?>( 0 ) );
			Assert.Equal( null, reader.GetField<int?>( 5 ) );
			Assert.Equal( Convert.ToInt64( data[0] ), reader.GetField<long>( 0 ) );
			Assert.Equal( Convert.ToInt64( data[0] ), reader.GetField<long?>( 0 ) );
			Assert.Equal( null, reader.GetField<long?>( 5 ) );
			Assert.Equal( Convert.ToDecimal( data[0] ), reader.GetField<decimal>( 0 ) );
			Assert.Equal( Convert.ToDecimal( data[0] ), reader.GetField<decimal?>( 0 ) );
			Assert.Equal( null, reader.GetField<decimal?>( 5 ) );
			Assert.Equal( Convert.ToSingle( data[0] ), reader.GetField<float>( 0 ) );
			Assert.Equal( Convert.ToSingle( data[0] ), reader.GetField<float?>( 0 ) );
			Assert.Equal( null, reader.GetField<float?>( 5 ) );
			Assert.Equal( Convert.ToDouble( data[0] ), reader.GetField<double>( 0 ) );
			Assert.Equal( Convert.ToDouble( data[0] ), reader.GetField<double?>( 0 ) );
			Assert.Equal( null, reader.GetField<double?>( 5 ) );
			Assert.Equal( data[1], reader.GetField<string>( 1 ) );
			Assert.Equal( string.Empty, reader.GetField<string>( 5 ) );
			Assert.Equal( Convert.ToDateTime( data[2] ), reader.GetField<DateTime>( 2 ) );
			Assert.Equal( Convert.ToDateTime( data[2] ), reader.GetField<DateTime?>( 2 ) );
			Assert.Equal( null, reader.GetField<DateTime?>( 5 ) );
			Assert.Equal( Convert.ToBoolean( data[3] ), reader.GetField<bool>( 3 ) );
			Assert.Equal( Convert.ToBoolean( data[3] ), reader.GetField<bool?>( 3 ) );
			Assert.Equal( null, reader.GetField<bool?>( 5 ) );
			Assert.Equal( Convert.ToChar( data[4] ), reader.GetField<char>( 4 ) );
			Assert.Equal( Convert.ToChar( data[4] ), reader.GetField<char?>( 4 ) );
			Assert.Equal( null, reader.GetField<char?>( 5 ) );
			Assert.Equal( new Guid( data[6] ), reader.GetField<Guid>( 6 ) );
			Assert.Equal( null, reader.GetField<Guid?>( 5 ) );
		}

		[Fact]
		public void GetFieldByIndexTest()
		{
			var data = new [] { "1", "2" };

			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			Assert.Equal( 1, reader.GetField<int>( 0 ) );
			Assert.Equal( 2, reader.GetField<int>( 1 ) );
		}

		[Fact]
		public void GetFieldByNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new [] { "One", "Two" };
			var data2 = new [] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			Assert.Equal( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.Equal( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
		}

		[Fact]
		public void GetFieldByNameAndIndexTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "One" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			Assert.Equal( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One", 0 ) );
			Assert.Equal( Convert.ToInt32( data2[1] ), reader.GetField<int>( "One", 1 ) );
		}

		[Fact]
		public void GetMissingFieldByNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Configuration.IsStrictMode = false;
			reader.Read();

			Assert.Throws<IndexOutOfRangeException>( () => reader.GetField<string>( "blah" ) );
		}

		[Fact]
		public void GetMissingFieldByNameStrictTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			Assert.Throws<CsvMissingFieldException>( () => reader.GetField<string>( "blah" ) );
		}

		[Fact]
		public void GetFieldByNameNoHeaderExceptionTest()
		{
			var data = new [] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () => data );

			var reader = new CsvReader( parserMock.Object ) { Configuration = { HasHeaderRecord = false } };
			reader.Read();

			Assert.Throws<CsvReaderException>( () => reader.GetField<int>( "One" ) );
		}

		[Fact]
		public void GetRecordWithDuplicateHeaderFields()
		{
			var data = new[] { "Field1", "Field1" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () => data );

			var reader = new CsvReader( parserMock.Object );
			reader.Configuration.IsStrictMode = true;
			reader.Read();
		}

		[Fact]
		public void GetRecordTest()
		{
			var headerData = new []
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var recordData = new []
            {
                "1",
                "string column",
				Guid.NewGuid().ToString(),
            };
			var isHeaderRecord = true;
			var mockFactory = new MockRepository( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
			csvParserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			csvParserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return headerData;
				}
				return recordData;
			} );

			var csv = new CsvReader( csvParserMock.Object );
			csv.Configuration.IsStrictMode = false;
			csv.Read();
			var record = csv.GetRecord<TestRecord>();

			Assert.Equal( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.Equal( recordData[1], record.StringColumn );
			Assert.Equal( "test", record.TypeConvertedColumn );
			Assert.Equal( Convert.ToInt32( recordData[0] ), record.FirstColumn );
			Assert.Equal( new Guid( recordData[2] ), record.GuidColumn );
		}

		[Fact]
		public void GetRecordsTest()
		{
			var headerData = new []
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var count = -1;
			var mockFactory = new MockRepository( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
			csvParserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var guid = Guid.NewGuid();
			csvParserMock.Setup( m => m.Read() ).Returns( () =>
			{
				count++;
				if( count == 0 )
				{
					return headerData;
				}
				if( count > 2 )
				{
					return null;
				}
				return new[] { count.ToString(), "string column " + count, guid.ToString() };
			} );

			var csv = new CsvReader( csvParserMock.Object );
			csv.Configuration.IsStrictMode = false;
			var records = csv.GetRecords<TestRecord>().ToList();

			Assert.Equal( 2, records.Count );

			for( var i = 1; i <= records.Count; i++ )
			{
				var record = records[i - 1];
				Assert.Equal( i , record.IntColumn );
				Assert.Equal( "string column " + i, record.StringColumn );
				Assert.Equal( "test", record.TypeConvertedColumn );
				Assert.Equal( i, record.FirstColumn );
				Assert.Equal( guid, record.GuidColumn );
			}
		}

		[Fact]
		public void GetRecordsWithDuplicateHeaderNames()
		{
			var headerData = new[]
            {
                "Column",
                "Column",
                "Column"
            };

			var count = -1;
			var mockFactory = new MockRepository( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
			csvParserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			csvParserMock.Setup( m => m.Read() ).Returns( () =>
			{
				count++;
				if( count == 0 )
				{
					return headerData;
				}
				if( count > 2 )
				{
					return null;
				}
				return new[] { "one", "two", "three" };
			} );

			var csv = new CsvReader( csvParserMock.Object );
			csv.Configuration.IsStrictMode = true;
			var records = csv.GetRecords<TestRecordDuplicateHeaderNames>().ToList();

			Assert.Equal( 2, records.Count );

			for( var i = 1; i <= records.Count; i++ )
			{
				var record = records[i - 1];
				Assert.Equal( "one", record.Column1 );
				Assert.Equal( "two", record.Column2 );
				Assert.Equal( "three", record.Column3 );
			}
		}

		[Fact]
		public void TryGetFieldInvalidIndexTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.False( got );
			Assert.Equal( default( int ), field );
		}

		[Fact]
		public void TryGetFieldInvalidNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.False( got );
			Assert.Equal( default( int ), field );
		}

		[Fact]
		public void TryGetFieldInvalidConverterIndexTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, new GuidConverter(), out field );
			Assert.False( got );
			Assert.Equal( default( int ), field );
		}

		[Fact]
		public void TryGetFieldInvalidConverterNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", new GuidConverter(), out field );
			Assert.False( got );
			Assert.Equal( default( int ), field );
		}

		[Fact]
		public void TryGetFieldTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.True( got );
			Assert.Equal( 1, field );
		}

		[Fact]
		public void TryGetFieldStrictTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockRepository( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.True( got );
			Assert.Equal( 1, field );
		}

		[Fact]
		public void TryGetFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an emptry string for a date.
			var data = new[] { " " };

			var mockFactory = new MockRepository( MockBehavior.Default );

			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			DateTime field;
			var got = reader.TryGetField( 0, out field );

			Assert.False( got );
			Assert.Equal( DateTime.MinValue, field );
		}

		[Fact]
		public void GetRecordNoAttributesTest()
		{
			var headerData = new[]
            {
                "IntColumn",
                "StringColumn",
                "GuidColumn",
                "CustomTypeColumn",
            };
			var recordData = new[]
            {
                "1",
                "string column",
				Guid.NewGuid().ToString(),
				"blah",
            };
			var isHeaderRecord = true;
			var mockFactory = new MockRepository( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
			csvParserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			csvParserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return headerData;
				}
				return recordData;
			} );

			var csv = new CsvReader( csvParserMock.Object );
			csv.Configuration.IsStrictMode = false;
			csv.Read();
			var record = csv.GetRecord<TestRecordNoAttributes>();

			Assert.Equal( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.Equal( recordData[1], record.StringColumn );
			Assert.Equal( default( string ), record.IgnoredColumn );
			Assert.Equal( default( string ), record.TypeConvertedColumn );
			Assert.Equal( default( int ),record.FirstColumn );
			Assert.Equal( new Guid( recordData[2] ), record.GuidColumn );
			Assert.Equal( default( int ), record.NoMatchingFields );
			Assert.Equal( default( TestRecord ), record.CustomTypeColumn );
		}

		[Fact]
		public void GetRecordEmptyValuesNullableTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			writer.WriteLine( "StringColumn,IntColumn,GuidColumn" );
			writer.WriteLine( "one,1,11111111-1111-1111-1111-111111111111" );
			writer.WriteLine( ",," );
			writer.WriteLine( "three,3,33333333-3333-3333-3333-333333333333" );
			writer.Flush();
			stream.Position = 0;

			var reader = new StreamReader( stream );
			var csvReader = new CsvReader( reader );

			csvReader.Read();
			var record = csvReader.GetRecord<TestNullable>();
			Assert.NotNull( record );
			Assert.Equal( "one", record.StringColumn );
			Assert.Equal( 1, record.IntColumn );
			Assert.Equal( new Guid( "11111111-1111-1111-1111-111111111111" ), record.GuidColumn );

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.NotNull( record );
			Assert.Equal( string.Empty, record.StringColumn );
			Assert.Equal( null, record.IntColumn );
			Assert.Equal( null, record.GuidColumn );

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.NotNull( record );
			Assert.Equal( "three", record.StringColumn );
			Assert.Equal( 3, record.IntColumn );
			Assert.Equal( new Guid( "33333333-3333-3333-3333-333333333333" ), record.GuidColumn );
		}

		[Fact]
		public void CaseInsensitiveHeaderMatchingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "One,Two,Three" );
				writer.WriteLine( "1,2,3" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.IsCaseSensitive = false;
				csv.Read();

				Assert.Equal( "1", csv.GetField( "one" ) );
				Assert.Equal( "2", csv.GetField( "TWO" ) );
				Assert.Equal( "3", csv.GetField( "ThreE" ) );
			}
		}

		[Fact]
		public void InvalidateRecordsCacheTest()
		{
			var csvParserMock = new Mock<ICsvParser>();
			csvParserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var csvReader = new CsvReader( csvParserMock.Object );
			csvReader.Configuration.AttributeMapping<TestRecord>();

			Assert.True( csvReader.Configuration.Properties.Count > 0 );

			csvReader.InvalidateRecordCache<TestRecord>();

			Assert.Equal( 0, csvReader.Configuration.Properties.Count );

			csvReader.Configuration.AttributeMapping<TestRecordNoAttributes>();

			Assert.True( csvReader.Configuration.Properties.Count > 0 );
		}

		private class TestNullable
		{
			public int? IntColumn { get; set; }
			public string StringColumn { get; set; }
			public Guid? GuidColumn { get; set; }
		}

		[DebuggerDisplay( "IntColumn = {IntColumn}, StringColumn = {StringColumn}, IgnoredColumn = {IgnoredColumn}, TypeConvertedColumn = {TypeConvertedColumn}, FirstColumn = {FirstColumn}" )]
		private class TestRecord
		{
			[TypeConverter( typeof( Int32Converter ) )]
			public int IntColumn { get; set; }

			[CsvField( Name = "String Column" )]
			public string StringColumn { get; set; }

			[CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			[CsvField( Index = 1 )]
			[TypeConverter( typeof( TestTypeConverter ) )]
			public string TypeConvertedColumn { get; set; }

			[CsvField( Index = 0 )]
			public int FirstColumn { get; set; }

			public Guid GuidColumn { get; set; }

			public int NoMatchingFields { get; set; }
		}

		private class TestRecordNoAttributes
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public string IgnoredColumn { get; set; }

			public string TypeConvertedColumn { get; set; }

			public int FirstColumn { get; set; }

			public Guid GuidColumn { get; set; }

			public int NoMatchingFields { get; set; }

			public TestRecord CustomTypeColumn { get; set; }
		}

		private class TestRecordDuplicateHeaderNames
		{
			[CsvField( Name = "Column", Index = 0 )]
			public string Column1 { get; set; }

			[CsvField( Name = "Column", Index = 1 )]
			public string Column2 { get; set; }

			[CsvField( Name = "Column", Index = 2 )]
			public string Column3 { get; set; }
		}

		private class TestTypeConverter : TypeConverter
		{
			public override object ConvertFrom( ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value )
			{
				return "test";
			}

			public override bool CanConvertFrom( ITypeDescriptorContext context, Type sourceType )
			{
				return sourceType == typeof( string );
			}
		}
	}
}
