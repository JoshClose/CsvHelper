using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.TypeConversion.DefaultValueTests
{
	public class BitIntegerConverter_ConvertFromString
	{
		[Fact]
		public void InvalidValue_UsesDefault()
		{
			var converter = new BigIntegerConverter();

			var data = new MemberMapData(typeof(Foo).GetProperty(nameof(Foo.Property)));
			data.IsDefaultSet = true;
			data.Default = (BigInteger)1;
			data.UseDefaultOnConversionFailure = true;

			var result = converter.ConvertFromString("foo", null, data);

			Assert.Equal(data.Default, result);
		}

		private class Foo
		{
			public BigInteger Property { get; set; }
		}
	}
}
