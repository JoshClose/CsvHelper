// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Mappings
{
	
	public class MapConstructorTests
	{
		[Fact]
		public void NoConstructor()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				Assert.Throws<MissingMethodException>(() => csv.Context.RegisterClassMap<TestMap>());
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			private TestMap(string test)
			{
				Map(m => m.Id);
				Map(m => m.Name);
			}
		}
	}
}
