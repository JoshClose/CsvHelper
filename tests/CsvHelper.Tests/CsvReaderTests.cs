// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#pragma warning disable 649

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderTests
	{
		[TestMethod]
		public void HasHeaderRecordNotReadExceptionTest()
		{
			var parserMock = new ParserMock();
			var reader = new CsvReader(parserMock);

			Assert.ThrowsException<ReaderException>(() => reader.GetField<int>(0));
		}

		[TestMethod]
		public void HasHeaderRecordTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two" },
				{ "1", "2" },
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			// Check to see if the header record and first record are set properly.
			Assert.AreEqual(Convert.ToInt32("1"), reader.GetField<int>("One"));
			Assert.AreEqual(Convert.ToInt32("2"), reader.GetField<int>("Two"));
			Assert.AreEqual(Convert.ToInt32("1"), reader.GetField<int>(0));
			Assert.AreEqual(Convert.ToInt32("2"), reader.GetField<int>(1));
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

			var parserMock = new ParserMock();
			parserMock.Add(data);
			parserMock.Add(data);
			parserMock.Add(null);

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.AreEqual(Convert.ToInt16(data[0]), reader.GetField<short>(0));
			Assert.AreEqual(Convert.ToInt16(data[0]), reader.GetField<short?>(0));
			Assert.AreEqual(null, reader.GetField<short?>(5));
			Assert.AreEqual(Convert.ToInt32(data[0]), reader.GetField<int>(0));
			Assert.AreEqual(Convert.ToInt32(data[0]), reader.GetField<int?>(0));
			Assert.AreEqual(null, reader.GetField<int?>(5));
			Assert.AreEqual(Convert.ToInt64(data[0]), reader.GetField<long>(0));
			Assert.AreEqual(Convert.ToInt64(data[0]), reader.GetField<long?>(0));
			Assert.AreEqual(null, reader.GetField<long?>(5));
			Assert.AreEqual(Convert.ToDecimal(data[0]), reader.GetField<decimal>(0));
			Assert.AreEqual(Convert.ToDecimal(data[0]), reader.GetField<decimal?>(0));
			Assert.AreEqual(null, reader.GetField<decimal?>(5));
			Assert.AreEqual(Convert.ToSingle(data[0]), reader.GetField<float>(0));
			Assert.AreEqual(Convert.ToSingle(data[0]), reader.GetField<float?>(0));
			Assert.AreEqual(null, reader.GetField<float?>(5));
			Assert.AreEqual(Convert.ToDouble(data[0]), reader.GetField<double>(0));
			Assert.AreEqual(Convert.ToDouble(data[0]), reader.GetField<double?>(0));
			Assert.AreEqual(null, reader.GetField<double?>(5));
			Assert.AreEqual(data[1], reader.GetField<string>(1));
			Assert.AreEqual(string.Empty, reader.GetField<string>(5));
			Assert.AreEqual(Convert.ToDateTime(data[2]), reader.GetField<DateTime>(2));
			Assert.AreEqual(Convert.ToDateTime(data[2]), reader.GetField<DateTime?>(2));
			Assert.AreEqual(null, reader.GetField<DateTime?>(5));
			Assert.AreEqual(Convert.ToBoolean(data[3]), reader.GetField<bool>(3));
			Assert.AreEqual(Convert.ToBoolean(data[3]), reader.GetField<bool?>(3));
			Assert.AreEqual(null, reader.GetField<bool?>(5));
			Assert.AreEqual(Convert.ToChar(data[4]), reader.GetField<char>(4));
			Assert.AreEqual(Convert.ToChar(data[4]), reader.GetField<char?>(4));
			Assert.AreEqual(null, reader.GetField<char?>(5));
			Assert.AreEqual(new Guid(data[6]), reader.GetField<Guid>(6));
			Assert.AreEqual(null, reader.GetField<Guid?>(5));
		}

		[TestMethod]
		public void GetFieldByIndexTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.AreEqual(1, reader.GetField<int>(0));
			Assert.AreEqual(2, reader.GetField<int>(1));
		}

		[TestMethod]
		public void GetFieldByNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var parserMock = new ParserMock();
			parserMock.Add(data1);
			parserMock.Add(data2);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>("One"));
			Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>("Two"));
		}

		[TestMethod]
		public void GetFieldByNameAndIndexTest()
		{
			var data1 = new[] { "One", "One" };
			var data2 = new[] { "1", "2" };
			var parserMock = new ParserMock();
			parserMock.Add(data1);
			parserMock.Add(data2);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			Assert.AreEqual(Convert.ToInt32(data2[0]), reader.GetField<int>("One", 0));
			Assert.AreEqual(Convert.ToInt32(data2[1]), reader.GetField<int>("One", 1));
		}

		[TestMethod]
		public void GetMissingFieldByNameTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var parserMock = new ParserMock(config);
			parserMock.Add(data1);
			parserMock.Add(data2);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();

			Assert.IsNull(reader.GetField<string>("blah"));
		}

		[TestMethod]
		public void GetMissingFieldByNameStrictTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two" },
				{ "1", "2" },
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();

			Assert.ThrowsException<MissingFieldException>(() => reader.GetField<string>("blah"), $"Field with name 'blah' does not exist. You can ignore missing fields by setting {nameof(reader.Configuration.MissingFieldFound)} to null.");
		}

		[TestMethod]
		public void GetMissingFieldByIndexStrictTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two" },
				{ "1", "2" },
				null,
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.ThrowsException<MissingFieldException>(() => reader.GetField(2), $"Field at index '2' does not exist. You can ignore missing fields by setting {nameof(reader.Configuration.MissingFieldFound)} to null.");
		}

		[TestMethod]
		public void GetMissingFieldGenericByIndexStrictTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two" },
				{ "1", "2" },
				null,
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.ThrowsException<MissingFieldException>(() => reader.GetField<string>(2), $"Field at index '2' does not exist. You can ignore missing fields by setting {nameof(reader.Configuration.MissingFieldFound)} to null.");
		}

		[TestMethod]
		public void GetMissingFieldByIndexStrictOffTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			var parserMock = new ParserMock(config)
			{
				{ "One", "Two" },
				{ "1", "2" },
				null,
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.IsNull(reader.GetField(2));
		}

		[TestMethod]
		public void GetMissingFieldGenericByIndexStrictOffTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			var parserMock = new ParserMock(config)
			{
				{ "One", "Two" },
				{ "1", "2" },
				null,
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.IsNull(reader.GetField<string>(2));
		}

		[TestMethod]
		public void GetFieldByNameNoHeaderExceptionTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" }
			};

			var reader = new CsvReader(parserMock);
			reader.Read();

			Assert.ThrowsException<ReaderException>(() => reader.GetField<int>("One"));
		}

		[TestMethod]
		public void GetRecordWithDuplicateHeaderFields()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			var parserMock = new ParserMock(config)
			{
				{ "Field1", "Field1" },
				{ "Field1", "Field1" },
			};

			var reader = new CsvReader(parserMock);
			reader.Read();
		}

		[TestMethod]
		public void GetRecordGenericTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
				HeaderValidated = null,
			};
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
			var csvParserMock = new ParserMock(config)
			{
				headerData,
				recordData,
				null,
			};

			var csv = new CsvReader(csvParserMock);
			csv.Context.RegisterClassMap<TestRecordMap>();
			csv.Read();
			var record = csv.GetRecord<TestRecord>();

			Assert.AreEqual(Convert.ToInt32(recordData[0]), record.IntColumn);
			Assert.AreEqual(recordData[1], record.StringColumn);
			Assert.AreEqual("test", record.TypeConvertedColumn);
			Assert.AreEqual(Convert.ToInt32(recordData[0]), record.FirstColumn);
			Assert.AreEqual(new Guid(recordData[2]), record.GuidColumn);
		}

		[TestMethod]
		public void GetRecordTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
				HeaderValidated = null,
			};
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
			var csvParserMock = new ParserMock(config)
			{
				headerData,
				recordData,
				null,
			};

			var csv = new CsvReader(csvParserMock);
			csv.Context.RegisterClassMap<TestRecordMap>();
			csv.Read();
			var record = (TestRecord)csv.GetRecord(typeof(TestRecord));

			Assert.AreEqual(Convert.ToInt32(recordData[0]), record.IntColumn);
			Assert.AreEqual(recordData[1], record.StringColumn);
			Assert.AreEqual("test", record.TypeConvertedColumn);
			Assert.AreEqual(Convert.ToInt32(recordData[0]), record.FirstColumn);
			Assert.AreEqual(new Guid(recordData[2]), record.GuidColumn);
		}

		[TestMethod]
		public void GetRecordsGenericTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
				HeaderValidated = null,
			};
			var headerData = new[]
			{
				"IntColumn",
				"String Column",
				"GuidColumn",
			};
			var guid = Guid.NewGuid();
			var csvParserMock = new ParserMock(config)
			{
				headerData,
				{ "1", "string column 1", guid.ToString() },
				{ "2", "string column 2", guid.ToString() },
			};

			var csv = new CsvReader(csvParserMock);
			csv.Context.RegisterClassMap<TestRecordMap>();
			var records = csv.GetRecords<TestRecord>().ToList();

			Assert.AreEqual(2, records.Count);

			for (var i = 1; i <= records.Count; i++)
			{
				var record = records[i - 1];
				Assert.AreEqual(i, record.IntColumn);
				Assert.AreEqual("string column " + i, record.StringColumn);
				Assert.AreEqual("test", record.TypeConvertedColumn);
				Assert.AreEqual(i, record.FirstColumn);
				Assert.AreEqual(guid, record.GuidColumn);
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
				HeaderValidated = null,
			};
			var headerData = new[]
			{
				"IntColumn",
				"String Column",
				"GuidColumn",
			};
			var guid = Guid.NewGuid();
			var csvParserMock = new ParserMock(config)
			{
				headerData,
				{ "1", "string column 1", guid.ToString() },
				{ "2", "string column 2", guid.ToString() },
			};

			var csv = new CsvReader(csvParserMock);
			csv.Context.RegisterClassMap<TestRecordMap>();
			var records = csv.GetRecords(typeof(TestRecord)).ToList();

			Assert.AreEqual(2, records.Count);

			for (var i = 1; i <= records.Count; i++)
			{
				var record = (TestRecord)records[i - 1];
				Assert.AreEqual(i, record.IntColumn);
				Assert.AreEqual("string column " + i, record.StringColumn);
				Assert.AreEqual("test", record.TypeConvertedColumn);
				Assert.AreEqual(i, record.FirstColumn);
				Assert.AreEqual(guid, record.GuidColumn);
			}
		}

		[TestMethod]
		public void GetRecordsWithDuplicateHeaderNames()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			var headerData = new[]
			{
				"Column",
				"Column",
				"Column"
			};

			var csvParserMock = new ParserMock(config)
			{
				headerData,
				{ "one", "two", "three" },
				{ "one", "two", "three" },
			};

			var csv = new CsvReader(csvParserMock);
			csv.Context.RegisterClassMap<TestRecordDuplicateHeaderNamesMap>();
			var records = csv.GetRecords<TestRecordDuplicateHeaderNames>().ToList();

			Assert.AreEqual(2, records.Count);

			for (var i = 1; i <= records.Count; i++)
			{
				var record = records[i - 1];
				Assert.AreEqual("one", record.Column1);
				Assert.AreEqual("two", record.Column2);
				Assert.AreEqual("three", record.Column3);
			}
		}

		[TestMethod]
		public void GetRecordEmptyFileWithHeaderOnTest()
		{
			var parserMock = new ParserMock
			{
				null,
			};

			var csvReader = new CsvReader(parserMock);
			try
			{
				csvReader.Read();
				csvReader.ReadHeader();
				csvReader.Read();
				Assert.Fail();
			}
			catch (ReaderException) { }
		}

		[TestMethod]
		public void GetRecordEmptyValuesNullableTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);

			writer.WriteLine("StringColumn,IntColumn,GuidColumn");
			writer.WriteLine("one,1,11111111-1111-1111-1111-111111111111");
			writer.WriteLine(",,");
			writer.WriteLine("three,3,33333333-3333-3333-3333-333333333333");
			writer.Flush();
			stream.Position = 0;

			var reader = new StreamReader(stream);
			var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

			csvReader.Read();
			var record = csvReader.GetRecord<TestNullable>();
			Assert.IsNotNull(record);
			Assert.AreEqual("one", record.StringColumn);
			Assert.AreEqual(1, record.IntColumn);
			Assert.AreEqual(new Guid("11111111-1111-1111-1111-111111111111"), record.GuidColumn);

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.IsNotNull(record);
			Assert.AreEqual(string.Empty, record.StringColumn);
			Assert.AreEqual(null, record.IntColumn);
			Assert.AreEqual(null, record.GuidColumn);

			csvReader.Read();
			record = csvReader.GetRecord<TestNullable>();
			Assert.IsNotNull(record);
			Assert.AreEqual("three", record.StringColumn);
			Assert.AreEqual(3, record.IntColumn);
			Assert.AreEqual(new Guid("33333333-3333-3333-3333-333333333333"), record.GuidColumn);
		}

		[TestMethod]
		public void CaseInsensitiveHeaderMatchingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = (header, index) => header.ToLower(),
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("One,Two,Three");
				writer.WriteLine("1,2,3");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.AreEqual("1", csv.GetField("one"));
				Assert.AreEqual("2", csv.GetField("TWO"));
				Assert.AreEqual("3", csv.GetField("ThreE"));
			}
		}

		[TestMethod]
		public void SpacesInHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = (header, index) => Regex.Replace(header, @"\s", string.Empty),
			};
			var parserMock = new ParserMock(config)
			{
				{ " Int Column ", " String Column " },
				{ "1", "one" },
			};
			var reader = new CsvReader(parserMock);
			var data = reader.GetRecords<TestDefaultValues>().ToList();
			Assert.IsNotNull(data);
			Assert.AreEqual(1, data.Count);
			Assert.AreEqual(1, data[0].IntColumn);
			Assert.AreEqual("one", data[0].StringColumn);
		}

		[TestMethod]
		public void BooleanTypeConverterTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);

			writer.WriteLine("BoolColumn,BoolNullableColumn,StringColumn");
			writer.WriteLine("true,true,1");
			writer.WriteLine("True,True,2");
			writer.WriteLine("1,1,3");
			writer.WriteLine("false,false,4");
			writer.WriteLine("False,False,5");
			writer.WriteLine("0,0,6");

			writer.Flush();
			stream.Position = 0;

			var reader = new StreamReader(stream);
			var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

			var records = csvReader.GetRecords<TestBoolean>().ToList();

			Assert.IsTrue(records[0].BoolColumn);
			Assert.IsTrue(records[0].BoolNullableColumn);
			Assert.IsTrue(records[1].BoolColumn);
			Assert.IsTrue(records[1].BoolNullableColumn);
			Assert.IsTrue(records[2].BoolColumn);
			Assert.IsTrue(records[2].BoolNullableColumn);
			Assert.IsFalse(records[3].BoolColumn);
			Assert.IsFalse(records[3].BoolNullableColumn);
			Assert.IsFalse(records[4].BoolColumn);
			Assert.IsFalse(records[4].BoolNullableColumn);
			Assert.IsFalse(records[5].BoolColumn);
			Assert.IsFalse(records[5].BoolNullableColumn);
		}

		[TestMethod]
		public void SkipEmptyRecordsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				ShouldSkipRecord = record => record.All(string.IsNullOrWhiteSpace),
			};

			var parserMock = new ParserMock(config)
			{
				{ "1", "2", "3" },
				{ "", "", "" },
				{ "4", "5", "6" },
			};

			var reader = new CsvReader(parserMock);

			reader.Read();
			Assert.AreEqual("1", reader.Parser.Record[0]);
			Assert.AreEqual("2", reader.Parser.Record[1]);
			Assert.AreEqual("3", reader.Parser.Record[2]);

			reader.Read();
			Assert.AreEqual("4", reader.Parser.Record[0]);
			Assert.AreEqual("5", reader.Parser.Record[1]);
			Assert.AreEqual("6", reader.Parser.Record[2]);

			Assert.IsFalse(reader.Read());
		}

		[TestMethod]
		public void SkipRecordCallbackTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				ShouldSkipRecord = row => row[1] == "2",
			};

			var parserMock = new ParserMock(config)
			{
				{ "1", "2", "3" },
				{ " ", "", "" },
				{ "4", "5", "6" },
			};

			var reader = new CsvReader(parserMock);

			reader.Read();
			Assert.AreEqual(" ", reader.Parser.Record[0]);
			Assert.AreEqual("", reader.Parser.Record[1]);
			Assert.AreEqual("", reader.Parser.Record[2]);

			reader.Read();
			Assert.AreEqual("4", reader.Parser.Record[0]);
			Assert.AreEqual("5", reader.Parser.Record[1]);
			Assert.AreEqual("6", reader.Parser.Record[2]);

			Assert.IsFalse(reader.Read());
		}

		[TestMethod]
		public void MultipleGetRecordsCalls()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				writer.WriteLine("IntColumn,String Column");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				csvReader.Context.RegisterClassMap<TestRecordMap>();
				var records = csvReader.GetRecords<TestRecord>();
				Assert.AreEqual(2, records.Count());
				Assert.AreEqual(0, records.Count());
			}
		}

		[TestMethod]
		public void IgnoreExceptionsTest()
		{
			var callbackCount = 0;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReadingExceptionOccurred = (ex) =>
				{
					callbackCount++;
					return false;
				},
			};
			var parserMock = new ParserMock(config)
			{
				{ "BoolColumn", "BoolNullableColumn", "StringColumn" },
				{ "1", "1", "one" },
				{ "two", "1", "two" },
				{ "1", "1", "three" },
				{ "four", "1", "four" },
				{ "1", "1", "five" },
			};
			var csv = new CsvReader(parserMock);

			var records = csv.GetRecords<TestBoolean>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(3, records.Count);
			Assert.AreEqual(2, callbackCount);
			Assert.AreEqual("one", records[0].StringColumn);
			Assert.AreEqual("three", records[1].StringColumn);
			Assert.AreEqual("five", records[2].StringColumn);
		}

		[TestMethod]
		public void ReadStructRecordsTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
			};
			var csv = new CsvReader(parserMock);
			var records = csv.GetRecords<TestStruct>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(1, records[0].Id);
			Assert.AreEqual("one", records[0].Name);
			Assert.AreEqual(2, records[1].Id);
			Assert.AreEqual("two", records[1].Name);
		}

		[TestMethod]
		public void WriteStructReferenceRecordsTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
			};
			var csv = new CsvReader(parserMock);
			csv.Context.RegisterClassMap<TestStructParentMap>();
			var records = csv.GetRecords<TestStructParent>().ToList();
			Assert.IsNotNull(records);
			Assert.AreEqual(1, records.Count);
			Assert.AreEqual(1, records[0].Test.Id);
			Assert.AreEqual("one", records[0].Test.Name);
		}

		[TestMethod]
		public void ReadPrimitiveRecordsHasHeaderTrueTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id" },
				{ "1" },
				{ "2" },
			};
			var csv = new CsvReader(parserMock);
			var records = csv.GetRecords<int>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(1, records[0]);
			Assert.AreEqual(2, records[1]);
		}

		[TestMethod]
		public void ReadPrimitiveRecordsHasHeaderFalseTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1" },
				{ "2" },
			};
			var csv = new CsvReader(parserMock);
			var records = csv.GetRecords<int>().ToList();

			Assert.IsNotNull(records);
			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(1, records[0]);
			Assert.AreEqual(2, records[1]);
		}

		[TestMethod]
		public void TrimHeadersTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
				PrepareHeaderForMatch = (header, index) => header.Trim(),
			};
			var parserMock = new ParserMock(config)
			{
				{ " one ", " two three " },
				{ "1", "2" },
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();
			Assert.AreEqual("1", reader.GetField("one"));
			Assert.AreEqual("2", reader.GetField("two three"));
			Assert.AreEqual(null, reader.GetField("twothree"));
		}

		[TestMethod]
		public void RowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "one" },
				{ "2", "two" },
			};

			var csv = new CsvReader(parserMock);

			csv.Read();
			Assert.AreEqual(1, csv.Parser.Row);

			csv.Read();
			Assert.AreEqual(2, csv.Parser.Row);
		}

		[TestMethod]
		public void DoNotIgnoreBlankLinesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.RegisterClassMap<SimpleMap>();

				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine(",");
				writer.WriteLine("");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Simple>().ToList();
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
				Assert.AreEqual(null, records[1].Id);
				Assert.AreEqual("", records[1].Name);
				Assert.AreEqual(null, records[2].Id);
				Assert.AreEqual("", records[2].Name);
				Assert.AreEqual(2, records[3].Id);
				Assert.AreEqual("two", records[3].Name);
			}
		}

		[TestMethod]
		public void WriteNestedHeadersTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReferenceHeaderPrefix = (type, name) => $"{name}.",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Simple1.Id,Simple1.Name,Simple2.Id,Simple2.Name");
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Nested>().ToList();
				Assert.IsNotNull(records);
				Assert.AreEqual(1, records[0].Simple1.Id);
				Assert.AreEqual("one", records[0].Simple1.Name);
				Assert.AreEqual(2, records[0].Simple2.Id);
				Assert.AreEqual("two", records[0].Simple2.Name);
			}
		}

		[TestMethod]
		public void ReaderDynamicHasHeaderTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				{ "2", "two" },
				null,
			};

			var csv = new CsvReader(parserMock);
			csv.Read();
			var row = csv.GetRecord<dynamic>();

			Assert.AreEqual("1", row.Id);
			Assert.AreEqual("one", row.Name);
		}

		[TestMethod]
		public void ReaderDynamicNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};

			var parserMock = new ParserMock(config)
			{
				{ "1", "one" },
				{ "2", "two" },
				null,
			};

		var csv = new CsvReader(parserMock);
			csv.Read();
			var row = csv.GetRecord<dynamic>();

			Assert.AreEqual("1", row.Field1);
			Assert.AreEqual("one", row.Field2);
		}

		private class Nested
		{
			public Simple Simple1 { get; set; }

			public Simple Simple2 { get; set; }
		}

		private class Simple
		{
			public int? Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class SimpleMap : ClassMap<Simple>
		{
			public SimpleMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
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

		private class OnlyFields
		{
			public string Name;
		}

		private sealed class OnlyFieldsMap : ClassMap<OnlyFields>
		{
			public OnlyFieldsMap()
			{
				Map(m => m.Name);
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

		private sealed class TestDefaultValuesMap : ClassMap<TestDefaultValues>
		{
			public TestDefaultValuesMap()
			{
				Map(m => m.IntColumn).Default(-1);
				Map(m => m.StringColumn).Default((string)null);
			}
		}

		private class TestNullable
		{
			public int? IntColumn { get; set; }

			public string StringColumn { get; set; }

			public Guid? GuidColumn { get; set; }
		}

		[DebuggerDisplay("IntColumn = {IntColumn}, StringColumn = {StringColumn}, IgnoredColumn = {IgnoredColumn}, TypeConvertedColumn = {TypeConvertedColumn}, FirstColumn = {FirstColumn}")]
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

		private sealed class TestRecordMap : ClassMap<TestRecord>
		{
			public TestRecordMap()
			{
				Map(m => m.IntColumn).TypeConverter<Int32Converter>();
				Map(m => m.StringColumn).Name("String Column");
				Map(m => m.TypeConvertedColumn).Index(1).TypeConverter<TestTypeConverter>();
				Map(m => m.FirstColumn).Index(0);
				Map(m => m.GuidColumn);
				Map(m => m.NoMatchingFields);
			}
		}

		private class TestRecordDuplicateHeaderNames
		{
			public string Column1 { get; set; }

			public string Column2 { get; set; }

			public string Column3 { get; set; }
		}

		private sealed class TestRecordDuplicateHeaderNamesMap : ClassMap<TestRecordDuplicateHeaderNames>
		{
			public TestRecordDuplicateHeaderNamesMap()
			{
				Map(m => m.Column1).Name("Column").NameIndex(0);
				Map(m => m.Column2).Name("Column").NameIndex(1);
				Map(m => m.Column3).Name("Column").NameIndex(2);
			}
		}

		private class TestTypeConverter : DefaultTypeConverter
		{
			public override object ConvertFromString(string text, IReaderRow row, MemberMapData propertyMapData)
			{
				return "test";
			}
		}
	}
}
