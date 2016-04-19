// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Int32Converter = CsvHelper.TypeConversion.Int32Converter;
#if !PCL
using System.Dynamic;
#endif
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
        [TestInitialize]
        public void TestInitialize()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        }

	    [TestMethod]
	    public void WriteFieldTest()
	    {
		    var stream = new MemoryStream();
		    var writer = new StreamWriter( stream ) { AutoFlush = true };

		    var csv = new CsvWriter( writer );

		    var date = DateTime.Now.ToString();
		    var guid = Guid.NewGuid().ToString();
		    csv.WriteField( "one" );
		    csv.WriteField( "one, two" );
		    csv.WriteField( "one \"two\" three" );
		    csv.WriteField( " one " );
		    csv.WriteField( date );
		    csv.WriteField( (short)1 );
		    csv.WriteField( 2 );
		    csv.WriteField( (long)3 );
		    csv.WriteField( (float)4 );
		    csv.WriteField( (double)5 );
		    csv.WriteField( guid );
		    csv.NextRecord();

		    var reader = new StreamReader( stream );
		    stream.Position = 0;
		    var data = reader.ReadToEnd();

		    Assert.AreEqual( "one,\"one, two\",\"one \"\"two\"\" three\",\" one \"," + date + ",1,2,3,4,5," + guid + "\r\n", data );
	    }

	    [TestMethod]
	    public void WriteEmptyFieldWithExcelLeadingZerosTest()
	    {
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.UseExcelLeadingZerosFormatForNumerics = true;
				csv.WriteField( string.Empty );
			}
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
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<TestRecordMap>();

            csv.WriteRecord(record);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "first column,1,string column,test\r\n";

            Assert.AreEqual(expected, csvFile);
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

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.RegisterClassMap<TestRecordNoIndexesMap>();

                csv.WriteRecord(record);

                stream.Position = 0;
                var reader = new StreamReader(stream);
                var csvFile = reader.ReadToEnd();

                var expected = "1,string column,first column,test\r\n";

                Assert.AreEqual(expected, csvFile);
            }
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
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<TestRecordMap>();

            csv.WriteRecords(records);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
            expected += "first column,1,string column,test\r\n";
            expected += "first column 2,2,string column 2,test\r\n";

            Assert.AreEqual(expected, csvFile);
        }

        [TestMethod]
        public void WriteEmptyArrayTest()
        {
            var records = new TestRecord[] { };

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<TestRecordMap>();

            csv.WriteRecords(records);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";

            Assert.AreEqual(expected, csvFile);
        }

        [TestMethod]
        public void WriteRecordsCalledWithTwoParametersTest()
        {
            var records = new List<object>
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
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<TestRecordMap>();

            csv.WriteRecords(records);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
            expected += "first column,1,string column,test\r\n";
            expected += "first column 2,2,string column 2,test\r\n";

            Assert.AreEqual(expected, csvFile);
        }

        [TestMethod]
        public void WriteRecordNoHeaderTest()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var csv = new CsvWriter(writer) { Configuration = { HasHeaderRecord = false } };
            csv.Configuration.RegisterClassMap<TestRecordMap>();
            csv.WriteRecord(new TestRecord());

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();

            Assert.AreEqual(",0,,test\r\n", csvFile);
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
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<TestRecordMap>();

            csv.WriteRecord(record);
            csv.WriteRecord((TestRecord)null);
            csv.WriteRecord(record);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "first column,1,string column,test\r\n";
            expected += ",,,\r\n";
            expected += "first column,1,string column,test\r\n";

            Assert.AreEqual(expected, csvFile);
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
            var csv = new CsvWriter(writer);
            csv.Configuration.RegisterClassMap<PersonMap>();

            csv.WriteRecord(record);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();

            var expected = "First Name,Last Name,Home Street,Home City,Home State,Home Zip,Work Street,Work City,Work State,Work Zip\r\n";

            Assert.AreEqual(expected, csvFile);
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
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvWriter = new CsvWriter(writer))
            {
                csvWriter.Configuration.QuoteAllFields = true;
                csvWriter.Configuration.RegisterClassMap<TestRecordMap>();
                csvWriter.WriteRecord(record);

                writer.Flush();
                stream.Position = 0;

                csv = reader.ReadToEnd();
            }

            var expected = "\"first column\",\"1\",\"string column\",\"test\"\r\n";

            Assert.AreEqual(expected, csv);
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
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvWriter = new CsvWriter(writer))
            {
                csvWriter.Configuration.QuoteNoFields = true;
                csvWriter.Configuration.RegisterClassMap<TestRecordMap>();
                csvWriter.WriteRecord(record);

                writer.Flush();
                stream.Position = 0;

                csv = reader.ReadToEnd();
            }

            var expected = "first, column,1,string \" column,test\r\n";

            Assert.AreEqual(expected, csv);
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
                csvWriter.Configuration.RegisterClassMap<TestRecordMap>();
                csvWriter.WriteHeader(typeof(TestRecord));

                writer.Flush();
                stream.Position = 0;

                csv = reader.ReadToEnd();
            }

            const string Expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
            Assert.AreEqual(Expected, csv);
        }

        [TestMethod]
        public void WriteHeaderFailsIfHasHeaderRecordIsNotConfiguredTest()
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var csvWriter = new CsvWriter(writer))
            {
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.Configuration.RegisterClassMap<TestRecordMap>();
                try
                {
                    csvWriter.WriteHeader(typeof(TestRecord));
                    Assert.Fail();
                }
                catch (CsvWriterException)
                {
                }
            }
        }

        [TestMethod]
        public void WriteRecordWithDelimiterInFieldTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                var record = new TestSinglePropertyRecord
                {
                    Name = "one,two"
                };
                csv.WriteRecord(record);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("\"one,two\"\r\n", text);
            }
        }

        [TestMethod]
        public void WriteRecordWithQuoteInFieldTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                var record = new TestSinglePropertyRecord
                {
                    Name = "one\"two"
                };
                csv.WriteRecord(record);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("\"one\"\"two\"\r\n", text);
            }
        }

        [TestMethod]
        public void WriteRecordWithQuoteAllFieldsOnAndDelimiterInFieldTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.QuoteAllFields = true;
                var record = new TestSinglePropertyRecord
                {
                    Name = "one,two"
                };
                csv.WriteRecord(record);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("\"one,two\"\r\n", text);
            }
        }

        [TestMethod]
        public void WriteRecordWithQuoteAllFieldsOnAndQuoteInFieldTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.QuoteAllFields = true;
                var record = new TestSinglePropertyRecord
                {
                    Name = "one\"two"
                };
                csv.WriteRecord(record);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("\"one\"\"two\"\r\n", text);
            }
        }

        [TestMethod]
        public void WriteRecordsWithInvariantCultureTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.CultureInfo = CultureInfo.InvariantCulture;
                csv.Configuration.RegisterClassMap<TestRecordMap>();

                var record = new TestRecord
                {
                    IntColumn = 1,
                    FirstColumn = "first column",
                    IgnoredColumn = "ignored column",
                    StringColumn = "string column",
                };

                csv.WriteRecord(record);
                writer.Flush();
                stream.Position = 0;

                var csvString = reader.ReadToEnd();
            }
        }

        [TestMethod]
        public void WriteNoGetterTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                var list = new List<TestPrivateGet>
				{
					new TestPrivateGet
					{
						Id = 1,
						Name = "one"
					}
				};
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var data = reader.ReadToEnd();
                var expected = new StringBuilder();
                expected.AppendLine("Id");
                expected.AppendLine("1");

                Assert.AreEqual(expected.ToString(), data);
            }
        }

        [TestMethod]
        public void WriteDynamicTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.WriteRecord(new { Id = 1, Name = "one" });
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();
                Assert.AreEqual("1,one\r\n", text);
            }
        }

        [TestMethod]
        public void WritePrimitivesRecordsHasHeaderTrueTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.HasHeaderRecord = true;
                var list = new List<int> { 1, 2, 3 };
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("1\r\n2\r\n3\r\n", text);
            }
        }

        [TestMethod]
        public void WritePrimitivesRecordsHasHeaderFalseTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.HasHeaderRecord = false;
                var list = new List<int> { 1, 2, 3 };
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("1\r\n2\r\n3\r\n", text);
            }
        }

        [TestMethod]
        public void WriteStructRecordsTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                var list = new List<TestStruct>
				{
					new TestStruct { Id = 1, Name = "one" },
					new TestStruct { Id = 2, Name = "two" },
				};
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();

                Assert.AreEqual("Id,Name\r\n1,one\r\n2,two\r\n", text);
            }
        }

        [TestMethod]
        public void WriteStructReferenceRecordsTest()
        {
            var list = new List<TestStructParent>
			{
				new TestStructParent
				{
					Test = new TestStruct
					{
						Id = 1,
						Name = "one",
					},
				},
			};

            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.RegisterClassMap<TestStructParentMap>();
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var data = reader.ReadToEnd();
                Assert.AreEqual("Id,Name\r\n1,one\r\n", data);
            }
        }

        [TestMethod]
        public void WriteNestedHeadersTest()
        {
            var list = new List<Person>
			{
				new Person
				{
					FirstName = "first",
					LastName = "last",
					HomeAddress = new Address
					{
						City = "home city",
						State = "home state",
						Street = "home street",
						Zip = "home zip",
					},
					WorkAddress = new Address
					{
						City = "work city",
						State = "work state",
						Street = "work street",
						Zip = "work zip",
					},
				},
			};

            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                csv.Configuration.PrefixReferenceHeaders = true;
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var text = reader.ReadToEnd();
                var expected = new StringBuilder();
                expected.AppendLine("FirstName,LastName,HomeAddress.Street,HomeAddress.City,HomeAddress.State,HomeAddress.Zip,WorkAddress.Street,WorkAddress.City,WorkAddress.State,WorkAddress.Zip");
                expected.AppendLine("first,last,home street,home city,home state,home zip,work street,work city,work state,work zip");
                Assert.AreEqual(expected.ToString(), text);
            }
        }

        [TestMethod]
        public void WriteEmptyListTest()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer))
            {
                var list = new List<TestRecord>();
                csv.WriteRecords(list);
                writer.Flush();
                stream.Position = 0;

                var data = reader.ReadToEnd();
                Assert.IsFalse(string.IsNullOrWhiteSpace(data));
            }
        }

		[TestMethod]
	    public void ClassWithStaticAutoMappingTest()
	    {
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				TestWithStatic.Name = "one";
				var records = new List<TestWithStatic>
				{
					new TestWithStatic
					{
						Id = 1
					},
				};

				csv.WriteRecords( records );
			}
	    }

		[TestMethod]
		public void ClassWithStaticUsingMappingTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<TestWithStaticMap>();

				TestWithStatic.Name = "one";
				var records = new List<TestWithStatic>
				{
					new TestWithStatic
					{
						Id = 1
					},
				};

				csv.WriteRecords( records );
			}
		}

