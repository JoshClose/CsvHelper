using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class DetectColumnCountChangesTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).DetectColumnCountChanges);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).DetectColumnCountChanges);
		}

		[DetectColumnCountChanges]
		private class FooTrue { }

		[DetectColumnCountChanges(false)]
		private class FooFalse { }
	}
}
