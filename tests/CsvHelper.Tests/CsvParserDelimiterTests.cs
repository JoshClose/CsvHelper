// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvParserDelimiterTests
	{
		[Fact]
		public void DifferentDelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "\t",
				WhiteSpaceChars = new[] { ' ' },
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1\t2\t3");
				writer.WriteLine("4\t5\t6");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("3", parser[2]);

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void MultipleCharDelimiter2Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "``",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1``2``3");
				writer.WriteLine("4``5``6");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("3", parser[2]);

				hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);

				hasRecords = parser.Read();
				Assert.False(hasRecords);
			}
		}

		[Fact]
		public void MultipleCharDelimiter3Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`\t`",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1`\t`2`\t`3");
				writer.WriteLine("4`\t`5`\t`6");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("3", parser[2]);

				hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);

				hasRecords = parser.Read();
				Assert.False(hasRecords);
			}
		}

		[Fact]
		public void AllFieldsEmptyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine(";;;;");
				writer.WriteLine(";;;;");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);
				Assert.Equal("", parser[2]);

				hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);
				Assert.Equal("", parser[2]);

				hasRecords = parser.Read();
				Assert.False(hasRecords);
			}
		}

		[Fact]
		public void AllFieldsEmptyNoEolOnLastLineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write(";;;;\r\n");
				writer.Write(";;;;");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);
				Assert.Equal("", parser[2]);

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);
				Assert.Equal("", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void EmptyLastFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1;;2;;");
				writer.WriteLine("4;;5;;");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("", parser[2]);

				hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(3, parser.Count);
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("", parser[2]);

				hasRecords = parser.Read();
				Assert.False(hasRecords);
			}
		}

		[Fact]
		public void EmptyLastFieldNoEolOnLastLineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2;;\r\n");
				writer.Write("4;;5;;");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("", parser[2]);

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void DifferentDelimiter2ByteCountTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				Delimiter = ";;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;2\r\n");
				writer.Write("4;;5\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(6, parser.ByteCount);

				parser.Read();
				Assert.Equal(12, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void DifferentDelimiter3ByteCountTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				Delimiter = ";;;",
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1;;;2\r\n");
				writer.Write("4;;;5\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(7, parser.ByteCount);

				parser.Read();
				Assert.Equal(14, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void MultipleCharDelimiterWithBufferEndingInMiddleOfDelimiterTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "|~|",
				BufferSize = 16,
			};

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("12340000004321|~|2");
				writer.Flush();
				stream.Position = 0;

				var hasRecords = parser.Read();
				Assert.True(hasRecords);
				Assert.Equal(2, parser.Count);
				Assert.Equal("12340000004321", parser[0]);
				Assert.Equal("2", parser[1]);

				hasRecords = parser.Read();
				Assert.False(hasRecords);
			}
		}
	}
}
