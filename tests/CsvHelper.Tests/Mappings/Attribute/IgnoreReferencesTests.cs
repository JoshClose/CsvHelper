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
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).IgnoreReferences);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).IgnoreReferences);
		}

		[IgnoreReferences]
		private class FooTrue { }

		[IgnoreReferences(false)]
		private class FooFalse { }
	}
}
