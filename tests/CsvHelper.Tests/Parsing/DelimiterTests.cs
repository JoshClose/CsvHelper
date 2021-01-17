// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class DelimiterTests
	{
		[TestMethod]
		public void MultipleCharDelimiterWithPartOfDelimiterInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "<|>",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1<|>2<3<|>4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2<3", parser[1]);
				Assert.AreEqual("4", parser[2]);
			}
		}

		[TestMethod]
		public void NullDelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "\0",
			};
			var s = new StringBuilder();
			s.Append("1\02\03\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.AreEqual("3", parser[2]);
			}
		}

		[TestMethod]
		public void FirstCharOfDelimiterNextToDelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "!#",
			};
			var s = new StringBuilder();
			s.AppendLine("1!!#2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual("1!", parser[0]);
				Assert.AreEqual("2", parser[1]);
			}
		}
	}
}