#if !PCL
		[TestMethod]
		public void WriteDynamicListTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<dynamic>();
				dynamic record = new { Id = 1, Name = "one" };
				list.Add( record );
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.AreEqual( "Id,Name\r\n1,one\r\n", text );
			}
		}

		// TODO: Make reader/writer work with expando objects.
		[Ignore]
		[TestMethod]
		public void WriteExpandoListTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<dynamic>();
				dynamic record = new ExpandoObject();
				record.Id = 1;
				record.Name = "one";
				list.Add( record );
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.AreEqual( "Id,Name\r\n1,one\r\n", text );
			}
		}
#endif

	    private class TestWithStatic
	    {
			public int Id { get; set; }

			public static string Name { get; set; }
	    }

	    private sealed class TestWithStaticMap : CsvClassMap<TestWithStatic>
	    {
		    public TestWithStaticMap()
		    {
			    Map( m => m.Id );
		    }
	    }

        private class TestStructParent
        {
            public TestStruct Test { get; set; }
        }

        private sealed class TestStructParentMap : CsvClassMap<TestStructParent>
        {
            public TestStructParentMap()
            {
                References<TestStructMap>(m => m.Test);
            }
        }

        private struct TestStruct
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class TestStructMap : CsvClassMap<TestStruct>
        {
            public TestStructMap()
            {
                Map(m => m.Id);
                Map(m => m.Name);
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
                Map(m => m.IntColumn).Name("Int Column").Index(1).TypeConverter<Int32Converter>();
                Map(m => m.StringColumn);
                Map(m => m.FirstColumn).Index(0);
                Map(m => m.TypeConvertedColumn).TypeConverter<TestTypeConverter>();
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
                Map(m => m.IntColumn).Name("Int Column").TypeConverter<Int32Converter>();
                Map(m => m.StringColumn);
                Map(m => m.FirstColumn);
                Map(m => m.TypeConvertedColumn).TypeConverter<TestTypeConverter>();
            }
        }

        private class TestTypeConverter : ITypeConverter
        {
            public string ConvertToString(TypeConverterOptions options, object value)
            {
                return "test";
            }

            public object ConvertFromString(TypeConverterOptions options, string text)
            {
                throw new NotImplementedException();
            }

            public bool CanConvertFrom(Type type)
            {
                throw new NotImplementedException();
            }

            public bool CanConvertTo(Type type)
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
                Map(m => m.FirstName);
                Map(m => m.LastName);
                References<HomeAddressMap>(m => m.HomeAddress);
                References<WorkAddressMap>(m => m.WorkAddress);
            }
        }

        private sealed class HomeAddressMap : CsvClassMap<Address>
        {
            public HomeAddressMap()
            {
                Map(m => m.Street).Name("HomeStreet");
                Map(m => m.City).Name("HomeCity");
                Map(m => m.State).Name("HomeState");
                Map(m => m.Zip).Name("HomeZip");
            }
        }

        private sealed class WorkAddressMap : CsvClassMap<Address>
        {
            public WorkAddressMap()
            {
                Map(m => m.Street).Name("WorkStreet");
                Map(m => m.City).Name("WorkCity");
                Map(m => m.State).Name("WorkState");
                Map(m => m.Zip).Name("WorkZip");
            }
        }

    }
}
