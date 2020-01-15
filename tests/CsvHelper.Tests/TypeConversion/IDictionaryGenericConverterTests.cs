// Copyright 2009-2020 Josh Close and Contributors
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Reflection;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class IDictionaryGenericConverterTests
	{
		[TestMethod]
		public void ConvertNoIndexEndTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
			var rowMock = new Mock<IReader>();
			var headers = new[] { "Id", "Name", "Prop1", "Prop2", "Prop3" };
			var currentRecord = new[] { "1", "One", "1", "2", "3" };
			var context = new ReadingContext(new StringReader(string.Empty), config, false)
			{
				HeaderRecord = headers,
				Record = currentRecord
			};
			rowMock.Setup(m => m.Configuration).Returns(config);
			rowMock.Setup(m => m.Context).Returns(context);
			rowMock.Setup(m => m.GetField(It.IsAny<Type>(), It.IsAny<int>())).Returns<Type, int>((type, index) => Convert.ToInt32(currentRecord[index]));
			var data = new MemberMapData(typeof(Test).GetTypeInfo().GetProperty("Dictionary"))
			{
				Index = 2
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IDictionaryGenericConverter();
			var dictionary = (IDictionary<string, int?>)converter.ConvertFromString("1", rowMock.Object, data);

			Assert.AreEqual(3, dictionary.Count);
			Assert.AreEqual(1, dictionary["Prop1"]);
			Assert.AreEqual(2, dictionary["Prop2"]);
			Assert.AreEqual(3, dictionary["Prop3"]);
		}

		[TestMethod]
		public void ConvertWithIndexEndTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
			var rowMock = new Mock<IReaderRow>();
			var headers = new[] { "Id", "Name", "Prop1", "Prop2", "Prop3" };
			var currentRecord = new[] { "1", "One", "1", "2", "3" };
			var context = new ReadingContext(new StringReader(string.Empty), config, false)
			{
				HeaderRecord = headers,
				Record = currentRecord
			};
			rowMock.Setup(m => m.Configuration).Returns(config);
			rowMock.Setup(m => m.Context).Returns(context);
			rowMock.Setup(m => m.GetField(It.IsAny<Type>(), It.IsAny<int>())).Returns<Type, int>((type, index) => Convert.ToInt32(currentRecord[index]));
			var data = new MemberMapData(typeof(Test).GetProperty("Dictionary"))
			{
				Index = 2,
				IndexEnd = 3
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IDictionaryGenericConverter();
			var dictionary = (IDictionary)converter.ConvertFromString("1", rowMock.Object, data);

			Assert.AreEqual(2, dictionary.Count);
			Assert.AreEqual(1, dictionary["Prop1"]);
			Assert.AreEqual(2, dictionary["Prop2"]);
		}

		[TestMethod]
		public void FullReadNoHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestIndexMap>();

				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch (ReaderException)
				{
					// You can't read into a dictionary without a header.
					// You need to header value to use as the key.
				}
			}
		}

		[TestMethod]
		public void FullReadWithHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Before,Dictionary1,Dictionary2,Dictionary3,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HeaderValidated = null;
				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].Dictionary;

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list["Dictionary1"]);
				Assert.AreEqual(3, list["Dictionary2"]);
				Assert.AreEqual(4, list["Dictionary3"]);
			}
		}

		[TestMethod]
		public void FullReadWithDefaultHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Before,Dictionary,Dictionary,Dictionary,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestDefaultMap>();

				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		[TestMethod]
		public void FullReadWithNamedHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Before,Dictionary,Dictionary,Dictionary,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		[TestMethod]
		public void FullReadWithHeaderListItemsScattered()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Before,Dictionary,A,Dictionary,B,Dictionary,After");
				writer.WriteLine("1,2,3,4,5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				try
				{
					var records = csv.GetRecords<Test>().ToList();
					Assert.Fail();
				}
				catch (ReaderException)
				{
					// Can't have same name with Dictionary.
				}
			}
		}

		private class Test
		{
			public string Before { get; set; }
			public Dictionary<string, int?> Dictionary { get; set; }
			public string After { get; set; }
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
