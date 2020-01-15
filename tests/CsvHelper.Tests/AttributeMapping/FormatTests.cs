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
	public class FormatTests
	{
		[TestMethod]
		public void FormatTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<FormatTestClass>().ToList();
				var actual = csv.Configuration.Maps.Find<FormatTestClass>().MemberMaps[1].Data.TypeConverterOptions.Formats[0];

				Assert.AreEqual("abc", actual);
			}
		}

		private class FormatTestClass
		{
			public int Id { get; set; }

			[Format("abc")]
			public string Name { get; set; }
		}
	}
}
