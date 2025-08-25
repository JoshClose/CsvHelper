using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests;

public class CsvHelperExceptionTests
{
	[Fact]
	public void ExceptionStoresOriginalMessageInOriginalMessage()
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = false,
		};
		var parser = new ParserMock(config)
		{
			{ "Id", "Name" }
		};

		var csv = new CsvReader(parser);

		var error = Assert.Throws<ReaderException>(() => csv.ReadHeader());
		Assert.StartsWith("Configuration.HasHeaderRecord is false.", error.Message);
		Assert.NotEqual("Configuration.HasHeaderRecord is false.", error.Message);
		Assert.Equal("Configuration.HasHeaderRecord is false.", error.OriginalMessage);
	}
}
