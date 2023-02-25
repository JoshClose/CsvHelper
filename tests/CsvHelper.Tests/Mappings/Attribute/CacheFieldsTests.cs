using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class CacheFieldsTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).CacheFields);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).CacheFields);
		}

		[CacheFields]
		private class FooTrue { }

		[CacheFields(false)]
		private class FooFalse { }
	}
}
