using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class IgnoreTests
    {
		[TestMethod]
        public void WritingWithAllPropertiesIgnoredTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1 },
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.RegisterClassMap<FooMap>();
				csv.WriteRecords(records);

				Assert.AreEqual("\r\n\r\n", writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id).Ignore();
			}
		}
    }
}
