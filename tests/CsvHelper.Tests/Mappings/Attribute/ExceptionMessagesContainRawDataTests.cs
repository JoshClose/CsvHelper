using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class ExceptionMessagesContainRawDataTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).ExceptionMessagesContainRawData);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).ExceptionMessagesContainRawData);
		}

		[ExceptionMessagesContainRawData]
		private class FooTrue { }

		[ExceptionMessagesContainRawData(false)]
		private class FooFalse { }
	}
}
