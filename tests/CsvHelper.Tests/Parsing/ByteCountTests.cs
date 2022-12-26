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

		[Theory]
		[MemberData(nameof(Utf8CharsData))]
		public void UTF8_ByteCounts(char[] chars, long expectedByteCount)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Encoding = Encoding.UTF8,
				CountBytes = true,
			};
			using (var reader = new CharsReader(chars))
			using (var parser = new CsvParser(reader, config))
			{
				while (parser.Read()) { }

				Assert.Equal(expectedByteCount, parser.ByteCount);
			}
		}

		public static IEnumerable<object[]> Utf8CharsData =>
		   new List<object[]>
		   {
				new object[] { "ABC✋😉👍".ToCharArray(), Encoding.UTF8.GetByteCount("ABC✋😉👍") },
				new object[] { "𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟".ToCharArray(), Encoding.UTF8.GetByteCount("𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟") },
				new object[] { new char[] { '\u0232' }, 2 }, // U+0232 (Ȳ - LATIN CAPITAL LETTER Y WITH MACRON)
				new object[] { new char[] { '\u0985' }, 3 }, // U+0985 (অ - BENGALI LETTER A)
				new object[] { new char[] { '\ud83d', '\ude17' }, 4 }, // U+1F617 (😗 - KISSING FACE)
				// The next line tests the encoder is flushed correctly: if the supplied TextReader terminates
				// on an unpaired (high) surrogate character then only upon flushing the encoder will the
				// ByteCount be increased, in this case by 3 corresponding to the number of UTF8 bytes
				// of the replacement character U+FFFD (the default fallback behaviour of the static Encoding.UTF8).
				new object[] { new char[] { '\ud800' }, 3 },
		   };

		private class CharsReader : TextReader
		{
			private readonly char[] _chars;
			private int idx = -1;

			public CharsReader(char[] chars)
			{
				_chars = chars;
			}

			public override int Peek()
			{
				return idx + 1 >= _chars.Length ? -1 : _chars[idx + 1];
			}

			public override int Read()
			{
				return idx + 1 >= _chars.Length ? -1 : _chars[++idx];
			}
		}
	}
}
