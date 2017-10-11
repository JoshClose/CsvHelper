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
    public class NumberStylesTests
    {
		[TestMethod]
		public void DateTimeStylesTest()
		{
			using( var reader = new StringReader( "Id,Name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<NumberStylesTestClass>().ToList();
				var actual = csv.Configuration.Maps.Find<NumberStylesTestClass>().MemberMaps[1].Data.TypeConverterOptions.NumberStyle;

				Assert.AreEqual( NumberStyles.AllowCurrencySymbol, actual );
			}
		}

		private class NumberStylesTestClass
		{
			public int Id { get; set; }

			[NumberStyles( NumberStyles.AllowCurrencySymbol )]
			public string Name { get; set; }
		}
	}
}
