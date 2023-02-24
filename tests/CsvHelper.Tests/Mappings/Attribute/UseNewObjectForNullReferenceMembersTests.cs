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
			Assert.True(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooTrue)).UseNewObjectForNullReferenceMembers);
			Assert.False(new CsvConfiguration(CultureInfo.InvariantCulture, typeof(FooFalse)).UseNewObjectForNullReferenceMembers);
		}

		[UseNewObjectForNullReferenceMembers]
		private class FooTrue { }

		[UseNewObjectForNullReferenceMembers(false)]
		private class FooFalse { }
	}
}
