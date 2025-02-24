// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Writing
{
    public class IAsyncEnumerableTests
    {
		[Fact]
        public async Task Test()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
				new Foo { Id = 2, Name = "two" },
			}.ToAsyncEnumerable();

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				await csv.WriteRecordsAsync(records);

				var expected = new StringBuilder();
				expected.Append("Id,Name\r\n");
				expected.Append("1,one\r\n");
				expected.Append("2,two\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }

			public string? Name { get; set; }
		}
    }
}
#endif
