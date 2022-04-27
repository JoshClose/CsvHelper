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

namespace CsvHelper.Tests.Writing
{
	
    public class CsvModeTests
    {
		[Fact]
		public void WriteField_EscapeMode_ContainsQuote_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\"b", true);
				csv.Flush();

				Assert.Equal("a\\\"b", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoEscapeMode_ContainsQuote_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\"b", true);
				csv.Flush();

				Assert.Equal("a\"b", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_EscapeMode_ContainsDelimiter_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a,b", true);
				csv.Flush();

				Assert.Equal("a\\,b", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoEscapeMode_ContainsDelimiter_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a,b", true);
				csv.Flush();

				Assert.Equal("a,b", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_EscapeMode_ContainsNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
				NewLine = "\n",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\nb", true);
				csv.Flush();

				Assert.Equal("a\\\nb", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_EscapeMode_Contains2CharNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
				NewLine = "\r\n",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\r\nb", true);
				csv.Flush();

				Assert.Equal("a\\\r\\\nb", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoEscapeMode_ContainsNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\r\nb", true);
				csv.Flush();

				Assert.Equal("a\r\nb", writer.ToString());
			}
		}
	}
}
