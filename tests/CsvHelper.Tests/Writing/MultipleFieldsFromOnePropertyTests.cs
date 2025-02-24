﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.Writing
{
	
	public class MultipleFieldsFromOnePropertyTests
	{
		[Fact]
		public void WriteMultipleFieldsFromSinglePropertyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, new CultureInfo("en-US")))
			{
				var records = new List<Test>
				{
					new Test { Dob = DateTime.Parse("9/6/2017", new CultureInfo("en-US")) }
				};
				csv.Context.RegisterClassMap<TestMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("A,B,C");
				expected.AppendLine("9/6/2017 12:00:00 AM,9/6/2017 12:00:00 AM,9/6/2017 12:00:00 AM");

				Assert.Equal(expected.ToString(), reader.ReadToEnd());
			}
		}

		[Fact]
		public void ReadingWhenMultipleMapsForAPropertyAreSpecifiedUsesTheLastMapTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("A,B,C");
				writer.WriteLine("9/6/2017 12:00:00 AM,9/7/2017 12:00:00 AM,9/8/2017 12:00:00 AM");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.Equal(DateTime.Parse("9/8/2017", CultureInfo.InvariantCulture), records[0].Dob);
			}
		}

		private class Test
		{
			public DateTime Dob { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map(m => m.Dob, false).Name("A");
				Map(m => m.Dob, false).Name("B");
				Map(m => m.Dob, false).Name("C");
			}
		}
	}
}
