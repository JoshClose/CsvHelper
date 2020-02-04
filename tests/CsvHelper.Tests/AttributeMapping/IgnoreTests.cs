// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
	public class IgnoreTests
	{
		[TestMethod]
		public void IgnoreTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
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

				var expected = new StringBuilder();
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.AreEqual(expected.ToString(), writer.ToString());
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

			[CsvHelper.Configuration.Attributes.Ignore]
			public GrandChild GrandChild { get; set; }
		}

		private class GrandChild
		{
			public DateTimeOffset Date { get; set; }
		}
	}
}
