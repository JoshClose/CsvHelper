// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Exceptions
{
	[TestClass]
	public class ExceptionTests
	{
		[TestMethod]
		public void NoDefaultConstructorTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,2" );
				writer.WriteLine( "3,4" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var list = csv.GetRecords<NoDefaultConstructor>().ToList();
					Assert.Fail();
				}
				catch( ArgumentException ex )
				{
					var data = ex.Data["CsvHelper"];
					var expected = new StringBuilder();
					expected.AppendLine( "Row: '2' (1 based)" );
					expected.AppendLine( "Type: 'CsvHelper.Tests.Exceptions.ExceptionTests+NoDefaultConstructor'" );
					expected.AppendLine( "Field Index: '-1' (0 based)" );

					Assert.IsNotNull( data );
					Assert.AreEqual( expected.ToString(), data );
				}
			}
		}

		private class NoDefaultConstructor
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public NoDefaultConstructor( int id, string name )
			{
				Id = id;
				Name = name;
			}
		}
	}
}
