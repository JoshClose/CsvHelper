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
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).ExceptionMessagesContainRawData);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).ExceptionMessagesContainRawData);
		}

		[ExceptionMessagesContainRawData]
		private class FooTrue { }

		[ExceptionMessagesContainRawData(false)]
		private class FooFalse { }
	}
}
