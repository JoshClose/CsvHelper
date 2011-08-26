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
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
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
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
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
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
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

			var reader = new CsvReader( parserMock.Object ) { Configuration = { Strict = true } };
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
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () => data );

			var reader = new CsvReader( parserMock.Object ) { Configuration = { HasHeaderRecord = false } };
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
		public void TryGetFieldInvalidIndexTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var mockFactory = new MockFactory( MockBehavior.Default );
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
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldInvalidNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var mockFactory = new MockFactory( MockBehavior.Default );
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
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldInvalidConverterIndexTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
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
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldInvalidConverterNameTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
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
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldTest()
		{
			var isHeaderRecord = true;
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var mockFactory = new MockFactory( MockBehavior.Default );
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

			var reader = new CsvReader( parserMock.Object ) { Configuration = { Strict = true } };
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
        [ExpectedException(typeof(CsvReaderException))]
        public void HasHeaderRecordWithDuplicateFieldsTest()
        {
            var isHeaderRecord = true;
            var data1 = new[] { "One", "Two", "Two" };
            var data2 = new[] { "1", "2", "3" };
            var mockFactory = new MockFactory(MockBehavior.Default);
            var parserMock = mockFactory.Create<ICsvParser>();
            parserMock.Setup(m => m.Configuration).Returns(new CsvConfiguration() { HasHeaderRecord = true });
            parserMock.Setup(m => m.Read()).Returns(() =>
            {
                if (isHeaderRecord)
                {
                    isHeaderRecord = false;
                    return data1;
                }
                return data2;
            });

            var reader = new CsvReader(parserMock.Object);
            reader.Read();

            // Check to see if the header record and first record are set properly.
            Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>("One"));
            Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>("Two"));
            Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>(0));
            Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>(1));
        }

        [TestMethod]
        public void HasHeaderRecordWithIgnoredDuplicateFieldsTest()
        {
            var isHeaderRecord = true;
            var data1 = new[] { "One", "Two", "Two", "Three" };
            var data2 = new[] { "1", "2", "3", "4" };
            var mockFactory = new MockFactory(MockBehavior.Default);
            var parserMock = mockFactory.Create<ICsvParser>();
            parserMock.Setup(m => m.Configuration).Returns(new CsvConfiguration() { HasHeaderRecord = true, IgnoreDuplicateHeaderFields = true });
            parserMock.Setup(m => m.Read()).Returns(() =>
            {
                if (isHeaderRecord)
                {
                    isHeaderRecord = false;
                    return data1;
                }
                return data2;
            });

            var reader = new CsvReader(parserMock.Object);
            reader.Read();

            // Check to see if the header record and first record are set properly.
            Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>("One"));
            Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>("Two"));
            Assert.AreEqual(Convert.ToInt32(data2[3]), reader.GetField<int>("Three"));
            Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>(0));
            Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>(1));
            Assert.AreEqual(Convert.ToInt32(data2[2]), reader.GetField<int>(2));
            Assert.AreEqual(Convert.ToInt32(data2[3]), reader.GetField<int>(3));
        }

        [TestMethod]
        public void GetRecordFinalValueInRowAsNullTest()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.WriteLine("Column1,Column2,Column3");
            writer.WriteLine("one,two,three");
            writer.Write("one,two,");
            writer.Flush();
            stream.Position = 0;

            var reader = new StreamReader(stream);
            var csvReader = new CsvReader(reader);

            csvReader.Read();
            Assert.AreEqual("three", csvReader["Column3"]);
            
            csvReader.Read();
            Assert.AreEqual("", csvReader["Column3"]);

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
