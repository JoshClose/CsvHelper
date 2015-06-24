// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterReferenceMappingPrefixTests
	{
		[TestMethod]
		public void ReferencesWithPrefixTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<AMap>();

				var list = new List<A>();
				for( var i = 0; i < 4; i++ )
				{
					var row = i + 1;
					list.Add( new A
					{
						Id = "a" + row,
						B = new B
						{
							Id = "b" + row,
							C = new C
							{
								Id = "c" + row
							}
						}
					} );
				}

				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "Id,BPrefix_Id,C.CId" );
				expected.AppendLine( "a1,b1,c1" );
				expected.AppendLine( "a2,b2,c2" );
				expected.AppendLine( "a3,b3,c3" );
				expected.AppendLine( "a4,b4,c4" );
				Assert.AreEqual( expected.ToString(), data );
			}
		}

		private class A
		{
			public string Id { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public string Id { get; set; }

			public C C { get; set; }
		}

		private class C
		{
			public string Id { get; set; }
		}

		private sealed class AMap : CsvClassMap<A>
		{
			public AMap()
			{
				Map( m => m.Id );
				References<BMap>( m => m.B ).Prefix( "BPrefix_" );
			}
		}

		private sealed class BMap : CsvClassMap<B>
		{
			public BMap()
			{
				Map( m => m.Id );
				References<CMap>( m => m.C ).Prefix();
			}
		}

		private sealed class CMap : CsvClassMap<C>
		{
			public CMap()
			{
				Map( m => m.Id ).Name( "CId" );
			}
		}
	}
}
