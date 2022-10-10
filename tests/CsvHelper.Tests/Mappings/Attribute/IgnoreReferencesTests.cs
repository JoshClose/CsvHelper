using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class IgnoreReferencesTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture, typeof(Foo));
			Assert.True(config.IgnoreReferences);
		}

		[IgnoreReferences(true)]
		private class Foo { }
	}
}
