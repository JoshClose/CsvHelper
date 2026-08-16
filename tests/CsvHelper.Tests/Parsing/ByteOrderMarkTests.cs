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
	// A StreamReader eats the BOM while detecting the encoding, so this only
	// shows up when the text arrives already decoded: a StringReader over an API
	// response or a database column. Left in place the BOM joins the first
	// field -- silently when that field is unquoted, and as bad data when it is
	// quoted, because the field then starts with a character before its opening
	// quote (issue #2311).
	public class ByteOrderMarkTests
	{
		private const string Bom = "﻿";


		// Compare field by field. Assert.Equal on two string[] does not catch a
		// leading U+FEFF difference under xUnit 2.4.1, so an array comparison
		// here would pass with or without the fix.
		private static void AssertRecord(CsvParser parser, params string[] expected)
		{
			var record = parser.Record;
			Assert.NotNull(record);
			Assert.Equal(expected.Length, record!.Length);
			for (var i = 0; i < expected.Length; i++)
			{
				Assert.Equal(expected[i], record[i]);
			}
		}

		private static CsvConfiguration Config(int bufferSize = 0x1000)
		{
			return new CsvConfiguration(CultureInfo.InvariantCulture) { BufferSize = bufferSize };
		}

		[Fact]
		public void QuotedFieldsAfterBomAreReadTest()
		{
			using (var reader = new StringReader(Bom + "\"Id\",\"Name\"\r\n\"1\",\"one\"\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				Assert.True(parser.Read());
				AssertRecord(parser, "Id", "Name");
				Assert.True(parser.Read());
				AssertRecord(parser, "1", "one");
				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void UnquotedFieldAfterBomDoesNotKeepTheMarkTest()
		{
			using (var reader = new StringReader(Bom + "Id,Name\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				Assert.True(parser.Read());
				AssertRecord(parser, "Id", "Name");
			}
		}

		[Fact]
		public void RawRecordExcludesTheBomTest()
		{
			using (var reader = new StringReader(Bom + "Id,Name\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				parser.Read();
				Assert.Equal("Id,Name\r\n", parser.RawRecord);
			}
		}

		// The mark is only a mark at the very start. Anywhere else it is data.
		[Fact]
		public void MarkInsideTheDocumentIsKeptTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1," + Bom + "one\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				parser.Read();
				parser.Read();
				AssertRecord(parser, "1", Bom + "one");
			}
		}

		[Fact]
		public void DocumentWithoutABomIsUnchangedTest()
		{
			using (var reader = new StringReader("Id,Name\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				parser.Read();
				AssertRecord(parser, "Id", "Name");
				Assert.Equal("Id,Name\r\n", parser.RawRecord);
			}
		}

		// The buffer refills mid-document; the mark must not be stripped again.
		[Fact]
		public void SmallBufferStripsTheBomOnlyOnceTest()
		{
			using (var reader = new StringReader(Bom + "Id,Name\r\n1," + Bom + "one\r\n"))
			using (var parser = new CsvParser(reader, Config(bufferSize: 4)))
			{
				parser.Read();
				AssertRecord(parser, "Id", "Name");
				parser.Read();
				AssertRecord(parser, "1", Bom + "one");
			}
		}

		[Fact]
		public async Task QuotedFieldsAfterBomAreReadAsyncTest()
		{
			using (var reader = new StringReader(Bom + "\"Id\",\"Name\"\r\n"))
			using (var parser = new CsvParser(reader, Config()))
			{
				Assert.True(await parser.ReadAsync());
				AssertRecord(parser, "Id", "Name");
			}
		}
	}
}
