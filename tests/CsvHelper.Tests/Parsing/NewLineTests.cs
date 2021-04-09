// Copyright 2009-2021 Josh Close
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
	
    public class NewLineTests
    {
		[Fact]
		public void Read_RfcMode_2CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one\a\b");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.RFC4180,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Read_RfcMode_1CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a");
			s.Append("1,one\a");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.RFC4180,
				NewLine = "\a",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Read_RfcMode_2CharNewLine_NoneOnLastLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.RFC4180,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
        public void Read_EscapeMode_2CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one\a\b");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Read_EscapeMode_1CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a");
			s.Append("1,one\a");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				NewLine = "\a",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}

		[Fact]
		public void Read_EscapeMode_2CharNewLine_NoneOnLastLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.True(parser.Read());
				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);

				Assert.False(parser.Read());
			}
		}
	}
}
