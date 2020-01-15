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
	public class BooleanValuesTests
	{
		[TestMethod]
		public void BooleanValuesTest()
		{
			using (var reader = new StringReader("IsTrue,IsFalse\r\ntrue,false\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<BooleanValuesTestClass>().ToList();
				Assert.AreEqual(true, records[0].IsTrue);
				Assert.AreEqual(false, records[0].IsFalse);
			}
		}

		private class BooleanValuesTestClass
		{
			[BooleanTrueValues("true")]
			public bool? IsTrue { get; set; }

			[BooleanFalseValues("false")]
			public bool? IsFalse { get; set; }
		}
	}
}
