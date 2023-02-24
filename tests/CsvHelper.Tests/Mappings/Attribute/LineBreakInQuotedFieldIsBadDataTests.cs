using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class LineBreakInQuotedFieldIsBadDataTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).LineBreakInQuotedFieldIsBadData);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).LineBreakInQuotedFieldIsBadData);
		}

		[LineBreakInQuotedFieldIsBadData]
		private class FooTrue { }

		[LineBreakInQuotedFieldIsBadData(false)]
		private class FooFalse { }
	}
}
