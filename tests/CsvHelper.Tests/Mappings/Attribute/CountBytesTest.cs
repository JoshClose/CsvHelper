using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class CountBytesTest
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).CountBytes);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).CountBytes);
		}

		[CountBytes]
		private class FooTrue { }

		[CountBytes(false)]
		private class FooFalse { }
	}
}
