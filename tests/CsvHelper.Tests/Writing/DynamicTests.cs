// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class DynamicTests
	{
		[TestMethod]
		public void WriteDynamicExpandoObjectsTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
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

				Assert.AreEqual(expected, reader.ReadToEnd());
			}
		}

		[TestMethod]
		public void WriteDynamicExpandoObjectTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				dynamic obj = new ExpandoObject();
				obj.Id = 1;
				obj.Name = "one";

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

				Assert.AreEqual(expected, reader.ReadToEnd());
			}
		}

		[TestMethod]
		public void WriteDynamicExpandoObjectHasDifferentPropertyOrderingTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";

				dynamic obj = new ExpandoObject();
				obj.Name = "one";
				obj.Id = 1;

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

				Assert.AreEqual(expected, writer.ToString());
			}
		}

		[TestMethod]
		public void WriteDynamicIDynamicMetaObjectProviderHasDifferentPropertyOrderingTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";

				dynamic obj = new DynamicObjectMock();
				obj.Name = "one";
				obj.Id = 1;

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

				Assert.AreEqual(expected, writer.ToString());
			}
		}

		[TestMethod]
		public void WriteDynamicExpandoObjectHasDifferentPropertyOrderingWithDynamicSortTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.DynamicPropertySort = Comparer<string>.Create((x, y) => x.CompareTo(y));

				dynamic obj = new ExpandoObject();
				obj.Name = "one";
				obj.Id = 1;

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

				Assert.AreEqual(expected, writer.ToString());
			}
		}

		[TestMethod]
		public void WriteDynamicIDynamicMetaObjectProviderHasDifferentPropertyOrderingWithDynamicSortTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.DynamicPropertySort = Comparer<string>.Create((x, y) => x.CompareTo(y));

				dynamic obj = new DynamicObjectMock();
				obj.Name = "one";
				obj.Id = 1;

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

				Assert.AreEqual(expected, writer.ToString());
			}
		}
	}
}
