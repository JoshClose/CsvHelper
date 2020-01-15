// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#pragma warning disable 649

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class FieldMappingTests
	{
		[TestMethod]
		public void ReadPublicFieldsWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("IdField,NameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MemberTypes = MemberTypes.Fields;
				var records = csv.GetRecords<APublic>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].IdField);
				Assert.AreEqual("one", records[0].BField.NameField);
			}
		}

		[TestMethod]
		public void WritePublicFieldsWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<APublic>
				{
					new APublic
					{
						IdField = 1,
						BField = new BPublic
						{
							NameField = "one"
						}
					}
				};
				csv.Configuration.MemberTypes = MemberTypes.Fields;
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("IdField,NameField");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void ReadPublicFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("IdField,NameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<APublicMap>();
				var records = csv.GetRecords<APublic>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].IdField);
				Assert.AreEqual("one", records[0].BField.NameField);
			}
		}

		[TestMethod]
		public void WritePublicFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<APublic>
				{
					new APublic
					{
						IdField = 1,
						BField = new BPublic
						{
							NameField = "one"
						}
					}
				};
				csv.Configuration.RegisterClassMap<APublicMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("IdField,NameField");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void ReadPrivateFieldsWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("idField,nameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.IncludePrivateMembers = true;
				csv.Configuration.MemberTypes = MemberTypes.Fields;
				var records = csv.GetRecords<APrivate>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].GetId());
				Assert.AreEqual("one", records[0].GetB().GetName());
			}
		}

		[TestMethod]
		public void WritePrivateFieldsWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<APrivate>
				{
					new APrivate( 1, "one" )
				};

				csv.Configuration.IncludePrivateMembers = true;
				csv.Configuration.MemberTypes = MemberTypes.Fields;
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("idField,nameField");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void ReadPrivateFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("idField,nameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<APrivateMap>();
				var records = csv.GetRecords<APrivate>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].GetId());
				Assert.AreEqual("one", records[0].GetB().GetName());
			}
		}

		[TestMethod]
		public void WritePrivateFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<APrivate>
				{
					new APrivate( 1, "one" )
				};
				csv.Configuration.RegisterClassMap<APrivateMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("idField,nameField");
				expected.AppendLine("1,one");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		[TestMethod]
		public void ReadPublicFieldsAndPropertiesWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("IdField,NameField,IdProp,NameProp");
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MemberTypes = MemberTypes.Properties | MemberTypes.Fields;
				var records = csv.GetRecords<APublic>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].IdField);
				Assert.AreEqual("one", records[0].BField.NameField);
			}
		}

		[TestMethod]
		public void WritePublicFieldsAndPropertiesWithAutoMapTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<APublic>
				{
					new APublic
					{
						IdField = 1,
						BField = new BPublic
						{
							NameField = "one",
							NameProp = "two"
						},
						IdProp = 2
					}
				};
				csv.Configuration.MemberTypes = MemberTypes.Properties | MemberTypes.Fields;
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine("IdProp,IdField,NameProp,NameField");
				expected.AppendLine("2,1,two,one");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		private class APublic
		{
			public int IdField;
			public BPublic BField;

			public int IdProp { get; set; }
		}

		private class BPublic
		{
			public string NameField;

			public string NameProp { get; set; }
		}

		private sealed class APublicMap : ClassMap<APublic>
		{
			public APublicMap()
			{
				Map(m => m.IdField);
				References<BPublicMap>(m => m.BField);
			}
		}

		private sealed class BPublicMap : ClassMap<BPublic>
		{
			public BPublicMap()
			{
				Map(m => m.NameField);
			}
		}

		private class APrivate
		{
			private int idField;
			private BPrivate bField;

			private int IdProp { get; set; }
			private BPrivate BProp { get; set; }

			public int GetId()
			{
				return idField;
			}

			public BPrivate GetB()
			{
				return bField;
			}

			public APrivate() { }

			public APrivate(int id, string name)
			{
				this.idField = id;
				bField = new BPrivate(name);
			}
		}

		private class BPrivate
		{
			private string nameField;

			public string GetName()
			{
				return nameField;
			}

			public BPrivate() { }

			public BPrivate(string name)
			{
				this.nameField = name;
			}
		}

		private sealed class APrivateMap : ClassMap<APrivate>
		{
			public APrivateMap()
			{
				var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
				{
					IncludePrivateMembers = true,
					MemberTypes = MemberTypes.Fields
				};
				AutoMap(config);
			}
		}
	}
}
