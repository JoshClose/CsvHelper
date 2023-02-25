using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class WhiteSpaceCharsTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = CsvConfiguration.FromType<Foo>(CultureInfo.InvariantCulture);
			Assert.Equal(new[] { 'a', 'b' }, config.WhiteSpaceChars);
		}

		[WhiteSpaceChars("a b")]
		private class Foo { }
	}
}
