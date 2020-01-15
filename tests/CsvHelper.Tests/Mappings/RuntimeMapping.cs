// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class RuntimeMapping
	{
		[TestMethod]
		public void ConstantTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("AId,BId,CId");
				writer.WriteLine("1,2,3");
				writer.Flush();
				stream.Position = 0;

				var map = new DefaultClassMap<A>();
				var type = typeof(A);
				var member = type.GetProperty("AId");
				map.Map(type, member).Constant(4);

				csv.Configuration.RegisterClassMap(map);
				var records = csv.GetRecords<A>().ToList();

				Assert.AreEqual(4, records[0].AId);
			}
		}

		[TestMethod]
		public void DefaultTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("AId,BId,CId");
				writer.WriteLine(",2,3");
				writer.Flush();
				stream.Position = 0;

				var map = new DefaultClassMap<A>();
				var type = typeof(A);
				var member = type.GetProperty("AId");
				map.Map(type, member).Default(4);

				csv.Configuration.RegisterClassMap(map);
				var records = csv.GetRecords<A>().ToList();

				Assert.AreEqual(4, records[0].AId);
			}
		}

		[TestMethod]
		public void ConstantValueTypeNullTest()
		{
			Assert.ThrowsException<ArgumentException>(() => new ConstantValueTypeNullMap());
		}

		[TestMethod]
		public void ConstantTypeMismatchTest()
		{
			Assert.ThrowsException<ArgumentException>(() => new ConstantTypeMismatchMap());
		}

		[TestMethod]
		public void DefaultValueTypeNullTest()
		{
			Assert.ThrowsException<ArgumentException>(() => new DefaultValueTypeNullMap());
		}

		[TestMethod]
		public void DefaultTypeMismatchTest()
		{
			Assert.ThrowsException<ArgumentException>(() => new DefaultTypeMismatchMap());
		}

		private class A
		{
			public int AId { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public int BId { get; set; }

			public C C { get; set; }
		}

		private class C
		{
			public int CId { get; set; }
		}

		private class ObjectProperty
		{
			public object O { get; set; }
		}

		private class ConstantValueTypeNullMap : ClassMap<A>
		{
			public ConstantValueTypeNullMap()
			{
				Map(m => m.AId).Constant(null);
			}
		}

		private class ConstantTypeMismatchMap : ClassMap<A>
		{
			public ConstantTypeMismatchMap()
			{
				Map(m => m.AId).Constant((uint)1);
			}
		}

		private class DefaultValueTypeNullMap : ClassMap<A>
		{
			public DefaultValueTypeNullMap()
			{
				Map(m => m.AId).Constant(null);
			}
		}

		private class DefaultTypeMismatchMap : ClassMap<A>
		{
			public DefaultTypeMismatchMap()
			{
				Map(m => m.AId).Constant((uint)1);
			}
		}
	}
}
