using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class IgnoreReferencesTests
	{
		[TestMethod]
		public void IgnoreReferncesWritingTest()
		{
			var records = new List<Foo>
			{
				new Foo
				{
					Id = 1,
					Bar = new Bar { Name = "one" }
				}
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.IgnoreReferences = true;
				csv.WriteRecords(records);
				writer.Flush();

				var expected = new StringBuilder();
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }

			public Bar Bar { get; set; }
		}

		private class Bar
		{
			public string Name { get; set; }
		}
	}
}
