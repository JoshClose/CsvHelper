using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class NewLineTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = CsvConfiguration.FromType<Foo>(CultureInfo.InvariantCulture);
			Assert.Equal("a", config.NewLine);
		}

		[NewLine("a")]
		private class Foo { }
	}
}
