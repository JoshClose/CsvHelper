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
    public class IgnoreTests
    {
		[TestMethod]
		public void IgnoreTest()
		{
			using( var reader = new StringReader( "Id,Name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<IgnoreTestClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].Name );
			}
		}

		private class IgnoreTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }

			[CsvHelper.Configuration.Attributes.Ignore]
			public DateTime Date { get; set; }
		}
	}
}
