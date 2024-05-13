// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvParserRawRecordTests
	{
		[Fact]
		public void RawRecordCrLfTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1,2\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3,4\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void RawRecordCrTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r");
				writer.Write("3,4\r");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1,2\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3,4\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void RawRecordLfTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\n");
				writer.Write("3,4\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1,2\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3,4\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void RawRecordCr2DelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2\r");
				writer.Write("3;;4\r");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1;;2\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3;;4\r", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void TinyBufferTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 1,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1,2\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3,4\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void HandleMultipleCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				NewLine = "|##|\r\n",
				Delimiter = "|*|"
			};
			
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
				
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1|*|2|##|\r\n");
				writer.Write("3|*|4|##|\r\n");
				writer.Write("|*5|*|6|##|\r\n");
				writer.Write("7|##||*|6||##|\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1|*|2|##|\r\n", parser.RawRecord);

				parser.Read();
				Assert.Equal("3|*|4|##|\r\n", parser.RawRecord);

				parser.Read();
				Assert.Equal("|*5|*|6|##|\r\n", parser.RawRecord);
				
				parser.Read();
				Assert.Equal("7|##||*|6||##|\r\n", parser.RawRecord);
				
				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}
		
		[Fact]
		public void HandleMultipleCharacterSmallBuffer()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				NewLine = "|##|\r\n",
				Delimiter = "|*|",
				BufferSize = 1,
			};
			
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1|*|2|##|\r\n");
				writer.Write("3|*|4|##|\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1|*|2|##|\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal("3|*|4|##|\r\n", parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(string.Empty, parser.RawRecord.ToString());
			}
		}
	}
}
