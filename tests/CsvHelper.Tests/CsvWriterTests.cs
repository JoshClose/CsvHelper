// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;
using Int32Converter = CsvHelper.TypeConversion.Int32Converter;

namespace CsvHelper.Tests
{

	public class CsvWriterTests
	{
		public CsvWriterTests()
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
		}

		[Fact]
		public void WriteFieldTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream) { AutoFlush = true };

			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

			var date = DateTime.Now.ToString();
			var guid = Guid.NewGuid().ToString();
			csv.WriteField("one");
			csv.WriteField("one, two");
			csv.WriteField("one \"two\" three");
			csv.WriteField(" one ");
			csv.WriteField(date);
			csv.WriteField((short)1);
			csv.WriteField(2);
			csv.WriteField((long)3);
			csv.WriteField((float)4);
			csv.WriteField((double)5);
			csv.WriteField(guid);
			csv.NextRecord();

			var reader = new StreamReader(stream);
			stream.Position = 0;
			var data = reader.ReadToEnd();

			Assert.Equal("one,\"one, two\",\"one \"\"two\"\" three\",\" one \"," + date + ",1,2,3,4,5," + guid + "\r\n", data);
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
			var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<TestRecordMap>();

			csv.WriteRecord(record);
			csv.NextRecord();

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();
			var expected = "first column,1,string column,test\r\n";

			Assert.Equal(expected, csvFile);
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

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream) { AutoFlush = true })
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<TestRecordNoIndexesMap>();

				csv.WriteRecord(record);
				csv.NextRecord();

				stream.Position = 0;
				var reader = new StreamReader(stream);
				var csvFile = reader.ReadToEnd();

				var expected = "1,string column,first column,test\r\n";

				Assert.Equal(expected, csvFile);
			}
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
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<TestRecordMap>();

			csv.WriteRecords(records);

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";
			expected += "first column 2,2,string column 2,test\r\n";

			Assert.Equal(expected, csvFile);
		}

		[Fact]
		public void WriteEmptyArrayTest()
		{
			var records = new TestRecord[] { };

			var stream = new MemoryStream();
			var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<TestRecordMap>();

			csv.WriteRecords(records);

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";

			Assert.Equal(expected, csvFile);
		}

		[Fact]
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
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<TestRecordMap>();

			csv.WriteRecords(records);

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();
			var expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			expected += "first column,1,string column,test\r\n";
			expected += "first column 2,2,string column 2,test\r\n";

			Assert.Equal(expected, csvFile);
		}

		[Fact]
		public void WriteRecordNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream) { AutoFlush = true };
			var csv = new CsvWriter(writer, config);
			csv.Context.RegisterClassMap<TestRecordMap>();
			csv.WriteRecord(new TestRecord());
			csv.NextRecord();

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();

			Assert.Equal(",0,,test\r\n", csvFile);
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
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<TestRecordMap>();

			csv.WriteRecord(record);
			csv.NextRecord();
			csv.WriteRecord((TestRecord)null);
			csv.NextRecord();
			csv.WriteRecord(record);
			csv.NextRecord();

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();
			var expected = new TestStringBuilder(csv.Configuration.NewLine);
			expected.AppendLine("first column,1,string column,test");
			expected.AppendLine(",,,");
			expected.AppendLine("first column,1,string column,test");

			Assert.Equal(expected, csvFile);
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
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
			csv.Context.RegisterClassMap<PersonMap>();

			csv.WriteRecord(record);
			csv.NextRecord();

			stream.Position = 0;
			var reader = new StreamReader(stream);
			var csvFile = reader.ReadToEnd();

			var expected = "First Name,Last Name,Home Street,Home City,Home State,Home Zip,Work Street,Work City,Work State,Work Zip\r\n";

			Assert.Equal(expected, csvFile);
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

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => true,
			};

			string csv;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, config))
			{
				csvWriter.Context.RegisterClassMap<TestRecordMap>();
				csvWriter.WriteRecord(record);
				csvWriter.NextRecord();

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = "\"first column\",\"1\",\"string column\",\"test\"\r\n";

			Assert.Equal(expected, csv);
		}

		[Fact]
		public void WriteRecordsNoFieldsQuotedTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				StringColumn = "string \" column",
				IgnoredColumn = "ignored column",
				FirstColumn = "first, column",
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => false,
			};
			string csv;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, config))
			{
				csvWriter.Context.RegisterClassMap<TestRecordMap>();
				csvWriter.WriteRecord(record);
				csvWriter.NextRecord();

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = "first, column,1,string \" column,test\r\n";

			Assert.Equal(expected, csv);
		}

		[Fact]
		public void WriteHeaderTest()
		{
			string csv;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csvWriter.Context.RegisterClassMap<TestRecordMap>();
				csvWriter.WriteHeader(typeof(TestRecord));
				csvWriter.NextRecord();

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			const string Expected = "FirstColumn,Int Column,StringColumn,TypeConvertedColumn\r\n";
			Assert.Equal(Expected, csv);
		}

		[Fact]
		public void WriteRecordWithDelimiterInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one,two"
				};
				csv.WriteRecord(record);
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("\"one,two\"\r\n", text);
			}
		}

		[Fact]
		public void WriteRecordWithQuoteInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one\"two"
				};
				csv.WriteRecord(record);
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("\"one\"\"two\"\r\n", text);
			}
		}

		[Fact]
		public void WriteRecordWithQuoteAllFieldsOnAndDelimiterInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one,two"
				};
				csv.WriteRecord(record);
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("\"one,two\"\r\n", text);
			}
		}

		[Fact]
		public void WriteRecordWithQuoteAllFieldsOnAndQuoteInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var record = new TestSinglePropertyRecord
				{
					Name = "one\"two"
				};
				csv.WriteRecord(record);
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("\"one\"\"two\"\r\n", text);
			}
		}

		[Fact]
		public void WriteRecordsWithInvariantCultureTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<TestRecordMap>();

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

		[Fact]
		public void WriteNoGetterTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
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
				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.Equal(expected.ToString(), data);
			}
		}

		[Fact]
		public void WriteDynamicTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecord(new { Id = 1, Name = "one" });
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.Equal("1,one\r\n", text);
			}
		}

		[Fact]
		public void WritePrimitivesRecordsHasHeaderTrueTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<int> { 1, 2, 3 };
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("1\r\n2\r\n3\r\n", text);
			}
		}

		[Fact]
		public void WritePrimitivesRecordsHasHeaderFalseTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<int> { 1, 2, 3 };
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				Assert.Equal("1\r\n2\r\n3\r\n", text);
			}
		}

		[Fact]
		public void WriteStructRecordsTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
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

				Assert.Equal("Id,Name\r\n1,one\r\n2,two\r\n", text);
			}
		}

		[Fact]
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
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<TestStructParentMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				Assert.Equal("Id,Name\r\n1,one\r\n", data);
			}
		}

		[Fact]
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

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReferenceHeaderPrefix = args => $"{args.MemberName}."
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("FirstName,LastName,HomeAddress.Street,HomeAddress.City,HomeAddress.State,HomeAddress.Zip,WorkAddress.Street,WorkAddress.City,WorkAddress.State,WorkAddress.Zip");
				expected.AppendLine("first,last,home street,home city,home state,home zip,work street,work city,work state,work zip");
				Assert.Equal(expected.ToString(), text);
			}
		}

		[Fact]
		public void WriteEmptyListTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<TestRecord>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				Assert.False(string.IsNullOrWhiteSpace(data));
			}
		}

		[Fact]
		public void ClassWithStaticAutoMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				TestWithStatic.Name = "one";
				var records = new List<TestWithStatic>
				{
					new TestWithStatic
					{
						Id = 1
					},
				};

				csv.WriteRecords(records);
			}
		}

		[Fact]
		public void ClassWithStaticUsingMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<TestWithStaticMap>();

				TestWithStatic.Name = "one";
				var records = new List<TestWithStatic>
				{
					new TestWithStatic
					{
						Id = 1
					},
				};

				csv.WriteRecords(records);
			}
		}

		[Fact]
		public void WriteDynamicListTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<dynamic>();
				dynamic record = new { Id = 1, Name = "one" };
				list.Add(record);
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.Equal("Id,Name\r\n1,one\r\n", text);
			}
		}

		[Fact]
		public void WriteExpandoListTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<dynamic>();
				dynamic record = new ExpandoObject();
				record.Id = 1;
				record.Name = "one";
				list.Add(record);
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.Equal("Id,Name\r\n1,one\r\n", text);
			}
		}


		[Fact]
		public void WriteInternalConstructorClassTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(new List<GetOnly>());
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				Assert.Equal("One\r\n", text);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WriteRecords_NonGeneric_HasHeaderRecord(bool hasHeaderRecord)
		{
			var confg = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = hasHeaderRecord
			};

			using var stringWriter = new StringWriter();
			using var csvWriter = new CsvWriter(stringWriter, confg);

			IEnumerable records = new object[] {
				new TestSinglePropertyRecord { Name = "test" },
				new TestRecord { IntColumn = 4 }
			};

			csvWriter.WriteRecords(records);
			var csv = stringWriter.ToString();

			if (hasHeaderRecord)
			{
				Assert.Equal("Name\r\ntest\r\n4,,,,\r\n", csv);
			}
			else
			{
				Assert.Equal("test\r\n4,,,,\r\n", csv);
			}
		}

		private class GetOnly
		{
			internal GetOnly(string someParam)
			{
			}

			public int One { get; }
		}

		private class TestWithStatic
		{
			public int Id { get; set; }

			public static string Name { get; set; }
		}

		private sealed class TestWithStaticMap : ClassMap<TestWithStatic>
		{
			public TestWithStaticMap()
			{
				Map(m => m.Id);
			}
		}

		private class TestStructParent
		{
			public TestStruct Test { get; set; }
		}

		private sealed class TestStructParentMap : ClassMap<TestStructParent>
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

		private sealed class TestStructMap : ClassMap<TestStruct>
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

		private sealed class TestRecordMap : ClassMap<TestRecord>
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

		private sealed class TestRecordNoIndexesMap : ClassMap<TestRecordNoIndexes>
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
			public string ConvertToString(object value, IWriterRow row, MemberMapData propertyMapData)
			{
				return "test";
			}

			public object ConvertFromString(string text, IReaderRow row, MemberMapData propertyMapData)
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

		private sealed class PersonMap : ClassMap<Person>
		{
			public PersonMap()
			{
				Map(m => m.FirstName);
				Map(m => m.LastName);
				References<HomeAddressMap>(m => m.HomeAddress);
				References<WorkAddressMap>(m => m.WorkAddress);
			}
		}

		private sealed class HomeAddressMap : ClassMap<Address>
		{
			public HomeAddressMap()
			{
				Map(m => m.Street).Name("HomeStreet");
				Map(m => m.City).Name("HomeCity");
				Map(m => m.State).Name("HomeState");
				Map(m => m.Zip).Name("HomeZip");
			}
		}

		private sealed class WorkAddressMap : ClassMap<Address>
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
