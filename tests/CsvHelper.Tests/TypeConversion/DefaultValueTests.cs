// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class DefaultValueTests
	{
		[Fact]
		public void BigIntegerConverter_ConvertFromString_InvalidValue_UsesDefault()
		{
			var converter = new BigIntegerConverter();

			var data = new MemberMapData(typeof(Foo).GetProperty(nameof(Foo.Property)));
			data.IsDefaultSet = true;
			data.Default = (BigInteger)1;
			data.UseDefaultOnConversionFailure = true;

			var result = converter.ConvertFromString("foo", null, data);

			Assert.Equal(data.Default, result);
		}

		[Fact]
		public void GetRecords_EmptyValue_DefaultSet_UsesDefault()
		{
			var s = new StringBuilder();
			s.Append("Property\r\n");
			s.Append("foo\r\n");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Equal(1, records[0].Property);
			}
		}

		private class Foo
		{
			public BigInteger Property { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Property).Default(1, useOnConversionFailure: true);
			}
		}
	}
}
