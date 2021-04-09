// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvHelper.Tests
{

	public class CsvClassMappingAutoMapTests
	{
		[Fact]
		public void Test()
		{
			var aMap = new AMap();

			Assert.Equal(3, aMap.MemberMaps.Count);
			Assert.Equal(0, aMap.MemberMaps[0].Data.Index);
			Assert.Equal(1, aMap.MemberMaps[1].Data.Index);
			Assert.Equal(2, aMap.MemberMaps[2].Data.Index);
			Assert.True(aMap.MemberMaps[2].Data.Ignore);

			Assert.Single(aMap.ReferenceMaps);
		}

		private class A
		{
			public int One { get; set; }

			public int Two { get; set; }

			public int Three { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public int Four { get; set; }

			public int Five { get; set; }

			public int Six { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				AutoMap(CultureInfo.InvariantCulture);
				Map(m => m.Three).Ignore();
			}
		}

		private sealed class BMap : ClassMap<B>
		{
		}
	}
}
