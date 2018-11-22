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
	public class CultureInfoTests
	{
		[TestMethod]
		public void CultureInfoTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<CultureInfoTestClass>().ToList();
				var expected = CultureInfo.GetCultureInfo("jp");
				var actual = csv.Configuration.Maps.Find<CultureInfoTestClass>().MemberMaps[1].Data.TypeConverterOptions.CultureInfo;

				Assert.AreEqual(expected, actual);
			}
		}

		private class CultureInfoTestClass
		{
			public int Id { get; set; }

			[CultureInfo("jp")]
			public string Name { get; set; }
		}
	}
}