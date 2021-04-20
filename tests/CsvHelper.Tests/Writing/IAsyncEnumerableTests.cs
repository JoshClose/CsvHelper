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

			public string Name { get; set; }
		}
    }
}
