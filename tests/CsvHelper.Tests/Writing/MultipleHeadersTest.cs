// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class MultipleHeadersTest
	{    
		[TestMethod]
		public void GenericTypeTest()
		{
			using( var writer = new StringWriter() )
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
