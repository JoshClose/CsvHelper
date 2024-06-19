// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.IO;
using System.Linq;
using Xunit;
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvHelper.Tests
{
	
	public class CsvReaderReferenceMappingTests
	{
		[Fact]
		public void NestedReferencesClassMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<AMap>();

				writer.WriteLine("AId,BId,CId,DId");
				writer.WriteLine("a1,b1,c1,d1");
				writer.WriteLine("a2,b2,c2,d2");
				writer.WriteLine("a3,b3,c3,d3");
				writer.WriteLine("a4,b4,c4,d4");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.NotNull(list);
				Assert.Equal(4, list.Count);

				for (var i = 0; i < 4; i++)
				{
					var rowId = i + 1;
					var row = list[i];
					Assert.Equal("a" + rowId, row.Id);
					Assert.Equal("b" + rowId, row.B.Id);
					Assert.Equal("c" + rowId, row.B.C.Id);
					Assert.Equal("d" + rowId, row.B.C.D.Id);
				}
			}
		}

		private class A
		{
			public string Id { get; set; } = string.Empty;

			public B B { get; set; } = new B();
		}

		private class B
		{
			public string Id { get; set; } = string.Empty;

			public C C { get; set; } = new C();
		}

		private class C
		{
			public string Id { get; set; } = string.Empty;

			public D D { get; set; } = new D();
		}

		private class D
		{
			public string Id { get; set; } = string.Empty;
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				Map(m => m.Id).Name("AId");
				References<BMap>(m => m.B);
			}
		}

		private sealed class BMap : ClassMap<B>
		{
			public BMap()
			{
				Map(m => m.Id).Name("BId");
				References<CMap>(m => m.C);
			}
		}

		private sealed class CMap : ClassMap<C>
		{
			public CMap()
			{
				Map(m => m.Id).Name("CId");
				References<DMap>(m => m.D);
			}
		}

		private sealed class DMap : ClassMap<D>
		{
			public DMap()
			{
				Map(m => m.Id).Name("DId");
			}
		}

	}
}
