using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Async
{
	[TestClass]
	public class ReadingTests
	{
		[TestMethod]
		public async Task ReadingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				var records = new List<Simple>();
				await csv.ReadAsync();
				csv.ReadHeader();
				while( await csv.ReadAsync() )
				{
					records.Add( csv.GetRecord<Simple>() );
				}

				Assert.AreEqual( 2, records.Count );

				var record = records[0];
				Assert.AreEqual( 1, record.Id );
				Assert.AreEqual( "one", record.Name );

				record = records[1];
				Assert.AreEqual( 2, record.Id );
				Assert.AreEqual( "two", record.Name );
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
