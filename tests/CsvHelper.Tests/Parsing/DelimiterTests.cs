// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class DelimiterTests
	{
		[TestMethod]
		public void MultipleCharDelimiterWithPartOfDelimiterInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1&|$2&3&|$4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.Delimiter = "&|$";
				var line = parser.Read();

				Assert.AreEqual(3, line.Length);
				Assert.AreEqual("1", line[0]);
				Assert.AreEqual("2&3", line[1]);
				Assert.AreEqual("4", line[2]);
			}
		}

		[TestMethod]
		public void NullDelimiterTest()
		{
			var s = new StringBuilder();
			s.Append("1\02\03\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = "\0";

				var line = parser.Read();

				Assert.AreEqual(3, line.Length);
				Assert.AreEqual("1", line[0]);
				Assert.AreEqual("2", line[1]);
				Assert.AreEqual("3", line[2]);
			}
		}

		[TestMethod]
		public void FirstCharOfDelimiterNextToDelimiterTest()
		{
			var s = new StringBuilder();
			s.AppendLine("1!#2!!#3");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = "!#";
				var row = parser.Read();

				Assert.AreEqual("1", row[0]);
				Assert.AreEqual("2!", row[1]);
				Assert.AreEqual("3", row[2]);
			}
		}
	}
}
