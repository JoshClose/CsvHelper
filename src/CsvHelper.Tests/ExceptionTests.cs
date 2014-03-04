// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
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
					expected.AppendLine( "Type: 'CsvHelper.Tests.ExceptionTests+NoDefaultConstructor'" );
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
