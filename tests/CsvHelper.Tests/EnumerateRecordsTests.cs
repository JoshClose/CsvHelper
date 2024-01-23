// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests
{
	
	public class EnumerateRecordsTests
	{
		[Fact]
		public void BasicTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var record = new Basic
				{
					Id = -1,
					Name = "-one"
				};

				var count = 1;
				foreach (var r in csv.EnumerateRecords(record))
				{
					if (count == 1)
					{
						Assert.Equal(1, r.Id);
						Assert.Equal("one", r.Name);
					}
					else if (count == 2)
					{
						Assert.Equal(2, r.Id);
						Assert.Equal("two", r.Name);
					}

					count++;
				}
			}
		}

		[Fact]
		public void UnUsedPropertyTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var record = new UnUsedProperty
				{
					Id = -1,
					Name = "-one",
					UnUsed = "nothing",
				};

				var count = 1;
				foreach (var r in csv.EnumerateRecords(record))
				{
					if (count == 1)
					{
						Assert.Equal(1, r.Id);
						Assert.Equal("one", r.Name);
						Assert.Equal("nothing", r.UnUsed);
					}
					else if (count == 2)
					{
						Assert.Equal(2, r.Id);
						Assert.Equal("two", r.Name);
						Assert.Equal("nothing", r.UnUsed);
					}

					count++;
				}
			}
		}

		[Fact]
		public void ReferenceTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var record = new HasReferences
				{
					Id = -1,
					Reference = new Reference
					{
						Name = "one"
					}
				};

				var count = 1;
				foreach (var r in csv.EnumerateRecords(record))
				{
					if (count == 1)
					{
						Assert.Equal(1, r.Id);
						Assert.Equal("one", r.Reference.Name);
					}
					else if (count == 2)
					{
						Assert.Equal(2, r.Id);
						Assert.Equal("two", r.Reference.Name);
					}

					count++;
				}
			}
		}

		private class Basic
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private class UnUsedProperty
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public string UnUsed { get; set; }
		}

		public class HasReferences
		{
			public int Id { get; set; }

			public Reference Reference { get; set; }
		}

		public class Reference
		{
			public string Name { get; set; }
		}
	}
}
