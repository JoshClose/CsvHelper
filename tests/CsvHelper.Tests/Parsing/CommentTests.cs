// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Parsing
{
	
	public class CommentTests
	{
		private static string NewLineName(string newLine) => newLine == "\r\n" ? "CRLF" : "LF";

		// A comment/blank line is parsed through ReadBlankLine, which consumes the line
		// including its line ending. When the line ending falls exactly on the read-buffer
		// boundary the parser used to read past the end of the buffer and throw an
		// IndexOutOfRangeException. FillBuffer doubles the buffer when a record is longer
		// than the buffer, so the boundary is hit at comment content lengths of the form
		// k * BufferSize - newLine.Length for power-of-two k (2, 4, 8, ...).
		//
		// The crash only reproduces with CRLF (ReadLineEnding consumes the '\n' after the
		// '\r', overshooting to the buffer end); a lone '\n' exits the scan loop safely.
		// Both line endings are covered to lock in that behavior.
		[Theory]
		[InlineData(16, "\r\n")]
		[InlineData(16, "\n")]
		[InlineData(32, "\r\n")]
		[InlineData(32, "\n")]
		[InlineData(64, "\r\n")]
		[InlineData(64, "\n")]
		public void CommentEndingOnBufferBoundaryDoesNotThrow(int bufferSize, string newLine)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				BufferSize = bufferSize,
			};

			for (var commentLength = 1; commentLength <= bufferSize * 8; commentLength++)
			{
				var csv = "1" + newLine + "#" + new string('a', commentLength - 1) + newLine + "2" + newLine;

				using (var reader = new StringReader(csv))
				using (var parser = new CsvParser(reader, config))
				{
					Assert.True(parser.Read(), $"first record (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
					Assert.Equal("1", parser[0]);
					Assert.True(parser.Read(), $"record after comment (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
					Assert.Equal("2", parser[0]);
					Assert.False(parser.Read(), $"end of file (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
				}
			}
		}

		[Theory]
		[InlineData(16, "\r\n")]
		[InlineData(16, "\n")]
		[InlineData(32, "\r\n")]
		[InlineData(32, "\n")]
		[InlineData(64, "\r\n")]
		[InlineData(64, "\n")]
		public async Task CommentEndingOnBufferBoundaryDoesNotThrowAsync(int bufferSize, string newLine)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				BufferSize = bufferSize,
			};

			for (var commentLength = 1; commentLength <= bufferSize * 8; commentLength++)
			{
				var csv = "1" + newLine + "#" + new string('a', commentLength - 1) + newLine + "2" + newLine;

				using (var reader = new StringReader(csv))
				using (var parser = new CsvParser(reader, config))
				{
					Assert.True(await parser.ReadAsync(), $"first record (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
					Assert.Equal("1", parser[0]);
					Assert.True(await parser.ReadAsync(), $"record after comment (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
					Assert.Equal("2", parser[0]);
					Assert.False(await parser.ReadAsync(), $"end of file (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, commentLength={commentLength})");
				}
			}
		}

		// Targeted at the library's realistic buffer sizes: a comment whose trailing line
		// ending lands exactly on the k*BufferSize boundary. The (4096, "\r\n", k=2) case
		// mirrors a real config file whose signature comment was 2*BufferSize-2 characters
		// and threw once the file was read with CRLF line endings.
		[Theory]
		[InlineData(4096, "\r\n")]
		[InlineData(4096, "\n")]
		[InlineData(8192, "\r\n")]
		[InlineData(8192, "\n")]
		public void CommentEndingExactlyOnLargeBufferBoundaryDoesNotThrow(int bufferSize, string newLine)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				BufferSize = bufferSize,
			};

			foreach (var k in new[] { 2, 4, 8 })
			{
				// Comment content length so the trailing newline ends on the k*BufferSize boundary.
				var commentLength = k * bufferSize - newLine.Length;
				var csv = "#" + new string('a', commentLength - 1) + newLine + "1,2" + newLine;

				using (var reader = new StringReader(csv))
				using (var parser = new CsvParser(reader, config))
				{
					Assert.True(parser.Read(), $"record after comment (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, k={k})");
					Assert.Equal("1", parser[0]);
					Assert.Equal("2", parser[1]);
					Assert.False(parser.Read(), $"end of file (bufferSize={bufferSize}, newLine={NewLineName(newLine)}, k={k})");
				}
			}
		}

		[Fact]
		public void CommentThatCrossesBuffersShouldNotAddToFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
				BufferSize = 16
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2\r\n");
				writer.Write("#abcdefghijklmnop\r\n");
				writer.Write("3,4");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}

		[Fact]
		public void WriteCommentCharInFieldWithCommentsOffTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("#no comment");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal("#no comment\r\n", result);
			}
		}

		[Fact]
		public void WriteCommentCharInFieldWithCommentsOnTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("#no comment");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal("#no comment\r\n", result);
			}
		}

		[Fact]
		public void WriteCommentWithCommentsOffTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteComment("comment\"has\" quote");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal("#comment\"has\" quote\r\n", result);
			}
		}

		[Fact]
		public void WriteCommentWithCommentsOnTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteComment("comment\"has\" quote");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.Equal("#comment\"has\" quote\r\n", result);
			}
		}
	}
}
