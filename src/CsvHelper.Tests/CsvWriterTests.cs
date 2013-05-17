// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterTests
	{
		[TestMethod]
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
			var writer = new StreamWriter( stream ) { AutoFlush = true };
			var csv = new CsvWriter( writer );
			csv.Configuration.ClassMapping<TestRecordMap>();

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "first column,1,string column,test\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
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
			csv.Configuration.ClassMapping<TestRecordNoIndexesMap>();

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			var expected = "1,string column,first column,test\r\n";

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
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer );
			csv.Configuration.ClassMapping<TestRecordMap>();

			csv.WriteRecords( records );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";
			expected += "first column 2,2,string column 2,test\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
		public void WriteRecordNoHeaderTest()
		{
			var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter( writer ) { Configuration = { HasHeaderRecord = false } };
			csv.Configuration.ClassMapping<TestRecordMap>();
			csv.WriteRecord( new TestRecord() );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			Assert.AreEqual( ",0,,test\r\n", csvFile );
		}

		[TestMethod]
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
			csv.Configuration.ClassMapping<TestRecordMap>();

			csv.WriteRecord( record );
			csv.WriteRecord( (TestRecord)null );
			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "first column,1,string column,test\r\n";
			expected += ",,,\r\n";
			expected += "first column,1,string column,test\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
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

			var expected = "First Name,Last Name,Home Street,Home City,Home State,Home Zip,Work Street,Work City,Work State,Work Zip\r\n";

			Assert.AreEqual( expected, csvFile );

		}

		[TestMethod]
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
				csvWriter.Configuration.ClassMapping<TestRecordMap>();
				csvWriter.WriteRecord( record );

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = "\"first column\",\"1\",\"string column\",\"test\"\r\n";

			Assert.AreEqual( expected, csv );
		}

		[TestMethod]
		public void WriteRecordsNoFieldsQuotedTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string \" column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first, column",
			};

			string csv;
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.QuoteNoFields = true;
				csvWriter.Configuration.ClassMapping<TestRecordMap>();
				csvWriter.WriteRecord( record );

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = "first, column,1,string \" column,test\r\n";

			Assert.AreEqual( expected, csv );
		}

        [TestMethod]
        public void WriteHeaderTest()
        {
            string csv;
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvWriter = new CsvWriter(writer))
            {
                csvWriter.Configuration.HasHeaderRecord = true;
	            csvWriter.Configuration.ClassMapping<TestRecordMap>();
                csvWriter.WriteHeader(typeof(TestRecord));

                writer.Flush();
                stream.Position = 0;

                csv = reader.ReadToEnd();
            }

            const string Expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
            Assert.AreEqual( Expected, csv );
        }

		[TestMethod]
		public void WriteHeaderFailsIfHasHeaderRecordIsNotConfiguredTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.HasHeaderRecord = false;
				csvWriter.Configuration.ClassMapping<TestRecordMap>();
				try
				{
					csvWriter.WriteHeader( typeof( TestRecord ) );
					Assert.Fail();
				}
				catch( CsvWriterException )
				{
				}
			}
		}

		[TestMethod]
		public void WriteRecordWithDelimiterInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one,two"
				};
				csv.WriteRecord( record );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "\"one,two\"\r\n", text );
			}
		}

		[TestMethod]
		public void WriteRecordWithQuoteInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one\"two"
				};
				csv.WriteRecord( record );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "\"one\"\"two\"\r\n", text );
			}
		}

		[TestMethod]
		public void WriteRecordWithQuoteAllFieldsOnAndDelimiterInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.QuoteAllFields = true;
				var record = new TestSinglePropertyRecord
				{
					Name = "one,two"
				};
				csv.WriteRecord( record );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "\"one,two\"\r\n", text );
			}
		}

		[TestMethod]
		public void WriteRecordWithQuoteAllFieldsOnAndQuoteInFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.QuoteAllFields = true;
				var record = new TestSinglePropertyRecord
				{
					Name = "one\"two"
				};
				csv.WriteRecord( record );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.AreEqual( "\"one\"\"two\"\r\n", text );
			}
		}

		[TestMethod]
		public void WriteRecordsWithInvariantCultureTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.CultureInfo = CultureInfo.InvariantCulture;
				csv.Configuration.ClassMapping<TestRecordMap>();

				var record = new TestRecord
				{
					IntColumn = 1,
					FirstColumn = "first column",
					IgnoredColumn = "ignored column",
					StringColumn = "string column",
				};

				csv.WriteRecord( record );
				writer.Flush();
				stream.Position = 0;

				var csvString = reader.ReadToEnd();
			}
		}

		[TestMethod]
		public void WriteNoGetterTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<TestPrivateGet>
				{
					new TestPrivateGet
					{
						Id = 1,
						Name = "one"
					}
				};
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = new StringBuilder();
				expected.AppendLine( "Id" );
				expected.AppendLine( "1" );

				Assert.AreEqual( expected.ToString(), data );
			}
		}

		[TestMethod]
		public void WriteDynamicTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.WriteRecord( new { Id = 1, Name = "one" } );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.AreEqual( "1,one\r\n", text );
			}
		}

		private class TestPrivateGet
		{
			public int Id { get; set; }

			public string Name { private get; set; }
		}

		private class TestSinglePropertyRecord
		{
			public string Name { get; set; }
		}

		private class TestRecord
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public string IgnoredColumn { get; set; }

			public string FirstColumn { get; set; }

			public string TypeConvertedColumn { get; set; }
		}

		private sealed class TestRecordMap : CsvClassMap<TestRecord>
		{
			public TestRecordMap()
			{
				Map( m => m.IntColumn ).Name( "Int Column" ).Index( 1 ).TypeConverter<Int32Converter>();
				Map( m => m.StringColumn );
				Map( m => m.FirstColumn ).Index( 0 );
				Map( m => m.TypeConvertedColumn ).TypeConverter<TestTypeConverter>();
			}
		}

		private class TestRecordNoIndexes
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public string IgnoredColumn { get; set; }

			public string FirstColumn { get; set; }

			public string TypeConvertedColumn { get; set; }
		}

		private sealed class TestRecordNoIndexesMap : CsvClassMap<TestRecordNoIndexes>
		{
			public TestRecordNoIndexesMap()
			{
				Map( m => m.IntColumn ).Name( "Int Column" ).TypeConverter<Int32Converter>();
				Map( m => m.StringColumn );
				Map( m => m.FirstColumn );
				Map( m => m.TypeConvertedColumn ).TypeConverter<TestTypeConverter>();
			}
		}

		private class TestTypeConverter : ITypeConverter
		{
			public string ConvertToString( TypeConverterOptions options, object value )
			{
				return "test";
			}

			public object ConvertFromString( TypeConverterOptions options, string text )
			{
				throw new NotImplementedException();
			}

			public bool CanConvertFrom( Type type )
			{
				throw new NotImplementedException();
			}

			public bool CanConvertTo( Type type )
			{
				return true;
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
