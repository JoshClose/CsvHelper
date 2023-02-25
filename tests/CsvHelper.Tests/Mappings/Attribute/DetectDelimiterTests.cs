using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class DetectDelimiterTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).DetectDelimiter);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).DetectDelimiter);
		}

		[DetectDelimiter]
		private class FooTrue { }

		[DetectDelimiter(false)]
		private class FooFalse { }
	}
}
