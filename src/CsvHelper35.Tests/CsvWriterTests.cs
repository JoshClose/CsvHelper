using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Xunit;

namespace CsvHelper35.Tests
{
	public class CsvWriterTests
	{
		[Fact]
		public void WriteRecordNonGenericTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.WriteRecord( typeof( Test ), new Test { Id = 1, Name = "one" } );
				csv.WriteRecord( typeof( Test ), new Test { Id = 2, Name = "two" } );

				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();

				var expected = new StringBuilder();
				expected.AppendLine( "Id,Name" );
				expected.AppendLine( "1,one" );
				expected.AppendLine( "2,two" );

				Assert.Equal( expected.ToString(), text );
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
