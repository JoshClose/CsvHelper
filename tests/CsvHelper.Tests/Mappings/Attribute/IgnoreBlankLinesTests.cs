// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.AttributeMapping
{
	public class IgnoreBlankLinesTests
	{
		[Fact]
		public void IgnoreBlankLinesTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<IgnoreBlankLinesTestClass>().ToList();
				var actual = csv.Configuration.IgnoreBlankLines;

				Assert.True(actual);
			}
		}

		[IgnoreBlankLines(true)]
		private class IgnoreBlankLinesTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
