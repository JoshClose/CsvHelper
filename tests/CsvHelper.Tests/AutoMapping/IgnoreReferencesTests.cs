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
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
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
