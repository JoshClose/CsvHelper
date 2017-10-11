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
    public class NameIndexTests
    {
		[TestMethod]
        public void NameIndexTest()
		{
			using( var reader = new StringReader( "Id,Name,Name\r\n1,one,two\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<NameIndexClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "two", records[0].Name );
			}
		}

		private class NameIndexClass
		{
			public int Id { get; set; }

			[NameIndex( 1 )]
			public string Name { get; set; }
		}
	}
}
