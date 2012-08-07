using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvWriterSubClassingTests
	{
		[Fact]
		public void WriteRecordTest()
		{
			var data = new List<Test>
			{
				new Test { Id = 1, Name = "one" },
				new Test { Id = 2, Name = "two" }
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csvWriter = new MyCsvWriter( writer );

			csvWriter.WriteRecords( data );
		}

		private class MyCsvWriter : CsvWriter
		{
			public MyCsvWriter( TextWriter writer ) : base( writer ){}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
