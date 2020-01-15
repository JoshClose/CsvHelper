// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ExcelCompatibleTests
	{
		[TestMethod]
		public void ParseTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("one,two,three");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// "one","two","three"
				writer.WriteLine("\"one\",\"two\",\"three\"");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedAndNonFieldsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// one,"two",three
				writer.WriteLine("one,\"two\",three");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithSpaceAfterTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// one,"two" ,three
				writer.WriteLine("one,\"two\" ,three");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two ", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithSpaceBeforeTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// one, "two",three
				writer.WriteLine("one, \"two\",three");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.BadDataFound = null;
				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual(" \"two\"", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithQuoteAfterTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// 1,"two" "2,3
				writer.WriteLine("1,\"two\" \"2,3");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.BadDataFound = null;
				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("1", record[0]);
				Assert.AreEqual("two \"2", record[1]);
				Assert.AreEqual("3", record[2]);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithEscapedQuoteTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// 1,"two "" 2",3
				writer.WriteLine("1,\"two \"\" 2\",3");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("1", record[0]);
				Assert.AreEqual("two \" 2", record[1]);
				Assert.AreEqual("3", record[2]);
			}
		}

		[TestMethod]
		public void ParseFieldMissingQuoteGoesToEndOfFileTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("a,b,\"c");
				writer.WriteLine("d,e,f");
				writer.Flush();
				stream.Position = 0;

				var row = parser.Read();
				Assert.IsNotNull(row);
				Assert.AreEqual("a", row[0]);
				Assert.AreEqual("b", row[1]);
				Assert.AreEqual("c\r\nd,e,f\r\n", row[2]);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
