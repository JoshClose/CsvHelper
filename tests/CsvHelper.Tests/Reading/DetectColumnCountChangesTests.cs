// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	
	public class DetectColumnCountChangesTests
	{
		[Fact]
		public void ConsistentColumnsWithDetectColumnChangesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectColumnCountChanges = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Column 1,Column 2");
				writer.WriteLine("1,2");
				writer.Flush();
				stream.Position = 0;

				while (!csv.Read())
				{
				}
			}
		}

		[Fact]
		public void InconsistentColumnsMultipleRowsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectColumnCountChanges = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Column 1,Column 2");
				writer.WriteLine("1,2"); // Valid
				writer.WriteLine("1,2,3"); // Error - too many fields
				writer.WriteLine("1,2"); // Valid
				writer.WriteLine("1"); // Error - not enough fields
				writer.WriteLine("1,2,3,4"); // Error - too many fields
				writer.WriteLine("1,2"); // Valid
				writer.WriteLine("1,2"); // Valid
				writer.Flush();
				stream.Position = 0;

				var failCount = 0;

				while (true)
				{
					try
					{
						if (!csv.Read())
						{
							break;
						}
					}
					catch (BadDataException)
					{
						failCount++;
					}
				}

				// Expect only 3 errors
				Assert.Equal<int>(3, failCount);
			}
		}

		[Fact]
		public void InconsistentColumnsSmallerTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectColumnCountChanges = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,2,3,4");
				writer.WriteLine("5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Read();

				try
				{
					csv.Read();
					throw new XunitException();
				}
				catch (BadDataException)
				{
				}
			}
		}

		[Fact]
		public void InconsistentColumnsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectColumnCountChanges = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Column 1,Column 2");
				writer.WriteLine("1,2,3");
				writer.Flush();
				stream.Position = 0;

				csv.Read();

				try
				{
					csv.Read();
					throw new XunitException();
				}
				catch (BadDataException)
				{
				}
			}
		}

		[Fact]
		public void WillThrowOnMissingFieldStillWorksTest()
		{
			var missingFieldExceptionCount = 0;
			var columnCountChangeExceptionCount = 0;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectColumnCountChanges = true,
				HeaderValidated = null,
				ReadingExceptionOccurred = (args) =>
				{
					if (args.Exception is MissingFieldException)
					{
						missingFieldExceptionCount++;
					}
					else if (args.Exception is BadDataException)
					{
						columnCountChangeExceptionCount++;
					}

					return false;
				},
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,2,3");
				writer.WriteLine("4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();
				Assert.Equal(1, missingFieldExceptionCount);
				Assert.Equal(1, columnCountChangeExceptionCount);
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
			}
		}
	}
}
