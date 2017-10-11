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
    public class HeaderPrefixTests
    {
		[TestMethod]
        public void DefaultHeaderPrefixTest()
		{
			using( var reader = new StringReader( "Id,B.Name,C.Name\r\n1,b,c" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<ADefault>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "b", records[0].B.Name );
				Assert.AreEqual( "c", records[0].C.Name );
			}
		}

		[TestMethod]
		public void CustomHeaderPrefixTest()
		{
			using( var reader = new StringReader( "Id,B_Name,C_Name\r\n1,b,c" ) )
			using( var csv = new CsvReader( reader ) )
			{
				var records = csv.GetRecords<ACustom>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "b", records[0].B.Name );
				Assert.AreEqual( "c", records[0].C.Name );
			}
		}

		private class ADefault
		{
			public int Id { get; set; }

			[HeaderPrefixAttribute]
			public B B { get; set; }

			[HeaderPrefixAttribute]
			public C C { get; set; }
		}

		private class ACustom
		{
			public int Id { get; set; }

			[HeaderPrefixAttribute( "B_" )]
			public B B { get; set; }

			[HeaderPrefixAttribute( "C_" )]
			public C C { get; set; }
		}

		private class B
		{
			public string Name { get; set; }
		}

		private class C
		{
			public string Name { get; set; }
		}
    }
}
