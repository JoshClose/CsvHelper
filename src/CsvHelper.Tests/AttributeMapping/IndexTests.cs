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
    public class IndexTests
    {
		[TestMethod]
		public void IndexTest()
		{
			using( var reader = new StringReader( "a,1,b,one,c\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords<IndexTestClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].Name );
			}
		}

		private class IndexTestClass
		{
			[Index( 1 )]
			public int Id { get; set; }

			[Index( 3 )]
			public string Name { get; set; }
		}
	}
}
