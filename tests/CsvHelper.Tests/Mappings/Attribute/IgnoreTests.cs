// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.Mappings.Attribute
{
	
	public class IgnoreTests
	{
		[Fact]
		public void IgnoreTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void IgnoreReferenceTest()
		{
			var records = new List<Parent>
			{
				new Parent
				{
					Id = 1,
					Child = new Child
					{
						Name = "one",
						GrandChild = new GrandChild
						{
							Date = DateTimeOffset.Now
						}
					}
				}
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }

			public string Name { get; set; }

			[CsvHelper.Configuration.Attributes.Ignore]
			public DateTime Date { get; set; }
		}

		private class Parent
		{
			public int Id { get; set; }

			[CsvHelper.Configuration.Attributes.Ignore]
			public Child Child { get; set; }
		}

		private class Child
		{
			public string Name { get; set; }

			public GrandChild GrandChild { get; set; }
		}

		private class GrandChild
		{
			public DateTimeOffset Date { get; set; }
		}
	}
}
