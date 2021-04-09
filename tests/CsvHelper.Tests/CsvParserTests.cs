// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvParserTests
	{
		[Fact]
		public void SimpleParseTest()
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

				Assert.True(parser.Read());
				Assert.True(parser.Read());
				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ParseNewRecordTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("four,five,six");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			var count = 0;
			while (parser.Read())
			{
				count++;
			}

			Assert.Equal(2, count);
		}

		[Fact]
		public void ParseEmptyRowsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = true,
			};
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("four,five,six");
			writer.WriteLine(",,");
			writer.WriteLine("");
			writer.WriteLine("");
			writer.WriteLine("seven,eight,nine");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, config);

			var records = new List<string[]>();
			while (parser.Read())
			{
				records.Add(parser.Record);
			}

			Assert.Equal(4, records.Count);
		}

		[Fact]
		public void ParseTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("four,five,six");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			Assert.True(parser.Read());
			Assert.Equal("one", parser[0]);
			Assert.Equal("two", parser[1]);
			Assert.Equal("three", parser[2]);

			Assert.True(parser.Read());
			Assert.Equal("four", parser[0]);
			Assert.Equal("five", parser[1]);
			Assert.Equal("six", parser[2]);

			Assert.False(parser.Read());
		}

		[Fact]
		public void ParseFieldQuotesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,\"two\",three");
			writer.WriteLine("four,\"\"\"five\"\"\",six");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			Assert.True(parser.Read());
			Assert.Equal("one", parser[0]);
			Assert.Equal("two", parser[1]);
			Assert.Equal("three", parser[2]);

			Assert.True(parser.Read());
			Assert.Equal("four", parser[0]);
			Assert.Equal("\"five\"", parser[1]);
			Assert.Equal("six", parser[2]);

			Assert.False(parser.Read());
		}

		[Fact]
		public void ParseSpacesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine(" one , \"two three\" , four ");
			writer.WriteLine(" \" five \"\" six \"\" seven \" ");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			var parser = new CsvParser(reader, config);

			Assert.True(parser.Read());
			Assert.Equal(" one ", parser[0]);
			Assert.Equal(" \"two three\" ", parser[1]);
			Assert.Equal(" four ", parser[2]);

			Assert.True(parser.Read());
			Assert.Equal(" \" five \"\" six \"\" seven \" ", parser[0]);

			Assert.False(parser.Read());
		}

		[Fact]
		public void CallingReadMultipleTimesAfterDoneReadingTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("four,five,six");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			parser.Read();
			parser.Read();
			parser.Read();
			parser.Read();
		}

		[Fact]
		public void ParseEmptyTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ParseCrOnlyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("\r");
				writer.Flush();
				stream.Position = 0;

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ParseLfOnlyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("\n");
				writer.Flush();
				stream.Position = 0;

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ParseCrLnOnlyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("\r\n");
				writer.Flush();
				stream.Position = 0;

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Parse1RecordWithNoCrlfTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.Write("one,two,three");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal("two", parser[1]);
				Assert.Equal("three", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Parse2RecordsLastWithNoCrlfTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.WriteLine("one,two,three");
				streamWriter.Write("four,five,six");
				streamWriter.Flush();
				memoryStream.Position = 0;

				parser.Read();
				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("four", parser[0]);
				Assert.Equal("five", parser[1]);
				Assert.Equal("six", parser[2]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ParseFirstFieldIsEmptyQuotedTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.WriteLine("\"\",\"two\",\"three\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("", parser[0]);
				Assert.Equal("two", parser[1]);
				Assert.Equal("three", parser[2]);
			}
		}

		[Fact]
		public void ParseLastFieldIsEmptyQuotedTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.WriteLine("\"one\",\"two\",\"\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal("two", parser[1]);
				Assert.Equal("", parser[2]);
			}
		}

		[Fact]
		public void ParseQuoteOnlyQuotedFieldTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.WriteLine("\"\"\"\",\"two\",\"three\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Count);
				Assert.Equal("\"", parser[0]);
				Assert.Equal("two", parser[1]);
				Assert.Equal("three", parser[2]);
			}
		}

		[Fact]
		public void ParseRecordsWithOnlyOneField()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.WriteLine("row one");
				streamWriter.WriteLine("row two");
				streamWriter.WriteLine("row three");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(1, parser.Count);
				Assert.Equal("row one", parser[0]);

				Assert.True(parser.Read());
				Assert.Equal(1, parser.Count);
				Assert.Equal("row two", parser[0]);

				Assert.True(parser.Read());
				Assert.Equal(1, parser.Count);
				Assert.Equal("row three", parser[0]);
			}
		}

		[Fact]
		public void ParseRecordWhereOnlyCarriageReturnLineEndingIsUsed()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.Write("one,two\r");
				streamWriter.Write("three,four\r");
				streamWriter.Write("five,six\r");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal("two", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("three", parser[0]);
				Assert.Equal("four", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("five", parser[0]);
				Assert.Equal("six", parser[1]);
			}
		}

		[Fact]
		public void ParseRecordWhereOnlyLineFeedLineEndingIsUsed()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				streamWriter.Write("one,two\n");
				streamWriter.Write("three,four\n");
				streamWriter.Write("five,six\n");
				streamWriter.Flush();
				memoryStream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("one", parser[0]);
				Assert.Equal("two", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("three", parser[0]);
				Assert.Equal("four", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("five", parser[0]);
				Assert.Equal("six", parser[1]);
			}
		}

		[Fact]
		public void ParseCommentedOutLineWithCommentsOn()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("#four,five,six");
			writer.WriteLine("seven,eight,nine");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			var parser = new CsvParser(reader, config);

			parser.Read();
			parser.Read();
			Assert.Equal("seven", parser[0]);
		}

		[Fact]
		public void ParseCommentedOutLineWithCommentsOff()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = false,
			};
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("#four,five,six");
			writer.WriteLine("seven,eight,nine");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, config);

			parser.Read();
			parser.Read();
			Assert.Equal("#four", parser[0]);
		}

		[Fact]
		public void ParseCommentedOutLineWithDifferentCommentCommentsOn()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("*four,five,six");
			writer.WriteLine("seven,eight,nine");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				Comment = '*',
			};
			var parser = new CsvParser(reader, config);

			parser.Read();
			parser.Read();
			Assert.Equal("seven", parser[0]);
		}

		[Fact]
		public void ParseUsingDifferentDelimiter()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one\ttwo\tthree");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "\t",
			};
			var parser = new CsvParser(reader, config);

			Assert.True(parser.Read());
			Assert.Equal("one", parser[0]);
			Assert.Equal("two", parser[1]);
			Assert.Equal("three", parser[2]);
		}

		[Fact]
		public void ParseUsingDifferentQuote()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("'one','two','three'");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Quote = '\''
			};
			var parser = new CsvParser(reader, config);

			Assert.True(parser.Read());
			Assert.Equal("one", parser[0]);
			Assert.Equal("two", parser[1]);
			Assert.Equal("three", parser[2]);
		}

		[Fact]
		public void ParseFinalRecordWithNoEndOfLineTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("1,2,");
			writer.Write("4,5,");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			Assert.True(parser.Read());
			Assert.Equal("", parser[2]);

			Assert.True(parser.Read());
			Assert.Equal("", parser[2]);

			Assert.False(parser.Read());
		}

		[Fact]
		public void ParseLastLineHasNoCrLf()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.Write("a");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			Assert.True(parser.Read());
			Assert.Equal("a", parser[0]);

			Assert.False(parser.Read());
		}

		[Fact]
		public void CharReadTotalTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true
			};
			using (var stream = new MemoryStream())
			using (var writer = new TestStreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				// This is a breakdown of the char counts.
				// Read() will read up to the first line end char
				// and any more on the line will get read with the next read.

				// [I][d][,][N][a][m][e][\r][\n]
				//  1  2  3  4  5  6  7   8   9
				// [1][,][o][n][e][\r][\n]
				// 10 11 12 13 14  15  16
				// [,][\r][\n]
				// 17  18  19
				// [\r][\n]
				//  20  21
				// [#][ ][c][o][m][m][e][n][t][s][\r][\n]
				// 22 23 24 25 26 27 28 29 30 31  32  33
				// [2][,][t][w][o][\r][\n]
				// 34 35 36 37 38  39  40
				// [3][,]["][t][h][r][e][e][,][ ][f][o][u][r]["][\r][\n]
				// 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55  56  57

				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine(",");
				writer.WriteLine("");
				writer.WriteLine("# comments");
				writer.WriteLine("2,two");
				writer.WriteLine("3,\"three, four\"");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(9, parser.CharCount);

				parser.Read();
				Assert.Equal(16, parser.CharCount);

				parser.Read();
				Assert.Equal(19, parser.CharCount);

				parser.Read();
				Assert.Equal(40, parser.CharCount);

				parser.Read();
				Assert.Equal(57, parser.CharCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void StreamSeekingUsingCharPositionTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				// This is a breakdown of the char counts.
				// Read() will read up to the first line end char
				// and any more on the line will get read with the next read.

				// [I][d][,][N][a][m][e][\r][\n]
				//  1  2  3  4  5  6  7   8   9
				// [1][,][o][n][e][\r][\n]
				// 10 11 12 13 14  15  16
				// [,][\r][\n]
				// 17  18  19
				// [\r][\n]
				//  20  21
				// [#][ ][c][o][m][m][e][n][t][s][\r][\n]
				// 22 23 24 25 26 27 28 29 30 31  32  33
				// [2][,][t][w][o][\r][\n]
				// 34 35 36 37 38  39  40
				// [3][,]["][t][h][r][e][e][,][ ][f][o][u][r]["][\r][\n]
				// 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55  56  57

				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine(",");
				writer.WriteLine("");
				writer.WriteLine("# comments");
				writer.WriteLine("2,two");
				writer.WriteLine("3,\"three, four\"");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.CharCount, SeekOrigin.Begin);
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.CharCount, SeekOrigin.Begin);
				parser.Read();
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.CharCount, SeekOrigin.Begin);
				parser.Read();
				Assert.Equal("2", parser[0]);
				Assert.Equal("two", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.CharCount, SeekOrigin.Begin);
				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("three, four", parser[1]);
			}
		}

		[Fact]
		public void RowTest()
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

				var rowCount = 0;
				while (parser.Read())
				{
					rowCount++;
					Assert.Equal(rowCount, parser.Row);
				}
			}
		}

		[Fact]
		public void RowBlankLinesTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Write("\r\n");
				writer.Write("3,4\r\n");
				writer.Write("\r\n");
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = 0;

				var rowCount = 1;
				while (parser.Read())
				{
					Assert.Equal(rowCount, parser.Row);
					rowCount += 2;
				}
			}
		}

		[Fact]
		public void IgnoreBlankLinesRowCountTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1,a");
				writer.WriteLine();
				writer.WriteLine("3,c");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(1, parser.Row);
				Assert.Equal("1", parser[0]);

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Row);
				Assert.Equal("3", parser[0]);
			}
		}

		[Fact]
		public void DoNotIgnoreBlankLinesRowCountTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1,a");
				writer.WriteLine();
				writer.WriteLine("3,c");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal(1, parser.Row);
				Assert.Equal("1", parser[0]);

				Assert.True(parser.Read());
				Assert.Equal(2, parser.Row);
				Assert.Equal(1, parser.Count);

				Assert.True(parser.Read());
				Assert.Equal(3, parser.Row);
				Assert.Equal("3", parser[0]);
			}
		}

		[Fact]
		public void RowCommentLinesTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2\r\n");
				writer.Write("# comment 1\r\n");
				writer.Write("3,4\r\n");
				writer.Write("# comment 2\r\n");
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = 0;

				var rowCount = 1;
				while (parser.Read())
				{
					Assert.Equal(rowCount, parser.Row);
					rowCount += 2;
				}
			}
		}

		[Fact]
		public void RowRawTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new TestStreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("1,\"2");
				writer.WriteLine("2 continued");
				writer.WriteLine("end of 2\",3");
				writer.WriteLine("4,5,6");
				writer.WriteLine("7,\"8");
				writer.WriteLine("8 continued");
				writer.WriteLine("end of 8\",9");
				writer.WriteLine("10,11,12");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("2\r\n2 continued\r\nend of 2", parser[1]);
				Assert.Equal("3", parser[2]);
				Assert.Equal(3, parser.RawRow);

				Assert.True(parser.Read());
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);
				Assert.Equal(4, parser.RawRow);

				Assert.True(parser.Read());
				Assert.Equal("7", parser[0]);
				Assert.Equal("8\r\n8 continued\r\nend of 8", parser[1]);
				Assert.Equal("9", parser[2]);
				Assert.Equal(7, parser.RawRow);

				Assert.True(parser.Read());
				Assert.Equal("10", parser[0]);
				Assert.Equal("11", parser[1]);
				Assert.Equal("12", parser[2]);
				Assert.Equal(8, parser.RawRow);
			}
		}

		[Fact]
		public void ByteCountTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, config.Encoding))
			using (var reader = new StreamReader(stream, config.Encoding))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(5, parser.ByteCount);

				parser.Read();
				Assert.Equal(10, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ByteCountTestWithQuotedFields()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, config.Encoding))
			using (var reader = new StreamReader(stream, config.Encoding))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,\"2\"\r\n");
				writer.Write("\"3\",4\r\n");
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
		public void ByteCountTestWithQuotedFieldsEmptyQuotedField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, config.Encoding))
			using (var reader = new StreamReader(stream, config.Encoding))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,\"\",2\r\n");
				writer.Write("\"3\",4,\"5\"\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(8, parser.ByteCount);

				parser.Read();
				Assert.Equal(19, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ByteCountTestWithQuotedFieldsClosingQuoteAtStartOfBuffer()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				BufferSize = 4
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, config.Encoding))
			using (var reader = new StreamReader(stream, config.Encoding))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,\"2\",3\r\n");
				writer.Write("\"4\",5,\"6\"\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(9, parser.ByteCount);

				parser.Read();
				Assert.Equal(20, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ByteCountTestWithQuotedFieldsEscapedQuoteAtStartOfBuffer()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				BufferSize = 4
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, config.Encoding))
			using (var reader = new StreamReader(stream, config.Encoding))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,\"2a\",3\r\n");
				writer.Write("\"\"\"4\"\"\",5,\"6\"\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(10, parser.ByteCount);

				parser.Read();
				Assert.Equal(25, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void ByteCountUsingCharWithMoreThanSingleByteTest()
		{
			var encoding = Encoding.Unicode;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				Encoding = encoding,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, encoding))
			using (var reader = new StreamReader(stream, encoding))
			using (var parser = new CsvParser(reader, config))
			{
				//崔钟铉
				writer.Write("1,崔\r\n");
				writer.Write("3,钟\r\n");
				writer.Write("5,铉\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal(10, parser.ByteCount);

				parser.Read();
				Assert.Equal(20, parser.ByteCount);

				parser.Read();
				Assert.Equal(30, parser.ByteCount);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void StreamSeekingUsingByteCountTest()
		{
			var encoding = Encoding.Unicode;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				CountBytes = true,
				Encoding = encoding,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, encoding))
			using (var reader = new StreamReader(stream, encoding))
			using (var parser = new CsvParser(reader, config))
			{
				// This is a breakdown of the char counts.
				// Read() will read up to the first line end char
				// and any more on the line will get read with the next read.

				// [I][d][,][N][a][m][e][\r][\n]
				//  1  2  3  4  5  6  7   8   9
				// [1][,][o][n][e][\r][\n]
				// 10 11 12 13 14  15  16
				// [,][\r][\n]
				// 17  18  19
				// [\r][\n]
				//  20  21
				// [#][ ][c][o][m][m][e][n][t][s][\r][\n]
				// 22 23 24 25 26 27 28 29 30 31  32  33
				// [2][,][t][w][o][\r][\n]
				// 34 35 36 37 38  39  40
				// [3][,]["][t][h][r][e][e][,][ ][f][o][u][r]["][\r][\n]
				// 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55  56  57

				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine(",");
				writer.WriteLine("");
				writer.WriteLine("# comments");
				writer.WriteLine("2,two");
				writer.WriteLine("3,\"three, four\"");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.ByteCount, SeekOrigin.Begin);
				record = parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.ByteCount, SeekOrigin.Begin);
				record = parser.Read();
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.ByteCount, SeekOrigin.Begin);
				record = parser.Read();
				Assert.Equal("2", parser[0]);
				Assert.Equal("two", parser[1]);

				stream.Position = 0;
				stream.Seek(parser.ByteCount, SeekOrigin.Begin);
				record = parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("three, four", parser[1]);
			}
		}

		[Fact]
		public void SimulateSeekingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				// Already read:
				// 1,2,3\r
				// Seeked to this position.
				writer.Write("\n4,5,6\r\n");
				writer.Flush();
				stream.Position = 0;

				// Make sure this doesn't throw an exception.
				Assert.True(parser.Read());
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);
			}
		}

		[Fact]
		public void NullCharTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("1,\0,3");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("\0", parser[1]);
				Assert.Equal("3", parser[2]);
			}
		}

		[Fact]
		public void RawRecordCorruptionTest()
		{
			var row1 = new string('a', 2038) + ",b\r\n";
			var row2 = "test1,test2";
			var val = row1 + row2;

			using (var reader = new StringReader(val))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal(row1, parser.RawRecord.ToString());

				parser.Read();
				Assert.Equal(row2, parser.RawRecord.ToString());
			}
		}

		[Fact]
		public void ParseNoQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("one,\"two\",three \" four, \"five\" \n"); // `one,"two",three " four, "five" `
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal("one", parser[0]);
				Assert.Equal("\"two\"", parser[1]);
				Assert.Equal("three \" four", parser[2]);
				Assert.Equal(" \"five\" ", parser[3]);
			}
		}

		[Fact]
		public void LastLineHasCommentTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("#comment");
				writer.Flush();
				stream.Position = 0;

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void LastLineHasCommentNoEolTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("#c");
				writer.Flush();
				stream.Position = 0;

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void DoNotIgnoreBlankLinesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("1,2,3");
				writer.WriteLine(",,");
				writer.WriteLine("");
				writer.WriteLine("4,5,6");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.Equal("3", parser[2]);

				parser.Read();
				Assert.Equal("", parser[0]);
				Assert.Equal("", parser[1]);
				Assert.Equal("", parser[2]);

				parser.Read();
				Assert.Equal(1, parser.Count);

				parser.Read();
				Assert.Equal("4", parser[0]);
				Assert.Equal("5", parser[1]);
				Assert.Equal("6", parser[2]);
			}
		}

		[Fact]
		public void QuotedFieldWithCarriageReturnTest()
		{
			using (var reader = new StringReader("\"a\r\",b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				Assert.True(parser.Read());
				Assert.Equal(new[] { "a\r", "b" }, parser.Record);
				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void QuotedFieldWithLineFeedTest()
		{
			using (var reader = new StringReader("\"a\n\",b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				Assert.True(parser.Read());
				Assert.Equal(new[] { "a\n", "b" }, parser.Record);
				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void RowCountWithSingleLineAndNoLineEndingTest()
		{
			using (var reader = new StringReader("a,b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				Assert.True(parser.Read());
				Assert.Equal(1, parser.Row);
			}
		}

		[Fact]
		public void RawRowCountWithSingleLineAndNoLineEndingTest()
		{
			using (var reader = new StringReader("a,b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				Assert.True(parser.Read());
				Assert.Equal(1, parser.RawRow);
			}
		}
	}
}
