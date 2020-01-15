// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class AnonymousTypesTests
	{
		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.Name);
			}
		}

		[TestMethod]
		public void ValueTypeAllRecordsNoHeaderTest()
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.Name);
			}

		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
		public void ReferenceTypeAllRecordsNoHeaderTest()
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,Id,Name,B");
				writer.WriteLine("-1,1,one,b");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(-1, record.A);
				Assert.AreEqual("b", record.B);
				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,Id,Name,B");
				writer.WriteLine("-1,1,one,b");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(-1, record.A);
				Assert.AreEqual("b", record.B);
				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
		public void ValueAndReferenceTypeAllRecordsNoHeaderTest()
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("-1,b,1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(-1, record.A);
				Assert.AreEqual("b", record.B);
				Assert.AreEqual(1, record.Reference.Id);
				Assert.AreEqual("one", record.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.Name);
			}
		}

		[TestMethod]
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

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[TestMethod]
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

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.AnonymousReference.AnonymousReference2.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name,A");
				writer.WriteLine("1,one,2");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(2, record.A);
				Assert.AreEqual(1, record.AnonymousReference.Reference.Id);
				Assert.AreEqual("one", record.AnonymousReference.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name,A");
				writer.WriteLine("1,one,2");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(2, record.A);
				Assert.AreEqual(1, record.AnonymousReference.Reference.Id);
				Assert.AreEqual("one", record.AnonymousReference.Reference.Name);
			}
		}

		[TestMethod]
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

			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("2,1,one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(2, record.A);
				Assert.AreEqual(1, record.AnonymousReference.Reference.Id);
				Assert.AreEqual("one", record.AnonymousReference.Reference.Name);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("ParentId,ChildId,ParentName,ChildName");
				writer.WriteLine("1,2,one,two");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();
				var record = csv.GetRecord(definition);

				Assert.AreEqual(1, record.Reference.ParentId);
				Assert.AreEqual("one", record.Reference.ParentName);
				Assert.AreEqual(2, record.Reference.ChildId);
				Assert.AreEqual("two", record.Reference.ChildName);
			}
		}

		[TestMethod]
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("ParentId,ChildId,ParentName,ChildName");
				writer.WriteLine("1,2,one,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Reference.ParentId);
				Assert.AreEqual("one", record.Reference.ParentName);
				Assert.AreEqual(2, record.Reference.ChildId);
				Assert.AreEqual("two", record.Reference.ChildName);
			}
		}

		[TestMethod]
		public void ParentChildReferenceAllRecordsNoHeaderTest()
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
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,one,2,two");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords(definition).ToList();
				Assert.AreEqual(1, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Reference.ChildId);
				Assert.AreEqual("one", record.Reference.ChildName);
				Assert.AreEqual(2, record.Reference.ParentId);
				Assert.AreEqual("two", record.Reference.ParentName);
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
