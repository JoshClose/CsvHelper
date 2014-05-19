using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

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

		private sealed class ContainerClassMap : CsvClassMap<ContainerClass>
		{
			public ContainerClassMap()
			{
				References<ThirdClassMap>( m => m.Contents );
			}
		}

		private sealed class ThirdClassMap : CsvClassMap<ThirdClass>
		{
			public ThirdClassMap()
			{
				References<FirstClassMap>( m => m.First );
				References<SecondClassMap>( m => m.Second );
				Map( m => m.Third );
			}
		}

		private class SecondClass
		{
			public int Second { get; set; }
		}

		private sealed class SecondClassMap : CsvClassMap<SecondClass>
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

		private sealed class FirstClassMap : CsvClassMap<FirstClass>
		{
			public FirstClassMap()
			{
				Map( m => m.First );
			}
		}
	}
}
