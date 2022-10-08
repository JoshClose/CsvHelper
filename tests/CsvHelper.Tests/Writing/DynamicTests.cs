// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests.Writing
{
	
	public class DynamicTests
	{
		[Fact]
		public void WriteDynamicExpandoObjectsTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var list = new List<dynamic>();

				dynamic obj = new ExpandoObject();
				obj.Id = 1;
				obj.Name = "one";
				list.Add(obj);

				obj = new ExpandoObject();
				obj.Id = 2;
				obj.Name = "two";
				list.Add(obj);

				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var expected = "Id,Name\r\n";
				expected += "1,one\r\n";
				expected += "2,two\r\n";

				Assert.Equal(expected, reader.ReadToEnd());
			}
		}

		[Fact]
		public void WriteDynamicExpandoObjectTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				dynamic obj = new ExpandoObject();
				obj.Id = 1;
				obj.Name = "one";

				csv.WriteDynamicHeader(obj);
				csv.NextRecord();

				csv.WriteRecord(obj);
				csv.NextRecord();

				obj = new ExpandoObject();
				obj.Id = 2;
				obj.Name = "two";

				csv.WriteRecord(obj);
				csv.NextRecord();

				writer.Flush();
				stream.Position = 0;

				var expected = "Id,Name\r\n";
				expected += "1,one\r\n";
				expected += "2,two\r\n";

				Assert.Equal(expected, reader.ReadToEnd());
			}
		}

		[Fact]
		public void WriteDynamicExpandoObjectHasDifferentPropertyOrderingTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				dynamic obj = new ExpandoObject();
				obj.Name = "one";
				obj.Id = 1;

				csv.WriteDynamicHeader(obj);
				csv.NextRecord();

				csv.WriteRecord(obj);
				csv.NextRecord();

				obj = new ExpandoObject();
				obj.Name = "two";
				obj.Id = 2;

				csv.WriteRecord(obj);
				csv.NextRecord();

				var expected = "Name,Id\r\n";
				expected += "one,1\r\n";
				expected += "two,2\r\n";

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteDynamicIDynamicMetaObjectProviderHasDifferentPropertyOrderingTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				dynamic obj = new DynamicObjectMock();
				obj.Name = "one";
				obj.Id = 1;

				csv.WriteDynamicHeader(obj);
				csv.NextRecord();

				csv.WriteRecord(obj);
				csv.NextRecord();

				obj = new ExpandoObject();
				obj.Name = "two";
				obj.Id = 2;

				csv.WriteRecord(obj);
				csv.NextRecord();

				var expected = "Name,Id\r\n";
				expected += "one,1\r\n";
				expected += "two,2\r\n";

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteDynamicExpandoObjectHasDifferentPropertyOrderingWithDynamicSortTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DynamicPropertySort = Comparer<string>.Create((x, y) => x.CompareTo(y)),
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				dynamic obj = new ExpandoObject();
				obj.Name = "one";
				obj.Id = 1;

				csv.WriteDynamicHeader(obj);
				csv.NextRecord();

				csv.WriteRecord(obj);
				csv.NextRecord();

				obj = new ExpandoObject();
				obj.Id = 2;
				obj.Name = "two";

				csv.WriteRecord(obj);
				csv.NextRecord();

				var expected = "Id,Name\r\n";
				expected += "1,one\r\n";
				expected += "2,two\r\n";

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteDynamicIDynamicMetaObjectProviderHasDifferentPropertyOrderingWithDynamicSortTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DynamicPropertySort = Comparer<string>.Create((x, y) => x.CompareTo(y)),
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				dynamic obj = new DynamicObjectMock();
				obj.Name = "one";
				obj.Id = 1;

				csv.WriteDynamicHeader(obj);
				csv.NextRecord();

				csv.WriteRecord(obj);
				csv.NextRecord();

				obj = new ExpandoObject();
				obj.Id = 2;
				obj.Name = "two";

				csv.WriteRecord(obj);
				csv.NextRecord();

				var expected = "Id,Name\r\n";
				expected += "1,one\r\n";
				expected += "2,two\r\n";

				Assert.Equal(expected, writer.ToString());
			}
		}
	}
}
