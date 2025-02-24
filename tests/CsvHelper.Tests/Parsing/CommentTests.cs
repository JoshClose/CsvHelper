// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Parsing
{
	
	public class CommentTests
	{
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
