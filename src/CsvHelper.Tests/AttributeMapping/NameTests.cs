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
	public class NameTests
    {
		[TestMethod]
		public void NameTest()
		{
			using( var reader = new StringReader( "id,name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<NameTestClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].Name );
			}
		}

		private class NameTestClass
		{
			[Name( "id" )]
			public int Id { get; set; }

			[Name( "name" )]
			public string Name { get; set; }
		}
	}
}
