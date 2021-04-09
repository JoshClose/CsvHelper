// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Mappings.Attribute
{
	
	public class ReferenceTests
	{
		[Fact]
		public void ReferenceTest()
		{
			using (var reader = new StringReader("id,name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<ReferenceTestClassA>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("one", records[0].B.Name);
			}
		}

		private class ReferenceTestClassA
		{
			[Name("id")]
			public int Id { get; set; }

			public ReferenceTestClassB B { get; set; }
		}

		private class ReferenceTestClassB
		{
			[Name("name")]
			public string Name { get; set; }
		}
	}
}
