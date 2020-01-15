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
	public class NumberStylesTests
	{
		[TestMethod]
		public void DateTimeStylesTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<NumberStylesTestClass>().ToList();
				var actual = csv.Configuration.Maps.Find<NumberStylesTestClass>().MemberMaps[1].Data.TypeConverterOptions.NumberStyle;

				Assert.AreEqual(NumberStyles.AllowCurrencySymbol, actual);
			}
		}

		private class NumberStylesTestClass
		{
			public int Id { get; set; }

			[NumberStyles(NumberStyles.AllowCurrencySymbol)]
			public string Name { get; set; }
		}
	}
}