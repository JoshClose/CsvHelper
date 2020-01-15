// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class PrivateSettersInParentTests
	{
		[TestMethod]
		public void AutoMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.IncludePrivateMembers = true;

				var records = csv.GetRecords<Child>().ToList();
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void ClassMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.IncludePrivateMembers = true;
				csv.Configuration.RegisterClassMap<ChildMap>();

				var records = csv.GetRecords<Child>().ToList();
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		private class Parent
		{
			public int Id { get; private set; }

			public string Name { get; set; }

			public Parent() { }

			public Parent(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class Child : Parent { }

		private sealed class ChildMap : ClassMap<Child>
		{
			public ChildMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
			}
		}
	}
}
