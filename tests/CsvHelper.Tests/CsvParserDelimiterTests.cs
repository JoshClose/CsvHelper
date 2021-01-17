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
	public class CsvParserDelimiterTests
	{
		[TestMethod]
		public void DifferentDelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "\t",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1\t2\t3");
				writer.WriteLine("4\t5\t6");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("3", parser[2]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("4", parser[0]);
				Assert.AreEqual("5", parser[1]);
				Assert.AreEqual("6", parser[2]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void MultipleCharDelimiter2Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "``",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1``2``3");
				writer.WriteLine("4``5``6");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("3", parser[2]);

				hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("4", parser[0]);
				Assert.AreEqual("5", parser[1]);
				Assert.AreEqual("6", parser[2]);

				hasRecords = parser.Read();
				Assert.IsFalse(hasRecords);
			}
		}

		[TestMethod]
		public void MultipleCharDelimiter3Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`\t`",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1`\t`2`\t`3");
				writer.WriteLine("4`\t`5`\t`6");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("3", parser[2]);

				hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("4", parser[0]);
				Assert.AreEqual("5", parser[1]);
				Assert.AreEqual("6", parser[2]);

				hasRecords = parser.Read();
				Assert.IsFalse(hasRecords);
			}
		}

		[TestMethod]
		public void AllFieldsEmptyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine(";;;;");
				writer.WriteLine(";;;;");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("", parser[0]);
				Assert.AreEqual("", parser[1]);
				Assert.AreEqual("", parser[2]);

				hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("", parser[0]);
				Assert.AreEqual("", parser[1]);
				Assert.AreEqual("", parser[2]);

				hasRecords = parser.Read();
				Assert.IsFalse(hasRecords);
			}
		}

		[TestMethod]
		public void AllFieldsEmptyNoEolOnLastLineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write(";;;;\r\n");
				writer.Write(";;;;");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("", parser[0]);
				Assert.AreEqual("", parser[1]);
				Assert.AreEqual("", parser[2]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("", parser[0]);
				Assert.AreEqual("", parser[1]);
				Assert.AreEqual("", parser[2]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void EmptyLastFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1;;2;;");
				writer.WriteLine("4;;5;;");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("", parser[2]);

				hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("4", parser[0]);
				Assert.AreEqual("5", parser[1]);
				Assert.AreEqual("", parser[2]);

				hasRecords = parser.Read();
				Assert.IsFalse(hasRecords);
			}
		}

		[TestMethod]
		public void EmptyLastFieldNoEolOnLastLineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2;;\r\n");
				writer.Write("4;;5;;");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("", parser[2]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("4", parser[0]);
				Assert.AreEqual("5", parser[1]);
				Assert.AreEqual("", parser[2]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void DifferentDelimiter2ByteCountTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2\r\n");
				writer.Write("4;;5\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(6, parser.ByteCount);

				parser.Read();
				Assert.AreEqual(12, parser.ByteCount);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void DifferentDelimiter3ByteCountTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				Delimiter = ";;;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;;2\r\n");
				writer.Write("4;;;5\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(7, parser.ByteCount);

				parser.Read();
				Assert.AreEqual(14, parser.ByteCount);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void MultipleCharDelimiterWithBufferEndingInMiddleOfDelimiterTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "|~|",
				BufferSize = 16,
			};

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("12340000004321|~|2");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.IsTrue(hasRecords);
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("12340000004321", parser[0]);
				Assert.AreEqual("2", parser[1]);

				hasRecords = parser.Read();
				Assert.IsFalse(hasRecords);
			}
		}
	}
}
