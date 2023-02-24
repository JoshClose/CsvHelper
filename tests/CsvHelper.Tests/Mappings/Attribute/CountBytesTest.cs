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
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).CountBytes);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).CountBytes);
		}

		[CountBytes]
		private class FooTrue { }

		[CountBytes(false)]
		private class FooFalse { }
	}
}
