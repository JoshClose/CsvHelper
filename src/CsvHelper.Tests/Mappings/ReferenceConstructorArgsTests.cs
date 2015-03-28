using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class ReferenceConstructorArgsTests
	{
		[TestMethod]
		public void Test()
		{
			var map = new AMap( "A Field" );
			var name = map.ReferenceMaps[0].Mapping.PropertyMap<B>( m => m.Name ).Data.Names[0];
			Assert.AreEqual( "B Field", name );
		}

		private class A
		{
			public string Name { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public string Name { get; set; }
		}

		private sealed class AMap : CsvClassMap<A>
		{
			public AMap( string name )
			{
				Map( m => m.Name ).Name( name );
				References<BMap>( m => m.B, "B Field" );
			}
		}

		private sealed class BMap : CsvClassMap<B>
		{
			public BMap( string name )
			{
				Map( m => m.Name ).Name( name );
			}
		}
	}
}
