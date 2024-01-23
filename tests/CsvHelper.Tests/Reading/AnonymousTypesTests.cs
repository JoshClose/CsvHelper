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
	
	public class AnonymousTypesTests
	{
		[Fact]
		public void ValueTypeSingleRecordTest()
		{
			var definition = new
			{
				Id = 0,
				Name = string.Empty
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		[Fact]
		public void ValueTypeAllRecordsTest()
		{
			var definition = new
			{
				Id = 0,
				Name = string.Empty
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		[Fact]
		public void ValueTypeAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				Id = 0,
				Name = string.Empty
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}

		}

		[Fact]
		public void ReferenceTypeSingleRecordTest()
		{
			var definition = new
			{
				Reference = new Test()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void ReferenceTypeAllRecordsTest()
		{
			var definition = new
			{
				Reference = new Test()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void ReferenceTypeAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				Reference = new Test()
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void ValueAndReferenceTypeSingleRecordTest()
		{
			var definition = new
			{
				A = 0,
				B = string.Empty,
				Reference = new Test()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("A,Id,Name,B");
				writer.WriteLine("-1,1,one,b");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(-1, record.A);
				Assert.Equal("b", record.B);
				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void ValueAndReferenceTypeAllRecordsTest()
		{
			var definition = new
			{
				A = 0,
				B = string.Empty,
				Reference = new Test()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("A,Id,Name,B");
				writer.WriteLine("-1,1,one,b");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(-1, record.A);
				Assert.Equal("b", record.B);
				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void ValueAndReferenceTypeAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				A = 0,
				B = string.Empty,
				Reference = new Test()
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("-1,b,1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(-1, record.A);
				Assert.Equal("b", record.B);
				Assert.Equal(1, record.Reference.Id);
				Assert.Equal("one", record.Reference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceSingleRecordTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					Name = string.Empty
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceAllRecordsTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					Name = string.Empty
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					Name = string.Empty
				}
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasAnonymousReferenceSingleRecordTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					AnonymousReference2 = new
					{
						Name = string.Empty
					}
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasAnonymousReferenceAllRecordsTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					AnonymousReference2 = new
					{
						Name = string.Empty
					}
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasAnonymousReferenceAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				Id = 0,
				AnonymousReference = new
				{
					AnonymousReference2 = new
					{
						Name = string.Empty
					}
				}
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasReferenceSingleRecordTest()
		{
			var definition = new
			{
				A = 0,
				AnonymousReference = new
				{
					Reference = new Test()
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name,A");
				writer.WriteLine("1,one,2");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(2, record.A);
				Assert.Equal(1, record.AnonymousReference.Reference.Id);
				Assert.Equal("one", record.AnonymousReference.Reference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasReferenceAllRecordsTest()
		{
			var definition = new
			{
				A = 0,
				AnonymousReference = new
				{
					Reference = new Test()
				}
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name,A");
				writer.WriteLine("1,one,2");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(2, record.A);
				Assert.Equal(1, record.AnonymousReference.Reference.Id);
				Assert.Equal("one", record.AnonymousReference.Reference.Name);
			}
		}

		[Fact]
		public void AnonymousReferenceHasReferenceAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				A = 0,
				AnonymousReference = new
				{
					Reference = new Test()
				}
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("2,1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(2, record.A);
				Assert.Equal(1, record.AnonymousReference.Reference.Id);
				Assert.Equal("one", record.AnonymousReference.Reference.Name);
			}
		}

		[Fact]
		public void ParentChildReferenceSingleRecordTest()
		{
			var definition = new
			{
				Reference = new Child()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("ParentId,ChildId,ParentName,ChildName");
				writer.WriteLine("1,2,one,two");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.Equal(1, record.Reference.ParentId);
				Assert.Equal("one", record.Reference.ParentName);
				Assert.Equal(2, record.Reference.ChildId);
				Assert.Equal("two", record.Reference.ChildName);
			}
		}

		[Fact]
		public void ParentChildReferenceAllRecordsTest()
		{
			var definition = new
			{
				Reference = new Child()
			};

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("ParentId,ChildId,ParentName,ChildName");
				writer.WriteLine("1,2,one,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Reference.ParentId);
				Assert.Equal("one", record.Reference.ParentName);
				Assert.Equal(2, record.Reference.ChildId);
				Assert.Equal("two", record.Reference.ChildName);
			}
		}

		[Fact]
		public void ParentChildReferenceAllRecordsNoHeaderTest()
		{
			var definition = new
			{
				Reference = new Child()
			};

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Reference.ChildId);
				Assert.Equal("one", record.Reference.ChildName);
				Assert.Equal(2, record.Reference.ParentId);
				Assert.Equal("two", record.Reference.ParentName);
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private class ContainsReference
		{
			public Test Test { get; set; }
		}

		private class Parent
		{
			public int ParentId { get; set; }

			public string ParentName { get; set; }
		}

		private class Child : Parent
		{
			public int ChildId { get; set; }

			public string ChildName { get; set; }
		}
	}
}
