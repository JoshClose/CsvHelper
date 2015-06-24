// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderReferenceMappingPrefixTests
	{
		[TestMethod]
		public void ReferencesWithPrefixTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				csv.Configuration.RegisterClassMap<AMap>();

				writer.WriteLine( "Id,BPrefix_Id,C.CId" );
				writer.WriteLine( "a1,b1,c1" );
				writer.WriteLine( "a2,b2,c2" );
				writer.WriteLine( "a3,b3,c3" );
				writer.WriteLine( "a4,b4,c4" );
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.IsNotNull( list );
				Assert.AreEqual( 4, list.Count );

				for( var i = 0; i < 4; i++ )
				{
					var rowId = i + 1;
					var row = list[i];
					Assert.AreEqual( "a" + rowId, row.Id );
					Assert.AreEqual( "b" + rowId, row.B.Id );
					Assert.AreEqual( "c" + rowId, row.B.C.Id );
				}
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
