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
		public void ConstructUsingTest()
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
				csv.Configuration.RegisterClassMap<AMap>();
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

			public A( string name )
			{
				Name = name;
			}
		}

		public class B
		{
			public string Name { get; set; }

			public B( string name )
			{
				Name = name;
			}
		}

		private sealed class AMap : CsvClassMap<A>
		{
			public AMap()
			{
				ConstructUsing( () => new A( "a name" ) );
				References<BMap>( m => m.B );
			}
		}

		private sealed class BMap : CsvClassMap<B>
		{
			public BMap()
			{
				ConstructUsing( () => new B( "b name" ) );
			}
		}
	}
}
