// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderTests
	{
		[TestMethod]
		public void HasHeaderRecordNotReadExceptionTest()
		{
			var parserMock = new ParserMock( new Queue<string[]>() );
			var reader = new CsvReader( parserMock );

			try
			{
				reader.GetField<int>( 0 );
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
		}

		[TestMethod]
		public void HasHeaderRecordTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );

			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			// Check to see if the header record and first record are set properly.
			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( 0 ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( 1 ) );
		}

		[TestMethod]
		public void GetTypeTest()
		{
			var data = new[]
			{
				"1",
				"blah",
				DateTime.Now.ToString(),
				"true",
				"c",
				"",
				Guid.NewGuid().ToString(),
			};
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( data );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			Assert.AreEqual( Convert.ToInt16( data[0] ), reader.GetField<short>( 0 ) );
			Assert.AreEqual( Convert.ToInt16( data[0] ), reader.GetField<short?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<short?>( 5 ) );
			Assert.AreEqual( Convert.ToInt32( data[0] ), reader.GetField<int>( 0 ) );
			Assert.AreEqual( Convert.ToInt32( data[0] ), reader.GetField<int?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<int?>( 5 ) );
			Assert.AreEqual( Convert.ToInt64( data[0] ), reader.GetField<long>( 0 ) );
			Assert.AreEqual( Convert.ToInt64( data[0] ), reader.GetField<long?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<long?>( 5 ) );
			Assert.AreEqual( Convert.ToDecimal( data[0] ), reader.GetField<decimal>( 0 ) );
			Assert.AreEqual( Convert.ToDecimal( data[0] ), reader.GetField<decimal?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<decimal?>( 5 ) );
			Assert.AreEqual( Convert.ToSingle( data[0] ), reader.GetField<float>( 0 ) );
			Assert.AreEqual( Convert.ToSingle( data[0] ), reader.GetField<float?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<float?>( 5 ) );
			Assert.AreEqual( Convert.ToDouble( data[0] ), reader.GetField<double>( 0 ) );
			Assert.AreEqual( Convert.ToDouble( data[0] ), reader.GetField<double?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<double?>( 5 ) );
			Assert.AreEqual( data[1], reader.GetField<string>( 1 ) );
			Assert.AreEqual( string.Empty, reader.GetField<string>( 5 ) );
			Assert.AreEqual( Convert.ToDateTime( data[2] ), reader.GetField<DateTime>( 2 ) );
			Assert.AreEqual( Convert.ToDateTime( data[2] ), reader.GetField<DateTime?>( 2 ) );
			Assert.AreEqual( null, reader.GetField<DateTime?>( 5 ) );
			Assert.AreEqual( Convert.ToBoolean( data[3] ), reader.GetField<bool>( 3 ) );
			Assert.AreEqual( Convert.ToBoolean( data[3] ), reader.GetField<bool?>( 3 ) );
			Assert.AreEqual( null, reader.GetField<bool?>( 5 ) );
			Assert.AreEqual( Convert.ToChar( data[4] ), reader.GetField<char>( 4 ) );
			Assert.AreEqual( Convert.ToChar( data[4] ), reader.GetField<char?>( 4 ) );
			Assert.AreEqual( null, reader.GetField<char?>( 5 ) );
			Assert.AreEqual( new Guid( data[6] ), reader.GetField<Guid>( 6 ) );
			Assert.AreEqual( null, reader.GetField<Guid?>( 5 ) );
		}

		[TestMethod]
		public void GetFieldByIndexTest()
		{
			var data = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			Assert.AreEqual( 1, reader.GetField<int>( 0 ) );
			Assert.AreEqual( 2, reader.GetField<int>( 1 ) );
		}

		[TestMethod]
		public void GetFieldByNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
		}

		[TestMethod]
		public void GetFieldByNameAndIndexTest()
		{
			var data1 = new[] { "One", "One" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One", 0 ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( "One", 1 ) );
		}

		[TestMethod]
		public void GetMissingFieldByNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.IsStrictMode = false;
			reader.Read();

			try
			{
				reader.GetField<string>( "blah" );
				Assert.Fail();
			}
			catch( IndexOutOfRangeException )
			{
			}
		}

		[TestMethod]
		public void GetMissingFieldByNameStrictTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			try
			{
				reader.GetField<string>( "blah" );
				Assert.Fail();
			}
			catch( CsvMissingFieldException )
			{
			}
		}

		[TestMethod]
		public void GetMissingFieldByIndexStrictTest()
		{
			var data = new Queue<string[]>();
			data.Enqueue( new[] { "One", "Two" } );
			data.Enqueue( new[] { "1", "2" } );
			data.Enqueue( null );
			var parserMock = new ParserMock( data );

			var reader = new CsvReader( parserMock ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			try
			{
				reader.GetField( 2 );
				Assert.Fail();
			}
			catch( CsvMissingFieldException )
			{
			}
		}

		[TestMethod]
		public void GetMissingFieldGenericByIndexStrictTest()
		{
			var data = new Queue<string[]>();
			data.Enqueue( new[] { "One", "Two" } );
			data.Enqueue( new[] { "1", "2" } );
			data.Enqueue( null );
			var parserMock = new ParserMock( data );

			var reader = new CsvReader( parserMock ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			try
			{
				reader.GetField<string>( 2 );
				Assert.Fail();
			}
			catch( CsvMissingFieldException )
			{
			}
		}

		[TestMethod]
		public void GetMissingFieldByIndexStrictOffTest()
		{
			var data = new Queue<string[]>();
			data.Enqueue( new[] { "One", "Two" } );
			data.Enqueue( new[] { "1", "2" } );
			data.Enqueue( null );
			var parserMock = new ParserMock( data );

			var reader = new CsvReader( parserMock ) { Configuration = { IsStrictMode = false } };
			reader.Read();

			Assert.IsNull( reader.GetField( 2 ) );
		}

		[TestMethod]
		public void GetMissingFieldGenericByIndexStrictOffTest()
		{
			var data = new Queue<string[]>();
			data.Enqueue( new[] { "One", "Two" } );
			data.Enqueue( new[] { "1", "2" } );
			data.Enqueue( null );
			var parserMock = new ParserMock( data );

			var reader = new CsvReader( parserMock )
			{
				Configuration = { IsStrictMode = false }
			};
			reader.Read();

			Assert.IsNull( reader.GetField<string>( 2 ) );
		}

		[TestMethod]
		public void GetFieldByNameNoHeaderExceptionTest()
		{
			var data = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock ) { Configuration = { HasHeaderRecord = false } };
			reader.Read();

			try
			{
				reader.GetField<int>( "One" );
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
		}

		[TestMethod]
		public void GetRecordWithDuplicateHeaderFields()
		{
			var data = new[] { "Field1", "Field1" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( data );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.IsStrictMode = true;
			reader.Read();
		}

		[TestMethod]
		public void GetRecordGenericTest()
		{
			var headerData = new[]
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var recordData = new[]
            {
                "1",
                "string column",
				Guid.NewGuid().ToString(),
            };
			var queue = new Queue<string[]>();
			queue.Enqueue( headerData );
			queue.Enqueue( recordData );
			queue.Enqueue( null );
			var csvParserMock = new ParserMock( queue );

			var csv = new CsvReader( csvParserMock );
			csv.Configuration.IsStrictMode = false;
			csv.Configuration.ClassMapping<TestRecordMap>();
			csv.Read();
			var record = csv.GetRecord<TestRecord>();

			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.AreEqual( recordData[1], record.StringColumn );
			Assert.AreEqual( "test", record.TypeConvertedColumn );
			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.FirstColumn );
			Assert.AreEqual( new Guid( recordData[2] ), record.GuidColumn );
		}

		[TestMethod]
		public void GetRecordTest()
		{
			var headerData = new[]
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var recordData = new[]
            {
                "1",
                "string column",
				Guid.NewGuid().ToString(),
            };
			var queue = new Queue<string[]>();
			queue.Enqueue( headerData );
			queue.Enqueue( recordData );
			queue.Enqueue( null );
			var csvParserMock = new ParserMock( queue );

			var csv = new CsvReader( csvParserMock );
			csv.Configuration.IsStrictMode = false;
			csv.Configuration.ClassMapping<TestRecordMap>();
			csv.Read();
			var record = (TestRecord)csv.GetRecord( typeof( TestRecord ));

			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.AreEqual( recordData[1], record.StringColumn );
			Assert.AreEqual( "test", record.TypeConvertedColumn );
			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.FirstColumn );
			Assert.AreEqual( new Guid( recordData[2] ), record.GuidColumn );
		}

		[TestMethod]
		public void GetRecordsGenericTest()
		{
			var headerData = new[]
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var guid = Guid.NewGuid();
			var queue = new Queue<string[]>();
			queue.Enqueue( headerData );
			queue.Enqueue( new[] { "1", "string column 1", guid.ToString() } );
			queue.Enqueue( new[] { "2", "string column 2", guid.ToString() } );
			queue.Enqueue( null );
			var csvParserMock = new ParserMock( queue );

			var csv = new CsvReader( csvParserMock );
			csv.Configuration.IsStrictMode = false;
			csv.Configuration.ClassMapping<TestRecordMap>();
			var records = csv.GetRecords<TestRecord>().ToList();

			Assert.AreEqual( 2, records.Count );

			for( var i = 1; i <= records.Count; i++ )
			{
				var record = records[i - 1];
				Assert.AreEqual( i , record.IntColumn );
				Assert.AreEqual( "string column " + i, record.StringColumn );
				Assert.AreEqual( "test", record.TypeConvertedColumn );
				Assert.AreEqual( i, record.FirstColumn );
				Assert.AreEqual( guid, record.GuidColumn );
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var headerData = new[]
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var guid = Guid.NewGuid();
			var queue = new Queue<string[]>();
			queue.Enqueue( headerData );
			queue.Enqueue( new[] { "1", "string column 1", guid.ToString() } );
			queue.Enqueue( new[] { "2", "string column 2", guid.ToString() } );
			queue.Enqueue( null );
			var csvParserMock = new ParserMock( queue );

			var csv = new CsvReader( csvParserMock );
			csv.Configuration.IsStrictMode = false;
			csv.Configuration.ClassMapping<TestRecordMap>();
			var records = csv.GetRecords( typeof( TestRecord ) ).ToList();

			Assert.AreEqual( 2, records.Count );

			for( var i = 1; i <= records.Count; i++ )
			{
				var record = (TestRecord)records[i - 1];
				Assert.AreEqual( i, record.IntColumn );
				Assert.AreEqual( "string column " + i, record.StringColumn );
				Assert.AreEqual( "test", record.TypeConvertedColumn );
				Assert.AreEqual( i, record.FirstColumn );
				Assert.AreEqual( guid, record.GuidColumn );
			}
		}

		[TestMethod]
		public void GetRecordsWithDuplicateHeaderNames()
		{
			var headerData = new[]
			{
                "Column",
                "Column",
                "Column"
            };

			var queue = new Queue<string[]>();
			queue.Enqueue( headerData );
			queue.Enqueue( new[] { "one", "two", "three" } );
			queue.Enqueue( new[] { "one", "two", "three" } );
			queue.Enqueue( null );
			var csvParserMock = new ParserMock( queue );

			var csv = new CsvReader( csvParserMock );
			csv.Configuration.IsStrictMode = true;
			csv.Configuration.ClassMapping<TestRecordDuplicateHeaderNamesMap>();
			var records = csv.GetRecords<TestRecordDuplicateHeaderNames>().ToList();

			Assert.AreEqual( 2, records.Count );

			for( var i = 1; i <= records.Count; i++ )
			{
				var record = records[i - 1];
				Assert.AreEqual( "one", record.Column1 );
				Assert.AreEqual( "two", record.Column2 );
				Assert.AreEqual( "three", record.Column3 );
			}
		}

		[TestMethod]
		public void GetRecordEmptyFileWithHeaderOnTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var csvReader = new CsvReader( parserMock );
			try
			{
				csvReader.Read();
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
		}

		[TestMethod]
		public void TryGetFieldInvalidIndexTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldInvalidNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
		public void TryGetFieldStrictTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock ) { Configuration = { IsStrictMode = true } };
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
		public void TryGetFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an emptry string for a date.
			var data = new[] { " " };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			DateTime field;
			var got = reader.TryGetField( 0, out field );

			Assert.IsFalse( got );
			Assert.AreEqual( DateTime.MinValue, field );
		}

		[TestMethod]
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
			Assert.IsNotNull( record );
			Assert.AreEqual( "one", record.StringColumn );
			Assert.AreEqual( 1, record.IntColumn );
			Assert.AreEqual( new Guid( "11111111-1111-1111-1111-111111111111" ), record.GuidColumn );

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.IsNotNull( record );
			Assert.AreEqual( string.Empty, record.StringColumn );
			Assert.AreEqual( null, record.IntColumn );
			Assert.AreEqual( null, record.GuidColumn );

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.IsNotNull( record );
			Assert.AreEqual( "three", record.StringColumn );
			Assert.AreEqual( 3, record.IntColumn );
			Assert.AreEqual( new Guid( "33333333-3333-3333-3333-333333333333" ), record.GuidColumn );
		}

		[TestMethod]
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

				Assert.AreEqual( "1", csv.GetField( "one" ) );
				Assert.AreEqual( "2", csv.GetField( "TWO" ) );
				Assert.AreEqual( "3", csv.GetField( "ThreE" ) );
			}
		}

		[TestMethod]
		public void DefaultValueTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			writer.WriteLine( "IntColumn,StringColumn" );
			writer.WriteLine( "," );
			writer.WriteLine( "2,two" );
			writer.Flush();
			stream.Position = 0;

			var reader = new StreamReader( stream );
			var csvReader = new CsvReader( reader );
			csvReader.Configuration.ClassMapping<TestDefaultValuesMap>();

			var records = csvReader.GetRecords<TestDefaultValues>().ToList();

			var record = records[0];
			Assert.AreEqual( -1, record.IntColumn );
			Assert.AreEqual( null, record.StringColumn );

			record = records[1];
			Assert.AreEqual( 2, record.IntColumn );
			Assert.AreEqual( "two", record.StringColumn );
		}

		[TestMethod]
		public void BooleanTypeConverterTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			writer.WriteLine( "BoolColumn,BoolNullableColumn,StringColumn" );
			writer.WriteLine( "true,true,1" );
			writer.WriteLine( "True,True,2" );
			writer.WriteLine( "1,1,3" );
			writer.WriteLine( "false,false,4" );
			writer.WriteLine( "False,False,5" );
			writer.WriteLine( "0,0,6" );

			writer.Flush();
			stream.Position = 0;

			var reader = new StreamReader( stream );
			var csvReader = new CsvReader( reader );

			var records = csvReader.GetRecords<TestBoolean>().ToList();

			Assert.IsTrue( records[0].BoolColumn );
			Assert.IsTrue( records[0].BoolNullableColumn);
			Assert.IsTrue( records[1].BoolColumn);
			Assert.IsTrue( records[1].BoolNullableColumn );
			Assert.IsTrue( records[2].BoolColumn );
			Assert.IsTrue( records[2].BoolNullableColumn );
			Assert.IsFalse( records[3].BoolColumn );
			Assert.IsFalse( records[3].BoolNullableColumn);
			Assert.IsFalse( records[4].BoolColumn );
			Assert.IsFalse( records[4].BoolNullableColumn );
			Assert.IsFalse( records[5].BoolColumn );
			Assert.IsFalse( records[5].BoolNullableColumn );
		}

		[TestMethod]
		public void SkipEmptyRecordsTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "2", "3" } );
			queue.Enqueue( new[] { "", "", "" } );
			queue.Enqueue( new[] { "4", "5", "6" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Configuration.SkipEmptyRecords = true;

			reader.Read();
			Assert.AreEqual( "1",  reader.CurrentRecord[0] );
			Assert.AreEqual( "2",  reader.CurrentRecord[1] );
			Assert.AreEqual( "3",  reader.CurrentRecord[2] );

			reader.Read();
			Assert.AreEqual( "4", reader.CurrentRecord[0] );
			Assert.AreEqual( "5", reader.CurrentRecord[1] );
			Assert.AreEqual( "6", reader.CurrentRecord[2] );

			Assert.IsFalse( reader.Read() );
		}

		[TestMethod]
		public void MultipleGetRecordsCalls()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "IntColumn,StringColumn" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.IsStrictMode = false;
				csvReader.Configuration.ClassMapping<TestRecordMap>();
				var records = csvReader.GetRecords<TestRecord>();
				Assert.AreEqual( 2, records.Count() );
				try
				{
					records.Count();
					Assert.Fail();
				}
				catch( CsvReaderException ) {}
			}
		}

		[TestMethod]
		public void OnlyFieldsTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "Name" } );
			queue.Enqueue( new[] { "name" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );

			try
			{
				// This needs a class map because auto mapping only works with properties.
				reader.Configuration.ClassMapping<OnlyFieldsMap>();
				reader.GetRecords<OnlyFields>().ToList();
				Assert.Fail();
			}
			catch( CsvConfigurationException )
			{
			}
		}

