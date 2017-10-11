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
    public class DefaultTests
    {
		[TestMethod]
		public void DefaultTest()
		{
			using( var reader = new StringReader( "Id,Name\r\n1,\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<DefaultTestClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].Name );
			}
		}

		private class DefaultTestClass
		{
			public int Id { get; set; }

			[Default( "one" )]
			public string Name { get; set; }
		}
	}
}
