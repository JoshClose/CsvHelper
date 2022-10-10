using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class UseNewObjectForNullReferenceMembersTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture, typeof(Foo));
			Assert.False(config.UseNewObjectForNullReferenceMembers);
		}

		[UseNewObjectForNullReferenceMembers(false)]
		private class Foo { }
	}
}
