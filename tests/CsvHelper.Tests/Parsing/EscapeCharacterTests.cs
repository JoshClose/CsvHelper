// Copyright 2009-2020 Josh Close and Contributors
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
			using (var reader = new StringReader("\"|\"a|\"\"\r\n"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Escape = '|';
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeNoNewlineTest()
		{
			using (var reader = new StringReader("\"|\"a|\"\""))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Escape = '|';
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimOutsideTest()
		{
			using (var reader = new StringReader(" \"|\"a|\"\" \r\n"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimInsideTest()
		{
			using (var reader = new StringReader("\" |\"a|\" \"\r\n"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimBothTest()
		{
			using (var reader = new StringReader(" \" |\"a|\" \" \r\n"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeWriteTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Escape = '|';
				csv.WriteField("\"a\"");
				csv.Flush();

				Assert.AreEqual("\"|\"a|\"\"", writer.ToString());
			}
		}
	}
}
