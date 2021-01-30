// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
    public class EnumIgnoreCaseTests
    {
		[TestMethod]
		public void GetRecords_UsingEnumIgnoreCaseFromClassMap_ReadsEnumValueWithDifferentCasing()
		{
			var s = new StringBuilder();
			s.Append("Id,Enum\r\n");
			s.Append("1,one");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<FooMap>();
				var records = csv.GetRecords<Foo>().ToList();
			}
		}

		[TestMethod]
		public void GetRecords_UsingEnumIgnoreCaseFromAttribute_ReadsEnumValueWithDifferentCasing()
		{
			var s = new StringBuilder();
			s.Append("Id,Enum\r\n");
			s.Append("1,one");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var records = csv.GetRecords<Bar>().ToList();
			}
		}

		[TestMethod]
		public void GetRecords_UsingEnumIgnoreCaseFromGlobal_ReadsEnumValueWithDifferentCasing()
		{
			var s = new StringBuilder();
			s.Append("Id,Enum\r\n");
			s.Append("1,one");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.AddOptions<TestEnum>(new TypeConverterOptions { EnumIgnoreCase = true });
				var records = csv.GetRecords<Foo>().ToList();
			}
		}

		private class Foo
		{
			public int Id { get; set; }
			public TestEnum Enum { get; set; }
		}

		private class FooMap : ClassMap<Foo>
		{
			public FooMap()
			{
				Map(m => m.Id);
				Map(m => m.Enum).TypeConverterOption.EnumIgnoreCase();
			}
		}

		private class Bar
		{
			public int Id { get; set; }
			[EnumIgnoreCase]
			public TestEnum Enum { get; set; }
		}

		private enum TestEnum
		{
			None = 0,
			One = 1
		}
    }
}
