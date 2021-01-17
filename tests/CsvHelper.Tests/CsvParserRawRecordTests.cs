// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvParserRawRecordTests
	{
		[TestMethod]
		public void RawRecordCrLfTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual("1,2\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual("3,4\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual(string.Empty, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void RawRecordCrTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r");
				writer.Write("3,4\r");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual("1,2\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual("3,4\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual(string.Empty, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void RawRecordLfTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\n");
				writer.Write("3,4\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual("1,2\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual("3,4\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual(string.Empty, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void RawRecordCr2DelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2\r");
				writer.Write("3;;4\r");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual("1;;2\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual("3;;4\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual(string.Empty, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void TinyBufferTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 1,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual("1,2\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual("3,4\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.AreEqual(string.Empty, parser.RawRecord.ToString());
			}
		}
	}
}
