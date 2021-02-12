// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class ShouldSkipRecordTests
	{
		[TestMethod]
		public void SkipEmptyHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldSkipRecord = args => args.Record.All(string.IsNullOrWhiteSpace),
			};
			var parser = new ParserMock(config)
			{
				{ " " },
				{ "First,Second" },
				{ "1", "2" },
			};

			var csv = new CsvReader(parser);

			csv.Read();
			csv.ReadHeader();
			csv.Read();
			Assert.AreEqual("1", csv.GetField(0));
			Assert.AreEqual("2", csv.GetField(1));
		}

		[TestMethod]
		public void SkipEmptyRowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldSkipRecord = args => args.Record.All(string.IsNullOrWhiteSpace),
			};
			var parser = new ParserMock(config)
			{
				{ "First,Second" },
				{ " " },
				{ "1", "2" },
			};

			var csv = new CsvReader(parser);

			csv.Read();
			csv.ReadHeader();
			csv.Read();
			Assert.AreEqual("1", csv.GetField(0));
			Assert.AreEqual("2", csv.GetField(1));
		}

		[TestMethod]
		public void ShouldSkipWithEmptyRows()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldSkipRecord = args => args.Record[0].StartsWith("skipme") || args.Record.All(string.IsNullOrWhiteSpace),
			};

			var parser = new ParserMock(config)
			{
				{ "First,Second" },
				{ "skipme," },
				{ "" },
				{ "1", "2" },
			};

			var csv = new CsvReader(parser);

			csv.Read();
			csv.Read();
			Assert.AreEqual("1", csv.GetField(0));
			Assert.AreEqual("2", csv.GetField(1));
		}
	}
}
