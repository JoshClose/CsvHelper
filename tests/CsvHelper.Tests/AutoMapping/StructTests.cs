// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.AutoMapping
{
	
	public class StructTests
	{
		[Fact]
		public void StructTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<B>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void StructPropertyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReferenceHeaderPrefix = args => $"{args.MemberName}.",
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Simple1.Id,Simple1.Name,Simple2.Id,Simple2.Name,Title");
				writer.WriteLine("1,one,2,two,Works!");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<A>().ToList();

				Assert.Equal(1, records[0].Simple1.Id);
				Assert.Equal("one", records[0].Simple1.Name);
				Assert.Equal(2, records[0].Simple2.Id);
				Assert.Equal("two", records[0].Simple2.Name);
				Assert.Equal("Works!", records[0].Title);
			}
		}

		public class A
		{
			public B Simple1 { get; set; } = new B();
			public string Title { get; set; } = string.Empty;
			public B Simple2 { get; set; } = new B();
		}

		public struct B
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
