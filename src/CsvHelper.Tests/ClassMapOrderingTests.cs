// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ClassMapOrderingTests
	{
		[TestMethod]
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

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<ContainerClassMap>();
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				Assert.AreEqual( "First,Second,Third", reader.ReadLine() );
			}
		}

		private class ContainerClass
		{
			public ThirdClass Contents { get; set; }
		}

		private class ThirdClass
		{
			public int Third { get; set; }

			public SecondClass Second { get; set; }

			public FirstClass First { get; set; }
		}

		private sealed class ContainerClassMap : ClassMap<ContainerClass>
		{
			public ContainerClassMap()
			{
				Map( m => m.Contents.First.First );
				Map( m => m.Contents.Second.Second );
				Map( m => m.Contents.Third );
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
				Map( m => m.Second );
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
				Map( m => m.First );
			}
		}
	}
}
