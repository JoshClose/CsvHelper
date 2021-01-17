// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using CsvHelper.Tests.Mocks;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class ArrayConverterTests
	{
		[TestMethod]
		public void FullReadNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
			}
		}

		[TestMethod]
		public void FullReadWithHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
			}
		}

		[TestMethod]
		public void FullReadWithDefaultHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestDefaultMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
			}
		}

		[TestMethod]
		public void FullReadWithNamedHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
			}
		}

		[TestMethod]
		public void FullReadWithHeaderListItemsScattered()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,List,A,List,B,List,After");
				writer.WriteLine("1,2,3,4,5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(4, list[1]);
				Assert.AreEqual(6, list[2]);
			}
		}

		private class Test
		{
			public string Before { get; set; }
			public int?[] List { get; set; }
			public string After { get; set; }
		}

		private sealed class TestIndexMap : ClassMap<Test>
		{
			public TestIndexMap()
			{
				Map(m => m.Before).Index(0);
				Map(m => m.List).Index(1, 3);
				Map(m => m.After).Index(4);
			}
		}

		private sealed class TestNamedMap : ClassMap<Test>
		{
			public TestNamedMap()
			{
				Map(m => m.Before).Name("Before");
				Map(m => m.List).Name("List");
				Map(m => m.After).Name("After");
			}
		}

		private sealed class TestDefaultMap : ClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map(m => m.Before);
				Map(m => m.List);
				Map(m => m.After);
			}
		}
	}
}
