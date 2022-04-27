// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Serializing
{
	
	public class SanitizationTests
	{
		[Fact]
		public void NoQuoteTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("=one");
				csv.Flush();
				writer.Flush();

				Assert.Equal("\t=one", writer.ToString());
			}
		}

		[Fact]
		public void QuoteTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				ShouldQuote = _ => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("\"=one\"");
				csv.Flush();

				Assert.Equal("\"\t=one\"", writer.ToString());
			}
		}

		[Fact]
		public void NoQuoteChangeEscapeCharacterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				InjectionEscapeCharacter = '\'',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("=one");
				csv.Flush();

				Assert.Equal("'=one", writer.ToString());
			}
		}

		[Fact]
		public void QuoteChangeEscapeCharacterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				InjectionEscapeCharacter = '\'',
				ShouldQuote = _ => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("\"=one\"");
				csv.Flush();

				Assert.Equal("\"'=one\"", writer.ToString());
			}
		}
	}
}
