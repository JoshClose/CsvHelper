using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class IgnoreReferencesTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).IgnoreReferences);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).IgnoreReferences);
		}

		[IgnoreReferences]
		private class FooTrue { }

		[IgnoreReferences(false)]
		private class FooFalse { }
	}
}
