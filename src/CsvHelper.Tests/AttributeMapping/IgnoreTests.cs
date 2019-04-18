﻿// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
	public class IgnoreTests
	{
		[TestMethod]
		public void IgnoreTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<IgnoreTestClass>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		private class IgnoreTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }

			[CsvHelper.Configuration.Attributes.Ignore]
			public DateTime Date { get; set; }
		}
	}
}