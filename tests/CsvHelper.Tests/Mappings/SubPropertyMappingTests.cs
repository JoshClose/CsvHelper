// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class SubPropertyMappingTests
	{
		[TestMethod]
		public void ReadTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("P3,P1,P2");
				writer.WriteLine("p3,p1,p2");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<AMap>();
				var records = csv.GetRecords<A>().ToList();

				Assert.AreEqual("p1", records[0].P1);
				Assert.AreEqual("p2", records[0].B.P2);
				Assert.AreEqual("p3", records[0].B.C.P3);
			}
		}

		[TestMethod]
		public void WriteTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<A>()
				{
					new A
					{
						P1 = "p1",
						B = new B
						{
							P2 = "p2",
							C = new C
							{
								P3 = "p3"
							}
						}
					}
				};

				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var expected = "P3,P1,P2\r\n";
				expected += "p3,p1,p2\r\n";
				var result = reader.ReadToEnd();

				Assert.AreEqual(expected, result);
			}
		}

		[TestMethod]
		public void ChangeMemberMapTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<A>();
			map.Map(m => m.B.C.P3).Index(3);
		}

		[TestMethod]
		public void AutoMapInClassMapTest()
		{
			var map = new AAutoMap();
		}

		private class A
		{
			public string P1 { get; set; }
			public B B { get; set; }
		}

		private class B
		{
			public string P2 { get; set; }
			public C C { get; set; }
		}

		private class C
		{
			public string P3 { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				Map(m => m.B.C.P3).Index(0);
				Map(m => m.P1).Index(1);
				Map(m => m.B.P2).Index(2);
			}
		}

		private sealed class AAutoMap : ClassMap<A>
		{
			public AAutoMap()
			{
				AutoMap(CultureInfo.InvariantCulture);
				Map(m => m.B.C.P3).Index(3);
			}
		}
	}
}
