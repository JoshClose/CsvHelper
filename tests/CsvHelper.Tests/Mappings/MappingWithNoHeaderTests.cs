// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Mappings
{
	public class MappingWithNoHeaderTests
	{
		[Fact]
		public void Read_NoHeader_HasNameAttribute_Reads()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" }
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<FooAttribute>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void Read_NoHeader_HasNameMap_Reads()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" }
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void Read_NoHeader_HasParameterAttribute_Reads()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" }
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<FooParameterAttribute>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void Read_NoHeader_HasParameterMap_Reads()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" }
			};

			using (var csv = new CsvReader(parser))
			{
				csv.Context.RegisterClassMap<FooParameterMap>();
				var records = csv.GetRecords<FooParameter>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].Name);
			}
		}

		[Fact]
		public void Write_HasHeader_HasNameAttribute_Writes()
		{
			var records = new List<FooAttribute>
			{
				new FooAttribute { Id = 1, Name = "one" },
			};

			var writer = new StringWriter();
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = "New Id,New Name\r\n1,one\r\n";
				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void Write_HasHeader_HasNameMap_Writes()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};

			var writer = new StringWriter();
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();
				csv.WriteRecords(records);

				var expected = "New Id,New Name\r\n1,one\r\n";
				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void Write_HasHeader_HasParameterAttribute_Writes()
		{
			var records = new List<FooParameterAttribute>
			{
				new FooParameterAttribute(1, "one"),
			};

			var writer = new StringWriter();
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = "Id,Name\r\n1,one\r\n";
				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void Write_HasHeader_HasParameterMap_Writes()
		{
			var records = new List<FooParameterAttribute>
			{
				new FooParameterAttribute(1, "one"),
			};

			var writer = new StringWriter();
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooParameterMap>();
				csv.WriteRecords(records);

				var expected = "Id,Name\r\n1,one\r\n";
				Assert.Equal(expected, writer.ToString());
			}
		}

		private class FooAttribute
		{
			[Index(0)]
			[Name("New Id")]
			public int Id { get; set; }

			[Index(1)]
			[Name("New Name")]
			public string? Name { get; set; }
		}

		private class Foo
		{
			public int Id { get; set; }

			public string? Name { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(x => x.Id).Name("New Id").Index(0);
				Map(x => x.Name).Name("New Name").Index(1);
			}
		}

		private class FooParameterAttribute
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public FooParameterAttribute([Name("New Id")]int id, [Name("New Name")]string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class FooParameter
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public FooParameter(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class FooParameterMap : ClassMap<FooParameter>
		{
			public FooParameterMap()
			{
				Parameter("id").Name("New Id").Index(0);
				Parameter("name").Name("New Name").Index(1);
			}
		}
	}
}
