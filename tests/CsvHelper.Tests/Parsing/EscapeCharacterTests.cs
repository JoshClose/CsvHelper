// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class EscapeCharacterTests
	{
		[TestMethod]
		public void EscapeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
			};
			using (var reader = new StringReader("\"|\"a|\"\"\r\n"))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("\"a\"", parser[0]);
			}
		}

		[TestMethod]
		public void EscapeNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
			};
			using (var reader = new StringReader("\"|\"a|\"\""))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("\"a\"", parser[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimOutsideTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
				TrimOptions = TrimOptions.Trim,
			};
			using (var reader = new StringReader(" \"|\"a|\"\" \r\n"))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("\"a\"", parser[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimInsideTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var reader = new StringReader("\" |\"a|\" \"\r\n"))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("\"a\"", parser[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimBothTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
				TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
			};
			using (var reader = new StringReader(" \" |\"a|\" \" \r\n"))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("\"a\"", parser[0]);
			}
		}

		[TestMethod]
		public void EscapeWriteTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Escape = '|',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("\"a\"");
				csv.Flush();

				Assert.AreEqual("\"|\"a|\"\"", writer.ToString());
			}
		}
	}
}
