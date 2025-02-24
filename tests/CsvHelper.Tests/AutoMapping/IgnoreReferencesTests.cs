// Copyright 2009-2024 Josh Close
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

namespace CsvHelper.Tests.AutoMapping
{
	
	public class IgnoreReferencesTests
	{
		[Fact]
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

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreReferences = true,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(records);
				writer.Flush();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id");
				expected.AppendLine("1");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }

			public Bar Bar { get; set; } = new Bar();
		}

		private class Bar
		{
			public string Name { get; set; } = string.Empty;
		}
	}
}
