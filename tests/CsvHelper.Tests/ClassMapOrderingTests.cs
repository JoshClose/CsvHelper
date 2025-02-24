﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class ClassMapOrderingTests
	{
		[Fact]
		public void OrderingTest()
		{
			var list = new List<ContainerClass>
			{
				new ContainerClass
				{
					Contents = new ThirdClass
					{
						Third = 3,
						Second = new SecondClass
						{
							Second = 2,
						},
						First = new FirstClass
						{
							First = 1,
						},
					}
				},
			};

			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.RegisterClassMap<ContainerClassMap>();
				csv.WriteRecords(list);
				writer.Flush();
				stream.Position = 0;

				Assert.Equal("First,Second,Third", reader.ReadLine());
			}
		}

		private class ContainerClass
		{
			public ThirdClass Contents { get; set; } = new ThirdClass();
		}

		private class ThirdClass
		{
			public int Third { get; set; }

			public SecondClass Second { get; set; } = new SecondClass();

			public FirstClass First { get; set; } = new FirstClass();
		}

		private sealed class ContainerClassMap : ClassMap<ContainerClass>
		{
			public ContainerClassMap()
			{
				References<ThirdClassMap>(m => m.Contents);
			}
		}

		private sealed class ThirdClassMap : ClassMap<ThirdClass>
		{
			public ThirdClassMap()
			{
				References<FirstClassMap>(m => m.First);
				References<SecondClassMap>(m => m.Second);
				Map(m => m.Third);
			}
		}

		private class SecondClass
		{
			public int Second { get; set; }
		}

		private sealed class SecondClassMap : ClassMap<SecondClass>
		{
			public SecondClassMap()
			{
				Map(m => m.Second);
			}
		}

		private class FirstClass
		{
			public int First { get; set; }
		}

		private sealed class FirstClassMap : ClassMap<FirstClass>
		{
			public FirstClassMap()
			{
				Map(m => m.First);
			}
		}
	}
}
