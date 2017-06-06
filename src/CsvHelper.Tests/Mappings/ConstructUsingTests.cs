// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class ConstructUsingTests
	{
		[TestMethod]
		public void ConstructUsingNewTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "1,2,3" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<ANewMap>();
				var records = csv.GetRecords<A>().ToList();
				var record = records[0];

				Assert.AreEqual( "a name", record.Name );
				Assert.AreEqual( "b name", record.B.Name );
			}
		}

		[TestMethod]
		public void ConstructUsingMemberInitTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "1,2,3" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<AMemberInitMap>();
				var records = csv.GetRecords<A>().ToList();
				var record = records[0];

				Assert.AreEqual( "a name", record.Name );
				Assert.AreEqual( "b name", record.B.Name );
			}
		}

		private class A
		{
			public string Name { get; set; }

			public B B { get; set; }

			public A() { }

			public A( string name )
			{
				Name = name;
			}
		}

		public class B
		{
			public string Name { get; set; }

			public B() { }

			public B( string name )
			{
				Name = name;
			}
		}

		private sealed class ANewMap : CsvClassMap<A>
		{
			public ANewMap()
			{
				ConstructUsing( () => new A( "a name" ) );
				References<BNewMap>( m => m.B );
			}
		}

		private sealed class BNewMap : CsvClassMap<B>
		{
			public BNewMap()
			{
				ConstructUsing( () => new B( "b name" ) );
			}
		}

		private sealed class AMemberInitMap : CsvClassMap<A>
		{
			public AMemberInitMap()
			{
				ConstructUsing( () => new A { Name = "a name" } );
				References<BMemberInitMap>( m => m.B );
			}
		}

		private sealed class BMemberInitMap : CsvClassMap<B>
		{
			public BMemberInitMap()
			{
				ConstructUsing( () => new B { Name = "b name" } );
			}
		}
	}
}
