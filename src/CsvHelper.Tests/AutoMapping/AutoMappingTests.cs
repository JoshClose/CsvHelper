// Copyright 2009-2017 Josh Close and Contributors
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class AutoMappingTests
	{
		[TestInitialize]
		public void TestInitialize()
		{
			CultureInfo.CurrentCulture = new CultureInfo("en-US");
		}

		[TestMethod]
		public void ReaderSimpleTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<Simple>().ToList();

				Assert.IsNotNull(list);
				Assert.AreEqual(2, list.Count);
				var row = list[0];
				Assert.AreEqual(1, row.Id);
				Assert.AreEqual("one", row.Name);
				row = list[1];
				Assert.AreEqual(2, row.Id);
				Assert.AreEqual("two", row.Name);
			}
		}

		[TestMethod]
		public void ReaderReferenceTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("AId,BId");
				writer.WriteLine("1,2");
				writer.Flush();
				stream.Position = 0;

				var list = csv.GetRecords<A>().ToList();

				Assert.IsNotNull(list);
				Assert.AreEqual(1, list.Count);
				var row = list[0];
				Assert.AreEqual(1, row.AId);
				Assert.IsNotNull(row.B);
				Assert.AreEqual(2, row.B.BId);
			}
		}

		[TestMethod]
		public void ReaderReferenceHasNoDefaultConstructorTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			using (var csv = new CsvReader(new StringReader(s.ToString())))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.PrepareHeaderForMatch = header => header.ToLower();
				var records = csv.GetRecords<SimpleReferenceHasNoDefaultConstructor>().ToList();
				var row = records[0];
				Assert.AreEqual(1, row.Id);
				Assert.AreEqual("one", row.Ref.Name);
			}
		}

		[TestMethod]
		public void ReaderHasNoDefaultConstructorReferenceHasNoDefaultConstructorTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			using (var csv = new CsvReader(new StringReader(s.ToString())))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.PrepareHeaderForMatch = header => header.ToLower();
				var records = csv.GetRecords<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>().ToList();
				var row = records[0];
				Assert.AreEqual(1, row.Id);
				Assert.AreEqual("one", row.Ref.Name);
			}
		}

		[TestMethod]
		public void WriterSimpleTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" }
				};
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), data);
			}
		}

		[TestMethod]
		public void WriterReferenceTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Delimiter = ",";
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

				var expected = new StringBuilder();
				expected.AppendLine("AId,BId");
				expected.AppendLine("1,2");

				Assert.AreEqual(expected.ToString(), data);
			}
		}

		[TestMethod]
		public void WriterReferenceHasNoDefaultConstructorTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Delimiter = ",";
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

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		[TestMethod]
		public void WriterHasNoDefaultConstructorReferenceHasNoDefaultConstructorTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>
				{
					new SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor(1, new NoDefaultConstructor("one"))
				};
				csv.WriteRecords(list);
				writer.Flush();

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}

		[TestMethod]
		public void AutoMapEnumerableTest()
		{
			var config = new CsvHelper.Configuration.Configuration();
			try
			{
				config.AutoMap(typeof(List<string>));
				Assert.Fail();
			}
			catch (ConfigurationException) { }
		}

		[TestMethod]
		public void AutoMapWithExistingMapTest()
		{
			var config = new CsvHelper.Configuration.Configuration();
			var existingMap = new SimpleMap();
			config.Maps.Add(existingMap);
			var data = new
			{
				Simple = new Simple
				{
					Id = 1,
					Name = "one"
				}
			};
			var map = config.AutoMap(data.GetType());

			Assert.IsNotNull(map);
			Assert.AreEqual(0, map.MemberMaps.Count);
			Assert.AreEqual(1, map.ReferenceMaps.Count);

			// Since Simple is a reference on the anonymous object, the type won't
			// be re-used. Types which are created from automapping aren't added
			// to the list of registered maps either.
			Assert.IsNotInstanceOfType(map.ReferenceMaps[0].Data.Mapping, typeof(SimpleMap));
		}

		[TestMethod]
		public void AutoMapWithNestedHeaders()
		{
			var config = new CsvHelper.Configuration.Configuration
			{
				ReferenceHeaderPrefix = (type, name) => $"{name}."
			};
			var map = config.AutoMap<Nested>();
			Assert.AreEqual("Simple1.Id", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("Simple1.Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[1].Data.Names[0]);
			Assert.AreEqual("Simple2.Id", map.ReferenceMaps[1].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("Simple2.Name", map.ReferenceMaps[1].Data.Mapping.MemberMaps[1].Data.Names[0]);
		}

		[TestMethod]
		public void AutoMapWithDefaultConstructor()
		{
			var config = new CsvHelper.Configuration.Configuration();
			ClassMap map = config.AutoMap<SimpleReferenceHasNoDefaultConstructor>();

			Assert.AreEqual("Id", map.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("name", map.ReferenceMaps[0].Data.Mapping.ParameterMaps[0].Data.Name);
		}

		[TestMethod]
		public void AutoMapWithNoDefaultConstructor()
		{
			var config = new CsvHelper.Configuration.Configuration();
			var map = config.AutoMap<SimpleHasNoDefaultConstructorReferenceHasNoDefaultConstructor>();

			Assert.AreEqual("Id", map.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("id", map.ParameterMaps[0].Data.Name);
			Assert.AreEqual("name", map.ParameterMaps[1].ConstructorTypeMap.ParameterMaps[0].Data.Name);
			Assert.AreEqual("Name", map.ReferenceMaps[0].Data.Mapping.MemberMaps[0].Data.Names[0]);
			Assert.AreEqual("name", map.ReferenceMaps[0].Data.Mapping.ParameterMaps[0].Data.Name);
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