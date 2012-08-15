#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvWriterTests
	{
		[Fact]
		public void WriteFieldTest()
		{
			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };

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

			Assert.Equal( "one,\"one, two\",\"one \"\"two\"\" three\",\" one \"," + date + ",1,1,1,1,1," + guid + "\r\n", data );
		}

		[Fact]
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
			var writer = new StreamWriter( stream ) { AutoFlush = true };
			var csv = new CsvWriter( writer );

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";

			Assert.Equal( expected, csvFile );
		}

		[Fact]
		public void WriteRecordNoIndexesTest()
		{
			var record = new TestRecordNoIndexes
			{
				IntColumn = 1,
				StringColumn = "string column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first column",
			};

			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer );

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "Int Column,StringColumn,FirstColumn,TypeConvertedColumn\r\n";
			expected += "1,string column,first column,test\r\n";

			Assert.Equal( expected, csvFile );
		}

		[Fact]
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
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer );

			csv.WriteRecords( records );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";
			expected += "first column 2,2,string column 2,test\r\n";

			Assert.Equal( expected, csvFile );
		}

		[Fact]
		public void WriteRecordNoHeaderTest()
		{
			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer ) { Configuration = { HasHeaderRecord = false } };
			csv.WriteRecord( new TestRecord() );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			Assert.Equal( ",0,,test\r\n", csvFile );
		}

		[Fact]
		public void WriteRecordWithNullRecordTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first column",
			};

			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer );

			csv.WriteRecord( record );
			csv.WriteRecord( (TestRecord)null );
			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";
			expected += ",,,\r\n";
			expected += "first column,1,string column,test\r\n";

			Assert.Equal( expected, csvFile );
		}

		[Fact]
		public void WriteRecordWithReferencesTest()
		{
			var record = new Person
			{
				FirstName = "First Name",
				LastName = "Last Name",
				HomeAddress = new Address
				{
					Street = "Home Street",
					City = "Home City",
					State = "Home State",
					Zip = "Home Zip",
				},
				WorkAddress = new Address
				{
					Street = "Work Street",
					City = "Work City",
					State = "Work State",
					Zip = "Work Zip",
				}
			};

			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer );
			csv.Configuration.ClassMapping<PersonMap>();

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			var expected = "FirstName,LastName,HomeStreet,HomeCity,HomeState,HomeZip,WorkStreet,WorkCity,WorkState,WorkZip\r\n" +
			               "First Name,Last Name,Home Street,Home City,Home State,Home Zip,Work Street,Work City,Work State,Work Zip\r\n";

			Assert.Equal( expected, csvFile );

		}

		[Fact]
		public void InvalidateRecordsCacheTest()
		{
			var people = new List<Person>
			{
				new Person { FirstName = "first name" }
			};
			var addresses = new List<Address>
			{
				new Address { City = "city" }
			};

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.WriteRecords( people );
				csvWriter.InvalidateRecordCache<Person>();
				csvWriter.WriteRecords( addresses );

				writer.Flush();
				stream.Position = 0;

				var csvText = reader.ReadToEnd();

				var expectedText = "FirstName,LastName,HomeAddress,WorkAddress\r\n";
				expectedText += "first name,,,\r\n";
				expectedText += ",city,,\r\n";

				Assert.Equal( expectedText, csvText );
			}
		}

		[Fact]
		public void WriteRecordsAllFieldsQuotedTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first column",
			};

			string csv;
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.QuoteAllFields = true;
				csvWriter.WriteRecord( record );

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = "\"FirstColumn\",\"Int Column\",\"StringColumn\",\"TypeConvertedColumn\"\r\n";
			expected += "\"first column\",\"1\",\"string column\",\"test\"\r\n";

			Assert.Equal( expected, csv );
		}

		[TypeConverter( "type name" )]
		private class TestRecord
		{
			[CsvField( Index = 1, Name = "Int Column" )]
			[TypeConverter( typeof( Int32Converter ) )]
			public int IntColumn { get; set; }

			[TypeConverter( "String" )]
			public string StringColumn { get; set; }

			[CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			[CsvField( Index = 0 )]
			public string FirstColumn { get; set; }

			[TypeConverter( typeof( TestTypeConverter ) )]
			public string TypeConvertedColumn { get; set; }
		}

		private class TestRecordNoIndexes
		{
			[CsvField( Name = "Int Column" )]
			[TypeConverter( typeof( Int32Converter ) )]
			public int IntColumn { get; set; }

			[TypeConverter( "String" )]
			public string StringColumn { get; set; }

			[CsvField( Ignore = true )]
			public string IgnoredColumn { get; set; }

			[CsvField]
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

		private class Person
		{
			public string FirstName { get; set; }

			public string LastName { get; set; }

			public Address HomeAddress { get; set; }

			public Address WorkAddress { get; set; }
		}

		private class Address
		{
			public string Street { get; set; }

			public string City { get; set; }

			public string State { get; set; }

			public string Zip { get; set; }
		}

		private sealed class PersonMap : CsvClassMap<Person>
		{
			public PersonMap()
			{
				Map( m => m.FirstName );
				Map( m => m.LastName );
				References<HomeAddressMap>( m => m.HomeAddress );
				References<WorkAddressMap>( m => m.WorkAddress );
			}
		}

		private sealed class HomeAddressMap : CsvClassMap<Address>
		{
			public HomeAddressMap()
			{
				Map( m => m.Street ).Name( "HomeStreet" );
				Map( m => m.City ).Name( "HomeCity" );
				Map( m => m.State ).Name( "HomeState" );
				Map( m => m.Zip ).Name( "HomeZip" );
			}
		}

		private sealed class WorkAddressMap : CsvClassMap<Address>
		{
			public WorkAddressMap()
			{
				Map( m => m.Street ).Name( "WorkStreet" );
				Map( m => m.City ).Name( "WorkCity" );
				Map( m => m.State ).Name( "WorkState" );
				Map( m => m.Zip ).Name( "WorkZip" );
			}
		}

	}
}
