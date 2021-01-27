// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ExcelCompatibilityTests
	{
		[TestMethod]
		public void Parse_EscapedFieldHasSpaceAfterLastQuote_FieldProcessedLeavingSpaceAtEnd()
		{
			var s = new StringBuilder();
			s.Append("one,\"two\" ,three\r\n"); // one,"two" ,three
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual("two ", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void Parse_EscapedFieldHasSpaceBeforeFirstQuote_FieldIsNotProcessed()
		{
			var s = new StringBuilder();
			s.Append("one, \"two\",three\r\n"); // one, "two",three
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual(" \"two\"", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void Parse_EscapedFieldHasExtraQuoteAfterLastQuote_CharsAfterLastQuoteAreNotProcessed()
		{
			var s = new StringBuilder();
			s.Append("1,\"two\" \"2,3\r\n"); // 1,"two" "2,3
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("two \"2", parser[1]);
				Assert.AreEqual("3", parser[2]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void Parse_EscapedFieldHasNoEndingQuote_GoesToEndOfFile()
		{
			var s = new StringBuilder();
			s.Append("a,b,\"c\r\n"); // a,b,"c\r\nd,e,f\r\n
			s.Append("d,e,f\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual("b", parser[1]);
				Assert.AreEqual("c\r\nd,e,f\r\n", parser[2]);
			}
		}

		[TestMethod]
		public void Parse_NonEscapedFieldHasQuotesInIt_IgnoresQuotes()
		{
			var s = new StringBuilder();
			s.Append("1,2\"3\"4,5\r\n"); // 1,2"3"4,5
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2\"3\"4", parser[1]);
				Assert.AreEqual("5", parser[2]);
			}
		}
	}
}
