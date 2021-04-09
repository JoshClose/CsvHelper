// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Writing
{
	
	public class NoPropertyMappingTests
	{
		[Fact]
		public void NoPropertyWithHeaderAndNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Context.RegisterClassMap<TestWithNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,Constant,Name");
				expected.AppendLine("1,const,");
				expected.AppendLine("2,const,");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void NoPropertyWithHeaderAndNoNameTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Context.RegisterClassMap<TestWithNoNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,");
				expected.AppendLine("1,const");
				expected.AppendLine("2,const");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void NoPropertyWithNoHeaderAndNameTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Context.RegisterClassMap<TestWithNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("1,const,");
				expected.AppendLine("2,const,");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void NoPropertyWithNoHeaderAndNoNameTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { Id = 1 },
					new Test { Id = 2 }
				};

				csv.Context.RegisterClassMap<TestWithNoNameMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("1,const");
				expected.AppendLine("2,const");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void OutOfOrderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
					new Test { Id = 2, Name = "two" }
				};

				csv.Context.RegisterClassMap<TestMapOutOfOrderWithEmptyFieldsMap>();
				csv.WriteRecords(list);

				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("one,,,1");
				expected.AppendLine("two,,,2");

				Assert.Equal(expected.ToString(), result);
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestWithNameMap : ClassMap<Test>
		{
			public TestWithNameMap()
			{
				Map(m => m.Id);
				Map().Name("Constant").Constant("const");
				Map(m => m.Name);
			}
		}

		private sealed class TestWithNoNameMap : ClassMap<Test>
		{
			public TestWithNoNameMap()
			{
				Map(m => m.Id);
				Map().Constant("const");
			}
		}

		private sealed class TestMapOutOfOrderWithEmptyFieldsMap : ClassMap<Test>
		{
			public TestMapOutOfOrderWithEmptyFieldsMap()
			{
				Map(m => m.Name).Index(0);
				Map().Index(1).Constant(null);
				Map().Index(2).Constant(string.Empty);
				Map(m => m.Id).Index(3);
			}
		}
	}
}
