// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Xunit;
#pragma warning disable 649

namespace CsvHelper.Tests.Mappings
{
	
	public class FieldMappingTests
	{
		[Fact]
		public void ReadPublicFieldsWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Fields,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("IdField,NameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<APublic>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].IdField);
				Assert.Equal("one", records[0].BField.NameField);
			}
		}

		[Fact]
		public void WritePublicFieldsWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Fields,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, config))
			{
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
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("IdField,NameField");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void ReadPublicFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("IdField,NameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<APublicMap>();
				var records = csv.GetRecords<APublic>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].IdField);
				Assert.Equal("one", records[0].BField.NameField);
			}
		}

		[Fact]
		public void WritePublicFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
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
				csv.Context.RegisterClassMap<APublicMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("IdField,NameField");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void ReadPrivateFieldsWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Fields,
				IncludePrivateMembers = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("idField,nameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<APrivate>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].GetId());
				Assert.Equal("one", records[0].GetB().GetName());
			}
		}

		[Fact]
		public void WritePrivateFieldsWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Fields,
				IncludePrivateMembers = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var list = new List<APrivate>
				{
					new APrivate( 1, "one" )
				};

				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("idField,nameField");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void ReadPrivateFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("idField,nameField");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<APrivateMap>();
				var records = csv.GetRecords<APrivate>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].GetId());
				Assert.Equal("one", records[0].GetB().GetName());
			}
		}

		[Fact]
		public void WritePrivateFieldsWithMappingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<APrivate>
				{
					new APrivate( 1, "one" )
				};
				csv.Context.RegisterClassMap<APrivateMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("idField,nameField");
				expected.AppendLine("1,one");

				Assert.Equal(expected.ToString(), result);
			}
		}

		[Fact]
		public void ReadPublicFieldsAndPropertiesWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Properties | MemberTypes.Fields,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("IdField,NameField,IdProp,NameProp");
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<APublic>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].IdField);
				Assert.Equal("one", records[0].BField.NameField);
			}
		}

		[Fact]
		public void WritePublicFieldsAndPropertiesWithAutoMapTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Properties | MemberTypes.Fields,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvWriter(writer, config))
			{
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
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("IdProp,IdField,NameProp,NameField");
				expected.AppendLine("2,1,two,one");

				Assert.Equal(expected.ToString(), result);
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
