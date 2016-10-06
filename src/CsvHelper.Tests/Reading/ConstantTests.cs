using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class ConstantTests
	{
		[TestMethod]
		public void ConstantAlwaysReturnsSameValueTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "one" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.RegisterClassMap<TestMap>();
			var records = csv.GetRecords<Test>().ToList();

			Assert.AreEqual( 1, records[0].Id );
			Assert.AreEqual( "constant", records[0].Name );
			Assert.AreEqual( 2, records[1].Id );
			Assert.AreEqual( "constant", records[0].Name );
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Id );
				Map( m => m.Name ).Constant( "constant" );
			}
		}
	}
}
