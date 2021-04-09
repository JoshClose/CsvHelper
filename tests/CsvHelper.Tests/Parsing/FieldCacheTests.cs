// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	
    public class FieldCacheTests
    {
		[Fact]
		public void Read_WithFieldCacheEnabled_ReturnsSameFieldInstance()
		{
			var s = new StringBuilder();
			s.Append("1,2\r\n");
			s.Append("2,1\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CacheFields = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				var a = parser[0];
				parser.Read();
				var b = parser[1];

				Assert.Same(a, b);
			}
		}

		[Fact]
		public void Read_WithFieldCacheDisabled_ReturnsDifferentFieldInstance()
		{
			var s = new StringBuilder();
			s.Append("1,2\r\n");
			s.Append("2,1\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				CacheFields = false,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				var a = parser[0];
				parser.Read();
				var b = parser[1];

				Assert.NotSame(a, b);
			}
		}

		[Fact]
		public void Test1()
		{
			// "542008", "27721116", "98000820" have hash code 3769566006

			var value1 = "542008";
			var value2 = "27721116";
			var value3 = "98000820";
			var value4 = "542008";

			var cache = new FieldCache(1);

			var field1 = cache.GetField(value1.ToCharArray(), 0, value1.Length);
			var field2 = cache.GetField(value2.ToCharArray(), 0, value2.Length);
			var field3 = cache.GetField(value3.ToCharArray(), 0, value3.Length);
			var field4 = cache.GetField(value4.ToCharArray(), 0, value4.Length);

			Assert.Equal(value1, field1);
			Assert.Equal(value2, field2);
			Assert.Equal(value3, field3);
			Assert.Equal(value4, field4);

			Assert.NotSame(value1, field1);
			Assert.NotSame(value2, field2);
			Assert.NotSame(value3, field3);
			Assert.NotSame(value4, field4);

			Assert.Same(field1, field4);
		}
	}
}
