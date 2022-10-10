using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class MemberTypesTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture, typeof(Foo));
			Assert.Equal(MemberTypes.Fields, config.MemberTypes);
		}

		[MemberTypes(MemberTypes.Fields)]
		private class Foo { }
	}
}
