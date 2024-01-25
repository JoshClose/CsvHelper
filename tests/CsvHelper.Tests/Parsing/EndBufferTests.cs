// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	
    public class EndBufferTests
    {
		[Fact]
		public void Read_BufferEndsInOneCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno,pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("abcdefghijklmno", parser[0]);
				Assert.Equal("pqrs", parser[1]);
			}
		}

		[Fact]
		public void Read_BufferEndsInFirstCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmnop;;qrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("abcdefghijklmnop", parser[0]);
				Assert.Equal("qrs", parser[1]);
			}
		}

		[Fact]
		public void Read_BufferEndsInSecondCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno;;pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal(2, parser.Count);
				Assert.Equal("abcdefghijklmno", parser[0]);
				Assert.Equal("pqrs", parser[1]);
			}
		}
	}
}
