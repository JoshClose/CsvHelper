// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
    public class EndBufferTests
    {
		[TestMethod]
		public void Read_BufferEndsInOneCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno,pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmno", parser[0]);
				Assert.AreEqual("pqrs", parser[1]);
			}
		}

		[TestMethod]
		public void Read_BufferEndsInFirstCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmnop;;qrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmnop", parser[0]);
				Assert.AreEqual("qrs", parser[1]);
			}
		}

		[TestMethod]
		public void Read_BufferEndsInSecondCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno;;pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmno", parser[0]);
				Assert.AreEqual("pqrs", parser[1]);
			}
		}
	}
}
