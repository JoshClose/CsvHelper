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
	
	public class HeaderPrefixTests
	{
		[Fact]
		public void DefaultHeaderPrefixTest()
		{
			using (var reader = new StringReader("Id,B.Name,C.Name\r\n1,b,c"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<ADefault>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("b", records[0].B.Name);
				Assert.Equal("c", records[0].C.Name);
			}
		}

		[Fact]
		public void CustomHeaderPrefixTest()
		{
			using (var reader = new StringReader("Id,B_Name,C_Name\r\n1,b,c"))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<ACustom>().ToList();

				Assert.Equal(1, records[0].Id);
				Assert.Equal("b", records[0].B.Name);
				Assert.Equal("c", records[0].C.Name);
			}
		}

		private class ADefault
		{
			public int Id { get; set; }

			[HeaderPrefixAttribute]
			public B B { get; set; }

			[HeaderPrefixAttribute]
			public C C { get; set; }
		}

		private class ACustom
		{
			public int Id { get; set; }

			[HeaderPrefixAttribute("B_")]
			public B B { get; set; }

			[HeaderPrefixAttribute("C_")]
			public C C { get; set; }
		}

		private class B
		{
			public string Name { get; set; }
		}

		private class C
		{
			public string Name { get; set; }
		}
	}
}
