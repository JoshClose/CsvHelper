// Copyright 2009-2022 Josh Close
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
	
	public class DateTimeStylesTests
	{
		[Fact]
		public void DateTimeStylesTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<DateTimeStylesTestClass>().ToList();
				var actual = csv.Context.Maps.Find<DateTimeStylesTestClass>().MemberMaps[1].Data.TypeConverterOptions.DateTimeStyle;

				Assert.Equal(DateTimeStyles.AdjustToUniversal, actual);
			}
		}

		private class DateTimeStylesTestClass
		{
			public int Id { get; set; }

			[DateTimeStyles(DateTimeStyles.AdjustToUniversal)]
			public string Name { get; set; }
		}
	}
}
