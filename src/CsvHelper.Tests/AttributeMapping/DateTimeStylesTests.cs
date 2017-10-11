using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
    public class DateTimeStylesTests
    {
		[TestMethod]
		public void DateTimeStylesTest()
		{
			using( var reader = new StringReader( "Id,Name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<DateTimeStylesTestClass>().ToList();
				var actual = csv.Configuration.Maps.Find<DateTimeStylesTestClass>().MemberMaps[1].Data.TypeConverterOptions.DateTimeStyle;

				Assert.AreEqual( DateTimeStyles.AdjustToUniversal, actual );
			}
		}

		private class DateTimeStylesTestClass
		{
			public int Id { get; set; }

			[DateTimeStyles( DateTimeStyles.AdjustToUniversal )]
			public string Name { get; set; }
		}
	}
}
