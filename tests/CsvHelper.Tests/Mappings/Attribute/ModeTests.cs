using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class ModeTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = CsvConfiguration.FromType<Foo>(CultureInfo.InvariantCulture);
			Assert.Equal(CsvMode.Escape, config.Mode);
		}

		[Mode(CsvMode.Escape)]
		private class Foo { }
	}
}
