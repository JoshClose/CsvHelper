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
