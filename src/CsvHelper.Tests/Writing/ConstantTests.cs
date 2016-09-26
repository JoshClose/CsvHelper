using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class ConstantTests
	{
		[TestMethod]
		public void SameValueIsWrittenForEveryRecordTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<Test>
				{
					new Test { Id = 1, Name = "one" },
					new Test { Id = 2, Name = "two" }
				};

				csv.Configuration.RegisterClassMap<TestMap>();
				csv.WriteRecords( records );
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine( "Id,Name" );
				expected.AppendLine( "1,constant" );
				expected.AppendLine( "2,constant" );

				var result = reader.ReadToEnd();

				Assert.AreEqual( expected.ToString(), result );
			}
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
