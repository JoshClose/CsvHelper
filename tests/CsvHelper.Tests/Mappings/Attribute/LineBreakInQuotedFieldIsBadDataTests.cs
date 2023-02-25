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
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).LineBreakInQuotedFieldIsBadData);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).LineBreakInQuotedFieldIsBadData);
		}

		[LineBreakInQuotedFieldIsBadData]
		private class FooTrue { }

		[LineBreakInQuotedFieldIsBadData(false)]
		private class FooFalse { }
	}
}
