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
	public class BufferSizeTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			var config = CsvConfiguration.FromType<Foo>(CultureInfo.InvariantCulture);
			Assert.Equal(2, config.BufferSize);
		}

		[BufferSize(2)]
		private class Foo { }
	}
}
