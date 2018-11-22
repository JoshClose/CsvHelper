using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			using (var csv = new CsvReader(reader))
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
			using (var csv = new CsvReader(reader))
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