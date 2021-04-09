// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class ExcelCompatibilityTests
	{
		[Fact]
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

				Assert.Equal(3, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal("two ", parser[1]);
				Assert.Equal("three", parser[2]);
			}
		}

		[Fact]
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

				Assert.Equal(3, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal(" \"two\"", parser[1]);
				Assert.Equal("three", parser[2]);
			}
		}

		[Fact]
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

				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("two \"2", parser[1]);
				Assert.Equal("3", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
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

				Assert.Equal("a", parser[0]);
				Assert.Equal("b", parser[1]);
				Assert.Equal("c\r\nd,e,f\r\n", parser[2]);
			}
		}

		[Fact]
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

				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2\"3\"4", parser[1]);
				Assert.Equal("5", parser[2]);
			}
		}
	}
}
