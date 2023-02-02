using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Xunit;

namespace CsvHelper.Tests.Issues
{
	public class Issue2118
	{
		[Fact]
		public void Issue2118Test()
		{
			var records = new List<Foo>
			{
				new() { Bar = new HashSet<string> { "foo" } },
				new() { Bar = new HashSet<string> { "bar" } },
			};

			string csvString;
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();
				csv.WriteRecords(records);
				csvString = writer.ToString();
			}

			Assert.Equal("Foo,Bar\r\nTrue,False\r\nFalse,True\r\n", csvString);
		}

		private class Foo
		{
			public HashSet<string> Bar { get; set; }
		}

		private sealed class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map().Index(0).Name("Foo").Convert(x => (x.Value as Foo).Bar.Contains("foo") ? "True" : "False");
				Map().Index(1).Name("Bar").Convert(x => (x.Value as Foo).Bar.Contains("bar") ? "True" : "False");
			}
		}
	}
}
