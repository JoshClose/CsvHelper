// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class DynamicProxyTests
	{
		[TestMethod]
		public void WriteDynamicProxyObjectTest()
		{
			var list = new List<TestClass>();
			var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
			for (var i = 0; i < 1; i++)
			{
				var proxy = proxyGenerator.CreateClassProxy<TestClass>();
				proxy.Id = i + 1;
				proxy.Name = "name" + proxy.Id;
				list.Add(proxy);
			}

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.RegisterClassMap<TestClassMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = new StringBuilder();
				expected.AppendLine("id,name");
				expected.AppendLine("1,name1");

				Assert.AreEqual(expected.ToString(), data);
			}
		}

		public class TestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestClassMap : ClassMap<TestClass>
		{
			public TestClassMap()
			{
				Map(m => m.Id).Name("id");
				Map(m => m.Name).Name("name");
			}
		}
	}
}
