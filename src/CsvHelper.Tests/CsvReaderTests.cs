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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderTests
	{
		[TestMethod]
		[ExpectedException( typeof( CsvReaderException ) )]
		public void HasHeaderRecordNotReadExceptionTest()
		{
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();

			var reader = new CsvReader( parserMock.Object );

			reader.GetField<int>( 0 );
		}

		[TestMethod]
		public void HasHeaderRecordTest()
		{
			var isHeaderRecord = true;
			var data1 = new [] { "One", "Two" };
			var data2 = new [] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
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
			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( 0 ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( 1 ) );
		}

		[TestMethod]
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

			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object );
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
			var data = new [] { "1", "2" };

			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();

			Assert.AreEqual( 1, reader.GetField<int>( 0 ) );
			Assert.AreEqual( 2, reader.GetField<int>( 1 ) );
		}

		[TestMethod]
		public void GetFieldByNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new [] { "One", "Two" };
			var data2 = new [] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
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

			Assert.AreEqual( Convert.ToInt32( data2[0] ), reader.GetField<int>( "One" ) );
			Assert.AreEqual( Convert.ToInt32( data2[1] ), reader.GetField<int>( "Two" ) );
		}

		[TestMethod]
		[ExpectedException( typeof( IndexOutOfRangeException ) )]
		public void GetMissingFieldByNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
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

			reader.GetField<string>( "blah" );
		}

		[TestMethod]
		[ExpectedException( typeof( CsvMissingFieldException ) )]
		public void GetMissingFieldByNameStrictTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object, new CsvReaderOptions { Strict = true } );
			reader.Read();

			reader.GetField<string>( "blah" );
		}

		[TestMethod]
		[ExpectedException( typeof( CsvReaderException ) )]
		public void GetFieldByNameNoHeaderExceptionTest()
		{
			var data = new [] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( () => data );

			var reader = new CsvReader( parserMock.Object, new CsvReaderOptions{ HasHeaderRecord = false } );
			reader.Read();

			Assert.AreEqual( Convert.ToInt32( data[0] ), reader.GetField<int>( "One" ) );
		}

		[TestMethod]
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
			var mockFactory = new MockFactory( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
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
			csv.Read();
			var record = csv.GetRecord<TestRecord>();

			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.AreEqual( recordData[1], record.StringColumn );
			Assert.AreEqual( "test", record.TypeConvertedColumn );
			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.FirstColumn );
			Assert.AreEqual( new Guid( recordData[2] ), record.GuidColumn );
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var headerData = new []
            {
                "IntColumn",
                "String Column",
                "GuidColumn",
            };
			var count = -1;
			var mockFactory = new MockFactory( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
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
			var records = csv.GetRecords<TestRecord>();

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
		public void TryGetFieldInvalidTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
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

			string field;
			var got = reader.TryGetField( -1, out field );
			Assert.IsFalse( got );
			Assert.IsNull( field );
		}

		[TestMethod]
		public void TryGetFieldInvalidStrictTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object, new CsvReaderOptions { Strict = true } );
			reader.Read();

			string field;
			var got = reader.TryGetField( -1, out field );
			Assert.IsFalse( got );
			Assert.IsNull( field );
		}

		[TestMethod]
		public void TryGetFieldTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
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
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
		public void TryGetFieldStrictTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( isHeaderRecord )
				{
					isHeaderRecord = false;
					return data1;
				}
				return data2;
			} );

			var reader = new CsvReader( parserMock.Object, new CsvReaderOptions { Strict = true } );
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
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
			var mockFactory = new MockFactory( MockBehavior.Default );
			var csvParserMock = mockFactory.Create<ICsvParser>();
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
			csv.Read();
			var record = csv.GetRecord<TestRecordNoAttributes>();

			Assert.AreEqual( Convert.ToInt32( recordData[0] ), record.IntColumn );
			Assert.AreEqual( recordData[1], record.StringColumn );
			Assert.AreEqual( default( string ), record.IgnoredColumn );
			Assert.AreEqual( default( string ), record.TypeConvertedColumn );
			Assert.AreEqual( default( int ),record.FirstColumn );
			Assert.AreEqual( new Guid( recordData[2] ), record.GuidColumn );
			Assert.AreEqual( default( int ), record.NoMatchingFields );
			Assert.AreEqual( default( TestRecord ), record.CustomTypeColumn );
		}

		[DebuggerDisplay( "IntColumn = {IntColumn}, StringColumn = {StringColumn}, IgnoredColumn = {IgnoredColumn}, TypeConvertedColumn = {TypeConvertedColumn}, FirstColumn = {FirstColumn}" )]
		private class TestRecord
		{
			[TypeConverter( typeof( Int32Converter ) )]
			public int IntColumn { get; set; }

			[CsvField( FieldName = "String Column" )]
			public string StringColumn { get; set; }

			[CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			[CsvField( FieldIndex = 1 )]
			[TypeConverter( typeof( TestTypeConverter ) )]
			public string TypeConvertedColumn { get; set; }

			[CsvField( FieldIndex = 0 )]
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
