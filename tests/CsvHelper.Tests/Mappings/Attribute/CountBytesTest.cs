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
			var config = new CsvConfiguration(CultureInfo.InvariantCulture, typeof(Foo));
			Assert.True(config.CountBytes);
		}

		[CountBytes(true)]
		private class Foo { }
	}
}
