#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterTests
	{
		[TestMethod]
		public void WriteFieldTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			var csv = new CsvWriter( writer );

			var date = DateTime.Now.ToString();
			var guid = Guid.NewGuid().ToString();
			csv.WriteField( "one" );
			csv.WriteField( "one, two" );
			csv.WriteField( "one \"two\" three" );
			csv.WriteField( " one " );
			csv.WriteField( date );
			csv.WriteField( (short)1 );
			csv.WriteField( 1 );
			csv.WriteField( (long)1 );
			csv.WriteField( (float)1 );
			csv.WriteField( (double)1 );
			csv.WriteField( guid );
			csv.NextRecord();

			var reader = new StreamReader( stream );
			stream.Position = 0;
			var data = reader.ReadToEnd();

			Assert.AreEqual( "one,\"one, two\",\"one \"\"two\"\" three\",\" one \"," + date + ",1,1,1,1,1," + guid + "\r\n", data );
		}

		[TestMethod]
		public void WriteRecordTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first column",
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csv = new CsvWriter( writer ) { HasHeaderRecord = true };

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,TypeConvertedColumn,StringColumn\r\n";
			expected += "first column,1,test,string column\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
		public void WriteRecordsTest()
		{
			var records = new List<TestRecord>
            {
                new TestRecord
                {
                    IntColumn = 1,
                    StringColumn = "string column",
                    IgnoredColumn = "ignored column",
                    FirstColumn = "first column",
                },
                new TestRecord
                {
                    IntColumn = 2,
                    StringColumn = "string column 2",
                    IgnoredColumn = "ignored column 2",
                    FirstColumn = "first column 2",
                },
            };

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csv = new CsvWriter( writer ) { HasHeaderRecord = true };

			csv.WriteRecords( records );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,TypeConvertedColumn,StringColumn\r\n";
			expected += "first column,1,test,string column\r\n";
			expected += "first column 2,2,test,string column 2\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
		public void WriteRecordNoHeaderTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csv = new CsvWriter( writer );
			csv.WriteRecord( new TestRecord() );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			Assert.AreEqual( ",0,test,\r\n", csvFile );
		}

		[TestMethod]
		public void WriteRecordWithNullRecord()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first column",
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csv = new CsvWriter( writer ) { HasHeaderRecord = true };

			csv.WriteRecord( record );
			csv.WriteRecord( (TestRecord)null );
			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,TypeConvertedColumn,StringColumn\r\n";
			expected += "first column,1,test,string column\r\n";
			expected += ",,,\r\n";
			expected += "first column,1,test,string column\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TypeConverter( "type name" )]
		private class TestRecord
		{
			[CsvField( FieldIndex = 1, FieldName = "Int Column" )]
			[TypeConverter( typeof( Int32Converter ) )]
			public int IntColumn { get; set; }

			[TypeConverter( "String" )]
			public string StringColumn { get; set; }

			[CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			[CsvField( FieldIndex = 0 )]
			public string FirstColumn { get; set; }

			[TypeConverter( typeof( TestTypeConverter ) )]
			public string TypeConvertedColumn { get; set; }
		}

		private class TestTypeConverter : TypeConverter
		{
			public override object ConvertTo( ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType )
			{
				return "test";
			}
		}
	}
}
