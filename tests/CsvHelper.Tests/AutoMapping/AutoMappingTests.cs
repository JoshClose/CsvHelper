// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.AutoMapping
{	
	public class AutoMappingTests
	{
		public AutoMappingTests()
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
		}

		[Fact]
		public void ReaderSimpleTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<Simple>().ToList();

				Assert.NotNull(list);
				Assert.Equal(2, list.Count);
				var row = list[0];
				Assert.Equal(1, row.Id);
				Assert.Equal("one", row.Name);
				row = list[1];
				Assert.Equal(2, row.Id);
				Assert.Equal("two", row.Name);
			}
		}

		[Fact]
		public void ReaderFloatingCultureDeTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.GetCultureInfo("de-DE")))
			{
				writer.WriteLine("Single;Double;Decimal");
				writer.WriteLine("1,1;+0000002,20;3,30000");
				writer.WriteLine("-0,1;-0,2;-,3");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<Numbers>().ToList();

				Assert.NotNull(list);
				Assert.Equal(2, list.Count);
				var row = list[0];
				Assert.Equal(1.1f, row.Single, 4);
				Assert.Equal(2.2d, row.Double, 4);
				Assert.Equal(3.3m, row.Decimal);

				row = list[1];
				Assert.Equal(-0.1f, row.Single, 4);
				Assert.Equal(-0.2d, row.Double, 4);
				Assert.Equal(-0.3m, row.Decimal);
			}
		}

		[Fact]
		public void ReaderReferenceTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("AId,BId");
				writer.WriteLine("1,2");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.NotNull(list);
				Assert.Single(list);
				var row = list[0];
				Assert.Equal(1, row.AId);
				Assert.NotNull(row.B);
				Assert.Equal(2, row.B.BId);
			}
		}

		[Fact]
		public void ReaderReferenceHasNoDefaultConstructorTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => args.Header.ToLower(),
			};
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			using (var csv = new CsvReader(new StringReader(s.ToString()), config))
			{
				var records = csv.GetRecords<SimpleReferenceHasNoDefaultConstructor>().ToList();
				var row = records[0];
				Assert.Equal(1, row.Id);
				Assert.Equal("one", row.Ref.Name);
			}
		}

		[Fact]
		public void ReaderHasNoDefaultConstructorReferenceHasNoDefaultConstructorTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => args.Header.ToLower(),
			};
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			using (var csv = new CsvReader(new StringReader(s.ToString()), config))
			{
				var records = csv.GetRecords<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>().ToList();
				var row = records[0];
				Assert.Equal(1, row.Id);
				Assert.Equal("one", row.Ref.Name);
			}
		}

		[Fact]
		public void WriterSimpleTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" }
				};
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), data);
			}
		}

		[Fact]
		public void WriterReferenceTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<A>
				{
					new A
					{
						AId = 1,
						B = new B
						{
							BId = 2
						}
					}
				};
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("AId,BId");
				expected.AppendLine("1,2");

				Assert.Equal(expected.ToString(), data);
			}
		}

		[Fact]
		public void WriterReferenceHasNoDefaultConstructorTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<SimpleReferenceHasNoDefaultConstructor>
				{
					new SimpleReferenceHasNoDefaultConstructor
					{
						Id = 1,
						Ref = new NoDefaultConstructor( "one" )
					}
				};
				csv.WriteRecords(list);
				writer.Flush();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		[Fact]
		public void WriterHasNoDefaultConstructorReferenceHasNoDefaultConstructorTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>
				{
					new SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor(1, new NoDefaultConstructor("one"))
				};
				csv.WriteRecords(list);
				writer.Flush();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		[Fact]
		public void AutoMapEnumerableTest()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));

			Assert.Throws<ConfigurationException>(() => context.AutoMap(typeof(List<string>)));
		}

		[Fact]
		public void AutoMapWithExistingMapTest()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
			var existingMap = new SimpleMap();
			context.Maps.Add(existingMap);
			var data = new
			{
				Simple = new Simple
				{
					Id = 1,
					Name = "one"
				}
			};
			var map = context.AutoMap(data.GetType());

			Assert.NotNull(map);
			Assert.Empty(map.MemberMaps);
			Assert.Single(map.ReferenceMaps);

			// Since Simple is a reference on the anonymous object, the type won't
			// be re-used. Types which are created from automapping aren't added
			// to the list of registered maps either.
			Assert.IsNotType<SimpleMap>(map.ReferenceMaps[0].Data.Mapping);
		}

		[Fact]
		public void AutoMapWithNestedHeaders()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReferenceHeaderPrefix = args => $"{args.MemberName}."
			};
			var context = new CsvContext(config);
			var map = context.AutoMap<Nested>();
			Assert.Equal("Simple1.Id", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.Equal("Simple1.Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[1].Data.Names[0]);
			Assert.Equal("Simple2.Id", map.ReferenceMaps[1].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.Equal("Simple2.Name", map.ReferenceMaps[1].Data.Mapping.MemberMaps[1].Data.Names[0]);
		}

		[Fact]
		public void AutoMapWithDefaultConstructor()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			ClassMap map = context.AutoMap<SimpleReferenceHasNoDefaultConstructor>();

			Assert.Equal("Id", map.MemberMaps[0].Data.Names[0]);
			Assert.Equal("Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.Equal("name", map.ReferenceMaps[0].Data.Mapping.ParameterMaps[0].Data.Names[0]);
		}

		[Fact]
		public void AutoMapWithNoDefaultConstructor()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			var map = context.AutoMap<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>();

			Assert.Equal("Id", map.MemberMaps[0].Data.Names[0]);
			Assert.Equal("id", map.ParameterMaps[0].Data.Names[0]);
			Assert.Equal("name", map.ParameterMaps[1].ConstructorTypeMap.ParameterMaps[0].Data.Names[0]);
			Assert.Equal("Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.Equal("name", map.ReferenceMaps[0].Data.Mapping.ParameterMaps[0].Data.Names[0]);
		}

		private class Nested
		{
			public Simple Simple1 { get; set; }

			public Simple Simple2 { get; set; }
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class SimpleMap : ClassMap<Simple>
		{
			public SimpleMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
			}
		}

		private class Numbers
		{
			public float Single { get; set; }
			public double Double { get; set; }
			public decimal Decimal { get; set; }
		}

		private class A
		{
			public int AId { get; set; }

			public B B { get; set; }
		}

		private class B
		{
			public int BId { get; set; }
		}

		private class SimpleReferenceHasNoDefaultConstructor
		{
			public int Id { get; set; }

			public NoDefaultConstructor Ref { get; set; }
		}

		private class SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor
		{
			public int Id { get; set; }

			public NoDefaultConstructor Ref { get; set; }

			public SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor(int id, NoDefaultConstructor ref_)
			{
				Id = id;
				Ref = ref_;
			}
		}

		private class NoDefaultConstructor
		{
			public string Name { get; set; }

			public NoDefaultConstructor(string name)
			{
				Name = name;
			}
		}
	}
}
