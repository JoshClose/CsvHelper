// Copyright 2009-2022 Josh Close
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
	
    public class ByteCountTests
	{
		[Fact]
		public void Read_CRLF_CharCountCorrect()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
			};
			var s = new StringBuilder();
			s.Append("1,2\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

		[Fact]
		public void Read_CR_CharCountCorrect()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
			};
			var s = new StringBuilder();
			s.Append("1,2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

		[Fact]
		public void Read_LF_CharCountCorrect()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
			};
			var s = new StringBuilder();
			s.Append("1,2\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

		[Fact]
		public void Read_NoLineEnding_CharCountCorrect()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
			};
			var s = new StringBuilder();
			s.Append("1,2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

		[Fact]
		public void CharCountFirstCharOfDelimiterNextToDelimiterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
				Delimiter = "!#",
			};
			var s = new StringBuilder();
			s.Append("1!!#2\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

		[Fact]
		public void Read_Trimmed_WhiteSpaceCorrect()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.Unicode,
				CountBytes = true,
				TrimOptions = TrimOptions.Trim
			};
			var s = new StringBuilder();
			s.Append("1, 2\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(config.Encoding.GetByteCount(s.ToString()), parser.ByteCount);
			}
		}

	}
}
