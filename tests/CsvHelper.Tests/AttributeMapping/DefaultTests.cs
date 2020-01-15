// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
	public class DefaultTests
	{
		[TestMethod]
		public void DefaultTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<DefaultTestClass>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		private class DefaultTestClass
		{
			public int Id { get; set; }

			[Default("one")]
			public string Name { get; set; }
		}
	}
}
