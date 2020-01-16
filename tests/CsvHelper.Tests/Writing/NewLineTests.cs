// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class NewLineTests
    {
		[TestMethod]
		public void CRLFTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			using (var writer = new StringWriter())
			using (var serializer = new CsvSerializer(writer, CultureInfo.InvariantCulture))
			{
				var csv = new CsvWriter(serializer);
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.NewLine = NewLine.CRLF;
				csv.WriteRecords(records);

				Assert.AreEqual("1,one\r\n", writer.ToString());
			}
		}

		[TestMethod]
		public void CRTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			using (var writer = new StringWriter())
			using (var serializer = new CsvSerializer(writer, CultureInfo.InvariantCulture))
			{
				var csv = new CsvWriter(serializer);
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.NewLine = NewLine.CR;
				csv.WriteRecords(records);

				Assert.AreEqual("1,one\r", writer.ToString());
			}
		}

		[TestMethod]
		public void LFTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			using (var writer = new StringWriter())
			using (var serializer = new CsvSerializer(writer, CultureInfo.InvariantCulture))
			{
				var csv = new CsvWriter(serializer);
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.NewLine = NewLine.LF;
				csv.WriteRecords(records);

				Assert.AreEqual("1,one\n", writer.ToString());
			}
		}

		[TestMethod]
		public void EnvironmentTest()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			using (var writer = new StringWriter())
			using (var serializer = new CsvSerializer(writer, CultureInfo.InvariantCulture))
			{
				var csv = new CsvWriter(serializer);
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.NewLine = NewLine.Environment;
				csv.WriteRecords(records);

				Assert.AreEqual($"1,one{Environment.NewLine}", writer.ToString());
			}
		}

		private class Foo
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
    }
}
