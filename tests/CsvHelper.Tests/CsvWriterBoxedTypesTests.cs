// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests
{
	
	public class CsvWriterBoxedTypesTests
	{
		[Fact]
		public void TypeMixedWithBoxedTypeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				var recordsTyped = new List<A>
				{
					new A { Id = 1, Name = "one" },
					new A { Id = 2, Name = "two" },
				};
				var recordsBoxed = new List<object>
				{
					new A { Id = 3, Name = "three" },
					new A { Id = 4, Name = "four" },
				};

				csv.Context.RegisterClassMap<AMap>();
				csv.WriteRecords(recordsTyped);
				csv.WriteRecords(recordsBoxed);
				writer.Flush();

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("1,one");
				expected.AppendLine("2,two");
				expected.AppendLine("3,three");
				expected.AppendLine("4,four");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}
	}

	public class A
	{
		public int Id { get; set; }

		public string? Name { get; set; }
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
