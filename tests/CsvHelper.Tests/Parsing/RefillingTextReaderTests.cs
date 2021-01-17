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
    public class RefillingTextReaderTests
    {
		[TestMethod]
        public void RefillTextReaderMultipleTimesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
				Assert.IsFalse(parser.Read());

				var position = stream.Position;
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
				Assert.IsFalse(parser.Read());

				position = stream.Position;
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("5", parser[0]);
				Assert.AreEqual("6", parser[1]);
				Assert.IsFalse(parser.Read());
			}
		}
    }
}
