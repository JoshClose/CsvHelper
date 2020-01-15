// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class StructTests
	{
		[TestMethod]
		public void StructTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<B>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void StructPropertyTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Simple1.Id,Simple1.Name,Simple2.Id,Simple2.Name,Title");
				writer.WriteLine("1,one,2,two,Works!");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.ReferenceHeaderPrefix = (type, name) => $"{name}.";
				var records = csv.GetRecords<A>().ToList();

				Assert.AreEqual(1, records[0].Simple1.Id);
				Assert.AreEqual("one", records[0].Simple1.Name);
				Assert.AreEqual(2, records[0].Simple2.Id);
				Assert.AreEqual("two", records[0].Simple2.Name);
				Assert.AreEqual("Works!", records[0].Title);
			}
		}

		public class A
		{
			public B Simple1 { get; set; }
			public string Title { get; set; }
			public B Simple2 { get; set; }
		}

		public struct B
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
