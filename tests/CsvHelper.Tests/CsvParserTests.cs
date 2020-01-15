// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvParserTests
	{
		[TestMethod]
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

				var row = parser.Read();
				Assert.IsNotNull(row);

				row = parser.Read();
				Assert.IsNotNull(row);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
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
			while (parser.Read() != null)
			{
				count++;
			}

			Assert.AreEqual(2, count);
		}

		[TestMethod]
		public void ParseEmptyRowsTest()
		{
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

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			var count = 0;
			while (parser.Read() != null)
			{
				count++;
			}

			Assert.AreEqual(4, count);
		}

		[TestMethod]
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
			parser.Configuration.Delimiter = ",";

			var record = parser.Read();
			Assert.AreEqual("one", record[0]);
			Assert.AreEqual("two", record[1]);
			Assert.AreEqual("three", record[2]);

			record = parser.Read();
			Assert.AreEqual("four", record[0]);
			Assert.AreEqual("five", record[1]);
			Assert.AreEqual("six", record[2]);

			record = parser.Read();
			Assert.IsNull(record);
		}

		[TestMethod]
		public void ParseFieldQuotesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,\"two\",three");
			writer.WriteLine("four,\"\"\"five\"\"\",six");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { BufferSize = 2000 };
			var parser = new CsvParser(reader, config);
			parser.Configuration.Delimiter = ",";

			var record = parser.Read();
			Assert.AreEqual("one", record[0]);
			Assert.AreEqual("two", record[1]);
			Assert.AreEqual("three", record[2]);

			record = parser.Read();
			Assert.AreEqual("four", record[0]);
			Assert.AreEqual("\"five\"", record[1]);
			Assert.AreEqual("six", record[2]);

			record = parser.Read();
			Assert.IsNull(record);
		}

		[TestMethod]
		public void ParseSpacesTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine(" one , \"two three\" , four ");
			writer.WriteLine(" \" five \"\" six \"\" seven \" ");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);
			parser.Configuration.Delimiter = ",";
			parser.Configuration.BadDataFound = null;

			var record = parser.Read();
			Assert.AreEqual(" one ", record[0]);
			Assert.AreEqual(" \"two three\" ", record[1]);
			Assert.AreEqual(" four ", record[2]);

			record = parser.Read();
			Assert.AreEqual(" \" five \"\" six \"\" seven \" ", record[0]);

			record = parser.Read();
			Assert.IsNull(record);
		}

		[TestMethod]
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

		[TestMethod]
		public void ParseEmptyTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				var record = parser.Read();
				Assert.IsNull(record);
			}
		}

		[TestMethod]
		public void ParseCrOnlyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("\r");
				writer.Flush();
				stream.Position = 0;

				var record = parser.Read();
				Assert.IsNull(record);
			}
		}

		[TestMethod]
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

				var record = parser.Read();
				Assert.IsNull(record);
			}
		}

		[TestMethod]
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

				var record = parser.Read();
				Assert.IsNull(record);
			}
		}

		[TestMethod]
		public void Parse1RecordWithNoCrlfTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.Write("one,two,three");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void Parse2RecordsLastWithNoCrlfTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.WriteLine("one,two,three");
				streamWriter.Write("four,five,six");
				streamWriter.Flush();
				memoryStream.Position = 0;

				parser.Read();
				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("four", record[0]);
				Assert.AreEqual("five", record[1]);
				Assert.AreEqual("six", record[2]);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ParseFirstFieldIsEmptyQuotedTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.WriteLine("\"\",\"two\",\"three\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
		public void ParseLastFieldIsEmptyQuotedTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.WriteLine("\"one\",\"two\",\"\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("", record[2]);
			}
		}

		[TestMethod]
		public void ParseQuoteOnlyQuotedFieldTest()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.WriteLine("\"\"\"\",\"two\",\"three\"");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(3, record.Length);
				Assert.AreEqual("\"", record[0]);
				Assert.AreEqual("two", record[1]);
				Assert.AreEqual("three", record[2]);
			}
		}

		[TestMethod]
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

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(1, record.Length);
				Assert.AreEqual("row one", record[0]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(1, record.Length);
				Assert.AreEqual("row two", record[0]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(1, record.Length);
				Assert.AreEqual("row three", record[0]);
			}
		}

		[TestMethod]
		public void ParseRecordWhereOnlyCarriageReturnLineEndingIsUsed()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.Write("one,two\r");
				streamWriter.Write("three,four\r");
				streamWriter.Write("five,six\r");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("three", record[0]);
				Assert.AreEqual("four", record[1]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("five", record[0]);
				Assert.AreEqual("six", record[1]);
			}
		}

		[TestMethod]
		public void ParseRecordWhereOnlyLineFeedLineEndingIsUsed()
		{
			using (var memoryStream = new MemoryStream())
			using (var streamReader = new StreamReader(memoryStream))
			using (var streamWriter = new StreamWriter(memoryStream))
			using (var parser = new CsvParser(streamReader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				streamWriter.Write("one,two\n");
				streamWriter.Write("three,four\n");
				streamWriter.Write("five,six\n");
				streamWriter.Flush();
				memoryStream.Position = 0;

				var record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("two", record[1]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("three", record[0]);
				Assert.AreEqual("four", record[1]);

				record = parser.Read();
				Assert.IsNotNull(record);
				Assert.AreEqual(2, record.Length);
				Assert.AreEqual("five", record[0]);
				Assert.AreEqual("six", record[1]);
			}
		}

		[TestMethod]
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

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture) { Configuration = { AllowComments = true } };
			parser.Configuration.Delimiter = ",";

			parser.Read();
			var record = parser.Read();
			Assert.AreEqual("seven", record[0]);
		}

		[TestMethod]
		public void ParseCommentedOutLineWithCommentsOff()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one,two,three");
			writer.WriteLine("#four,five,six");
			writer.WriteLine("seven,eight,nine");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture) { Configuration = { AllowComments = false } };
			parser.Configuration.Delimiter = ",";

			parser.Read();
			var record = parser.Read();
			Assert.AreEqual("#four", record[0]);
		}

		[TestMethod]
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

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture) { Configuration = { AllowComments = true, Comment = '*' } };
			parser.Configuration.Delimiter = ",";

			parser.Read();
			var record = parser.Read();
			Assert.AreEqual("seven", record[0]);
		}

		[TestMethod]
		public void ParseUsingDifferentDelimiter()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("one\ttwo\tthree");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture) { Configuration = { Delimiter = "\t" } };

			var record = parser.Read();
			Assert.AreEqual("one", record[0]);
			Assert.AreEqual("two", record[1]);
			Assert.AreEqual("three", record[2]);
		}

		[TestMethod]
		public void ParseUsingDifferentQuote()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.WriteLine("'one','two','three'");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture) { Configuration = { Quote = '\'' } };
			parser.Configuration.Delimiter = ",";

			var record = parser.Read();
			Assert.AreEqual("one", record[0]);
			Assert.AreEqual("two", record[1]);
			Assert.AreEqual("three", record[2]);
		}

		[TestMethod]
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
			parser.Configuration.Delimiter = ",";

			var record = parser.Read();

			Assert.IsNotNull(record);
			Assert.AreEqual("", record[2]);

			record = parser.Read();

			Assert.IsNotNull(record);
			Assert.AreEqual("", record[2]);

			record = parser.Read();

			Assert.IsNull(record);
		}

		[TestMethod]
		public void ParseLastLineHasNoCrLf()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.Write("a");
			writer.Flush();
			stream.Position = 0;
			var reader = new StreamReader(stream);

			var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

			var record = parser.Read();

			Assert.IsNotNull(record);
			Assert.AreEqual("a", record[0]);

			record = parser.Read();

			Assert.IsNull(record);
		}

		[TestMethod]
		public void CharReadTotalTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.AllowComments = true;

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
				Assert.AreEqual(9, parser.FieldReader.Context.CharPosition);

				parser.Read();
				Assert.AreEqual(16, parser.FieldReader.Context.CharPosition);

				parser.Read();
				Assert.AreEqual(19, parser.FieldReader.Context.CharPosition);

				parser.Read();
				Assert.AreEqual(40, parser.FieldReader.Context.CharPosition);

				parser.Read();
				Assert.AreEqual(57, parser.FieldReader.Context.CharPosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void StreamSeekingUsingCharPositionTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.AllowComments = true;

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
				Assert.AreEqual("Id", record[0]);
				Assert.AreEqual("Name", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.CharPosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("1", record[0]);
				Assert.AreEqual("one", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.CharPosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("", record[0]);
				Assert.AreEqual("", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.CharPosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("2", record[0]);
				Assert.AreEqual("two", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.CharPosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("3", record[0]);
				Assert.AreEqual("three, four", record[1]);
			}
		}

		[TestMethod]
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
				while (parser.Read() != null)
				{
					rowCount++;
					Assert.AreEqual(rowCount, parser.Context.Row);
				}
			}
		}

		[TestMethod]
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
				while (parser.Read() != null)
				{
					Assert.AreEqual(rowCount, parser.Context.Row);
					rowCount += 2;
				}
			}
		}

		[TestMethod]
		public void IgnoreBlankLinesRowCountTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.IgnoreBlankLines = true;
				writer.WriteLine("1,a");
				writer.WriteLine();
				writer.WriteLine("3,c");
				writer.Flush();
				stream.Position = 0;

				var row = parser.Read();

				Assert.AreEqual(1, parser.Context.Row);
				Assert.AreEqual("1", row[0]);

				row = parser.Read();

				Assert.AreEqual(3, parser.Context.Row);
				Assert.AreEqual("3", row[0]);
			}
		}

		[TestMethod]
		public void DoNotIgnoreBlankLinesRowCountTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.IgnoreBlankLines = false;
				writer.WriteLine("1,a");
				writer.WriteLine();
				writer.WriteLine("3,c");
				writer.Flush();
				stream.Position = 0;

				var row = parser.Read();

				Assert.AreEqual(1, parser.Context.Row);
				Assert.AreEqual("1", row[0]);

				row = parser.Read();

				Assert.AreEqual(2, parser.Context.Row);
				Assert.AreEqual(0, row.Length);

				row = parser.Read();

				Assert.AreEqual(3, parser.Context.Row);
				Assert.AreEqual("3", row[0]);
			}
		}

		[TestMethod]
		public void RowCommentLinesTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Write("# comment 1\r\n");
				writer.Write("3,4\r\n");
				writer.Write("# comment 2\r\n");
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.AllowComments = true;
				var rowCount = 1;
				while (parser.Read() != null)
				{
					Assert.AreEqual(rowCount, parser.Context.Row);
					rowCount += 2;
				}
			}
		}

		[TestMethod]
		public void RowRawTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
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

				var row = parser.Read();
				Assert.AreEqual("1", row[0]);
				Assert.AreEqual("2\r\n2 continued\r\nend of 2", row[1]);
				Assert.AreEqual("3", row[2]);
				Assert.AreEqual(3, parser.Context.RawRow);

				row = parser.Read();
				Assert.AreEqual("4", row[0]);
				Assert.AreEqual("5", row[1]);
				Assert.AreEqual("6", row[2]);
				Assert.AreEqual(4, parser.Context.RawRow);

				row = parser.Read();
				Assert.AreEqual("7", row[0]);
				Assert.AreEqual("8\r\n8 continued\r\nend of 8", row[1]);
				Assert.AreEqual("9", row[2]);
				Assert.AreEqual(7, parser.Context.RawRow);

				row = parser.Read();
				Assert.AreEqual("10", row[0]);
				Assert.AreEqual("11", row[1]);
				Assert.AreEqual("12", row[2]);
				Assert.AreEqual(8, parser.Context.RawRow);
			}
		}

		[TestMethod]
		public void ByteCountTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.CountBytes = true;
				writer.Write("1,2\r\n");
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(5, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(10, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountTestWithQuotedFields()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.CountBytes = true;
				writer.Write("1,\"2\"\r\n");
				writer.Write("\"3\",4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(7, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(14, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountTestWithQuotedFieldsExtraQuote()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.CountBytes = true;
				parser.Configuration.BadDataFound = null;

				writer.Write("1,\"2\" \" a\r\n");
				writer.Write("\"3\",4\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(11, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(18, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountTestWithQuotedFieldsEmptyQuotedField()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.CountBytes = true;
				writer.Write("1,\"\",2\r\n");
				writer.Write("\"3\",4,\"5\"\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(8, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(19, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountTestWithQuotedFieldsClosingQuoteAtStartOfBuffer()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				BufferSize = 4
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("1,\"2\",3\r\n");
				writer.Write("\"4\",5,\"6\"\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(9, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(20, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountTestWithQuotedFieldsEscapedQuoteAtStartOfBuffer()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CountBytes = true,
				BufferSize = 4
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("1,\"2a\",3\r\n");
				writer.Write("\"\"\"4\"\"\",5,\"6\"\r\n");
				writer.Flush();
				stream.Position = 0;

				var r1 = parser.Read();
				Assert.AreEqual(10, parser.FieldReader.Context.BytePosition);

				var r2 = parser.Read();
				Assert.AreEqual(25, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void ByteCountUsingCharWithMoreThanSingleByteTest()
		{
			var encoding = Encoding.Unicode;
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, encoding))
			using (var reader = new StreamReader(stream, encoding))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				//崔钟铉
				parser.Configuration.CountBytes = true;
				parser.Configuration.Encoding = encoding;
				writer.Write("1,崔\r\n");
				writer.Write("3,钟\r\n");
				writer.Write("5,铉\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				Assert.AreEqual(10, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(20, parser.FieldReader.Context.BytePosition);

				parser.Read();
				Assert.AreEqual(30, parser.FieldReader.Context.BytePosition);

				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void StreamSeekingUsingByteCountTest()
		{
			var encoding = Encoding.Unicode;
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, encoding))
			using (var reader = new StreamReader(stream, encoding))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.CountBytes = true;
				parser.Configuration.Encoding = encoding;
				parser.Configuration.AllowComments = true;

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
				Assert.AreEqual("Id", record[0]);
				Assert.AreEqual("Name", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.BytePosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("1", record[0]);
				Assert.AreEqual("one", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.BytePosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("", record[0]);
				Assert.AreEqual("", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.BytePosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("2", record[0]);
				Assert.AreEqual("two", record[1]);

				stream.Position = 0;
				stream.Seek(parser.FieldReader.Context.BytePosition, SeekOrigin.Begin);
				record = parser.Read();
				Assert.AreEqual("3", record[0]);
				Assert.AreEqual("three, four", record[1]);
			}
		}

		[TestMethod]
		public void SimulateSeekingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				// Already read:
				// 1,2,3\r
				// Seeked to this position.
				writer.Write("\n4,5,6\r\n");
				writer.Flush();
				stream.Position = 0;

				// Make sure this doesn't throw an exception.
				var row = parser.Read();

				Assert.IsNotNull(row);
				Assert.AreEqual("4", row[0]);
				Assert.AreEqual("5", row[1]);
				Assert.AreEqual("6", row[2]);
			}
		}

		[TestMethod]
		public void EndBufferTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 12
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("111,222,333\r\naaa,bbb,ccc\r\n");
				writer.Flush();
				stream.Position = 0;

				// BufferSize is set to 12 to force a buffer read after the first \r
				var row = parser.Read();

				Assert.IsNotNull(row);
				Assert.AreEqual("111", row[0]);
				Assert.AreEqual("222", row[1]);
				Assert.AreEqual("333", row[2]);

				row = parser.Read();

				Assert.IsNotNull(row);
				Assert.AreEqual("aaa", row[0]);
				Assert.AreEqual("bbb", row[1]);
				Assert.AreEqual("ccc", row[2]);
			}
		}

		[TestMethod]
		public void NullCharTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("1,\0,3");
				writer.Flush();
				stream.Position = 0;

				var row = parser.Read();
				Assert.IsNotNull(row);
				Assert.AreEqual("1", row[0]);
				Assert.AreEqual("\0", row[1]);
				Assert.AreEqual("3", row[2]);
			}
		}

		[TestMethod]
		public void RawRecordCorruptionTest()
		{
			var row1 = new string('a', 2038) + ",b\r\n";
			var row2 = "test1,test2";
			var val = row1 + row2;

			using (var reader = new StringReader(val))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual(row1, parser.FieldReader.Context.RawRecord);

				parser.Read();
				Assert.AreEqual(row2, parser.FieldReader.Context.RawRecord);
			}
		}

		[TestMethod]
		public void ParseNoQuotesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("one,\"two\",three \" four, \"five\" ");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.IgnoreQuotes = true;
				var record = parser.Read();

				Assert.IsNotNull(record);
				Assert.AreEqual("one", record[0]);
				Assert.AreEqual("\"two\"", record[1]);
				Assert.AreEqual("three \" four", record[2]);
				Assert.AreEqual(" \"five\" ", record[3]);
			}
		}

		[TestMethod]
		public void LastLineHasCommentTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("#comment");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.AllowComments = true;

				var record = parser.Read();

				Assert.IsNull(record);
			}
		}

		[TestMethod]
		public void LastLineHasCommentNoEolTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("#c");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.AllowComments = true;

				var record = parser.Read();

				Assert.IsNull(record);
			}
		}

		[TestMethod]
		public void DoNotIgnoreBlankLinesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.IgnoreBlankLines = false;

				writer.WriteLine("1,2,3");
				writer.WriteLine(",,");
				writer.WriteLine("");
				writer.WriteLine("4,5,6");
				writer.Flush();
				stream.Position = 0;

				var row = parser.Read();
				Assert.AreEqual("1", row[0]);
				Assert.AreEqual("2", row[1]);
				Assert.AreEqual("3", row[2]);

				row = parser.Read();
				Assert.AreEqual("", row[0]);
				Assert.AreEqual("", row[1]);
				Assert.AreEqual("", row[2]);

				row = parser.Read();
				Assert.AreEqual(0, row.Length);

				row = parser.Read();
				Assert.AreEqual("4", row[0]);
				Assert.AreEqual("5", row[1]);
				Assert.AreEqual("6", row[2]);
			}
		}

		[TestMethod]
		public void QuotedFieldWithCarriageReturnTest()
		{
			using (var reader = new StringReader("\"a\r\",b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				var row = parser.Read();

				Assert.IsNotNull(row);
				CollectionAssert.AreEqual(new[] { "a\r", "b" }, row);
				Assert.IsNull(parser.Read());
			}
		}

		[TestMethod]
		public void QuotedFieldWithLineFeedTest()
		{
			using (var reader = new StringReader("\"a\n\",b"))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Configuration.Delimiter = ",";
				var row = parser.Read();

				Assert.IsNotNull(row);
				CollectionAssert.AreEqual(new[] { "a\n", "b" }, row);
				Assert.IsNull(parser.Read());
			}
		}
	}
}
