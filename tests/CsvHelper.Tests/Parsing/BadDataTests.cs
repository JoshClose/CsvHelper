// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Parsing
{
	
	public class BadDataTests
	{
		[Fact]
		public void CallbackTest()
		{
			string? rawRecord = null;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = args => rawRecord = args.Context.Parser?.RawRecord.ToString(),
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write(" a\"bc\",d\r\n");        //   a"bc",d\r\n
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				var record = parser.Record;
				Assert.Equal(" a\"bc\",d\r\n", rawRecord);

				rawRecord = null;
				parser.Read();
				record = parser.Record;
				Assert.Null(rawRecord);
			}
		}

		[Fact]
		public void ThrowExceptionTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("1,2");
				writer.WriteLine(" a\"bc\",d");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				try
				{
					parser.Read();
					var record = parser.Record;
					throw new XUnitException("Failed to throw exception on bad data.");
				}
				catch (BadDataException) { }
			}
		}

		[Fact]
		public void IgnoreQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("one,2\"two,three\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.Equal("2\"two", parser[1]);
			}
		}

		[Fact]
		public void LineBreakInQuotedFieldIsBadDataCrTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\rb\""))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.Throws<BadDataException>(() => parser.Record);
			}
		}

		[Fact]
		public void LineBreakInQuotedFieldIsBadDataLfTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\nb\""))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.Throws<BadDataException>(() => parser.Record);
			}
		}

		[Fact]
		public void LineBreakInQuotedFieldIsBadDataCrLfTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\r\nb\""))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.Throws<BadDataException>(() => parser.Record);
			}
		}

		[Fact]
		public void Read_AccessingParserRecordInBadDataFound_ThrowsParserException()
		{
			var badstring = new StringReader("Fish,\"DDDD");

			string[] record = new string[0];
			var cfg = new CsvConfiguration(CultureInfo.CurrentCulture)
			{
				BadDataFound = args => record = args.Context.Parser?.Record!
			};
			var parser = new CsvParser(badstring, cfg);

			parser.Read();

			Assert.Throws<ParserException>(() => parser[1]);
		}

		[Fact]
		public void ConsecutiveBadDataTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
				CacheFields = false,
				ProcessFieldBufferSize = 4
			};
			// These 3 fields each use the processFieldBuffer.
			// The test is to ensure consistency of the fields during a read,
			// i.e. the memory that each field points to is not overwritten
			// during the processing of the other fields in the same row.
			string csv = "\"\"\"\",\"two\" \"2,\"three\" \"3\r\n"; // """","two" "2,"three" "3
			using (var reader = new StringReader(csv)) 
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());

				Assert.Equal(3, parser.Count);
				Assert.Equal("\"", parser.GetFieldSpan(0).ToString());
				Assert.Equal("two \"2", parser.GetFieldSpan(1).ToString());
				Assert.Equal("three \"3", parser.GetFieldSpan(2).ToString());
				Assert.Equal("two \"2", parser.GetFieldSpan(1).ToString());
				Assert.Equal("\"", parser.GetFieldSpan(0).ToString());

				Assert.False(parser.Read());
			}
		}
	}
}
