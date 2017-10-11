using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
    public class FormatTests
	{
		[TestMethod]
		public void FormatTest()
		{
			using( var reader = new StringReader( "Id,Name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<FormatTestClass>().ToList();
				var actual = csv.Configuration.Maps.Find<FormatTestClass>().MemberMaps[1].Data.TypeConverterOptions.Formats[0];

				Assert.AreEqual( "abc", actual );
			}
		}

		private class FormatTestClass
		{
			public int Id { get; set; }

			[Format( "abc" )]
			public string Name { get; set; }
		}
	}
}