#if !NET_3_5
		[TestMethod]
		public void ReaderDynamicHasHeaderTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "Id", "Name" } );
			queue.Enqueue( new[] { "1", "one" } );
			queue.Enqueue( new[] { "2", "two" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csv = new CsvReader( parserMock );
			csv.Read();
			var row = csv.GetRecord<dynamic>();

			Assert.AreEqual( "1", row.Id );
			Assert.AreEqual( "one", row.Name );
		}

		[TestMethod]
		public void ReaderDynamicNoHeaderTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1", "one" } );
			queue.Enqueue( new[] { "2", "two" } );
			queue.Enqueue( null );

			var parserMock = new ParserMock( queue );

			var csv = new CsvReader( parserMock );
			csv.Configuration.HasHeaderRecord = false;
			csv.Read();
			var row = csv.GetRecord<dynamic>();

			Assert.AreEqual( "1", row.Field1 );
			Assert.AreEqual( "one", row.Field2 );
		}
#endif

		private class OnlyFields
		{
			public string Name;
		}

		private sealed class OnlyFieldsMap : CsvClassMap<OnlyFields>
		{
			public OnlyFieldsMap()
			{
				Map( m => m.Name );
			}
		}

		private class TestBoolean
		{
			public bool BoolColumn { get; set; }

			public bool BoolNullableColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private class TestDefaultValues
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class TestDefaultValuesMap : CsvClassMap<TestDefaultValues>
		{
			public TestDefaultValuesMap()
			{
				Map( m => m.IntColumn ).Default( -1 );
				Map( m => m.StringColumn ).Default( null );
			}
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
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public string IgnoredColumn { get; set; }

			public string TypeConvertedColumn { get; set; }

			public int FirstColumn { get; set; }

			public Guid GuidColumn { get; set; }

			public int NoMatchingFields { get; set; }
		}

		private sealed class TestRecordMap : CsvClassMap<TestRecord>
		{
			public TestRecordMap()
			{
				Map( m => m.IntColumn ).TypeConverter<Int32Converter>();
				Map( m => m.StringColumn ).Name( "String Column" );
				Map( m => m.TypeConvertedColumn ).Index( 1 ).TypeConverter<TestTypeConverter>();
				Map( m => m.FirstColumn ).Index( 0 );
				Map( m => m.GuidColumn );
				Map( m => m.NoMatchingFields );
			}
		}

		private class TestRecordDuplicateHeaderNames
		{
			public string Column1 { get; set; }

			public string Column2 { get; set; }

			public string Column3 { get; set; }
		}

		private sealed class TestRecordDuplicateHeaderNamesMap : CsvClassMap<TestRecordDuplicateHeaderNames>
		{
			public TestRecordDuplicateHeaderNamesMap()
			{
				Map( m => m.Column1 ).Name( "Column" ).Index( 0 );
				Map( m => m.Column2 ).Name( "Column" ).Index( 1 );
				Map( m => m.Column3 ).Name( "Column" ).Index( 2 );
			}
		}

		private class TestTypeConverter : DefaultTypeConverter
		{
			public override object ConvertFromString( TypeConverterOptions options, string text )
			{
				return "test";
			}

			public override bool CanConvertFrom( Type type )
			{
				return type == typeof( string );
			}
		}
	}
}
