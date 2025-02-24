// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Reading
{
	
	public class PrivateSettersInParentTests
	{
		[Fact]
		public void AutoMappingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IncludePrivateMembers = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Child>().ToList();
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void ClassMappingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IncludePrivateMembers = true,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<ChildMap>();

				var records = csv.GetRecords<Child>().ToList();
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		private class Parent
		{
			public int Id { get; private set; }

			public string? Name { get; set; }

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
