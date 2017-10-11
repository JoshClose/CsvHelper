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
    public class ReferenceTests
    {
		[TestMethod]
		public void ReferenceTest()
		{
			using( var reader = new StringReader( "id,name\r\n1,one\r\n" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<ReferenceTestClassA>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].B.Name );
			}
		}

		private class ReferenceTestClassA
		{
			[Name( "id" )]
			public int Id { get; set; }

			public ReferenceTestClassB B { get; set; }
		}

		private class ReferenceTestClassB
		{
			[Name( "name" )]
			public string Name { get; set; }
		}
	}
}
