using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class MapConstructorTests
	{
		[TestMethod]
		public void NoConstructor()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				try
				{
					csv.Configuration.RegisterClassMap<TestMap>();
					Assert.Fail();
				}
				catch( InvalidOperationException ex )
				{
					Assert.AreEqual( "No public parameterless constructor found.", ex.Message );
				}
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			private TestMap()
			{
				Map( m => m.Id );
				Map( m => m.Name );
			}
		}
	}
}
