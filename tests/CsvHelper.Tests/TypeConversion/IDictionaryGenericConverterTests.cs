// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System.Reflection;
using CsvHelper.Tests.Mocks;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class IDictionaryGenericConverterTests
	{
		[Fact]
		public void FullReadNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();

				try
				{
					var records = csv.GetRecords<Test>().ToList();
					throw new XUnitException();
				}
				catch (ReaderException)
				{
					// You can't read into a dictionary without a header.
					// You need to header value to use as the key.
				}
			}
		}

		[Fact]
		public void FullReadWithHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HeaderValidated = null,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Before,Dictionary1,Dictionary2,Dictionary3,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].Dictionary;

				Assert.Equal(3, list.Count);
				Assert.Equal(2, list["Dictionary1"]);
				Assert.Equal(3, list["Dictionary2"]);
				Assert.Equal(4, list["Dictionary3"]);
			}
		}

		[Fact]
		public void FullReadWithDefaultHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,Dictionary,Dictionary,Dictionary,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestDefaultMap>();

				try
				{
					var records = csv.GetRecords<Test>().ToList();
					throw new XUnitException();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		[Fact]
		public void FullReadWithNamedHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,Dictionary,Dictionary,Dictionary,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					throw new XUnitException();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		[Fact]
		public void FullReadWithHeaderListItemsScattered()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Before,Dictionary,A,Dictionary,B,Dictionary,After");
				writer.WriteLine("1,2,3,4,5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					throw new XUnitException();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		private class Test
		{
			public string? Before { get; set; }
			public Dictionary<string, int?> Dictionary { get; set; } = new Dictionary<string, int?>();
			public string? After { get; set; }
		}

		private sealed class TestIndexMap : ClassMap<Test>
		{
			public TestIndexMap()
			{
				Map(m => m.Before).Index(0);
				Map(m => m.Dictionary).Index(1, 3);
				Map(m => m.After).Index(4);
			}
		}

		private sealed class TestNamedMap : ClassMap<Test>
		{
			public TestNamedMap()
			{
				Map(m => m.Before).Name("Before");
				Map(m => m.Dictionary).Name("Dictionary");
				Map(m => m.After).Name("After");
			}
		}

		private sealed class TestDefaultMap : ClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map(m => m.Before);
				Map(m => m.Dictionary);
				Map(m => m.After);
			}
		}
	}
}
