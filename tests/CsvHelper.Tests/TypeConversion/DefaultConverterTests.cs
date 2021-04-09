// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
	
    public class DefaultConverterTests
    {
		[Fact]
		public void ConvertToString_ValueIsNull_ReturnsEmptyString()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.Equal(string.Empty, value);
		}

		[Fact]
        public void ConvertToString_SingleNullValue_UsesValue()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { NullValues = { "Foo" } },
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void ConvertToString_MultipleNullValues_UsesFirstValue()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { NullValues = { "Foo", "Bar" } },
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.Equal("Foo", value);
		}

		[Fact]
		public void WriteField_NullValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("Foo");

				csv.WriteField<string>(null);
				csv.Flush();
				writer.Flush();

				Assert.Equal("Foo", writer.ToString());
			}
		}
	}
}
