// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using Xunit;

namespace CsvHelper.Tests.Writing
{
	public class WriteCustomEscapeTests
	{
		[Fact]
		public void WriteField_CustomEscapeChar_ModeRFC4180_EscapesQuotesAndEscapeCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				Escape = '\\',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				// {"json":"{\"name\":\"foo\"}"}
				// json string -> csv field
				// "{\"json\":\"{\\\"name\\\":\\\"foo\\\"}\"}"
				csv.WriteField(@"{""json"":""{\""name\"":\""foo\""}""}");
				csv.Flush();

				var expected = @"""{\""json\"":\""{\\\""name\\\"":\\\""foo\\\""}\""}""";
				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_CustomEscapeChar_ModeEscape_EscapesQuotesAndEscapeCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				Escape = '\\',
				Mode = CsvMode.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				// {"json":"{\"name\":\"foo\"}"}
				// json string -> csv field
				// {\"json\":\"{\\\"name\\\":\\\"foo\\\"}\"}
				csv.WriteField(@"{""json"":""{\""name\"":\""foo\""}""}");
				csv.Flush();

				var expected = @"{\""json\"":\""{\\\""name\\\"":\\\""foo\\\""}\""}";
				Assert.Equal(expected, writer.ToString());
			}
		}
	}
}
