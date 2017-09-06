using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Async
{
	[TestClass]
    public class WritingTests
    {    
		[TestMethod]
		public async Task WritingTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" },
					new Simple { Id = 2, Name = "two" },
				};
				csv.WriteHeader<Simple>();
				await csv.NextRecordAsync();
				foreach( var record in records )
				{
					csv.WriteRecord( record );
					await csv.NextRecordAsync();
				}

				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine( "Id,Name" );
				expected.AppendLine( "1,one" );
				expected.AppendLine( "2,two" );

				Assert.AreEqual( expected.ToString(), reader.ReadToEnd() );
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
    }
}
