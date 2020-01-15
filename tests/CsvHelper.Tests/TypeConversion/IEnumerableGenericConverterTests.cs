// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class IEnumerableGenericConverterTests
	{
		[TestMethod]
		public void ConvertNoIndexEndTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			var rowMock = new Mock<IReaderRow>();
			var context = new ReadingContext(new StringReader(string.Empty), config, false)
			{
				Record = currentRecord
			};
			rowMock.Setup(m => m.Configuration).Returns(config);
			rowMock.Setup(m => m.Context).Returns(context);
			rowMock.Setup(m => m.GetField(It.IsAny<Type>(), It.IsAny<int>())).Returns<Type, int>((type, index) => Convert.ToInt32(currentRecord[index]));
			var data = new MemberMapData(typeof(Test).GetTypeInfo().GetProperty("List"))
			{
				Index = 2
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IEnumerableGenericConverter();
			var enumerable = (IEnumerable<int?>)converter.ConvertFromString("1", rowMock.Object, data);
			var list = enumerable.ToList();

			Assert.AreEqual(3, list.Count);
			Assert.AreEqual(1, list[0]);
			Assert.AreEqual(2, list[1]);
			Assert.AreEqual(3, list[2]);
		}

		[TestMethod]
		public void ConvertWithIndexEndTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
			var rowMock = new Mock<IReaderRow>();
			var currentRecord = new[] { "1", "one", "1", "2", "3" };
			var context = new ReadingContext(new StringReader(string.Empty), config, false)
			{
				Record = currentRecord
			};
			rowMock.Setup(m => m.Configuration).Returns(config);
			rowMock.Setup(m => m.Context).Returns(context);
			rowMock.Setup(m => m.GetField(It.IsAny<Type>(), It.IsAny<int>())).Returns<Type, int>((type, index) => Convert.ToInt32(currentRecord[index]));
			var data = new MemberMapData(typeof(Test).GetProperty("List"))
			{
				Index = 2,
				IndexEnd = 3
			};
			data.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var converter = new IEnumerableGenericConverter();
			var enumerable = (IEnumerable<int?>)converter.ConvertFromString("1", rowMock.Object, data);
			var list = enumerable.ToList();

			Assert.AreEqual(2, list.Count);
			Assert.AreEqual(1, list[0]);
			Assert.AreEqual(2, list[1]);
		}

		[TestMethod]
		public void FullReadNoHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
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
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
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
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestDefaultMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
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
				writer.WriteLine("Before,List,List,List,After");
				writer.WriteLine("1,2,3,4,5");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(3, list[1]);
				Assert.AreEqual(4, list[2]);
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
				writer.WriteLine("Before,List,A,List,B,List,After");
				writer.WriteLine("1,2,3,4,5,6,7");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = true;
				csv.Configuration.RegisterClassMap<TestNamedMap>();
				var records = csv.GetRecords<Test>().ToList();

				var list = records[0].List.ToList();

				Assert.AreEqual(3, list.Count);
				Assert.AreEqual(2, list[0]);
				Assert.AreEqual(4, list[1]);
				Assert.AreEqual(6, list[2]);
			}
		}

		[TestMethod]
		public void FullWriteNoHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { List = new List<int?> { 1, 2, 3 } }
				};
				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual(",1,2,3,\r\n", result);
			}
		}

		[TestMethod]
		public void FullWriteWithHeaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var list = new List<Test>
				{
					new Test { List = new List<int?> { 1, 2, 3 } }
				};
				csv.Configuration.RegisterClassMap<TestIndexMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();
				var expected = new StringBuilder();
				expected.AppendLine("Before,List1,List2,List3,After");
				expected.AppendLine(",1,2,3,");

				Assert.AreEqual(expected.ToString(), result);
			}
		}

		private class Test
		{
			public string Before { get; set; }
			public IEnumerable<int?> List { get; set; }
			public string After { get; set; }
		}

		private sealed class TestIndexMap : ClassMap<Test>
		{
			public TestIndexMap()
			{
				Map(m => m.Before).Index(0);
				Map(m => m.List).Index(1, 3);
				Map(m => m.After).Index(4);
			}
		}

		private sealed class TestNamedMap : ClassMap<Test>
		{
			public TestNamedMap()
			{
				Map(m => m.Before).Name("Before");
				Map(m => m.List).Name("List");
				Map(m => m.After).Name("After");
			}
		}

		private sealed class TestDefaultMap : ClassMap<Test>
		{
			public TestDefaultMap()
			{
				Map(m => m.Before);
				Map(m => m.List);
				Map(m => m.After);
			}
		}
	}
}
