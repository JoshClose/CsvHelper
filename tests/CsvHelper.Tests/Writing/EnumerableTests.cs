using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.Writing
{
	public class EnumerableTests
	{
		[Fact]
		public void WriteRecords_WithProjection_CalledOnce()
		{
			var writer = new StringWriter();
			var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

			var i = 0;
			var r = Enumerable.Range(0, 1).Select(r =>
			{
				i++;
				return (object)new Foo { Id = i, Name = "bar" };
			});

			csv.WriteRecords(r);

			var expected = "Id,Name\r\n1,bar\r\n";

			Assert.Equal(expected, writer.ToString());
		}

		private class Foo
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
			}
		}
	}
}
