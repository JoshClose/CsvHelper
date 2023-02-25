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
			Assert.True(CsvConfiguration.FromType<FooTrue>(CultureInfo.InvariantCulture).UseNewObjectForNullReferenceMembers);
			Assert.False(CsvConfiguration.FromType<FooFalse>(CultureInfo.InvariantCulture).UseNewObjectForNullReferenceMembers);
		}

		[UseNewObjectForNullReferenceMembers]
		private class FooTrue { }

		[UseNewObjectForNullReferenceMembers(false)]
		private class FooFalse { }
	}
}
