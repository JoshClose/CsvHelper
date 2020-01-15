// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterBoxedTypesTests
	{
		[TestMethod]
		public void TypeMixedWithBoxedTypeTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var recordsTyped = new List<A>
				{
					new A { Id = 1, Name = "one" },
				};
				var recordsBoxed = new List<object>
				{
					new A { Id = 2, Name = "two" },
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords(recordsTyped);
				csv.WriteRecords(recordsBoxed);
				writer.Flush();

				var expected = new StringBuilder();
				expected.AppendLine("1,one");
				expected.AppendLine("2,two");

				Assert.AreEqual(expected.ToString(), writer.ToString());
			}
		}
	}

	public class A
	{
		public int Id { get; set; }

		public string Name { get; set; }
	}

	public sealed class AMap : ClassMap<A>
	{
		public AMap()
		{
			Map(m => m.Id).Index(0);
			Map(m => m.Name).Index(1);
		}
	}
}
