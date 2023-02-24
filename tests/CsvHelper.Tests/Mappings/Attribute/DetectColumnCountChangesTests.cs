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
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).DetectColumnCountChanges);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).DetectColumnCountChanges);
		}

		[DetectColumnCountChanges]
		private class FooTrue { }

		[DetectColumnCountChanges(false)]
		private class FooFalse { }
	}
}
