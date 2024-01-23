// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Mappings.Attribute
{
	
	public class DefaultTests
	{
		[Fact]
		public void DefaultTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<DefaultTestClass>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
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
