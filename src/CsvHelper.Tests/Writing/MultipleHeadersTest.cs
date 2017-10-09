using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class MultipleHeadersTest
    {    
		[TestMethod]
		public void GenericTypeTest()
		{
			using( var writer = new StringWriter() )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.WriteHeader<A>();
				csv.NextRecord();
				csv.WriteRecord( new A { Id = 1 } );
				csv.NextRecord();

				csv.WriteHeader<B>();
				csv.NextRecord();
				csv.WriteRecord( new B { Name = "one" } );
				csv.NextRecord();
				writer.Flush();

				var expected = new StringBuilder();
				expected.AppendLine( "Id" );
				expected.AppendLine( "1" );
				expected.AppendLine( "Name" );
				expected.AppendLine( "one" );

				Assert.AreEqual( expected.ToString(), writer.ToString() );
			}
		}

		[TestMethod]
		public void DynamicTypeTest()
		{
			using( var writer = new StringWriter() )
			using( var csv = new CsvWriter( writer ) )
			{
				dynamic a = new ExpandoObject();
				a.Id = 1;
				csv.WriteDynamicHeader( a );
				csv.NextRecord();
				csv.WriteRecord( a );
				csv.NextRecord();

				dynamic b = new ExpandoObject();
				b.Name = "one";
				csv.WriteDynamicHeader( b );
				csv.NextRecord();
				csv.WriteRecord( b );
				csv.NextRecord();
				writer.Flush();

				var expected = new StringBuilder();
				expected.AppendLine( "Id" );
				expected.AppendLine( "1" );
				expected.AppendLine( "Name" );
				expected.AppendLine( "one" );

				Assert.AreEqual( expected.ToString(), writer.ToString() );
			}
		}

		private class A
		{
			public int Id { get; set; }
		}

		private class B
		{
			public string Name { get; set; }
		}
    }
}
