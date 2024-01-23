// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	
    public class NewLineTests
    {
		[Fact]
		public void CRLFTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(records);

				Assert.Equal("1,one\r\n", writer.ToString());
			}
		}

		[Fact]
		public void CRTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				NewLine = "\r",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(records);

				Assert.Equal("1,one\r", writer.ToString());
			}
		}

		[Fact]
		public void LFTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				NewLine = "\n",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(records);

				Assert.Equal("1,one\n", writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
    }
}
