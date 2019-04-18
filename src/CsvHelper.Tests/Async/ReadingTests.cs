﻿// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Async
{
	[TestClass]
	public class ReadingTests
	{
		[TestMethod]
		public async Task ReadingTest()
		{
			var parser = new ParserMock
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				new [] { "2", "two" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				var records = new List<Simple>();
				await csv.ReadAsync();
				csv.ReadHeader();
				while (await csv.ReadAsync())
				{
					records.Add(csv.GetRecord<Simple>());
				}

				Assert.AreEqual(2, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.Name);

				record = records[1];
				Assert.AreEqual(2, record.Id);
				Assert.AreEqual("two", record.Name);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}