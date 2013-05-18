// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CsvHelper.Configuration;

namespace CsvHelper.Tests
{
	[TestClass]
	public class AutoMappingTests
	{
		[TestMethod]
		public void ReaderSimpleTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<Simple>().ToList();

				Assert.IsNotNull( list );
				Assert.AreEqual( 2, list.Count );
				var row = list[0];
				Assert.AreEqual( 1, row.Id );
				Assert.AreEqual( "one", row.Name );
				row = list[1];
				Assert.AreEqual( 2, row.Id );
				Assert.AreEqual( "two", row.Name );
			}
		}

		[TestMethod]
		public void ReaderReferenceTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "AId,BId" );
				writer.WriteLine( "1,2" );
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.IsNotNull( list );
				Assert.AreEqual( 1, list.Count );
				var row = list[0];
				Assert.AreEqual( 1, row.AId );
				Assert.IsNotNull( row.B );
				Assert.AreEqual( 2, row.B.BId );
			}
		}

		[TestMethod]
		public void ReaderReferenceNoDefaultConstructorTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<SimpleReferenceNoDefaultConstructor>().ToList();

				Assert.IsNotNull( list );
				Assert.AreEqual( 1, list.Count );
				var row = list[0];
				Assert.AreEqual( 1, row.Id );
				Assert.IsNull( row.Ref );
			}
		}

		[TestMethod]
		public void WriterSimpleTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" }
				};
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "Id,Name" );
				expected.AppendLine( "1,one" );

				Assert.AreEqual( expected.ToString(), data );
			}
		}

		[TestMethod]
		public void WriterReferenceTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<A>
				{
					new A
					{
						AId = 1,
						B = new B
						{
							BId = 2
						}
					}
				};
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "AId,BId" );
				expected.AppendLine( "1,2" );

				Assert.AreEqual( expected.ToString(), data );
			}
		}

		[TestMethod]
		public void WriterReferenceNoDefaultConstructorTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var list = new List<SimpleReferenceNoDefaultConstructor>
				{
					new SimpleReferenceNoDefaultConstructor
					{
						Id = 1,
						Ref = new NoDefaultConstructor( "one" )
					}
				};
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "Id" );
				expected.AppendLine( "1" );

				Assert.AreEqual( expected.ToString(), data );
			}
		}

		[TestMethod]
		public void AutoMapEnumerableTest()
		{
			var config = new CsvConfiguration();
			try
			{
				config.AutoMap( typeof( List<string> ) );
				Assert.Fail();
			}
			catch( CsvConfigurationException )
			{
			}
		}

		[TestMethod]
		public void AutoMapWithExistingMapTest()
		{
			var config = new CsvConfiguration();
			config.Maps.Add( new SimpleMap() );
			var data = new
			{
				Simple = new Simple
				{
					Id = 1,
					Name = "one"
				}
			};
			var map = config.AutoMap( data.GetType() );

			Assert.IsNotNull( map );
			Assert.AreEqual( 0, map.PropertyMaps.Count );
			Assert.AreEqual( 1, map.ReferenceMaps.Count );
			Assert.IsInstanceOfType( map.ReferenceMaps[0].Mapping, typeof( SimpleMap ) );
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class SimpleMap : CsvClassMap<Simple>
		{
			public SimpleMap()
			{
				Map( m => m.Id );
				Map( m => m.Name );
			}
		}

		private class A
		{
			public int AId { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public int BId { get; set; }
		}

		private class SimpleReferenceNoDefaultConstructor
		{
			public int Id { get; set; }

			public NoDefaultConstructor Ref { get; set; }
		}

		private class NoDefaultConstructor
		{
			public string Name { get; set; }

			public NoDefaultConstructor( string name )
			{
				Name = name;
			}
		}
	}
}
