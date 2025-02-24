﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Mappings
{
	
	public class ReferenceConstructorArgsTests
	{
		[Fact]
		public void Test()
		{
			var map = new AMap( "A Field" );
			var name = map.ReferenceMaps[0].Data.Mapping.MemberMaps.Find<B>( m => m.Name )?.Data.Names[0];
			Assert.Equal( "B Field", name );
		}

		private class A
		{
			public string? Name { get; set; }

			public B B { get; set; } = new B();
		}

		private class B
		{
			public string? Name { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap( string name )
			{
				Map( m => m.Name ).Name( name );
				References<BMap>( m => m.B, "B Field" );
			}
		}

		private sealed class BMap : ClassMap<B>
		{
			public BMap( string name )
			{
				Map( m => m.Name ).Name( name );
			}
		}
	}
}
