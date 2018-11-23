// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;
using System;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderReferenceMappingSkipTests
	{
		[TestMethod]
		public void ReferencesWithSkipTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.RegisterClassMap<AMap>();

				writer.WriteLine("AId,BId,CId,DId");
				writer.WriteLine("a1,b1,c1,d1");
				writer.WriteLine("a2,b2,c2,skip");
				writer.WriteLine("a3,b3,skip,d3");
				writer.WriteLine("a4,skip,c4,d4");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.IsNotNull(list);
				Assert.AreEqual(4, list.Count);

                for (var i = 0; i < 4; i++)
				{
					var rowId = i + 1;
					var row = list[i];
					Assert.AreEqual("a" + rowId, row.Id);

                    if (i < 3)
					    Assert.AreEqual("b" + rowId, row.B.Id);
                    if (i < 2)
					    Assert.AreEqual("c" + rowId, row.B.C.Id);
                    if (i < 1)
					    Assert.AreEqual("d" + rowId, row.B.C.D.Id);
                }

                Assert.IsNull(list[1].B.C.D);
                Assert.IsNull(list[2].B.C);
                Assert.IsNull(list[3].B);
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

			public D D { get; set; }
		}

		private class D
		{
			public string Id { get; set; }
		}
        
		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				Map(m => m.Id).Name("AId");
				References<BMap>(m => m.B).Skip(row =>
                {
                    return string.Equals(row.GetField("BId"), "skip", StringComparison.OrdinalIgnoreCase);
                });
			}
		}

		private sealed class BMap : ClassMap<B>
		{
			public BMap()
			{
				Map(m => m.Id).Name("BId");
				References<CMap>(m => m.C).Skip(row =>
                {
                    return string.Equals(row.GetField("CId"), "skip", StringComparison.OrdinalIgnoreCase);
                });
			}
		}

		private sealed class CMap : ClassMap<C>
		{
			public CMap()
			{
				Map(m => m.Id).Name("CId");
				References<DMap>(m => m.D).Skip(row =>
                {
                    return string.Equals(row.GetField("DId"), "skip", StringComparison.OrdinalIgnoreCase);
                });
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